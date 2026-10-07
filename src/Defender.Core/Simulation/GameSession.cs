using Defender.Core.Audio;
using Defender.Core.Input;
using Defender.Core.Scoring;

namespace Defender.Core.Simulation;

public enum SessionState { Attract, LifeStart, Playing, Dying, WaveComplete, GameOver, EnterInitials, TurnOver }

/// <summary>
/// Headless simulation of one cabinet. Call <see cref="Step"/> once per 1/60 s tick with that tick's input.
///
/// Per-tick order (Playing): input → player physics &amp; scroll → stars → lasers (and laser kills) →
/// enemy AI + velocity integration → humanoids → shells → player collisions → game executive
/// (every 15 frames: spawning, baiters, escalation, wave end) → lifecycle cleanup → palette/scanner.
/// The original runs COLCHK first and AI in cooperative processes; the per-frame outcome is the same
/// apart from one-frame ordering differences (documented in ARCHITECTURE.md).
/// </summary>
public sealed partial class GameSession
{
    private readonly WaveTable _table;
    private readonly List<SoundId> _sounds = new();

    public GameSession(GameRules? rules = null, GamePolicy? policy = null, IRandom? rng = null,
                       WaveTable? table = null, Terrain? terrain = null, HighScoreBook? highScores = null)
    {
        Rules = (rules ?? new GameRules()).Validated();
        Policy = policy ?? GamePolicy.Classic;
        Rng = rng ?? new XorShiftRandom((uint)Environment.TickCount);
        _table = table ?? WaveTable.Default;
        Terrain = terrain ?? new Terrain();
        HighScores = highScores ?? new HighScoreBook();
        for (int i = 0; i < Humanoids.Length; i++) Humanoids[i] = new Humanoid();
        for (int i = 0; i < 16; i++) Stars.Add(new Star());
        ResetPalette();
        RandomizeStars();
        EnterAttract();
    }

    public GameRules Rules { get; }
    public GamePolicy Policy { get; set; }
    public IRandom Rng { get; }
    public Terrain Terrain { get; }
    public HighScoreBook HighScores { get; }
    public WaveTable Table => _table;

    public SessionState State { get; private set; }
    public int StateTimer { get; private set; }
    public long Frame { get; private set; }
    public bool Paused { get; private set; }

    public void SetPaused(bool paused) { if (Policy.AllowPause && State is SessionState.Playing or SessionState.LifeStart) Paused = paused; }

    public int Score { get; private set; }
    public int NextReplay { get; private set; }
    public int Lives { get; private set; }   // reserve ships (current ship not counted)
    public int SmartBombs { get; private set; }
    public int Wave { get; private set; }
    public WaveParams Params { get; private set; } = new();
    public bool PlanetActive { get; private set; } = true;
    public int HumanoidsAlive => Humanoids.Count(h => h.Alive);

    // Reserves (ELIST counters): not yet on screen.
    public int LanderReserve { get; private set; }
    public int BomberReserve { get; private set; }
    public int PodReserve { get; private set; }
    public int MutantReserve { get; private set; }
    public int SwarmerReserve { get; private set; }

    public Player Player { get; } = new();
    public List<Enemy> Enemies { get; } = new();
    public Humanoid[] Humanoids { get; } = new Humanoid[Arcade.HumanoidCount];
    public List<Shell> Shells { get; } = new();
    public List<Laser> Lasers { get; } = new();
    public List<Popup> Popups { get; } = new();
    /// <summary>The player-explosion (PLEX) pieces, in screen coordinates (1/256 px).</summary>
    public List<Particle> Particles { get; } = new();
    public List<Star> Stars { get; } = new();
    public byte[] Palette { get; } = new byte[16];

    /// <summary>World X of the left screen edge (BGL).</summary>
    public int CameraX { get; private set; }

    public IReadOnlyList<SoundId> Sounds => _sounds;
    public bool ThrustSoundOn { get; private set; }
    public int FlashFrames { get; private set; }

