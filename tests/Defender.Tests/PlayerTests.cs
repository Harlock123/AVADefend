using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

public class TerrainTests
{
    [Fact]
    public void Terrain_IsDeterministic_ForSeed()
    {
        var a = new Terrain(123); var b = new Terrain(123); var c = new Terrain(124);
        Assert.True(Enumerable.Range(0, Arcade.WorldPixels).All(x => a.HeightAtPixel(x) == b.HeightAtPixel(x)));
        Assert.False(Enumerable.Range(0, Arcade.WorldPixels).All(x => a.HeightAtPixel(x) == c.HeightAtPixel(x)));
    }

    [Theory]
    [InlineData(Terrain.DefaultSeed)]
    [InlineData(1)]
    [InlineData(99)]
    public void Terrain_MatchesOriginalFormatConstraints(int seed)
    {
        var t = new Terrain(seed);
        int prev = Arcade.TerrainStartY;
        for (int x = 0; x < Arcade.WorldPixels; x++)
        {
            int y = t.HeightAtPixel(x);
            Assert.Equal(1, Math.Abs(y - prev));      // every pixel step is exactly 1 up or down
            Assert.InRange(y, Terrain.MinY, Terrain.MaxY);
            prev = y;
        }
        Assert.Equal(Arcade.TerrainStartY, t.HeightAtPixel(Arcade.WorldPixels - 1)); // loop closes seamlessly
        Assert.Equal(t.HeightAtPixel(5), t.HeightAtPixel(5 + Arcade.WorldPixels));  // wraps
    }
}

public class PlayerPhysicsTests
{
    private static readonly PlayerInput Thrust = new() { ThrustHeld = true };

    [Fact]
    public void Thrust_AccumulatesToDragLimitedTopSpeed_Of6PxPerFrame()
    {
        var s = TestUtil.NewPlaying();
        s.Step(Thrust);
        Assert.Equal(3, s.Player.V16);                  // +3 units/frame² (PLADIR $0300)
        s.Run(1200, Thrust);
        Assert.InRange(s.Player.V16, 185, 192);         // terminal ≈ 192 units = 6 px/frame (V/64 drag)
        Assert.True(s.Player.V16 < Arcade.MaxPlayerSpeed16);
    }

    [Fact]
    public void Drag_DecaysVelocity_WhenThrustReleased()
    {
        var s = TestUtil.NewPlaying();
        s.Run(300, Thrust);
        int v0 = s.Player.V16;
        s.Step(default);
        Assert.Equal(v0 - v0 / 64, s.Player.V16, tolerance: 1);
        s.Run(600);
        Assert.InRange(s.Player.V16, 0, 3);
    }

    [Fact]
    public void ScrollSpeed_EqualsShipWorldSpeed_IncludingDuringReverseSlide()
    {
        var s = TestUtil.NewPlaying();
        s.Run(600, Thrust);
        int before = s.Player.WorldX(s.CameraX);
        int v = s.Player.V16;
        s.Step(new PlayerInput { ReversePressed = true }); // facing flips; slide begins
        Assert.Equal(-1, s.Player.Facing);
        int after = s.Player.WorldX(s.CameraX);
        int moved = (short)(ushort)((after - before) & Arcade.WorldMask);
        Assert.InRange(moved, v - v / 64 - 40, v + 40); // world motion ≈ velocity (32-unit quantised PLABX)
        Assert.True(s.Player.V16 > 0, "reverse does not change velocity directly");
    }

    [Fact]
    public void Reverse_SlidesShipAcrossScreen_At2PxPerFrame()
    {
        var s = TestUtil.NewPlaying();
        Assert.Equal(64, s.Player.ScreenPx);
        s.Step(new PlayerInput { ReversePressed = true });
        Assert.Equal(66, s.Player.ScreenPx);
        s.Run(100);
        Assert.Equal(224, s.Player.ScreenPx);           // left-facing base column $70
    }

    [Fact]
    public void Reverse_HasRearmDelay()
    {
        var s = TestUtil.NewPlaying();
        s.Step(new PlayerInput { ReversePressed = true });
        s.Step(new PlayerInput { ReversePressed = true });
        Assert.Equal(-1, s.Player.Facing);
        s.Run(5);
        s.Step(new PlayerInput { ReversePressed = true });
        Assert.Equal(1, s.Player.Facing);
    }

