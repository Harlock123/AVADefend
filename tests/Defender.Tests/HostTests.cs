using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Defender.Avalonia;
using Defender.Core;
using Defender.Core.Input;
using Defender.Core.Simulation;
using Defender.Infrastructure.Audio;

namespace Defender.Tests;

public class HostTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "defender-host-" + Guid.NewGuid().ToString("N"));

    private GameHost NewHost(Action<GameHost>? configure = null)
    {
        var h = new GameHost(_dir, new NullSoundEngine(), enableGamepad: false);
        configure?.Invoke(h);
        return h;
    }

    private static void PlayABit(GameHost h)
    {
        h.Session.StartGame();
        for (int i = 0; i < 200; i++) h.Session.Step(default);
        Assert.True(h.Session.CanSuspend);
    }

    [Fact]
    public void ModernSuspend_GoesToSelectedSlot_AndOnlyThatSlotResumes()
    {
        var a = NewHost(h => { h.Settings.Mode = GameMode.Modern; h.Settings.SuspendSlot = 2; h.ApplySettings(); h.SaveSettings(); });
        PlayABit(a);
        int score = a.Session.Score; long frame = a.Session.Frame;
        Assert.True(a.SuspendIfPlaying());
        Assert.True(File.Exists(Path.Combine(_dir, "suspend-2.json")));
        Assert.False(File.Exists(Path.Combine(_dir, "suspend-1.json")));
        Assert.StartsWith("wave 1", a.DescribeSlot(2));
        Assert.Equal("empty", a.DescribeSlot(1));

        var b = NewHost(); // loads settings: Modern, slot 2 → resumes
        Assert.Equal(frame, b.Session.Frame);
        Assert.Equal(score, b.Session.Score);
        Assert.False(File.Exists(Path.Combine(_dir, "suspend-2.json"))); // consumed
    }

    [Fact]
    public void ClassicNeverSuspends()
    {
        var a = NewHost();
        Assert.Equal(GameMode.Classic, a.Settings.Mode);
        PlayABit(a);
        Assert.False(a.SuspendIfPlaying());
        Assert.False(Directory.Exists(_dir) && Directory.GetFiles(_dir, "suspend-*.json").Length > 0);
    }

    [AvaloniaFact]
    public void HintBar_ShownOnlyInModern()
    {
        var h = NewHost();
        var w = new MainWindow(h);
        w.Show();
        TextBlock Bar() => w.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("Thrust ") == true);
        Assert.False(Bar().IsVisible);
        w.Close();
        var h2 = NewHost(x => { x.Settings.Mode = GameMode.Modern; x.ApplySettings(); });
        var w2 = new MainWindow(h2);
        w2.Show();
        var bar = w2.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("Thrust ") == true);
        Assert.True(bar.IsVisible);
        Assert.Contains("Fire LeftCtrl", bar.Text);
        w2.Close();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