    /// <summary>Raised when a new high-score entry is committed (UI persists the table).</summary>
    public event Action<HighScoreEntry>? HighScoreCommitted;

    // Initials entry
    public char[] Initials { get; } = ['A', 'A', 'A'];
    public int InitialsCursor { get; private set; }
    public int PendingRank { get; private set; } = -1;

    private int _nextEnemyId = 1;
    private int _gexecCounter, _intraCounter, _waveTimer, _baiterTimer;
    private int _bomberSquadCounter;
    private bool _bomberFlip;
    private bool _waveEndedOnDeath;
    private int _starScrollAcc;
    private int _scannerTimer;
    private int _planetBlowTimer;
    private bool _firstGexec;

    public void Step(in PlayerInput input)
    {
        _sounds.Clear();
        if (input.PausePressed && Policy.AllowPause && State is SessionState.Playing or SessionState.LifeStart)
            Paused = !Paused;
        if (Paused) { ThrustSoundOn = false; return; }

        Frame++;
        StateTimer++;
        switch (State)
        {
            case SessionState.Attract:
                if (input.StartPressed) StartGame(1);
                else if (input.Start2Pressed) StartGame(2);
                break;
            case SessionState.LifeStart:
                StepWorldIdle();
                if (StateTimer >= LifeStartFrames) BeginPlay();
                break;
            case SessionState.TurnOver:
                StepWorldIdle();
                if (StateTimer >= TurnOverFrames) SwitchPlayers();
                break;
            case SessionState.Playing:
                StepPlaying(input);
                break;
            case SessionState.Dying:
                StepDying();
                break;
            case SessionState.WaveComplete:
                StepWaveComplete();
                break;
            case SessionState.GameOver:
                if (StateTimer >= 180) AfterGameOver();
                break;
            case SessionState.EnterInitials:
                StepInitials(input);
                break;
        }
        ThrustSoundOn = State == SessionState.Playing && Player.Alive && !Player.InHyperspace && input.ThrustHeld;
        UpdatePalette();
        if (FlashFrames > 0) FlashFrames--;
    }

    // ----- state transitions -------------------------------------------------------------------

    private void EnterAttract()
    {
        State = SessionState.Attract;
        StateTimer = 0;
        Paused = false;
        Enemies.Clear(); Shells.Clear(); Lasers.Clear(); Popups.Clear(); Particles.Clear(); Blasts.Clear();
        PlanetActive = true;
    }

    public void StartGame(int players = 1)
    {
        PlayerCount = Math.Clamp(players, 1, 2);
        CurrentPlayer = 0;
        _slots[0] = _slots[1] = null;
        _initialsQueue.Clear();
        // Ships are consumed as each player launches (net: ships-1 in reserve); bombs = ships (defa7.src:1142-1147).
        if (PlayerCount == 2) _slots[1] = FreshPlayer();
        FreshPlayer();
        _sounds.Add(SoundId.GameStart);
        StartLife(consumeShip: true);
    }

    /// <summary>GETWV: parameters and reserves for <see cref="Wave"/>.</summary>
    private void LoadWave()
    {
        Params = WaveParams.For(_table, Wave, Rules.InitialDifficulty, Rules.DifficultyCeiling);
        LanderReserve = Params[WaveVar.Landers];
        BomberReserve = Params[WaveVar.Bombers];
        PodReserve = Params[WaveVar.Pods];
        MutantReserve = Params[WaveVar.Mutants];
        SwarmerReserve = Params[WaveVar.Swarmers];
        _intraCounter = 0;
    }

    /// <summary>1-player: a 96-frame pause. 2-player: "PLAYER n" for 128 frames, then the pause (defa7.src:1290-1305).</summary>
    public int LifeStartFrames => PlayerCount == 2 ? 128 + 96 : 96;
    public const int TurnOverFrames = 96;