    [Fact]
    public void ShipLeadsForward_AtSpeed()
    {
        var s = TestUtil.NewPlaying();
        s.Run(1200, Thrust);
        Assert.InRange(s.Player.ScreenPx, 108, 112);    // 64 + V/4 px
    }

    [Fact]
    public void Vertical_NoInertia_StartsAt1_RampsTo2PxPerFrame()
    {
        var s = TestUtil.NewPlaying();
        int y0 = s.Player.PixelY;
        s.Step(new PlayerInput { Vertical = 1 });
        Assert.Equal(y0 + 1, s.Player.PixelY);
        s.Run(40, new PlayerInput { Vertical = 1 });
        Assert.Equal(0x200, s.Player.VyMag);
        int y1 = s.Player.Y;
        s.Step(default);
        Assert.Equal(y1, s.Player.Y);                  // stops instantly
    }

    [Fact]
    public void Vertical_IsClampedToPlayfield()
    {
        var s = TestUtil.NewPlaying();
        s.Run(300, new PlayerInput { Vertical = -1 });
        Assert.Equal(Arcade.PlayerMinY, s.Player.PixelY);
        s.Run(300, new PlayerInput { Vertical = 1 });
        Assert.Equal(Arcade.PlayerMaxY, s.Player.PixelY);
    }

    [Fact]
    public void WorldWrap_IsSeamless_InBothDirections()
    {
        var s = TestUtil.NewPlaying();
        var seen = new HashSet<int>();
        int prev = s.CameraX, wraps = 0;
        for (int i = 0; i < 2000; i++)
        {
            s.Step(Thrust);
            if (s.CameraX < prev - 30000) wraps++;
            prev = s.CameraX;
            Assert.InRange(s.CameraX, 0, Arcade.WorldMask);
        }
        Assert.True(wraps >= 1, "camera should wrap past the right seam");
        s.Step(new PlayerInput { ReversePressed = true });
        prev = s.CameraX; wraps = 0;
        for (int i = 0; i < 3000; i++)
        {
            s.Step(Thrust);
            if (s.CameraX > prev + 30000) wraps++;
            prev = s.CameraX;
        }
        Assert.True(wraps >= 1, "camera should wrap past the left seam");
    }
}

public class LaserTests
{
    [Fact]
    public void FireIsEdgeTriggered_InClassic_HoldingDoesNotRepeat()
    {
        var s = TestUtil.NewPlaying();
        s.Step(new PlayerInput { FirePressed = true, FireHeld = true });
        s.Run(30, new PlayerInput { FireHeld = true });
        Assert.Empty(s.Lasers);       // the one shot has long gone; holding fired nothing more
    }

    [Fact]
    public void HoldToFire_IsModernOptIn_AndRateLimited()
    {
        var s = TestUtil.NewPlaying(policy: GamePolicy.Modern with { HoldToFire = true });
        int fired = 0;
        for (int i = 0; i < 60; i++) { s.Step(new PlayerInput { FireHeld = true }); fired += s.Sounds.Count(x => x == Core.Audio.SoundId.Fire); }
        Assert.InRange(fired, 6, 8);  // one per 8 frames
    }

    [Fact]
    public void AtMostFourLasers()
    {
        var s = TestUtil.NewPlaying();
        for (int i = 0; i < 6; i++) s.Step(new PlayerInput { FirePressed = true });
        Assert.Equal(Arcade.MaxLasers, s.Lasers.Count);
    }

    [Fact]
    public void Laser_HeadMoves8PxPerFrame_AndExpiresAtScreenEdge()
    {
        var s = TestUtil.NewPlaying();
        s.Step(new PlayerInput { FirePressed = true });
        var l = s.Lasers[0];
        int h = l.Head;
        s.Step(default);
        Assert.Equal(h + 8, l.Head);
        s.RunUntil(() => s.Lasers.Count == 0, 60);
    }

    [Fact]
    public void Laser_KillsFirstEnemyOnly_AndScores()
    {
        var s = TestUtil.NewPlaying();
        int y = s.Player.PixelY;
        var near = s.TestSpawn(EnemyKind.Lander, s.WorldAtScreen(150), y);
        var far = s.TestSpawn(EnemyKind.Lander, s.WorldAtScreen(200), y);
        near.Vx = far.Vx = 0; near.Vy = far.Vy = 0; near.Nap = far.Nap = 10000;
        s.Step(new PlayerInput { FirePressed = true });
        s.Run(25);
        Assert.True(near.Dead);
        Assert.False(far.Dead);
        Assert.Equal(150, s.Score);
    }
}
