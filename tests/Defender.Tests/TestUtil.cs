using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

internal static class TestUtil
{
    public static GameSession NewPlaying(uint seed = 7, GamePolicy? policy = null, bool keepReserves = false)
    {
        var s = new GameSession(policy: policy, rng: new XorShiftRandom(seed));
        s.TestStartEmpty(keepReserves);
        return s;
    }

    public static void Run(this GameSession s, int ticks, PlayerInput input = default)
    {
        for (int i = 0; i < ticks; i++) s.Step(input);
    }

    public static void RunUntil(this GameSession s, Func<bool> cond, int maxTicks, PlayerInput input = default)
    {
        for (int i = 0; i < maxTicks && !cond(); i++) s.Step(input);
        Assert.True(cond(), $"condition not reached within {maxTicks} ticks");
    }

    /// <summary>World X that is at the given screen pixel right now.</summary>
    public static int WorldAtScreen(this GameSession s, int px) => (s.CameraX + px * Arcade.UnitsPerPixel) & Arcade.WorldMask;

    public static void ClearHumanoidsExcept(this GameSession s, int keep)
    {
        for (int i = 0; i < s.Humanoids.Length; i++)
            if (i != keep) s.Humanoids[i].State = HumanoidState.Dead;
    }
}
