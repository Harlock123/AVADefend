using System.Diagnostics;
using Defender.Avalonia.Rendering;
using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

public class PerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void WorstCaseLoad_SimulationPlusRender_FitsComfortablyInA60HzFrame()
    {
        var s = TestUtil.NewPlaying(5);
        s.TestSetWave(20);
        s.TestInvulnerable = true;
        for (int i = 0; i < 8; i++) s.TestSpawn(EnemyKind.Lander, s.WorldAtScreen(20 + i * 30), 80 + i * 10);
        for (int i = 0; i < 20; i++) s.TestSpawn(EnemyKind.Swarmer, s.WorldAtScreen(10 * i), 60 + i * 7);
        for (int i = 0; i < 12; i++) s.TestSpawn(EnemyKind.Baiter, s.WorldAtScreen(25 * i), 70 + i * 9);
        for (int i = 0; i < 6; i++) s.TestSpawn(EnemyKind.Pod, s.WorldAtScreen(50 * i), 120);
        for (int i = 0; i < 5; i++) s.TestSpawn(EnemyKind.Bomber, s.WorldAtScreen(60 * i), 90);
        foreach (var e in s.Enemies) e.ShotTimer = 1;
        int Respawn() { int n = 0; while (s.Enemies.Count < 45) { s.TestSpawn(n % 2 == 0 ? EnemyKind.Swarmer : EnemyKind.Baiter, s.WorldAtScreen(n++ * 13 % 290), 60 + n * 5 % 150).ShotTimer = 1; } return n; }
        var snap = new FrameSnapshot();
        var r = new SoftwareRenderer();
        var input = new PlayerInput { ThrustHeld = true, Vertical = 0 };

        for (int i = 0; i < 120; i++) { s.Step(input); s.BuildSnapshot(snap); r.Render(snap); } // warm-up / JIT
        var sw = Stopwatch.StartNew();
        int frames = 0;
        double worst = 0;
        while (frames < 600)
        {
            long t0 = sw.ElapsedTicks;
            Respawn();
            s.Step(input with { FirePressed = frames % 6 == 0 });
            s.BuildSnapshot(snap);
            r.Render(snap);
            worst = Math.Max(worst, (sw.ElapsedTicks - t0) * 1000.0 / Stopwatch.Frequency);
            frames++;
        }
        double avg = sw.Elapsed.TotalMilliseconds / frames;
        output.WriteLine($"frames={frames} enemies≈{s.Enemies.Count} shells={s.Shells.Count} avg={avg:0.000} ms worst={worst:0.000} ms");
        Assert.Equal(600, frames);
        Assert.True(avg < 4.0, $"average {avg:0.000} ms per frame (budget 16.7 ms)");
    }
}
