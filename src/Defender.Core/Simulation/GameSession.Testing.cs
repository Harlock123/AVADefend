namespace Defender.Core.Simulation;

/// <summary>Scenario staging for headless tests (internal; exposed to Defender.Tests only).</summary>
public sealed partial class GameSession
{
    /// <summary>Starts a game and jumps straight into Playing with an empty sky (no reserves unless kept).</summary>
    internal void TestStartEmpty(bool keepReserves = false)
    {
        StartGame();
        if (!keepReserves) LanderReserve = BomberReserve = PodReserve = MutantReserve = SwarmerReserve = 0;
        BeginPlay();
        Enemies.Clear();
        _firstGexec = false;
        _waveTimer = 1000;
        _baiterTimer = 100000;
        TestHoldWave = !keepReserves;
    }

    /// <summary>Prevents the wave ending and baiters spawning in a staged empty sky (tests only).</summary>
    internal bool TestHoldWave { get; set; }

    /// <summary>Skips player collisions (performance measurements only).</summary>
    internal bool TestInvulnerable { get; set; }

    internal Enemy TestSpawn(EnemyKind kind, int worldX, int yPx)
    {
        Enemy e;
        switch (kind)
        {
            case EnemyKind.Lander:
                e = NewEnemy(kind, worldX, yPx, appear: false);
                e.Vy = Params[WaveVar.LanderYV];
                e.ShotTimer = 10000;
                break;
            case EnemyKind.Swarmer:
                e = NewEnemy(kind, worldX, yPx, appear: false);   // bypasses the 20-swarmer cap
                e.Dir = 1;
                break;
            default:
                e = NewEnemy(kind, worldX, yPx, appear: false);
                if (kind == EnemyKind.Bomber) { e.Squad = 99; e.CruiseY = 80; }
                break;
        }
        e.ShotTimer = 10000;
        return e;
    }

    internal Enemy TestSpawnAppearing(EnemyKind kind, int worldX, int yPx) => NewEnemy(kind, worldX, yPx, appear: true);

    internal void TestKill(Enemy e) => KillEnemy(e, scored: true);
    internal void TestAddScore(int points) => AddScore(points);
    internal void TestSetLanderReserve(int n) => LanderReserve = n;
    internal int TestBaiterTimer { get => _baiterTimer; set => _baiterTimer = value; }
    internal void TestRunExecutive() => GameExecutive();
    internal void TestSetWave(int wave) { Wave = wave; LoadWave(); }
    internal void TestCompleteWaveNow() => BeginWaveComplete();
    internal void TestKillHumanoid(Humanoid h) => KillHumanoid(h);
}
