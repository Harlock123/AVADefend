using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

public class EnemyTests
{
    [Theory]
    [InlineData(EnemyKind.Lander, 150)]
    [InlineData(EnemyKind.Mutant, 150)]
    [InlineData(EnemyKind.Swarmer, 150)]
    [InlineData(EnemyKind.Baiter, 200)]
    [InlineData(EnemyKind.Bomber, 250)]
    [InlineData(EnemyKind.Pod, 1000)]
    public void KillScores_MatchOriginal(EnemyKind kind, int points)
    {
        var s = TestUtil.NewPlaying();
        s.TestKill(s.TestSpawn(kind, 0x8000, 100));
        Assert.Equal(points, s.Score);
    }

    [Fact]
    public void Pod_Releases1To7Swarmers()
    {
        var counts = new HashSet<int>();
        for (uint seed = 1; seed < 200; seed++)
        {
            var s = TestUtil.NewPlaying(seed);
            s.TestKill(s.TestSpawn(EnemyKind.Pod, 0x8000, 100));
            int n = s.Enemies.Count(e => e.Kind == EnemyKind.Swarmer && !e.Dead);
            Assert.InRange(n, 1, 7);
            counts.Add(n);
        }
        Assert.Equal(7, counts.Count); // every value 1..7 occurs
    }

    [Fact]
    public void Swarmers_CappedAt20()
    {
        var s = TestUtil.NewPlaying();
        for (int i = 0; i < 10; i++) s.TestKill(s.TestSpawn(EnemyKind.Pod, 0x8000, 100));
        Assert.True(s.Enemies.Count(e => e.Kind == EnemyKind.Swarmer && !e.Dead) <= Arcade.MaxSwarmers);
    }

    [Fact]
    public void Bomber_LaysStationaryMines_UpToCap()
    {
        var s = TestUtil.NewPlaying();
        var b = s.TestSpawn(EnemyKind.Bomber, 0x8000, 90);
        b.Vx = 0x20;
        int maxMines = 0;
        for (int i = 0; i < 400; i++)
        {
            s.Step(default);
            var mines = s.Shells.Where(x => x.Mine).ToList();
            maxMines = Math.Max(maxMines, mines.Count);
            Assert.All(mines, m => Assert.Equal(0, m.Vx16));
            Assert.All(mines, m => Assert.InRange(m.Life, 0, 256));
        }
        Assert.InRange(maxMines, 1, Arcade.MaxMines);
    }

    [Fact]
    public void Mutant_HomesTowardsPlayer()
    {
        var s = TestUtil.NewPlaying();
        var m = s.TestSpawn(EnemyKind.Mutant, s.WorldAtScreen(250), 100);
        int d0 = Math.Abs(s.SignedScreenX(m.X) - s.Player.ScreenPx);
        s.Run(60);
        int d1 = Math.Abs(s.SignedScreenX(m.X) - s.Player.ScreenPx);
        Assert.True(d1 < d0 - 30, $"{d0} -> {d1}");
    }

    [Fact]
    public void EnemyShot_IsAimed_AndPooledTo20()
    {
        var s = TestUtil.NewPlaying();
        for (int i = 0; i < 30; i++)
        {
            var e = s.TestSpawn(EnemyKind.Baiter, s.WorldAtScreen(200), 60 + i);
            e.ShotTimer = 1; e.Nap = 1;
        }
        s.Step(default);
        Assert.Equal(Arcade.MaxShells, s.Shells.Count);
        Assert.All(s.Shells, sh => Assert.True(sh.Vx16 < 0, "shots head towards the player on the left"));
    }
}

public class ExecutiveTests
{
    [Fact]
    public void WaveParams_EffectiveValues_MatchSourceArithmetic()
    {
        var p1 = WaveParams.For(WaveTable.Default, 1, 5, 15);
        Assert.Equal(0x20, p1[WaveVar.LanderXV]);
        Assert.Equal(0x40, p1[WaveVar.LanderShotTimer]);
        Assert.Equal(0x80, p1[WaveVar.MutantYV]);
        Assert.Equal(192, p1[WaveVar.BaiterTime]);
        Assert.Equal(200, p1[WaveVar.BaiterSeek]);
        Assert.Equal(15, p1[WaveVar.Landers]);
        var p4 = WaveParams.For(WaveTable.Default, 4, 5, 15);
        Assert.Equal((20, 5, 4), (p4[WaveVar.Landers], p4[WaveVar.Bombers], p4[WaveVar.Pods]));
        Assert.Equal(128, p4[WaveVar.BaiterTime]);
        // Ceiling: waves 14+ saturate.
        Assert.Equal(WaveParams.For(WaveTable.Default, 14, 5, 15).Raw, WaveParams.For(WaveTable.Default, 40, 5, 15).Raw);
    }

