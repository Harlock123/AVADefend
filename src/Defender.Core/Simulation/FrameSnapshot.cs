namespace Defender.Core.Simulation;

public readonly record struct SpriteDraw(Sprite Sprite, int X, int Y, int Appear = 0, byte? Mono = null);
public readonly record struct LaserDraw(int Tail, int Fizzle, int Head, int Y, int Dir);
public readonly record struct PointDraw(int X, int Y, byte Color);
public readonly record struct TextDraw(int X, int Y, string Text, byte Color);

/// <summary>
/// Everything the renderer needs for one frame, in game-screen pixel coordinates (304×256 space,
/// before the 292×240 visible-area crop). Lists are reused between frames to avoid allocation.
/// </summary>
public sealed class FrameSnapshot
{
    public SessionState State;
    public int StateTimer;
    public bool Paused;
    public AttractPhase AttractPhase;
    public bool Demo;
    public GameMode Mode;
    public int Score, Lives, SmartBombs, Wave, HighScore;
    public int PlayerCount = 1, CurrentPlayer, InitialsPlayer, InitialsScore;
    public readonly int[] PlayerScores = new int[2], PlayerLives = new int[2], PlayerBombs = new int[2];
    public bool PlanetActive;
    public bool PlayerVisible;
    public bool HyperspaceBlank;
    public int HumanoidsAlive, WaveBonusPerHumanoid, HumanoidsBonusCounted;
    public readonly byte[] Palette = new byte[16];
    public readonly int[] TerrainY = new int[Arcade.ScreenWidth];   // -1 = no ground
    public readonly List<SpriteDraw> Sprites = new();
    public readonly List<LaserDraw> Lasers = new();
    public readonly List<PointDraw> Stars = new();
    public readonly List<PointDraw> Particles = new();
    public readonly List<TextDraw> Popups = new();
    public readonly List<ScannerBlip> Scanner = new();
    public readonly List<PointDraw> ScannerTerrain = new();
    public int ScannerPlayerX, ScannerPlayerY, ScannerWindowLeft, ScannerWindowRight;
    public char[] Initials = new char[3];
    public int InitialsCursor;
}

public sealed partial class GameSession
{
    private readonly List<ScannerBlip> _scanner = new();
    private readonly List<PointDraw> _scannerTerrain = new();
    private int _scanPlayerX, _scanPlayerY, _scanWinL, _scanWinR;

    /// <summary>Scanner left edge: the whole 2048-px planet centred on the middle of the screen (amode1.src:1180-1276).</summary>
    private int ScannerLeftWorld => CameraX + 150 * Arcade.UnitsPerPixel - 1024 * Arcade.UnitsPerPixel;

    private int ScanX(int worldX) => Arcade.ScannerLeft + (((worldX - ScannerLeftWorld) & Arcade.WorldMask) >> 10) * 2;
    private static int ScanY(int pixelY) => pixelY / 8 + 7;

    private void RefreshScanner()
    {
        _scanner.Clear();
        foreach (var e in Enemies)
        {
            if (e.Dead) continue;
            (byte up, byte lo) = e.Kind switch
            {
                EnemyKind.Lander => (Pal.Yellow, Pal.Green),
                EnemyKind.Mutant => (Pal.CycleC, Pal.Green),
                EnemyKind.Baiter => (Pal.Green, Pal.Green),
                EnemyKind.Pod => (Pal.CycleC, Pal.CycleC),
                EnemyKind.Swarmer => (Pal.Red, Pal.Yellow),
                _ => (Pal.Purple, Pal.Purple),
            };
            _scanner.Add(new ScannerBlip(ScanX(e.X), ScanY(e.PixelY), up, lo));
        }
        foreach (var h in Humanoids)
            if (h.Alive) _scanner.Add(new ScannerBlip(ScanX(h.X), ScanY(h.PixelY), Pal.Grey, Pal.Grey));

        _scannerTerrain.Clear();
        if (PlanetActive)
            for (int c = 0; c < Arcade.ScannerWidth / 2; c++)
            {
                int wx = ScannerLeftWorld + (c << 10);
                _scannerTerrain.Add(new PointDraw(Arcade.ScannerLeft + c * 2, ScanY(Terrain.HeightAtUnits(wx)), Pal.Brown));
            }
        _scanPlayerX = ScanX(PlayerWorldX);
        _scanPlayerY = ScanY(Player.PixelY);
        _scanWinL = ScanX(CameraX);
        _scanWinR = ScanX(CameraX + Arcade.PlayfieldVisibleWidth * Arcade.UnitsPerPixel);
    }

