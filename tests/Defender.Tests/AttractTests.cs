using Defender.Core.Simulation;

namespace Defender.Tests;

public class AttractTests
{
    [Fact]
    public void Cycle_IsLogoThenHallOfFameThenScriptedDemo_WithSourceDurations()
    {
        var d = new AttractDirector();
        Assert.Equal(AttractPhase.Title, d.Phase);                    // power-on starts on the logo page
        for (int i = 0; i < AttractDirector.TitleFrames; i++) d.Step();
        Assert.Equal(AttractPhase.HallOfFame, d.Phase);
        for (int i = 0; i < AttractDirector.HallFrames; i++) d.Step();
        Assert.Equal(AttractPhase.Demo, d.Phase);
        int demoFrames = 0;
        while (d.Phase == AttractPhase.Demo) { d.Step(); demoFrames++; }
        Assert.Equal(AttractDemo.TotalFrames, demoFrames);
        Assert.InRange(AttractDemo.TotalFrames, 2270, 2290);           // ≈ 2279 frames (38 s)
        Assert.Equal(AttractPhase.Title, d.Phase);
    }

    [Fact]
    public void ScriptedDemo_HitsItsCheckpoints()
    {
        var demo = new AttractDemo();
        var terrain = new Terrain();
        var snap = new FrameSnapshot();
        void At(int frame) { while (demo.Frame < frame) demo.Step(); demo.Build(snap, terrain); }

        At(100);
        Assert.Contains(snap.Labels, l => l.Text == "SCANNER");
        At(412);                                                      // lander shot at 411
        Assert.NotEmpty(snap.Blasts);
        Assert.DoesNotContain(snap.Sprites, s => s.Sprite == Sprites.Lander[0]);
        At(502);                                                      // caught at 501
        Assert.Contains(snap.Popups, p => p.Text == "500");
        At(AttractDemo.RescueFrames + 1);
        Assert.DoesNotContain(snap.Popups, p => p.Text == "500");
        At(AttractDemo.TotalFrames - 1);
        foreach (var r in AttractDemo.Roster)
        {
            Assert.Contains(snap.Labels, l => l.Text == r.Name);
            Assert.Contains(snap.Sprites, s => s.X == r.X && s.Y == r.Y);   // each enemy sits in its display slot
        }
    }

    [Fact]
    public void Reset_ReturnsToTitle()
    {
        var d = new AttractDirector();
        for (int i = 0; i < AttractDirector.TitleFrames + AttractDirector.HallFrames + 10; i++) d.Step();
        d.Reset();
        Assert.Equal((AttractPhase.Title, 0), (d.Phase, d.PhaseTimer));
        Assert.Null(d.Demo);
    }
}
