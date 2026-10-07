using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

public class HumanoidTests
{
    /// <summary>Stage: humanoid 0 off-screen at world X 0x8000 (so it does not walk), humanoid 1 kept alive elsewhere.</summary>
    private static (GameSession s, Humanoid h) Stage()
    {
        var s = TestUtil.NewPlaying();
        for (int i = 2; i < s.Humanoids.Length; i++) s.Humanoids[i].State = HumanoidState.Dead;
        var h = s.Humanoids[0];
        h.X = 0x8000;
        h.Y = (s.Terrain.HeightAtUnits(h.X) + 4) << 8;
        s.Humanoids[1].X = 0xC000;
        return (s, h);
    }

    [Fact]
    public void StartsWith10Humanoids_SpreadAroundThePlanet()
    {
        var s = new GameSession(rng: new XorShiftRandom(3));
        s.TestStartEmpty();
        Assert.Equal(10, s.HumanoidsAlive);
        var quadrants = s.Humanoids.Take(8).Select(h => h.X >> 14).GroupBy(q => q).ToList();
        Assert.Equal(4, quadrants.Count);
        Assert.All(quadrants, g => Assert.Equal(2, g.Count()));
    }

    [Fact]
    public void Lander_Descends_Grabs_Lifts_AndBecomesMutant()
    {
        var (s, h) = Stage();
        var l = s.TestSpawn(EnemyKind.Lander, h.X + 8 * Arcade.UnitsPerPixel, 80);
        l.Target = 0;
        l.Vx = 0;
        s.RunUntil(() => l.Phase == LanderPhase.Descend, 30);
        s.RunUntil(() => h.State == HumanoidState.Grabbed, 600);
        Assert.Equal(LanderPhase.Lift, l.Phase);
        Assert.Contains(Core.Audio.SoundId.Abduction, s.Sounds);
        s.RunUntil(() => l.Kind == EnemyKind.Mutant, 1200);
        Assert.Equal(HumanoidState.Dead, h.State);
        Assert.True(l.PixelY <= Arcade.YMin + 9);
        Assert.Equal(1, s.HumanoidsAlive);
        Assert.True(s.PlanetActive);
    }

    [Fact]
    public void ShootingAbductor_DropsHumanoid_WhichCanBeCaught_ThenSetDown_For500Plus500()
    {
        var (s, h) = Stage();
        var l = s.TestSpawn(EnemyKind.Lander, h.X, 120);
        l.Target = 0; l.Vx = 0;
        s.RunUntil(() => l.Phase == LanderPhase.Lift && l.PixelY < 150, 900);
        s.TestKill(l);
        Assert.Equal(150, s.Score);
        Assert.Equal(HumanoidState.Falling, h.State);
        // Fly the ship onto the falling humanoid.
        h.X = s.Player.WorldX(s.CameraX) + 4 * Arcade.UnitsPerPixel;
        h.Y = s.Player.Y + (2 << 8);
        s.Step(default);
        Assert.Equal(HumanoidState.Rescued, h.State);
        Assert.Equal(650, s.Score);
        // Descend to the ground: set down for another 500.
        s.RunUntil(() => h.State == HumanoidState.Walking, 400, new PlayerInput { Vertical = 1 });
        Assert.Equal(1150, s.Score);
    }

    [Fact]
    public void ShortFall_LandsSafely_For250()
    {
        var (s, h) = Stage();
        int ground = s.Terrain.HeightAtUnits(h.X) + 4;
        h.State = HumanoidState.Falling; h.Vy = 0; h.Y = (ground - 30) << 8;
        s.RunUntil(() => h.State != HumanoidState.Falling, 400);
        Assert.Equal(HumanoidState.Walking, h.State);
        Assert.Equal(250, s.Score);
    }

    [Fact]
    public void LongFall_Kills()
    {
        var (s, h) = Stage();
        int ground = s.Terrain.HeightAtUnits(h.X) + 4;
        h.State = HumanoidState.Falling; h.Vy = 0; h.Y = (ground - 100) << 8;
        s.RunUntil(() => h.State != HumanoidState.Falling, 600);
        Assert.Equal(HumanoidState.Dead, h.State);
        Assert.Equal(0, s.Score);
    }

    [Fact]
    public void SafeFallHeight_IsAbout49Px()
    {
        // Find the largest drop that survives (source-derived expectation ≈ 49 px).
        int best = 0;
        for (int drop = 30; drop <= 70; drop++)
        {
            var (s, h) = Stage();
            int ground = s.Terrain.HeightAtUnits(h.X) + 4;
            h.State = HumanoidState.Falling; h.Vy = 0; h.Y = (ground - drop) << 8;
            s.RunUntil(() => h.State != HumanoidState.Falling, 800);
            if (h.State == HumanoidState.Walking) best = drop;
        }
        Assert.InRange(best, 44, 54);
    }

    [Fact]
    public void LosingLastHumanoid_ExplodesPlanet_AndLandersBecomeMutants()
    {
        var (s, h) = Stage();
        var lander = s.TestSpawn(EnemyKind.Lander, 0x4000, 100);
        s.TestKillHumanoid(s.Humanoids[1]);
        Assert.True(s.PlanetActive);
        s.TestKillHumanoid(h);
        Assert.False(s.PlanetActive);
        Assert.Contains(Core.Audio.SoundId.PlanetExplode, s.Sounds);
        s.Run(7);
        Assert.Equal(EnemyKind.Mutant, lander.Kind);
        // New lander squads arrive as mutants while the planet is gone.
        s.TestSetLanderReserve(5);
        s.TestRunExecutive();
        Assert.DoesNotContain(s.Enemies, e => e.Kind == EnemyKind.Lander && !e.Dead);
        Assert.True(s.Enemies.Count(e => e.Kind == EnemyKind.Mutant) >= 6);
    }

    [Fact]
    public void Planet_And10Humanoids_RestoredOnEveryFifthWave()
    {
        var s = TestUtil.NewPlaying();
        foreach (var h in s.Humanoids) s.TestKillHumanoid(h);
        Assert.False(s.PlanetActive);
        s.TestSetWave(4);
        s.TestCompleteWaveNow();
        s.RunUntil(() => s.State == SessionState.LifeStart, 1000);
        Assert.Equal(5, s.Wave);
        Assert.True(s.PlanetActive);
        Assert.Equal(10, s.HumanoidsAlive);
    }

    [Fact]
    public void SurvivingHumanoids_CarryOver_BetweenNonRestoreWaves()
    {
        var s = TestUtil.NewPlaying();
        for (int i = 0; i < 4; i++) s.TestKillHumanoid(s.Humanoids[i]);
        s.TestCompleteWaveNow();
        s.RunUntil(() => s.State == SessionState.LifeStart, 1000);
        Assert.Equal(2, s.Wave);
        Assert.Equal(6, s.HumanoidsAlive);
    }
}
