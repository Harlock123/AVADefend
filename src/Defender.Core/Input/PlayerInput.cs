namespace Defender.Core.Input;

/// <summary>
/// Logical inputs for one simulation tick. "Held" values are levels; "Pressed" values are edges
/// that are true for exactly one tick per physical press (key-repeat never produces new edges).
/// </summary>
public readonly record struct PlayerInput(
    bool ThrustHeld,
    bool ReversePressed,
    int Vertical,          // -1 = up, 0 = none, +1 = down
    bool FireHeld,
    bool FirePressed,
    bool SmartBombPressed,
    bool HyperspacePressed,
    bool PausePressed,
    bool StartPressed,
    bool Start2Pressed = false)
{
    public static readonly PlayerInput None = default;
}

/// <summary>Raw logical button levels sampled from devices. Edges are derived by <see cref="InputEdgeDetector"/>.</summary>
public struct InputLevels
{
    public bool Thrust, Reverse, Up, Down, Fire, SmartBomb, Hyperspace, Pause, Start, Start2;
    /// <summary>Optional direction request from devices that combine facing and thrust (gamepad stick): -1 left, +1 right, 0 none.</summary>
    public int FaceRequest;
}

/// <summary>
/// Converts level samples into per-tick inputs. Edges are latched between ticks so that a press
/// and release that both happen between two ticks are not lost, and are consumed exactly once.
/// </summary>
public sealed class InputEdgeDetector
{
    private InputLevels _prev;
    private bool _latReverse, _latFire, _latBomb, _latHyper, _latPause, _latStart, _latStart2;

    /// <summary>Feed device state whenever it changes (may be many times per tick).</summary>
    public void Sample(in InputLevels now)
    {
        if (now.Reverse && !_prev.Reverse) _latReverse = true;
        if (now.Fire && !_prev.Fire) _latFire = true;
        if (now.SmartBomb && !_prev.SmartBomb) _latBomb = true;
        if (now.Hyperspace && !_prev.Hyperspace) _latHyper = true;
        if (now.Pause && !_prev.Pause) _latPause = true;
        if (now.Start && !_prev.Start) _latStart = true;
        if (now.Start2 && !_prev.Start2) _latStart2 = true;
        _prev = now;
    }

    /// <summary>Produce the input for one tick, consuming latched edges.</summary>
    public PlayerInput Consume(int facing)
    {
        var l = _prev;
        bool reverse = _latReverse;
        bool thrust = l.Thrust;
        // Stick-style direction: pushing opposite to facing generates a reverse edge; pushing either way thrusts.
        if (l.FaceRequest != 0)
        {
            if (l.FaceRequest != facing) reverse = true;
            thrust = true;
        }
        var input = new PlayerInput(
            ThrustHeld: thrust,
            ReversePressed: reverse,
            Vertical: (l.Up ? -1 : 0) + (l.Down ? 1 : 0),
            FireHeld: l.Fire,
            FirePressed: _latFire,
            SmartBombPressed: _latBomb,
            HyperspacePressed: _latHyper,
            PausePressed: _latPause,
            StartPressed: _latStart,
            Start2Pressed: _latStart2);
        _latReverse = _latFire = _latBomb = _latHyper = _latPause = _latStart = _latStart2 = false;
        return input;
    }
}