    private void StartLife(bool consumeShip)
    {
        if (consumeShip) Lives--;
        // PLRES re-creates the surviving humanoids each life: spread over the quadrants, random facing.
        PlaceHumanoids(Humanoids.Count(h => h.Alive));
        _intraCounter = 0;
        _planetBlowTimer = 0;   // the explosion effect belongs to the life in which it happened
        State = SessionState.LifeStart;
        StateTimer = 0;
        Enemies.Clear(); Shells.Clear(); Lasers.Clear(); Particles.Clear(); Popups.Clear(); Blasts.Clear();
        Player.Reset();
        CameraX = 0;  // BGL = 0 on a new life (defa7.src:1241)
        FlashFrames = 0;
    }

    private void BeginPlay()
    {
        State = SessionState.Playing;
        StateTimer = 0;
        _gexecCounter = 0;
        _firstGexec = true;
        _waveTimer = 0;
        _baiterTimer = Params[WaveVar.BaiterTime];
        SpawnLifeStartEnemies();
    }

    private void KillPlayer()
    {
        if (!Player.Alive) return;
        Player.Alive = false;
        Player.InHyperspace = false;
        State = SessionState.Dying;
        StateTimer = 0;
        _sounds.Add(SoundId.PlayerExplode);
        Lasers.Clear();
        // Drop a rescued humanoid: it will fall from where the ship was.
        foreach (var h in Humanoids)
            if (h.State == HumanoidState.Rescued) { h.State = HumanoidState.Falling; h.Vy = 0; h.FallFrames = 0; }
    }

    // 32-frame glow, 2 frames white, ~108 frames of explosion (PLEND/PLEX, defa7.src:1328-1377, blk71.src:618-671).
    private const int DeathGlowFrames = 32, DeathTotalFrames = 142;

    private void StepDying()
    {
        // While the ship glows the enemies keep moving (STATUS $58 does not stop VELO/OPROC/SHELL);
        // everything freezes at the white flash, then the ship explodes.
        if (StateTimer < DeathGlowFrames)
        {
            UpdateEnemies();
            UpdateHumanoids();
            UpdateShells();
            UpdateBlasts();
            Enemies.RemoveAll(e => e.Dead);
            Shells.RemoveAll(s => s.Dead);
        }
        if (StateTimer == DeathGlowFrames)
        {
            if (!Policy.SuppressFlashes) FlashFrames = 2;
            // STATUS $7F cancels every explosion and finishes every appear; then PLEX.
            Blasts.Clear();
            foreach (var e in Enemies) e.Appear = 0;
        }
        if (StateTimer == DeathGlowFrames + 2) StartPlex();
        if (StateTimer > DeathGlowFrames + 2) UpdatePlex();
        if (StateTimer < DeathTotalFrames) return;
        SaveEnemiesToReserves();
        // A death that empties the wave still pays the wave bonus first (defa7.src:1386-1390).
        if (EnemiesRemainingForWave == 0 && !TestHoldWave) { _waveEndedOnDeath = true; BeginWaveComplete(); return; }
        NextTurnAfterDeath();
    }

    /// <summary>PLSAV (INF): enemies still alive go back into the reserves and re-enter on the next life.</summary>
    private void SaveEnemiesToReserves()
    {
        foreach (var e in Enemies.Where(e => !e.Dead))
        {
            switch (e.Kind)
            {
                case EnemyKind.Lander: LanderReserve++; break;
                case EnemyKind.Mutant: MutantReserve++; break;
                case EnemyKind.Bomber: BomberReserve++; break;
                case EnemyKind.Pod: PodReserve++; break;
                case EnemyKind.Swarmer: SwarmerReserve++; break;
            }
        }
        Enemies.Clear();
        foreach (var h in Humanoids)
        {
            if (h.State is HumanoidState.Grabbed or HumanoidState.Falling or HumanoidState.Rescued)
            {
                h.State = HumanoidState.Walking;
                h.Carrier = -1;
                h.Vy = 0;
                h.Y = Math.Min(Terrain.HeightAtUnits(h.X) + 4, Arcade.HumanoidMaxWalkY) << 8;
            }
        }
    }

