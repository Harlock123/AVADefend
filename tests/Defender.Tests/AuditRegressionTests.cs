using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

/// <summary>One test per finding of the source-fidelity audit (see FIDELITY.md for citations).</summary>
public class AuditRegressionTests
{
    private static Enemy Frozen(GameSession s, EnemyKind k, int screenX, int y)
    {
        var e = s.TestSpawn(k, s.WorldAtScreen(screenX), y);
        e.Vx = e.Vy = 0;
        return e;
    }

    [Fact] // 1
    public void Laser_KillsWalkingHumanoid_WithoutScore()
    {
        var s = TestUtil.NewPlaying();
        var h = s.Humanoids[0];
        h.X = s.WorldAtScreen(150);
        h.Y = (s.Player.PixelY + 4 - 3) << 8;
        int before = s.HumanoidsAlive;
        s.Step(new PlayerInput { FirePressed = true });
        s.Run(20);
        Assert.Equal(HumanoidState.Dead, h.State);
        Assert.Equal(before - 1, s.HumanoidsAlive);
        Assert.Equal(0, s.Score);
    }

    [Fact] // 2
    public void Mutant_ShootsFromTheAvoidBranch_WhenOnScreen()
    {
        var s = TestUtil.NewPlaying();
        var m = Frozen(s, EnemyKind.Mutant, 250, 60);  // far from the player in X: avoid branch
        m.ShotTimer = 1; m.Nap = 1;
        s.Step(default);
        Assert.Single(s.Shells);
    }

    [Theory] // 3
    [InlineData(-30, true)]   // mutant 30 px LEFT of the player: player − mutant = +30 → seek
    [InlineData(30, false)]   // mutant 30 px RIGHT: −30 → outside the −12..+44 window
    public void MutantSeekWindow_IsPlayerMinusMutant(int mutantOffsetPx, bool seeks)
    {
        var s = TestUtil.NewPlaying();
        var m = s.TestSpawn(EnemyKind.Mutant, (s.Player.WorldX(s.CameraX) + mutantOffsetPx * 32) & 0xFFFF, s.Player.PixelY - 40);
        m.Nap = 1;
        s.Step(default);
        Assert.Equal(seeks, m.Vy > 0); // seeking: heads down toward the player; avoiding: 0 (40 px away)
    }

    [Fact] // 4
    public void Lander_ShootsWhileDescending()
    {
        var s = TestUtil.NewPlaying();
        var h = s.Humanoids[0];
        h.X = s.WorldAtScreen(150);
        var l = Frozen(s, EnemyKind.Lander, 150, 120);
        l.Target = 0; l.Phase = LanderPhase.Descend; l.ShotTimer = 3; l.Nap = 1;
        s.Run(3);
        Assert.NotEmpty(s.Shells);
    }

    [Fact] // 5
    public void LanderReachingTop_WithDeadHumanoid_ReturnsToReserve_Unscored()
    {
        var s = TestUtil.NewPlaying();
        var h = s.Humanoids[0];
        h.X = 0x8000;
        var l = s.TestSpawn(EnemyKind.Lander, 0x8000, 120);
        l.Target = 0; l.Vx = 0;
        s.RunUntil(() => l.Phase == LanderPhase.Lift, 900);
        s.TestKillHumanoid(h);
        Assert.Equal(LanderPhase.Lift, l.Phase);      // keeps climbing, no target check
        int reserve = s.LanderReserve;
        s.RunUntil(() => l.Dead, 600);
        Assert.Equal(reserve + 1, s.LanderReserve);
        Assert.Equal(0, s.Score);
    }

    [Fact] // 6
    public void LanderLosingTargetDuringDescent_StopsMovingInX()
    {
        var s = TestUtil.NewPlaying();
        var h = s.Humanoids[0];
        h.X = 0x8000;
        var l = s.TestSpawn(EnemyKind.Lander, 0x8000, 100);
        l.Target = 0; l.Phase = LanderPhase.Descend; l.Vx = 0; l.Nap = 1;
        s.TestKillHumanoid(h);
        s.Step(default);
        Assert.Equal(LanderPhase.Cruise, l.Phase);
        Assert.Equal(0, l.Vx);
    }

