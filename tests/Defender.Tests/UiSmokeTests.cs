using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Defender.Avalonia;
using Defender.Avalonia.Rendering;
using Defender.Core.Simulation;
using Defender.Infrastructure.Audio;

[assembly: AvaloniaTestApplication(typeof(Defender.Tests.TestAppBuilder))]

namespace Defender.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class UiSmokeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "defender-ui-" + Guid.NewGuid().ToString("N"));

    private GameHost NewHost() => new(_dir, new NullSoundEngine(), enableGamepad: false);

    [AvaloniaFact]
    public void Window_RendersFrames_WithoutException_AndShowsPixels()
    {
        var host = NewHost();
        var w = new MainWindow(host);
        w.Show();
        for (int i = 0; i < 5; i++) { host.Frame(TimeSpan.FromMilliseconds(16.7)); w.View.RenderFrame(); }
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(w.View.FramesPresented >= 5);
        Assert.Contains(w.View.Renderer.Pixels, p => p != 0xFF000000u); // something besides black was drawn
        w.Close();
    }

    [AvaloniaFact]
    public void KeyboardInput_ReachesEngine()
    {
        var host = NewHost();
        var w = new MainWindow(host);
        w.Show();
        Assert.Equal(SessionState.Attract, host.Session.State);
        w.KeyPress(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, "1");
        host.Frame(TimeSpan.FromMilliseconds(17));
        w.KeyRelease(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, "1");
        host.Frame(TimeSpan.FromMilliseconds(17));
        Assert.Equal(SessionState.LifeStart, host.Session.State);
        host.Frame(TimeSpan.FromSeconds(0.13)); host.Frame(TimeSpan.FromSeconds(0.13)); // drains LifeStart (capped ticks/frame)
        for (int i = 0; i < 20; i++) host.Frame(TimeSpan.FromMilliseconds(100));
        Assert.Equal(SessionState.Playing, host.Session.State);
        w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        for (int i = 0; i < 30; i++) host.Frame(TimeSpan.FromMilliseconds(16.7));
        Assert.True(host.Session.Player.V16 > 0, "holding Space thrusts");
        Assert.True(host.Session.ThrustSoundOn);
        w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        w.Close();
    }

    [AvaloniaTheory]
    [InlineData(800, 600)]
    [InlineData(1920, 1080)]
    [InlineData(300, 900)]
    public void Scaling_PreservesArcadeAspectRatio(double width, double height)
    {
        var host = NewHost();
        var w = new MainWindow(host) { Width = width, Height = height };
        w.Show();
        var r = w.View.DestinationRect(new Size(width, height));
        Assert.Equal(292.0 / 240.0, r.Width / r.Height, 3);
        Assert.True(r.Width <= width + 0.01 && r.Height <= height + 0.01);
        Assert.Equal((width - r.Width) / 2, r.X, 3); // centred letterbox/pillarbox
        w.Close();
    }

    [Fact]
    public void SoftwareRenderer_DrawsEveryStateHeadlessly()
    {
        var s = new GameSession(rng: new XorShiftRandom(9));
        var snap = new FrameSnapshot();
        var r = new SoftwareRenderer();
        var pilot = new Autopilot();
        var seen = new HashSet<SessionState>();
        for (int i = 0; i < 60 * 60 * 6 && seen.Count < 5; i++)
        {
            s.Step(pilot.Next(s));
            s.BuildSnapshot(snap);
            r.Render(snap);
            seen.Add(s.State);
        }
        Assert.Contains(SessionState.Playing, seen);
        Assert.Contains(SessionState.Dying, seen);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}

public class SettingsUiTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "defender-settings-" + Guid.NewGuid().ToString("N"));

    [AvaloniaFact]
    public void F10_OpensSettings_EscClosesAndPersists()
    {
        var host = new GameHost(_dir, new NullSoundEngine(), enableGamepad: false);
        var w = new MainWindow(host) { Width = 900, Height = 760 };
        w.Show();
        w.KeyPress(Key.F10, RawInputModifiers.None, PhysicalKey.F10, null);
        var panel = w.GetVisualDescendants().OfType<SettingsPanel>().Single();
        Assert.True(panel.IsVisible);
        var frame = w.CaptureRenderedFrame();
        var shot = Environment.GetEnvironmentVariable("DEFENDER_SETTINGS_SHOT");
        if (frame is not null && shot is not null) frame.Save(shot);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.False(panel.IsVisible);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
        w.Close();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
