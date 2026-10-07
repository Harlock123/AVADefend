using Defender.Core.Simulation;

namespace Defender.Tests;

public class SchedulerAndWrapTests
{
    [Fact]
    public void Scheduler_DispatchesWholeTicks_AndCarriesRemainder()
    {
        var s = new FixedStepScheduler(60);
        Assert.Equal(0, s.Advance(TimeSpan.FromMilliseconds(10)));
        Assert.Equal(1, s.Advance(TimeSpan.FromMilliseconds(10))); // 20ms total -> 1 tick, 3.33ms left
        int total = 1;
        for (int i = 0; i < 98; i++) total += s.Advance(TimeSpan.FromMilliseconds(10));
        Assert.Equal(60, total); // 1.0 s => 60 ticks
    }

    [Fact]
    public void Scheduler_CapsBacklogAfterStall()
    {
        var s = new FixedStepScheduler(60, maxTicksPerAdvance: 5);
        Assert.Equal(5, s.Advance(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, s.Advance(TimeSpan.Zero));
    }

    [Fact]
    public void Scheduler_TimeScaleSlowsSimulation()
    {
        var s = new FixedStepScheduler(60) { TimeScale = 0.5 };
        int total = 0;
        for (int i = 0; i < 100; i++) total += s.Advance(TimeSpan.FromMilliseconds(10));
        Assert.Equal(30, total);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2047, 2047)]
    [InlineData(2048, 0)]
    [InlineData(-1, 2047)]
    [InlineData(-2049, 2047)]
    [InlineData(4100, 4)]
    public void Wrap_HandlesBothSeams(int x, int expected) => Assert.Equal(expected, WorldMath.Wrap(x, 2048));

    [Theory]
    [InlineData(10, 20, 10)]
    [InlineData(2040, 8, 16)]     // across the right seam
    [InlineData(8, 2040, -16)]    // across the left seam
    [InlineData(0, 1024, 1024)]
    public void Delta_IsShortestSignedPath(int from, int to, int expected) => Assert.Equal(expected, WorldMath.Delta(from, to, 2048));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(65535, 65535)]
    [InlineData(65536, 0)]        // right seam
    [InlineData(-1, 65535)]       // left seam
    [InlineData(65536 + 100, 100)]
    public void EngineWrapUnits_HandlesBothSeams(int x, int expected) => Assert.Equal(expected, WorldMath.WrapUnits(x));

    [Theory]
    [InlineData(100, 200, 100)]
    [InlineData(65500, 30, 66)]       // across the right seam, forwards
    [InlineData(30, 65500, -66)]      // across the left seam, backwards
    [InlineData(0, 32767, 32767)]
    [InlineData(0, 32768, -32768)]    // exactly half a planet: 16-bit signed convention
    public void EngineDeltaUnits_IsShortestSignedPathAcrossSeams(int from, int to, int expected) =>
        Assert.Equal(expected, WorldMath.DeltaUnits(from, to));
}
