namespace Defender.Core.Simulation;

/// <summary>
/// Converts variable wall-clock frame intervals into a whole number of fixed simulation ticks.
/// Rendering is decoupled: the renderer calls <see cref="Advance"/> once per presented frame and
/// runs the returned number of ticks. A cap prevents a "spiral of death" after stalls.
/// </summary>
public sealed class FixedStepScheduler
{
    private double _accumulator;

    public FixedStepScheduler(double ticksPerSecond, int maxTicksPerAdvance = 8)
    {
        if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
        if (maxTicksPerAdvance < 1) throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvance));
        TickSeconds = 1.0 / ticksPerSecond;
        MaxTicksPerAdvance = maxTicksPerAdvance;
    }

    public double TickSeconds { get; }
    public int MaxTicksPerAdvance { get; }

    /// <summary>Speed multiplier (accessibility "deliberate" mode). 1.0 = real time.</summary>
    public double TimeScale { get; set; } = 1.0;

    /// <summary>Fraction of a tick left in the accumulator (0..1), usable for render interpolation.</summary>
    public double Alpha => _accumulator / TickSeconds;

    public int Advance(TimeSpan elapsed)
    {
        double seconds = Math.Max(0, elapsed.TotalSeconds) * TimeScale;
        _accumulator += seconds;
        int ticks = (int)(_accumulator / TickSeconds);
        if (ticks > MaxTicksPerAdvance)
        {
            ticks = MaxTicksPerAdvance;
            _accumulator = 0; // drop backlog instead of fast-forwarding
        }
        else
        {
            _accumulator -= ticks * TickSeconds;
        }
        return ticks;
    }

    public void Reset() => _accumulator = 0;
}
