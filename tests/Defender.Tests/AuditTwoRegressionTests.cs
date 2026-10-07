using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

/// <summary>Second source-fidelity audit (player, humanoids, shells, scanner, stars, sound triggers).</summary>
public class AuditTwoRegressionTests
{
    [Fact]
    public void Humanoid_WalksOneStepEvery32Frames()
    {
        var s = TestUtil.NewPlaying();
        var h = s.Humanoids[0];
        h.X = s.WorldAtScreen(150);
        int steps = 0, x = h.X;
        for (int i = 0; i < 320; i++) { s.Step(default); if (h.X != x) { steps++; x = h.X; } }
        Assert.Equal(10, steps);
    }

    [Fact]
    public void Humanoids_ArePlacedAtY224()
    {
        var s = TestUtil.NewPlaying();
        Assert.All(s.Humanoids.Where(h => h.Alive), h => Assert.Equal(224, h.PixelY));
    }

    [Fact]
    public void MineCap_CountsEveryShell()
    {
        var s = TestUtil.NewPlaying();
        for (int i = 0; i < 10; i++) s.Shells.Add(new Shell { X16 = s.WorldAtScreen(20 + i) << 4, Y = 60 << 8, Life = 1000, Vx16 = 0 });
        var b = s.TestSpawn(EnemyKind.Bomber, s.WorldAtScreen(150), 90);
        b.Vx = 0; b.Dir = 0;
        s.Run(100);
        Assert.DoesNotContain(s.Shells, x => x.Mine);
    }

    [Fact]
    public void EnemyShot_StartsAtShooter_WithColumnJitter_AndFiresDuringHyperspaceAppear()
    {
        int minVx = int.MaxValue, maxVx = int.MinValue;
        for (uint seed = 1; seed < 300; seed++)
        {
            var s = TestUtil.NewPlaying(seed);
            var e = s.TestSpawn(EnemyKind.Baiter, s.WorldAtScreen(s.Player.ScreenPx), 100);   // same column as the ship
            e.Vx = e.Vy = 0; e.ShotTimer = 1; e.Nap = 1; e.Dir = 1;
            s.Step(default);
            var sh = s.Shells.Single();
            Assert.Equal(e.Y, sh.Y - sh.Vy);              // starts at the shooter (then moves once this tick)
            minVx = Math.Min(minVx, sh.Vx16); maxVx = Math.Max(maxVx, sh.Vx16);
        }
        Assert.InRange(minVx, -16 * 16, -15 * 16);        // jitter −16..+15 byte columns
        Assert.InRange(maxVx, 14 * 16, 15 * 16);

        var h = TestUtil.NewPlaying(3);
        h.Step(new PlayerInput { HyperspacePressed = true });
        h.Run(Arcade.HyperspaceBlankFrames + 1);                 // now reappearing (40 frames)
        var b = h.TestSpawn(EnemyKind.Baiter, h.WorldAtScreen(150), 100);
        b.Vx = b.Vy = 0; b.ShotTimer = 1; b.Nap = 1; b.Dir = 1;
        h.Step(default);
        Assert.NotEmpty(h.Shells);
    }

    [Fact]
    public void ScannerWindowTicks_AreFixed_AndPlayerBlipTracksScreenColumn()
    {
        var s = TestUtil.NewPlaying();
        var snap = new FrameSnapshot();
        s.Run(200, new PlayerInput { ThrustHeld = true });
        s.BuildSnapshot(snap);
        Assert.Equal((152, 167), (snap.ScannerWindowLeft, snap.ScannerWindowRight));
        Assert.Equal(150 + s.Player.ScreenPx / 32 * 2, snap.ScannerPlayerX);
    }

    [Theory]
    [InlineData(EnemyKind.Lander, Core.Audio.SoundId.EnemyExplode)]
    [InlineData(EnemyKind.Mutant, Core.Audio.SoundId.MutantHit)]
    [InlineData(EnemyKind.Baiter, Core.Audio.SoundId.BaiterHit)]
    [InlineData(EnemyKind.Bomber, Core.Audio.SoundId.BomberHit)]
    [InlineData(EnemyKind.Pod, Core.Audio.SoundId.PodExplode)]
    [InlineData(EnemyKind.Swarmer, Core.Audio.SoundId.SwarmerHit)]
    public void EachEnemyKind_HasItsOwnHitSound(EnemyKind kind, Core.Audio.SoundId expected)
    {
        var t = TestUtil.NewPlaying();
        // Row 2 of every enemy sprite is solid; the laser runs along ship Y + 4.
        var target = t.TestSpawn(kind, t.WorldAtScreen(150), t.Player.PixelY + 2);
        target.Vx = target.Vy = 0; target.Nap = 1000;
        var heard = new List<Core.Audio.SoundId>();
        t.Step(new PlayerInput { FirePressed = true });
        for (int i = 0; i < 30 && !target.Dead; i++) { t.Step(default); heard.AddRange(t.Sounds); }
        Assert.True(target.Dead);
        Assert.Contains(expected, heard);
    }

    [Fact]
    public void Hyperspace_IsSilentOnEntry_AppearSoundOnReturn()
    {
        var s = TestUtil.NewPlaying();
        s.Step(new PlayerInput { HyperspacePressed = true });
        Assert.DoesNotContain(Core.Audio.SoundId.Hyperspace, s.Sounds);
        var heard = new List<Core.Audio.SoundId>();
        for (int i = 0; i < Arcade.HyperspaceBlankFrames; i++) { s.Step(default); heard.AddRange(s.Sounds); }
        Assert.Contains(Core.Audio.SoundId.LanderMaterialize, heard);
    }
}
