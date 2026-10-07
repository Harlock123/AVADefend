namespace Defender.Core.Simulation;

public readonly record struct SpriteDraw(Sprite Sprite, int X, int Y, int Appear = 0, byte? Mono = null);
public readonly record struct LaserDraw(int Tail, int Fizzle, int Head, int Y, int Dir);
public readonly record struct PointDraw(int X, int Y, byte Color);
public readonly record struct TextDraw(int X, int Y, string Text, byte Color);

/// <summary>Sprite drawn as spread 2-px × 2-row tiles (explosion or appear). Columns are 2 px; S = spread factor.</summary>
public readonly record struct BlastDraw(Sprite Sprite, int TopCol, int TopRow, int CenterCol, int CenterRow, int S);

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
    public int AttractTimer;
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
    public readonly List<PointDraw> Particles = new();   // PLEX pieces (2×2, colour $B)
    public readonly List<BlastDraw> Blasts = new();
    public readonly List<TextDraw> Labels = new();   // attract-demo labels
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
        // Player blip (amode1.src:1243-1263): column $4B + screen column/16, row Y/8 + SCANH (centre of a 5-px plus).
        _scanPlayerX = 0x4B * 2 + (Player.ScreenPx / 32) * 2;
        _scanPlayerY = Player.PixelY / 8 + Arcade.ScannerTop;
        // Visible-window ticks are fixed at byte columns $4C (left pixel) and $53 (right pixel) (MTX, amode1.src:1228-1235).
        _scanWinL = 0x4C * 2;
        _scanWinR = 0x53 * 2 + 1;
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
        s.Blasts.Clear();
        s.Popups.Clear();
        if (inPlay && !s.HyperspaceBlank && State != SessionState.WaveComplete)
        {
            bool frozenHidden = dyingHidden; // enemies vanish once the ship explodes
            if (!frozenHidden)
            {
                foreach (var h in Humanoids)
                    if (h.Alive) AddIfVisible(s, Sprites.Humanoid, h.X, h.PixelY, 0);
                foreach (var e in Enemies)
                {
                    if (e.Dead) continue;
                    if (e.Appear > 0) AddAppear(s, SpriteOf(e), e.X, e.PixelY, e.Appear - 1);
                    else AddIfVisible(s, SpriteOf(e), e.X, e.PixelY, 0);
                }
                foreach (var b in Blasts)
                {
                    int tc = SignedScreenX(b.TopLeftX) >> 1, cc = SignedScreenX(b.CenterX) >> 1;
                    s.Blasts.Add(new BlastDraw(Sprites.ByName(b.SpriteName), tc, b.TopRow, cc, b.CenterRow, b.Size >> 8));
                }
                foreach (var sh in Shells)
                    if (!sh.Dead) AddIfVisible(s, sh.Mine ? Sprites.Mine : Sprites.Shot, sh.X, sh.Y >> 8, 0);
                foreach (var l in Lasers) s.Lasers.Add(new LaserDraw(l.Tail, l.Fizzle, l.Head, l.Y, l.Dir));
            }
            if (s.PlayerVisible)
            {
                if (Player.InHyperspace && Player.HyperAppearing)
                    AddAppear(s, Player.Sprite, Player.WorldX(CameraX), Player.PixelY, Math.Max(0, Player.HyperTimer - 1));
                else if (State != SessionState.Dying || (StateTimer / 2) % 2 == 1)   // death silhouette: 2 frames off, 2 on
                    s.Sprites.Add(new SpriteDraw(Player.Sprite, Player.ScreenPx, Player.PixelY, 0,
                                                 State == SessionState.Dying ? Pal.DeathGlow : null));
            }
        }
        if (inPlay)
        {
            if (State == SessionState.Dying)
                foreach (var p in Particles)
                    if (p.Life != 0) s.Particles.Add(new PointDraw(p.X >> 8, p.Y >> 8, Pal.DeathGlow));
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

    /// <summary>APST appear: tiles converge on a pseudo-random column at mid-height (samexap7.src:188-197).</summary>
    private void AddAppear(FrameSnapshot s, Sprite spr, int worldX, int y, int size)
    {
        int sx = SignedScreenX(worldX);
        if (sx < -96 || sx >= 416) return;
        int top = sx >> 1, wCols = (spr.Width + 1) / 2;
        int k = (2 * (((top & 0xFF) * 218) >> 8)) & 0xFF;
        s.Blasts.Add(new BlastDraw(spr, top, y, top + (wCols * k >> 8), y + spr.Height / 2, size));
    }

    private void AddIfVisible(FrameSnapshot s, Sprite spr, int worldX, int y, int appear)
    {
        int sx = SignedScreenX(worldX);
        if (sx + spr.Width <= 0 || sx >= Arcade.ScreenWidth) return;
        s.Sprites.Add(new SpriteDraw(spr, sx, y, appear));
    }
}
