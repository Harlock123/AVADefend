namespace Defender.Core.Input;

public enum LogicalButton { Thrust, Reverse, Up, Down, Fire, SmartBomb, Hyperspace, Pause, Start, Start2 }

/// <summary>Physical gamepad controls (SDL GameController layout names; A = bottom face button).</summary>
public enum PadControl
{
    A, B, X, Y, LeftShoulder, RightShoulder, LeftTrigger, RightTrigger, Back, Start,
    DpadUp, DpadDown, DpadLeft, DpadRight, LeftStickUp, LeftStickDown, LeftStickLeft, LeftStickRight,
    LeftStickPress, RightStickPress,
}

/// <summary>
/// Remappable bindings. Keyboard keys are stored by name (Avalonia <c>Key</c> enum names) so Core stays
/// UI-independent. "Stick steering" makes left/right on stick or D-pad act as face-and-thrust together.
/// </summary>
public sealed class InputBindings
{
    public Dictionary<LogicalButton, List<string>> Keyboard { get; set; } = new();
    public Dictionary<LogicalButton, List<PadControl>> Gamepad { get; set; } = new();
    public bool PadStickSteering { get; set; } = true;
    public float PadDeadzone { get; set; } = 0.2f;

    public static InputBindings CreateDefault() => new()
    {
        Keyboard = new()
        {
            [LogicalButton.Up] = ["Up", "W"],
            [LogicalButton.Down] = ["Down", "S"],
            [LogicalButton.Thrust] = ["Space", "Z"],
            [LogicalButton.Reverse] = ["X", "Left", "Right"],
            [LogicalButton.Fire] = ["LeftCtrl", "LeftAlt", "J"],
            [LogicalButton.SmartBomb] = ["LeftShift", "K"],
            [LogicalButton.Hyperspace] = ["Enter", "L"],
            [LogicalButton.Pause] = ["Escape", "P"],
            [LogicalButton.Start] = ["D1", "F2"],
            [LogicalButton.Start2] = ["D2", "F3"],
        },
        Gamepad = new()
        {
            [LogicalButton.Up] = [PadControl.DpadUp, PadControl.LeftStickUp],
            [LogicalButton.Down] = [PadControl.DpadDown, PadControl.LeftStickDown],
            [LogicalButton.Thrust] = [PadControl.RightTrigger],
            [LogicalButton.Reverse] = [PadControl.LeftShoulder, PadControl.X],
            [LogicalButton.Fire] = [PadControl.A],
            [LogicalButton.SmartBomb] = [PadControl.B, PadControl.LeftTrigger],
            [LogicalButton.Hyperspace] = [PadControl.Y, PadControl.RightShoulder],
            [LogicalButton.Pause] = [PadControl.Start],
            [LogicalButton.Start] = [PadControl.Back],
            [LogicalButton.Start2] = [PadControl.RightStickPress],
        },
    };

    /// <summary>Fills any missing logical button with defaults and strips nonsense, so a hand-edited file can't brick input.</summary>
    public InputBindings Sanitized()
    {
        var d = CreateDefault();
        var r = new InputBindings { PadStickSteering = PadStickSteering, PadDeadzone = Math.Clamp(float.IsFinite(PadDeadzone) ? PadDeadzone : 0.2f, 0.05f, 0.9f) };
        foreach (var b in Enum.GetValues<LogicalButton>())
        {
            var keys = Keyboard?.GetValueOrDefault(b)?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
            r.Keyboard[b] = keys is { Count: > 0 } ? keys : d.Keyboard[b];
            var pads = Gamepad?.GetValueOrDefault(b)?.Where(Enum.IsDefined).Distinct().ToList();
            r.Gamepad[b] = pads is { Count: > 0 } ? pads : d.Gamepad[b];
        }
        return r;
    }

    /// <summary>
    /// Makes <paramref name="item"/> the primary binding of <paramref name="action"/> (keeping its old primary as
    /// secondary) and removes it from every other action. An action that would be left with nothing receives
    /// this action's old binding instead, so no control ever drives two actions and none is left unbound.
    /// </summary>
    public static void Rebind<T>(Dictionary<LogicalButton, List<T>> map, LogicalButton action, T item) where T : notnull
    {
        var previous = map.GetValueOrDefault(action, []).Where(x => !x.Equals(item)).ToList();
        foreach (var (other, list) in map)
        {
            if (other == action || !list.Remove(item) || list.Count > 0 || previous.Count == 0) continue;
            list.Add(previous[^1]);
            previous.RemoveAt(previous.Count - 1);
        }
        map[action] = [item, .. previous.Take(1)];
    }

    public LogicalButton? KeyboardLookup(string key)
    {
        foreach (var (b, keys) in Keyboard)
            if (keys.Contains(key)) return b;
        return null;
    }
}
