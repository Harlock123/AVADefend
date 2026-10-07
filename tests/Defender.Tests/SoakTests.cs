using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

public class SoakTests
{
    [Theory]
    [InlineData(1u)]
    [InlineData(42u)]
    [InlineData(1981u)]
    public void RandomPlay_RunsWithoutExceptions_AndReachesGameOver(uint seed)
    {
        var s = new GameSession(rng: new XorShiftRandom(seed));
        var snap = new FrameSnapshot();
        var r = new Random((int)seed);
        bool sawPlaying = false, sawGameOver = false;
        s.Step(new PlayerInput { StartPressed = true });
        for (int i = 0; i < 60 * 60 * 20 && !sawGameOver; i++)
        {
            var input = new PlayerInput
            {
                ThrustHeld = r.Next(3) > 0,
                ReversePressed = r.Next(40) == 0,
                Vertical = r.Next(3) - 1,
                FirePressed = r.Next(4) == 0,
                SmartBombPressed = r.Next(500) == 0,
                HyperspacePressed = r.Next(900) == 0,
            };
            s.Step(input);
            if (i % 3 == 0) s.BuildSnapshot(snap);
            sawPlaying |= s.State == SessionState.Playing;
            sawGameOver |= s.State == SessionState.GameOver;
            Assert.InRange(s.Shells.Count, 0, Arcade.MaxShells);
            Assert.InRange(s.Lasers.Count, 0, Arcade.MaxLasers);
            Assert.InRange(s.Player.PixelY, Arcade.PlayerMinY, Arcade.PlayerMaxY);
        }
        Assert.True(sawPlaying);
        Assert.True(sawGameOver, $"score {s.Score} wave {s.Wave} state {s.State}");
    }
}