    public void BuildSnapshot(FrameSnapshot s)
    {
        s.State = State; s.StateTimer = StateTimer; s.Paused = Paused; s.Mode = Policy.Mode;
        s.Score = Score; s.Lives = Lives; s.SmartBombs = SmartBombs; s.Wave = Wave;
        s.HighScore = HighScores.Best;
        s.PlayerCount = PlayerCount; s.CurrentPlayer = CurrentPlayer; s.InitialsPlayer = InitialsPlayer; s.InitialsScore = InitialsScore;
        for (int p = 0; p < 2; p++) { s.PlayerScores[p] = ScoreOf(p); s.PlayerLives[p] = LivesOf(p); s.PlayerBombs[p] = BombsOf(p); }
        s.PlanetActive = PlanetActive;
        s.HumanoidsAlive = HumanoidsAlive;
        s.WaveBonusPerHumanoid = WaveBonusPerHumanoid;
        s.HumanoidsBonusCounted = _bonusCounted;
        Palette.CopyTo(s.Palette, 0);
        Initials.CopyTo(s.Initials, 0);
        s.InitialsCursor = InitialsCursor;

        bool inPlay = State is SessionState.LifeStart or SessionState.Playing or SessionState.Dying or SessionState.WaveComplete or SessionState.TurnOver;
        bool dyingHidden = State == SessionState.Dying && StateTimer >= DeathGlowFrames;
        s.HyperspaceBlank = State == SessionState.Playing && Player.InHyperspace && !Player.HyperAppearing;
        s.PlayerVisible = inPlay && State != SessionState.WaveComplete && !dyingHidden && !s.HyperspaceBlank;

        for (int x = 0; x < Arcade.ScreenWidth; x++)
            s.TerrainY[x] = inPlay && PlanetActive && !s.HyperspaceBlank && State != SessionState.WaveComplete ? Terrain.HeightAtUnits(CameraX + x * Arcade.UnitsPerPixel) : -1;

        s.Stars.Clear();
        if (inPlay && !s.HyperspaceBlank && State != SessionState.WaveComplete)
            foreach (var st in Stars) s.Stars.Add(new PointDraw(st.X, st.Y, st.Color));

        s.Sprites.Clear();
        s.Lasers.Clear();
        s.Particles.Clear();
        s.Popups.Clear();
        if (inPlay && !s.HyperspaceBlank && State != SessionState.WaveComplete)
        {
            bool frozenHidden = dyingHidden; // enemies vanish once the ship explodes
            if (!frozenHidden)
            {
                foreach (var h in Humanoids)
                    if (h.Alive) AddIfVisible(s, Sprites.Humanoid, h.X, h.PixelY, 0);
                foreach (var e in Enemies)
                    if (!e.Dead) AddIfVisible(s, SpriteOf(e), e.X, e.PixelY, e.Appear);
                foreach (var sh in Shells)
                    if (!sh.Dead) AddIfVisible(s, sh.Mine ? Sprites.Mine : Sprites.Shot, sh.X, sh.Y >> 8, 0);
                foreach (var l in Lasers) s.Lasers.Add(new LaserDraw(l.Tail, l.Fizzle, l.Head, l.Y, l.Dir));
            }
            if (s.PlayerVisible)
            {
                int appear = Player.InHyperspace && Player.HyperAppearing ? Player.HyperTimer : 0;
                byte? mono = State == SessionState.Dying ? Pal.DeathGlow : null;
                s.Sprites.Add(new SpriteDraw(Player.Sprite, Player.ScreenPx, Player.PixelY, appear, mono));
            }
        }
        if (inPlay)
        {
            foreach (var p in Particles)
            {
                int sx = SignedScreenX(p.X);
                if (sx >= 0 && sx < Arcade.ScreenWidth) s.Particles.Add(new PointDraw(sx, p.Y >> 8, p.Color));
            }
            foreach (var p in Popups)
            {
                int sx = SignedScreenX(p.X);
                if (sx > -16 && sx < Arcade.ScreenWidth) s.Popups.Add(new TextDraw(sx, p.Y, p.Text, Pal.White));
            }
        }

        if (Policy.ScannerEveryFrame || Frame % Arcade.ScannerRefreshFrames == 0 || _scannerTerrain.Count == 0) RefreshScanner();
        s.Scanner.Clear();
        s.ScannerTerrain.Clear();
        if (inPlay)
        {
            s.Scanner.AddRange(_scanner);
            s.ScannerTerrain.AddRange(_scannerTerrain);
        }
        s.ScannerPlayerX = _scanPlayerX; s.ScannerPlayerY = _scanPlayerY;
        s.ScannerWindowLeft = _scanWinL; s.ScannerWindowRight = _scanWinR;
    }

    private void AddIfVisible(FrameSnapshot s, Sprite spr, int worldX, int y, int appear)
    {
        int sx = SignedScreenX(worldX);
        if (sx + spr.Width <= 0 || sx >= Arcade.ScreenWidth) return;
        s.Sprites.Add(new SpriteDraw(spr, sx, y, appear));
    }
}
