using Defender.Core.Input;

namespace Defender.Core.Simulation;

/// <summary>
/// Simple deterministic pilot used for the attract demo, screenshots and soak tests. It thrusts,
/// tracks the nearest on-screen enemy's altitude and fires; it is not meant to play well.
/// </summary>
public sealed class Autopilot
{
    private int _t;

    public PlayerInput Next(GameSession s)
    {
        _t++;
        if (s.State == SessionState.Attract) return new PlayerInput { StartPressed = _t % 60 == 0 };
        if (s.State == SessionState.EnterInitials) return new PlayerInput { FirePressed = _t % 20 == 0 };
        var p = s.Player;
        Enemy? target = s.Enemies.Where(e => !e.Dead && e.Appear == 0 && s.OnScreen(e.X))
            .OrderBy(e => Math.Abs(s.SignedScreenX(e.X) - p.ScreenPx)).FirstOrDefault();
        int vertical = 0;
        bool reverse = false;
        if (target is not null)
        {
            int dy = target.PixelY + 3 - (p.PixelY + 4);
            vertical = Math.Abs(dy) < 3 ? 0 : Math.Sign(dy);
            int dx = s.SignedScreenX(target.X) - p.ScreenPx;
            reverse = Math.Sign(dx) != p.Facing && Math.Abs(dx) > 40 && _t % 30 == 0;
        }
        else if (p.PixelY > 200) vertical = -1;
        return new PlayerInput
        {
            ThrustHeld = target is null || _t % 90 < 50,
            ReversePressed = reverse,
            Vertical = vertical,
            FirePressed = _t % 6 == 0,
            SmartBombPressed = s.Enemies.Count(e => !e.Dead && s.OnScreen(e.X)) >= 6 && _t % 300 == 0,
        };
    }
}
