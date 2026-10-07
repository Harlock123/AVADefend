using Defender.Core.Simulation;
using Defender.Infrastructure.Persistence;

namespace Defender.Tests;

public class SuspendTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "defender-suspend-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SuspendThroughJson_ThenResume_IsExactlyEquivalent()
    {
        var a = new GameSession(policy: GamePolicy.Modern, rng: new XorShiftRandom(77));
        var pilot = new Autopilot();
        for (int i = 0; i < 60 * 40; i++) a.Step(pilot.Next(a));
        Assert.True(a.CanSuspend, a.State.ToString());

        var store = new Storage(_dir).Suspend(1);
        store.Save(new SuspendFile { SavedUtc = DateTime.UtcNow, Data = a.CaptureSuspend() });
        var loaded = store.Load();
        Assert.Equal(LoadStatus.Loaded, loaded.Status);
        var b = GameSession.Restore(loaded.Value.Data!, GamePolicy.Modern);
        if (b.Paused) b.SetPaused(false);

        // Drive both with identical inputs (generated from a); they must stay in lock-step.
        var pilotB = new Autopilot();
        for (int i = 0; i < 60 * 30; i++)
        {
            var input = pilot.Next(a);
            a.Step(input);
            b.Step(input);
        }
        Assert.Equal((a.Score, a.Wave, a.Lives, a.SmartBombs, a.State, a.CameraX, a.Enemies.Count, a.Rng.State),
                     (b.Score, b.Wave, b.Lives, b.SmartBombs, b.State, b.CameraX, b.Enemies.Count, b.Rng.State));
    }

    [Fact]
    public void Restore_RejectsInconsistentData()
    {
        var a = TestUtil.NewPlaying();
        var d = a.CaptureSuspend();
        d.Humanoids.RemoveAt(0);
        Assert.Throws<InvalidDataException>(() => GameSession.Restore(d, GamePolicy.Modern));
    }

    [Fact]
    public void CannotSuspend_InAttract()
    {
        var s = new GameSession();
        Assert.False(s.CanSuspend);
        Assert.Throws<InvalidOperationException>(() => s.CaptureSuspend());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