    [Fact] // 7
    public void Humanoids_AreRePlacedEachLife_KeepingTheCount()
    {
        var s = TestUtil.NewPlaying();
        s.TestKillHumanoid(s.Humanoids[0]);
        var before = s.Humanoids.Where(h => h.Alive).Select(h => h.X).ToList();
        var e = Frozen(s, EnemyKind.Mutant, s.Player.ScreenPx, s.Player.PixelY);
        e.Nap = 1000;
        s.Step(default);
        s.RunUntil(() => s.State == SessionState.LifeStart, 400);
        Assert.Equal(9, s.HumanoidsAlive);
        Assert.NotEqual(before, s.Humanoids.Where(h => h.Alive).Select(h => h.X).ToList());
    }

    [Fact] // 8
    public void EnemiesKeepMovingDuringDeathGlow_ThenFreeze()
    {
        var s = TestUtil.NewPlaying();
        var mover = s.TestSpawn(EnemyKind.Pod, 0x8000, 100);
        mover.Vx = 20;
        var e = Frozen(s, EnemyKind.Mutant, s.Player.ScreenPx, s.Player.PixelY);
        e.Nap = 1000;
        s.Step(default);
        Assert.Equal(SessionState.Dying, s.State);
        int x0 = mover.X;
        s.Run(10);
        Assert.Equal((x0 + 200) & 0xFFFF, mover.X);
        s.Run(30);
        int x1 = mover.X;
        s.Run(20);
        Assert.Equal(x1, mover.X);
    }

    [Fact] // 9
    public void Mines_DieWhenOffScreen()
    {
        var s = TestUtil.NewPlaying();
        s.Shells.Add(new Shell { X16 = s.WorldAtScreen(150) << 4, Y = 100 << 8, Mine = true, Life = 250 });
        s.Run(5, new PlayerInput { ThrustHeld = true });
        Assert.Single(s.Shells);
        s.RunUntil(() => s.Shells.Count == 0, 200, new PlayerInput { ThrustHeld = true });
    }

    [Fact] // 10
    public void SplitByteWaveRows_NeverCarryIntoTheHighByte()
    {
        var p = WaveParams.For(WaveTable.Default, 2, 5, 15);
        Assert.Equal(0x0FE, p[WaveVar.MutantYV]);
        p.ApplyIntra(WaveTable.Default);          // +8 would overflow the LSB → skipped
        Assert.Equal(0x0FE, p[WaveVar.MutantYV]);
        var l = WaveParams.For(WaveTable.Default, 2, 5, 15);
        for (int i = 0; i < 50; i++) l.ApplyIntra(WaveTable.Default);
        Assert.True(l[WaveVar.LanderYV] <= 0xFF);
    }

    [Fact] // 11
    public void SquadTimer_ReloadsEvenWhenNoSquadLaunches()
    {
        var s = TestUtil.NewPlaying(keepReserves: true);
        s.TestHoldWave = true;
        for (int i = 0; i < 9; i++) s.TestSpawn(EnemyKind.Lander, 0x8000 + i * 0x400, 100);
        s.TestRunExecutive();                        // first tick: 9 alive → no launch, timer reloaded
        int alive = s.Enemies.Count(e => e.Kind == EnemyKind.Lander);
        foreach (var e in s.Enemies.Where(e => e.Kind == EnemyKind.Lander).Take(3)) s.TestKill(e);
        s.Run(1);
        s.TestRunExecutive();                        // 6 alive, but the timer has not expired → still no launch
        Assert.Equal(alive - 3, s.Enemies.Count(e => e.Kind == EnemyKind.Lander && !e.Dead));
    }

    [Fact] // 12
    public void OnePlayerLifeStart_Is96Frames_TwoPlayerIs224()
    {
        Assert.Equal(96, new GameSession().LifeStartFrames);
        var s2 = new GameSession(rng: new XorShiftRandom(1));
        s2.Step(new PlayerInput { Start2Pressed = true });
        Assert.Equal(224, s2.LifeStartFrames);
    }