    [Fact]
    public void WaveTable_RejectsMalformedData()
    {
        var rows = WaveTable.Default.Rows.ToList();
        Assert.Throws<InvalidDataException>(() => new WaveTable(rows.Skip(1)));
        Assert.Throws<InvalidDataException>(() => new WaveTable(rows.Append(rows[0])));
        rows[0] = rows[0] with { Min = 50, Max = 10 };
        Assert.Throws<InvalidDataException>(() => new WaveTable(rows));
    }

    [Fact]
    public void Landers_ArriveInSquadsOf5_AtMost8Alive()
    {
        var s = TestUtil.NewPlaying(keepReserves: true);
        s.TestHoldWave = true;
        s.TestRunExecutive();
        Assert.Equal(5, s.Enemies.Count(e => e.Kind == EnemyKind.Lander));
        for (int i = 0; i < 40; i++) s.TestRunExecutive();
        Assert.True(s.Enemies.Count(e => e.Kind == EnemyKind.Lander) <= 10); // squads only launch while < 8 alive
    }

    [Fact]
    public void Wave1_ContainsOnly15Landers()
    {
        var s = new GameSession(rng: new XorShiftRandom(5));
        s.Step(new PlayerInput { StartPressed = true });
        Assert.Equal(15, s.LanderReserve);
        Assert.Equal(0, s.BomberReserve + s.PodReserve);
    }

    [Fact]
    public void Baiter_FirstAppears_After192ExecutiveTicks_WithManyEnemiesLeft()
    {
        var s = TestUtil.NewPlaying(keepReserves: true);
        s.TestHoldWave = false;
        s.TestBaiterTimer = s.Params[WaveVar.BaiterTime];
        s.TestSetLanderReserve(15);
        int ticks = 0;
        while (!s.Enemies.Any(e => e.Kind == EnemyKind.Baiter) && ticks < 400) { s.TestRunExecutive(); ticks++; }
        Assert.InRange(ticks, 190, 193); // ≈ 192 × 15 frames ≈ 48 s
    }

    [Fact]
    public void Baiter_ComesSooner_WhenFewEnemiesRemain()
    {
        var s = TestUtil.NewPlaying();
        s.TestHoldWave = false;
        s.TestSpawn(EnemyKind.Bomber, 0x8000, 90);
        s.TestBaiterTimer = s.Params[WaveVar.BaiterTime];
        int ticks = 0;
        while (!s.Enemies.Any(e => e.Kind == EnemyKind.Baiter) && ticks < 400) { s.TestRunExecutive(); ticks++; }
        Assert.InRange(ticks, 1, 192 / 4 + 1);
    }

    [Fact]
    public void WaveClears_WhenAllButBaitersAreGone_AndPays100xWavePerHumanoid()
    {
        var s = TestUtil.NewPlaying();
        s.TestHoldWave = false;
        s.TestSpawn(EnemyKind.Baiter, 0x8000, 100);
        s.TestRunExecutive();
        Assert.Equal(SessionState.WaveComplete, s.State);
        s.RunUntil(() => s.State == SessionState.LifeStart, 1000);
        Assert.Equal(10 * 100, s.Score);
        Assert.Equal(2, s.Wave);
    }

    [Fact]
    public void WaveBonusMultiplier_CapsAt500()
    {
        var s = TestUtil.NewPlaying();
        s.TestSetWave(9);
        Assert.Equal(500, s.WaveBonusPerHumanoid);
    }
}

public class SmartBombAndHyperspaceTests
{
    [Fact]
    public void SmartBomb_KillsOnScreenEnemiesOnly_Scores_SparesHumanoidsAndShells()
    {
        var s = TestUtil.NewPlaying();
        var on1 = s.TestSpawn(EnemyKind.Lander, s.WorldAtScreen(150), 100);
        var on2 = s.TestSpawn(EnemyKind.Pod, s.WorldAtScreen(250), 160);
        var off = s.TestSpawn(EnemyKind.Bomber, s.WorldAtScreen(600), 100);
        var h = s.Humanoids[0];
        h.X = s.WorldAtScreen(100);
        s.Shells.Add(new Shell { X16 = s.WorldAtScreen(200) << 4, Y = 150 << 8, Mine = true, Life = 200 });
        int humans = s.HumanoidsAlive;
        s.Step(new PlayerInput { SmartBombPressed = true });
        Assert.True(on1.Dead && on2.Dead);
        Assert.False(off.Dead);
        Assert.Equal(1150, s.Score);
        Assert.Equal(humans, s.HumanoidsAlive);
        Assert.Contains(s.Shells, x => x.Mine);
        Assert.Equal(2, s.SmartBombs);
    }

    [Fact]
    public void SmartBomb_HasRearmDelay_AndRunsOut()
    {
        var s = TestUtil.NewPlaying();
        s.Step(new PlayerInput { SmartBombPressed = true });
        s.Step(new PlayerInput { SmartBombPressed = true });
        Assert.Equal(2, s.SmartBombs);
        for (int i = 0; i < 5; i++) { s.Run(11); s.Step(new PlayerInput { SmartBombPressed = true }); }
        Assert.Equal(0, s.SmartBombs);
    }

