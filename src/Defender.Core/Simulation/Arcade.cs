namespace Defender.Core.Simulation;

/// <summary>
/// Hardware/game constants. Citations refer to the original Red Label source as mirrored at
/// github.com/mwenge/defender @ e740063 (facts only; see RESEARCH.md). "INF" = inferred from code.
/// </summary>
public static class Arcade
{
    public const int TicksPerSecond = 60;            // defa7.src:9 "SLEEP TIME X 16MSEC"

    // Screen (game pixel coordinates, before MAME visible-area crop).
    public const int ScreenWidth = 304;              // lasers stop at byte column $98 (defa7.src:2802)
    public const int ScreenHeight = 256;             // INF: Williams frame buffer rows
    public const int PlayfieldVisibleWidth = 300;    // objects drawn if OX16-BGL < 150*64 (defa7.src:2527)
    public const int YMin = 42;                      // phr6.src:21
    public const int YMax = 240;                     // phr6.src:20
    public const int ScannerTop = 8;                 // phr6.src:158 SCANH
    public const int ScannerBottom = 40;             // border bottom row SCANH+$20 (defa7.src:907)
    public const int ScannerLeft = 96;               // byte column $30
    public const int ScannerWidth = 128;             // 64 byte columns (amode1.src:1225)

    // World.
    public const int UnitsPerPixel = 32;             // defa7.src:3334 "100*32 ;100 PIXEL"
    public const int WorldUnits = 65536;             // 16-bit OX16 wraps
    public const int WorldPixels = WorldUnits / UnitsPerPixel; // 2048
    public const int WorldMask = 0xFFFF;
    public const int TerrainStartY = 224;            // blk71.src:103

    // Player (defa7.src:2339-2476).
    public const int ThrustPerFrame = 0x300;         // PLADIR, added into 24-bit velocity
    public const int MaxPlayerSpeed16 = 0x100;       // hard clamp on top-16 velocity
    public const int ShipBaseRightPx = 64;           // screen column $20
    public const int ShipBaseLeftPx = 224;           // screen column $70
    public const int ShipSlidePerFrame128 = 256;     // 1 column (2 px) per frame, in 1/128 px
    public const int ShipStartY = 128;               // NPLAXC=$2080
    public const int PlayerMinY = YMin + 1;
    public const int PlayerMaxY = 238;
    public const int VerticalStartSpeed = 0x100;     // 1 px/frame on first frame
    public const int VerticalMaxSpeed = 0x200;       // 2 px/frame
    public const int VerticalAccel = 8;              // 8/256 px/frame²
    public const int ReverseRearmFrames = 5;         // released + 5 frames (defa7.src:3157-3171)

    // Laser (defa7.src:2763-2886).
    public const int MaxLasers = 4;
    public const int LaserHeadPxPerFrame = 8;
    public const int LaserFizzlePxPerFrame = 6;
    public const int LaserTailPxPerFrame = 2;

    // Smart bomb / hyperspace.
    public const int SmartBombRearmFrames = 10;      // defa7.src:3203-3208
    public const int HyperspaceBlankFrames = 15;     // defa7.src:3213-3278
    public const int HyperspaceAppearFrames = 40;    // $28
    public const int HyperspaceDeathThreshold = 192; // dies if random byte > 192 (≈24.6%)

    // Shells (enemy shots and mines).
    public const int MaxShells = 20;                 // defa7.src:2557
    public const int MaxMines = 10;                  // defb6.src:1136
    public const int ShellLifetimeFrames = 160;      // 20 SHSCAN ticks × 8
    public const int AimFrames = 64;                 // vx = d*4/256 → reaches aim point in 64 frames (INF)

    // Population caps.
    public const int MaxLandersAlive = 8;            // GEXEC launches squads only if < 8 alive
    public const int MaxSwarmers = 20;               // defb6.src:146
    public const int MaxBaiters = 12;                // defa7.src:1678
    public const int HumanoidCount = 10;             // defa7.src:1146

    // Executive cadence.
    public const int GexecFrames = 15;               // NAP 15 (defa7.src:1731)
    public const int IntraWaveGexecTicks = 40;       // 40 × 15 frames = 10 s (defa7.src:1723)
    public const int ScannerRefreshFrames = 8;       // SCPROC cycle 2+2+4

    // Humanoid physics (defb6.src:294-417, 927-962).
    public const int HumanoidFallAccel = 8;          // OYV += 8 every 4 frames
    public const int HumanoidFallMax = 0x300;
    public const int HumanoidSafeLandingSpeed = 0xE0;
    public const int HumanoidMaxWalkY = 232;
    public const int ScorePopupFrames = 50;
}