    [Fact] // 13
    public void MutantHop_IsExactlyPlusOrMinusSzry()
    {
        var s = TestUtil.NewPlaying();
        s.TestSetWave(3);                            // SZRY = 2
        var m = Frozen(s, EnemyKind.Mutant, 250, 60);   // far from the player in X and Y: avoid branch, vy = 0
        var deltas = new HashSet<int>();
        for (int i = 0; i < 40; i++)
        {
            m.Nap = 1; m.ShotTimer = 1000;
            m.Y = 60 << 8;
            s.Step(default);
            deltas.Add(m.PixelY - 60);
        }
        Assert.Equal(new HashSet<int> { -2, 2 }, deltas);
    }

    [Fact] // 14
    public void Pods_HaveVerticalSpeed32To64()
    {
        var flow = new GameSession(rng: new XorShiftRandom(8));
        flow.Step(new PlayerInput { StartPressed = true });
        flow.TestSetWave(4);
        flow.RunUntil(() => flow.State == SessionState.Playing, 400);
        var pods = flow.Enemies.Where(e => e.Kind == EnemyKind.Pod).ToList();
        Assert.NotEmpty(pods);
        Assert.All(pods, p => Assert.InRange(Math.Abs(p.Vy), 32, 64));
    }

    [Fact] // 15
    public void BomberSquad_Spaced0x180_HalfAPlanetAway_AtY80()
    {
        var s = new GameSession(rng: new XorShiftRandom(8));
        s.Step(new PlayerInput { StartPressed = true });
        s.TestSetWave(4);
        int px = s.Player.WorldX(0);
        s.RunUntil(() => s.State == SessionState.Playing, 400);
        var squad = s.Enemies.Where(e => e.Kind == EnemyKind.Bomber && e.Squad == s.Enemies.First(b => b.Kind == EnemyKind.Bomber).Squad)
                     .OrderBy(e => e.Dir).ToList();
        Assert.Equal(3, squad.Count);
        Assert.All(squad, b => Assert.Equal(0, b.Appear));
        int[] xs = squad.Select(b => (b.X - squad[^1].X) & 0xFFFF).OrderBy(x => x).ToArray();
        Assert.Equal([0, 0x180, 0x300], xs);
    }

    [Fact] // 16
    public void HyperspaceBlank_FreezesEnemies()
    {
        var s = TestUtil.NewPlaying();
        var pod = s.TestSpawn(EnemyKind.Pod, 0x8000, 100);
        pod.Vx = 20;
        s.Step(new PlayerInput { HyperspacePressed = true });
        int x0 = pod.X;
        s.Run(10);
        Assert.Equal(x0, pod.X);
    }

    [Fact] // 17
    public void Baiter_FirstShotTimerIs8()
    {
        var s = TestUtil.NewPlaying();
        s.TestHoldWave = false;
        s.TestSpawn(EnemyKind.Bomber, 0x8000, 90);   // keeps the wave alive
        s.TestBaiterTimer = 1;
        s.TestRunExecutive();
        var b = s.Enemies.Single(e => e.Kind == EnemyKind.Baiter);
        Assert.Equal(8, b.ShotTimer);
    }

    [Fact] // 18
    public void Swarmers_SpawnExactlyAtThePod_WithoutMaterialising()
    {
        var s = TestUtil.NewPlaying(3);
        var pod = s.TestSpawn(EnemyKind.Pod, 0x8000, 100);
        s.TestKill(pod);
        var sw = s.Enemies.Where(e => e.Kind == EnemyKind.Swarmer).ToList();
        Assert.NotEmpty(sw);
        Assert.All(sw, w => Assert.Equal((0x8000, 100, 0), (w.X, w.PixelY, w.Appear)));
    }

    [Fact] // appear (APST)
    public void Materialising_OnlyOnScreen_AndImmuneToSmartBomb()
    {
        for (uint seed = 1; seed < 200; seed++)
        {
            var s = TestUtil.NewPlaying(seed);
            s.TestSetLanderReserve(5);
            s.TestRunExecutive();
            Assert.All(s.Enemies, e => Assert.Equal(((e.X - s.CameraX) & 0xFFFF) <= 0x2600, e.Appear > 0));
            var appearing = s.Enemies.FirstOrDefault(e => e.Appear > 0);
            if (appearing is null) continue;
            s.Step(new PlayerInput { SmartBombPressed = true });
            Assert.False(appearing.Dead);
            return;
        }
        Assert.Fail("no seed produced an on-screen spawn");
    }
}
