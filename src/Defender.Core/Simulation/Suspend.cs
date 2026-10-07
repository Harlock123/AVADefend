namespace Defender.Core.Simulation;

/// <summary>
/// Complete, serialisable simulation state for Modern-mode suspend/resume. Restoring it and feeding
/// the same inputs reproduces the original run exactly (verified by tests).
/// </summary>
public sealed class SuspendData
{
    public int TerrainSeed { get; set; }
    public uint RngState { get; set; }
    public GameRules Rules { get; set; } = new();
    public long Frame { get; set; }
    public SessionState State { get; set; }
    public int StateTimer { get; set; }
    public int Score { get; set; }
    public int NextReplay { get; set; }
    public int Lives { get; set; }
    public int SmartBombs { get; set; }
    public int Wave { get; set; }
    public int[] Params { get; set; } = [];
    public bool PlanetActive { get; set; }
    public int[] Reserves { get; set; } = [];   // lander, bomber, pod, mutant, swarmer
    public int CameraX { get; set; }
    public int FlashFrames { get; set; }
    public int[] Counters { get; set; } = [];   // executive and housekeeping counters
    public bool FirstGexec { get; set; }
    public bool BomberFlip { get; set; }
    public int[] Blow { get; set; } = [0, 0, 0, 0];
    public List<Blast> Blasts { get; set; } = new();
    public bool WaveEndedOnDeath { get; set; }
    public byte[] Palette { get; set; } = [];
    public Player Player { get; set; } = new();
    public List<Enemy> Enemies { get; set; } = new();
    public List<Humanoid> Humanoids { get; set; } = new();
    public List<Shell> Shells { get; set; } = new();
    public List<Laser> Lasers { get; set; } = new();
    public List<Popup> Popups { get; set; } = new();
    public List<Particle> Particles { get; set; } = new();
    public List<Star> Stars { get; set; } = new();
    public int PlayerCount { get; set; } = 1;
    public int CurrentPlayer { get; set; }
    public PlayerState? WaitingPlayer { get; set; }
}

public sealed partial class GameSession
{
    /// <summary>Suspend is only meaningful mid-game.</summary>
    public bool CanSuspend => State is SessionState.Playing or SessionState.LifeStart or SessionState.WaveComplete
        or SessionState.Dying or SessionState.TurnOver;

    public SuspendData CaptureSuspend()
    {
        if (!CanSuspend) throw new InvalidOperationException($"Cannot suspend in state {State}");
        return new SuspendData
        {
            TerrainSeed = Terrain.Seed, RngState = Rng.State, Rules = Rules, Frame = Frame,
            State = State, StateTimer = StateTimer, Score = Score, NextReplay = NextReplay, Lives = Lives,
            SmartBombs = SmartBombs, Wave = Wave, Params = (int[])Params.Raw.Clone(), PlanetActive = PlanetActive,
            Reserves = [LanderReserve, BomberReserve, PodReserve, MutantReserve, SwarmerReserve],
            CameraX = CameraX, FlashFrames = FlashFrames,
            Counters = [_nextEnemyId, _gexecCounter, _intraCounter, _waveTimer, _baiterTimer, _bomberSquadCounter,
                        _starScrollAcc, _scannerTimer, _planetBlowTimer, _walkSlot, _bonusCounted, WaveBonusAwarded],
            FirstGexec = _firstGexec, BomberFlip = _bomberFlip,
            Blow = [BlowState.iteration, BlowState.wait, BlowState.flash, BlowState.color],
            Blasts = Blasts.Select(Clone).ToList(), WaveEndedOnDeath = _waveEndedOnDeath,
            Palette = (byte[])Palette.Clone(),
            Player = Clone(Player), Enemies = Enemies.Select(Clone).ToList(), Humanoids = Humanoids.Select(Clone).ToList(),
            Shells = Shells.Select(Clone).ToList(), Lasers = Lasers.Select(Clone).ToList(), Popups = Popups.Select(Clone).ToList(),
            Particles = Particles.Select(Clone).ToList(), Stars = Stars.Select(Clone).ToList(),
            PlayerCount = PlayerCount, CurrentPlayer = CurrentPlayer, WaitingPlayer = WaitingPlayer,
        };
    }