    private void EnterGameOver()
    {
        State = SessionState.GameOver;
        StateTimer = 0;   // the original only silences the board here ($13)
    }

    private void AfterGameOver()
    {
        QueueInitials();
        NextInitialsOrAttract();
    }

    private void NextInitialsOrAttract()
    {
        if (_initialsQueue.Count == 0) { EnterAttract(); return; }
        InitialsPlayer = _initialsQueue.Peek().Player;
        State = SessionState.EnterInitials;
        StateTimer = 0;
        InitialsCursor = 0;
        Initials[0] = 'A'; Initials[1] = Initials[2] = ' ';   // first initial A, the others blank
        _initialTimer = 0; _lastVertical = 0; _fireUpFrames = 5;
    }

    // HALLOF (amode1.src:117-371): the stick cycles space and A-Z with wrap-around.
    private const string InitialsAlphabet = " ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private int _repeatWait, _repeatDelay, _lastVertical, _fireUpFrames, _initialTimer;

    private void StepInitials(in PlayerInput input)
    {
        // Hold-to-repeat: the first step 3 frames after pushing, then delays 32, 21, 15, 12, 11, 10… (d = d/2 + 5 from 55).
        if (input.Vertical != 0)
        {
            if (input.Vertical != _lastVertical) { _repeatWait = 3; _repeatDelay = 55; }
            if (--_repeatWait == 0)
            {
                StepLetter(-input.Vertical);
                _repeatDelay = _repeatDelay / 2 + 5;
                _repeatWait = _repeatDelay;
            }
        }
        _lastVertical = input.Vertical;

        // Fire counts only after the button has been released for at least 5 frames.
        if (input.FirePressed && _fireUpFrames >= 5) AdvanceInitial();
        _fireUpFrames = input.FireHeld || input.FirePressed ? 0 : _fireUpFrames + 1;

        // 40 s for the first initial, 20 s for each later one; then the entry is committed as it stands.
        if (++_initialTimer > (InitialsCursor == 0 ? 40 : 20) * Arcade.TicksPerSecond) CommitInitials();
    }

    private void StepLetter(int delta)
    {
        int i = InitialsAlphabet.IndexOf(Initials[InitialsCursor]);
        if (i < 0) i = 0;
        Initials[InitialsCursor] = InitialsAlphabet[(i + delta + InitialsAlphabet.Length) % InitialsAlphabet.Length];
    }

    /// <summary>Lets the UI type initials directly (keyboard) as an alternative to the joystick.</summary>
    public void TypeInitial(char c)
    {
        if (State != SessionState.EnterInitials) return;
        c = char.ToUpperInvariant(c);
        if (c is not (>= 'A' and <= 'Z') && c != ' ') return;
        Initials[InitialsCursor] = c;
        AdvanceInitial();
    }

    private void AdvanceInitial()
    {
        _initialTimer = 0;
        if (++InitialsCursor >= 3) CommitInitials();
    }

    private void CommitInitials()
    {
        var who = _initialsQueue.Count > 0 ? _initialsQueue.Dequeue() : new PendingInitials(CurrentPlayer, Score, Wave);
        var (allRank, todayRank) = HighScores.Insert(new string(Initials), who.Score, who.Wave);
        PendingRank = allRank;
        if (allRank >= 0) HighScoreCommitted?.Invoke(HighScores.AllTime.Entries[allRank]);
        else if (todayRank >= 0) HighScoreCommitted?.Invoke(HighScores.Today.Entries[todayRank]);
        // A later player may no longer qualify once an earlier entry has been inserted.
        while (_initialsQueue.Count > 0 && !HighScores.Qualifies(_initialsQueue.Peek().Score)) _initialsQueue.Dequeue();
        NextInitialsOrAttract();
    }

