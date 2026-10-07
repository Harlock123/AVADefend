using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

    [Fact]
    public void SettingsOpen_FreezesAnyState_AndDiscardsPressesMadeMeanwhile()
    {
        var h = NewHost();
        h.SettingsOpen = true;
        h.KeyDown("D1");                       // e.g. a pad "start" pressed while rebinding
        for (int i = 0; i < 5; i++) h.Frame(TimeSpan.FromMilliseconds(17));
        h.KeyUp("D1");
        Assert.Equal(SessionState.Attract, h.Session.State);
        h.SettingsOpen = false;
        for (int i = 0; i < 5; i++) h.Frame(TimeSpan.FromMilliseconds(17));
        Assert.Equal(SessionState.Attract, h.Session.State); // the press was dropped, not replayed

        h.Session.StartGame();
        long f = h.Session.Frame;
        h.FocusHold = true;                    // e.g. Modern focus loss while dying
        for (int i = 0; i < 10; i++) h.Frame(TimeSpan.FromMilliseconds(17));
        Assert.Equal(f, h.Session.Frame);
        h.FocusHold = false;
        h.Frame(TimeSpan.FromMilliseconds(17)); h.Frame(TimeSpan.FromMilliseconds(17));
        Assert.True(h.Session.Frame > f);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"entries\":null}")]
    [InlineData("{\"schemaVersion\":2,\"entries\":[null,{\"initials\":null,\"score\":500,\"wave\":1}],\"today\":null}")]
    public void MalformedHighScoreFile_DoesNotCrash(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "highscores-classic.json"), json);
        var h = NewHost();
        Assert.Equal(SessionState.Attract, h.Session.State);
        Assert.All(h.Session.HighScores.AllTime.Entries, e => Assert.Equal(3, e.Initials.Length));
    }

    [Theory]
    [InlineData("\"player\":null")]
    [InlineData("\"enemies\":[null]")]
    [InlineData("\"enemies\":[{\"kind\":\"Lander\",\"target\":99}]")]
    [InlineData("\"rules\":{\"ships\":0}")]
    public void MalformedSuspendFile_IsIgnored_NotACrash(string fragment)
    {
        var a = NewHost(h => { h.Settings.Mode = GameMode.Modern; h.ApplySettings(); h.SaveSettings(); });
        PlayABit(a);
        a.SuspendIfPlaying();
        var path = Path.Combine(_dir, "suspend-1.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        var patch = System.Text.Json.Nodes.JsonNode.Parse("{" + fragment + "}")!.AsObject();
        foreach (var (k, v) in patch.ToList()) { patch.Remove(k); json["data"]![k] = v; }
        File.WriteAllText(path, json.ToJsonString());
        var b = NewHost();                                   // must not throw
        Assert.Equal(SessionState.Attract, b.Session.State);
        Assert.Contains(b.Messages, m => m.Contains("Suspend file ignored"));
        Assert.False(File.Exists(path));
    }

    private static void ReachInitials(GameSession s)
    {
        s.TestAddScore(5000);
        while (s.State != SessionState.EnterInitials)
        {
            if (s.State == SessionState.Playing)
            {
                var e = s.TestSpawn(EnemyKind.Mutant, s.Player.WorldX(s.CameraX), s.Player.PixelY);
                e.Vx = e.Vy = 0; e.Nap = 1000;
            }
            s.Step(default);
        }
    }

    [AvaloniaFact]
    public void HeldLetterKey_TypesOneInitial_NotThreeViaKeyRepeat()
    {
        var h = NewHost();
        var w = new MainWindow(h);
        w.Show();
        h.Session.StartGame();
        ReachInitials(h.Session);
        w.KeyPress(Key.Q, RawInputModifiers.None, PhysicalKey.Q, "q");
        for (int i = 0; i < 5; i++) w.KeyPress(Key.Q, RawInputModifiers.None, PhysicalKey.Q, "q"); // OS repeats
        Assert.Equal(SessionState.EnterInitials, h.Session.State);
        Assert.Equal(1, h.Session.InitialsCursor);
        w.KeyRelease(Key.Q, RawInputModifiers.None, PhysicalKey.Q, "q");
        w.Close();
    }

    [Fact]
    public void ClosingDuringInitials_CommitsTheEntry()
    {
        var h = NewHost();
        h.Session.StartGame();
        ReachInitials(h.Session);
        h.Session.TypeInitial('Z');
        h.Session.CommitPendingInitials();
        Assert.Equal(SessionState.Attract, h.Session.State);
        Assert.Equal("Z--", h.Session.HighScores.AllTime.Entries[0].Initials);
    }

    [Fact]
    public void ModernSuspend_WorksDuringDeathSequence()
    {
        var a = NewHost(x => { x.Settings.Mode = GameMode.Modern; x.ApplySettings(); x.SaveSettings(); });
        a.Session.StartGame();
        a.Session.RunUntil(() => a.Session.State == SessionState.Playing, 400);
        var e = a.Session.TestSpawn(EnemyKind.Mutant, a.Session.Player.WorldX(a.Session.CameraX), a.Session.Player.PixelY);
        e.Vx = e.Vy = 0; e.Nap = 1000;
        a.Session.Step(default);
        Assert.Equal(SessionState.Dying, a.Session.State);
        Assert.True(a.SuspendIfPlaying());
        var b = NewHost();
        Assert.Equal(SessionState.Dying, b.Session.State);
        b.Session.RunUntil(() => b.Session.State == SessionState.LifeStart, 400);
    }

    [Fact]
    public void PlanetExplosionEffect_DoesNotLeakIntoTheNextLife()
    {
        var s = TestUtil.NewPlaying();
        foreach (var hm in s.Humanoids) s.TestKillHumanoid(hm);
        Assert.True(s.PlanetExploding);
        var e = s.TestSpawn(EnemyKind.Mutant, s.Player.WorldX(s.CameraX), s.Player.PixelY);
        e.Vx = e.Vy = 0; e.Nap = 1000;
        s.Step(default);
        s.RunUntil(() => s.State == SessionState.LifeStart, 400);
        Assert.False(s.PlanetExploding);
    }

    [Fact]
    public void AfterAGame_AttractResumesAtTheHallOfFame()
    {
        var h = NewHost();
        Assert.Equal(AttractPhase.Title, h.Attract.Phase);         // power-on: logo page
        h.Session.StartGame();
        h.Frame(TimeSpan.FromMilliseconds(17));
        for (int life = 0; life < 3; life++)
        {
            h.Session.RunUntil(() => h.Session.State == SessionState.Playing, 400);
            var e = h.Session.TestSpawn(EnemyKind.Mutant, h.Session.Player.WorldX(h.Session.CameraX), h.Session.Player.PixelY);
            e.Vx = e.Vy = 0; e.Nap = 1000;
            h.Session.Step(default);
            h.Session.RunUntil(() => h.Session.State != SessionState.Dying, 400);
        }
        h.Session.RunUntil(() => h.Session.State == SessionState.EnterInitials, 600);   // 450 points qualifies
        h.Session.CommitPendingInitials();                                               // → attract
        h.Frame(TimeSpan.FromMilliseconds(17));
        Assert.Equal(AttractPhase.HallOfFame, h.Attract.Phase);
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