    /// <summary>Builds a paused session from suspend data. Throws <see cref="InvalidDataException"/> if inconsistent.</summary>
    public static GameSession Restore(SuspendData d, GamePolicy policy, Scoring.HighScoreBook? highScores = null)
    {
        Validate(d);
        GameRules rules;
        try { rules = d.Rules.Validated(); }
        catch (ArgumentOutOfRangeException ex) { throw new InvalidDataException("Suspend data has invalid rules: " + ex.ParamName); }
        if (d.Params.Length != Enum.GetValues<WaveVar>().Length || d.Reserves.Length != 5 || d.Counters.Length != 12
            || d.Humanoids.Count != Arcade.HumanoidCount || d.Palette.Length != 16 || d.Stars.Count == 0
            || d.Wave < 1 || d.Lives < 0 || d.SmartBombs < 0 || d.Score < 0
            || d.PlayerCount is < 1 or > 2 || d.CurrentPlayer < 0 || d.CurrentPlayer >= d.PlayerCount
            || (d.WaitingPlayer is { } w && (w.Humanoids.Count != Arcade.HumanoidCount || w.Params.Length != d.Params.Length || w.Reserves.Length != 5))
            || d.State is not (SessionState.Playing or SessionState.LifeStart or SessionState.WaveComplete or SessionState.Dying or SessionState.TurnOver))
            throw new InvalidDataException("Suspend data is inconsistent");
        var s = new GameSession(rules, policy, new XorShiftRandom(1), terrain: new Terrain(d.TerrainSeed), highScores: highScores);
        s.Rng.State = d.RngState;
        s.Frame = d.Frame; s.State = d.State; s.StateTimer = d.StateTimer;
        s.Score = d.Score; s.NextReplay = d.NextReplay; s.Lives = d.Lives; s.SmartBombs = d.SmartBombs; s.Wave = d.Wave;
        d.Params.CopyTo(s.Params.Raw, 0);
        s.PlanetActive = d.PlanetActive;
        (s.LanderReserve, s.BomberReserve, s.PodReserve, s.MutantReserve, s.SwarmerReserve) = (d.Reserves[0], d.Reserves[1], d.Reserves[2], d.Reserves[3], d.Reserves[4]);
        s.CameraX = d.CameraX & Arcade.WorldMask; s.FlashFrames = d.FlashFrames;
        var c = d.Counters;
        (s._nextEnemyId, s._gexecCounter, s._intraCounter, s._waveTimer, s._baiterTimer, s._bomberSquadCounter) = (c[0], c[1], c[2], c[3], c[4], c[5]);
        (s._starScrollAcc, s._scannerTimer, s._planetBlowTimer, s._walkSlot, s._bonusCounted, s.WaveBonusAwarded) = (c[6], c[7], c[8], c[9], c[10], c[11]);
        s._firstGexec = d.FirstGexec; s._bomberFlip = d.BomberFlip;
        if (d.Blow is { Length: 4 } b) s.BlowState = (b[0], b[1], b[2], (byte)b[3]);
        if (d.Blasts is not null) s.Blasts.AddRange(d.Blasts.Where(x => x is not null && x.SpriteName is not null).Select(Clone)); s._waveEndedOnDeath = d.WaveEndedOnDeath;
        d.Palette.CopyTo(s.Palette, 0);
        Copy(d.Player, s.Player);
        s.Enemies.AddRange(d.Enemies.Select(Clone));
        for (int i = 0; i < Arcade.HumanoidCount; i++) s.Humanoids[i] = Clone(d.Humanoids[i]);
        s.Shells.AddRange(d.Shells.Select(Clone)); s.Lasers.AddRange(d.Lasers.Select(Clone));
        s.Popups.AddRange(d.Popups.Select(Clone)); s.Particles.AddRange(d.Particles.Select(Clone));
        s.Stars.Clear(); s.Stars.AddRange(d.Stars.Select(Clone));
        s.PlayerCount = d.PlayerCount; s.CurrentPlayer = d.CurrentPlayer;
        if (d.PlayerCount == 2) s._slots[1 - d.CurrentPlayer] = d.WaitingPlayer;
        s.Paused = s.State is SessionState.Playing or SessionState.LifeStart;
        return s;
    }

    /// <summary>Structural checks so hand-edited or damaged files are rejected instead of crashing later.</summary>
    private static void Validate(SuspendData? d)
    {
        static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string what) { if (!ok) throw new InvalidDataException("Suspend data is missing or invalid: " + what); }
        Need(d is not null, "data");
        Need(d!.Rules is not null && d.Player is not null, "rules/player");
        Need(d.Params is not null && d.Reserves is not null && d.Counters is not null && d.Palette is not null, "arrays");
        Need(d.Enemies is not null && d.Humanoids is not null && d.Shells is not null && d.Lasers is not null
             && d.Popups is not null && d.Particles is not null && d.Stars is not null, "lists");
        Need(d.Enemies.All(e => e is not null) && d.Humanoids.All(h => h is not null) && d.Shells.All(x => x is not null)
             && d.Lasers.All(x => x is not null) && d.Popups.All(x => x is not null && x.Text is not null)
             && d.Particles.All(x => x is not null) && d.Stars.All(x => x is not null), "null entries");
        Need(d.Enemies.All(e => e.Target >= -1 && e.Target < Arcade.HumanoidCount && Enum.IsDefined(e.Kind) && Enum.IsDefined(e.Phase)), "enemy targets");
        Need(d.Humanoids.All(h => Enum.IsDefined(h.State)), "humanoid states");
        Need(d.WaitingPlayer is null || (d.WaitingPlayer.Humanoids is not null && d.WaitingPlayer.Params is not null
             && d.WaitingPlayer.Reserves is not null && d.WaitingPlayer.Humanoids.All(h => h is not null)), "waiting player");
    }

    // Shallow member-wise copies (all entity members are value types or strings).
    private static T Clone<T>(T o) where T : class => (T)typeof(T).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(o, null)!;

    private static void Copy(Player from, Player to)
    {
        foreach (var p in typeof(Player).GetProperties().Where(p => p.CanWrite)) p.SetValue(to, p.GetValue(from));
    }
}