    // ----- wave completion ---------------------------------------------------------------------

    public int WaveBonusPerHumanoid => 100 * Math.Min(Wave, 5);
    public int WaveBonusAwarded { get; private set; }
    private int _bonusCounted;

    private void BeginWaveComplete()
    {
        State = SessionState.WaveComplete;
        StateTimer = 0;
        Lasers.Clear(); Shells.Clear(); Blasts.Clear();
        Enemies.RemoveAll(e => e.Kind == EnemyKind.Baiter || e.Dead);
        WaveBonusAwarded = 0;
        _bonusCounted = 0;
        foreach (var h in Humanoids)
            if (h.State is HumanoidState.Falling or HumanoidState.Rescued or HumanoidState.Grabbed)
            {
                h.State = HumanoidState.Walking; h.Carrier = -1; h.Vy = 0;
                h.Y = Math.Min(Terrain.HeightAtUnits(h.X) + 4, Arcade.HumanoidMaxWalkY) << 8;
            }
    }

    private void StepWaveComplete()
    {
        // BONUS (defa7.src:1786-1845): screen cleared, one humanoid counted every 4 frames, then 128 frames.
        int alive = HumanoidsAlive;
        if (StateTimer % 4 == 1 && _bonusCounted < alive)
        {
            _bonusCounted++;
            AddScore(WaveBonusPerHumanoid);
            WaveBonusAwarded += WaveBonusPerHumanoid;
        }
        if (StateTimer < alive * 4 + 128) return;
        Wave++;
        if (Rules.RestoreWave > 0 && Wave % Rules.RestoreWave == 0)
        {
            PlanetActive = true;
            PlaceHumanoids(Arcade.HumanoidCount);
        }
        LoadWave();
        if (_waveEndedOnDeath) { _waveEndedOnDeath = false; NextTurnAfterDeath(); }
        else StartLife(consumeShip: false);
    }

    public int HumanoidsBonusCounted => _bonusCounted;

    // ----- scoring ------------------------------------------------------------------------------

    private void AddScore(int points)
    {
        Score += points;
        while (Score >= NextReplay)
        {
            Lives++;
            SmartBombs++;   // +1 smart bomb with each extra ship (defa7.src:525)
            NextReplay += Rules.ReplayEvery;
            _sounds.Add(SoundId.ExtraLife);
        }
    }

    private void AddPopup(int x, int yPx, string text) =>
        Popups.Add(new Popup { X = x, Y = yPx, Text = text, Life = Arcade.ScorePopupFrames });

    // ----- shared helpers -------------------------------------------------------------------------

    /// <summary>Screen pixel X of a world X, or a large value when not within the 300-px window.</summary>
    public int ScreenX(int worldX) => ((worldX - CameraX) & Arcade.WorldMask) / Arcade.UnitsPerPixel;

    public bool OnScreen(int worldX) => ((worldX - CameraX) & Arcade.WorldMask) < Arcade.PlayfieldVisibleWidth * Arcade.UnitsPerPixel;

    /// <summary>Signed screen X (handles objects just left of the screen edge).</summary>
    public int SignedScreenX(int worldX)
    {
        int d = (worldX - CameraX) & Arcade.WorldMask;
        if (d >= Arcade.WorldUnits / 2) d -= Arcade.WorldUnits;
        return d / Arcade.UnitsPerPixel;
    }

    private static int WrapX(int x) => WorldMath.WrapUnits(x);

    private static int WrapY(int y)
    {
        if (y < Arcade.YMin << 8) return Arcade.YMax << 8;
        if (y > Arcade.YMax << 8) return Arcade.YMin << 8;
        return y;
    }

    private void StepWorldIdle()
    {
        UpdateStars(0);
        UpdatePopups();
    }

    private void UpdatePopups()
    {
        foreach (var p in Popups) p.Life--;
        Popups.RemoveAll(p => p.Life <= 0);
    }
}
