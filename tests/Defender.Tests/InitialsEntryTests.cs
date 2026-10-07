using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

/// <summary>HALLOF initials entry (amode1.src:117-371).</summary>
public class InitialsEntryTests
{
    private static GameSession AtInitials()
    {
        var s = TestUtil.NewPlaying();
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
        return s;
    }

    [Fact]
    public void Starts_A_Blank_Blank()
    {
        var s = AtInitials();
        Assert.Equal(['A', ' ', ' '], s.Initials);
    }

    [Fact]
    public void HoldingUp_RepeatsOnTheOriginalSchedule()
    {
        var s = AtInitials();
        var up = new PlayerInput { Vertical = -1 };
        var stepFrames = new List<int>();
        char last = s.Initials[0];
        for (int f = 1; f <= 120; f++)
        {
            s.Step(up);
            if (s.Initials[0] != last) { stepFrames.Add(f); last = s.Initials[0]; }
        }
        // First step after 3 frames, then gaps of 32, 21, 15, 12, 11, 10, 10…
        Assert.Equal([3, 35, 56, 71, 83, 94, 104, 114], stepFrames);
        Assert.Equal('I', s.Initials[0]);   // A + 8 steps
    }

    [Fact]
    public void Letters_WrapThroughSpace()
    {
        var s = AtInitials();
        s.Step(new PlayerInput { Vertical = 1 }); s.Step(new PlayerInput { Vertical = 1 }); s.Step(new PlayerInput { Vertical = 1 });
        Assert.Equal(' ', s.Initials[0]);   // A − 1 = space
        s.Step(default);
        for (int i = 0; i < 3; i++) s.Step(new PlayerInput { Vertical = 1 });
        Assert.Equal('Z', s.Initials[0]);   // space − 1 wraps to Z
    }

    [Fact]
    public void Fire_CountsOnlyAfterFiveFramesReleased()
    {
        var s = AtInitials();
        s.Step(new PlayerInput { FirePressed = true, FireHeld = true });
        Assert.Equal(1, s.InitialsCursor);
        s.Run(3);                                                        // released 3 frames only
        s.Step(new PlayerInput { FirePressed = true, FireHeld = true });
        Assert.Equal(1, s.InitialsCursor);
        s.Run(5);
        s.Step(new PlayerInput { FirePressed = true, FireHeld = true });
        Assert.Equal(2, s.InitialsCursor);
    }

    [Fact]
    public void Timeouts_40sForTheFirstInitial_ThenCommits()
    {
        var s = AtInitials();
        s.Run(40 * 60);
        Assert.Equal(SessionState.EnterInitials, s.State);
        s.Run(2);
        Assert.Equal(SessionState.Attract, s.State);
        Assert.Equal("A  ", s.HighScores.AllTime.Entries[0].Initials);
    }
}