    [Fact]
    public void Hyperspace_Relocates_ClearsShells_AndKillsAboutQuarterOfTheTime()
    {
        int deaths = 0, trials = 2000;
        for (uint seed = 1; seed <= trials; seed++)
        {
            var s = TestUtil.NewPlaying(seed);
            s.Shells.Add(new Shell { X16 = s.WorldAtScreen(200) << 4, Y = 100 << 8, Life = 100 });
            s.Step(new PlayerInput { HyperspacePressed = true });
            Assert.True(s.Player.InHyperspace);
            s.Run(Arcade.HyperspaceBlankFrames);
            Assert.Empty(s.Shells);
            Assert.InRange(s.Player.PixelY, Arcade.YMin, Arcade.YMin + 128);
            Assert.Equal(0, s.Player.V16);
            s.Run(Arcade.HyperspaceAppearFrames);
            if (s.State == SessionState.Dying) deaths++;
        }
        double rate = deaths / (double)trials;
        Assert.InRange(rate, 0.21, 0.28); // expected 63/256 ≈ 0.246; ±~4σ
    }
}

public class ScoringAndLivesTests
{
    [Fact]
    public void StartsWith3Ships_And3SmartBombs()
    {
        var s = TestUtil.NewPlaying();
        Assert.Equal(2, s.Lives);         // reserve ships while the first is flying
        Assert.Equal(3, s.SmartBombs);
    }

    [Fact]
    public void BonusShipAndSmartBomb_Every10000()
    {
        var s = TestUtil.NewPlaying();
        s.TestAddScore(9999);
        Assert.Equal(2, s.Lives);
        s.TestAddScore(1);
        Assert.Equal((3, 4), (s.Lives, s.SmartBombs));
        Assert.Contains(Core.Audio.SoundId.ExtraLife, s.Sounds);
        s.TestAddScore(25_000);
        Assert.Equal((5, 6), (s.Lives, s.SmartBombs));
    }

    [Fact]
    public void Ramming_KillsAndScoresEnemy_ThenPlayerDies()
    {
        var s = TestUtil.NewPlaying();
        var e = s.TestSpawn(EnemyKind.Lander, s.Player.WorldX(s.CameraX), s.Player.PixelY);
        e.Vx = e.Vy = 0; e.Nap = 1000;
        s.Step(default);
        Assert.True(e.Dead);
        Assert.Equal(150, s.Score);
        Assert.Equal(SessionState.Dying, s.State);
    }

    [Fact]
    public void LosingAllShips_EndsGame_ThenHighScoreEntry()
    {
        var s = TestUtil.NewPlaying();
        s.TestAddScore(1234);
        for (int life = 0; life < 3; life++)
        {
            s.RunUntil(() => s.State == SessionState.Playing, 400);
            var e = s.TestSpawn(EnemyKind.Mutant, s.Player.WorldX(s.CameraX), s.Player.PixelY);
            e.Vx = e.Vy = 0; e.Nap = 1000;
            s.Step(default);
            Assert.Equal(SessionState.Dying, s.State);
            s.RunUntil(() => s.State != SessionState.Dying, 400);
        }
        Assert.Equal(SessionState.GameOver, s.State);
        s.RunUntil(() => s.State == SessionState.EnterInitials, 400);
        foreach (var c in "ZED") s.TypeInitial(c);
        Assert.Equal(SessionState.Attract, s.State);
        Assert.Equal("ZED", s.HighScores.AllTime.Entries[0].Initials);
        Assert.Equal(1234 + 3 * 150, s.HighScores.AllTime.Entries[0].Score);
    }

    [Fact]
    public void Replay_IsDeterministic_ForSeedAndInputs()
    {
        static (int, int, int, long) Play(uint seed)
        {
            var s = new GameSession(rng: new XorShiftRandom(seed));
            var pilot = new Autopilot();
            for (int i = 0; i < 60 * 120; i++) s.Step(pilot.Next(s));
            return (s.Score, s.Wave, s.Lives, s.Frame);
        }
        Assert.Equal(Play(11), Play(11));
        Assert.NotEqual(Play(11), Play(12));
    }

    [Fact]
    public void Pause_FreezesSimulation()
    {
        var s = TestUtil.NewPlaying();
        s.Step(new PlayerInput { PausePressed = true });
        long f = s.Frame;
        s.Run(100, new PlayerInput { ThrustHeld = true });
        Assert.Equal(f, s.Frame);
        Assert.False(s.ThrustSoundOn);
        s.Step(new PlayerInput { PausePressed = true });
        s.Step(default);
        Assert.Equal(f + 2, s.Frame); // the unpausing tick also simulates
    }
}
