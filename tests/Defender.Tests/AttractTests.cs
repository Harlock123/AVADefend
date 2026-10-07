using Defender.Core.Scoring;
using Defender.Core.Simulation;

namespace Defender.Tests;

public class AttractTests
{
    [Fact]
    public void Cycle_TitleThenDemoThenHallOfFameThenTitle()
    {
        var d = new AttractDirector(() => GamePolicy.Classic, new XorShiftRandom(4));
        for (int i = 0; i < AttractDirector.TitleFrames - 1; i++) d.Step();
        Assert.Equal(AttractPhase.Title, d.Phase);
        d.Step();
        Assert.Equal(AttractPhase.Demo, d.Phase);
        Assert.NotNull(d.Demo);
        bool sawPlaying = false;
        for (int i = 0; i < AttractDirector.DemoMaxFrames && d.Phase == AttractPhase.Demo; i++)
        {
            d.Step();
            sawPlaying |= d.Demo?.State == SessionState.Playing;
        }
        Assert.True(sawPlaying, "the demo actually flies");
        Assert.Equal(AttractPhase.HallOfFame, d.Phase);
        Assert.Null(d.Demo);
        for (int i = 0; i < AttractDirector.HallFrames; i++) d.Step();
        Assert.Equal(AttractPhase.Title, d.Phase);
    }

    [Fact]
    public void Demo_UsesItsOwnScoreBook_AndCannotPause()
    {
        var real = new HighScoreBook();
        var d = new AttractDirector(() => GamePolicy.Classic, new XorShiftRandom(4));
        for (int i = 0; i < AttractDirector.TitleFrames; i++) d.Step();
        Assert.NotSame(real, d.Demo!.HighScores);
        Assert.False(d.Demo.Policy.AllowPause);
        while (d.Phase == AttractPhase.Demo) d.Step();
        Assert.Empty(real.AllTime.Entries);
    }

    [Fact]
    public void Reset_ReturnsToTitle()
    {
        var d = new AttractDirector(() => GamePolicy.Classic, new XorShiftRandom(4));
        for (int i = 0; i < AttractDirector.TitleFrames + 10; i++) d.Step();
        d.Reset();
        Assert.Equal((AttractPhase.Title, 0), (d.Phase, d.PhaseTimer));
        Assert.Null(d.Demo);
    }
}
