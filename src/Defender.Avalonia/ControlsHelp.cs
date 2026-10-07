using Defender.Core.Input;

namespace Defender.Avalonia;

public static class ControlsHelp
{
    public static string Text(InputBindings b)
    {
        string K(LogicalButton x) => string.Join(" / ", b.Keyboard.GetValueOrDefault(x) ?? []);
        string P(LogicalButton x) => string.Join(" / ", b.Gamepad.GetValueOrDefault(x) ?? []);
        var rows = new[]
        {
            ("Up / Down (joystick)", LogicalButton.Up, LogicalButton.Down),
            ("Thrust (hold)", LogicalButton.Thrust, LogicalButton.Thrust),
            ("Reverse (flip facing)", LogicalButton.Reverse, LogicalButton.Reverse),
            ("Fire (one shot per press)", LogicalButton.Fire, LogicalButton.Fire),
            ("Smart Bomb", LogicalButton.SmartBomb, LogicalButton.SmartBomb),
            ("Hyperspace (~25% risk!)", LogicalButton.Hyperspace, LogicalButton.Hyperspace),
            ("Start game", LogicalButton.Start, LogicalButton.Start),
            ("Pause", LogicalButton.Pause, LogicalButton.Pause),
        };
        var sb = new System.Text.StringBuilder("CONTROLS                       (F1 to close)\n\n");
        foreach (var (name, a, c) in rows)
        {
            string keys = a == c ? K(a) : K(a) + "  |  " + K(c);
            string pads = a == c ? P(a) : P(a) + " | " + P(c);
            sb.AppendLine($"{name,-27} {keys}");
            sb.AppendLine($"{"",-27} pad: {pads}");
        }
        sb.AppendLine();
        sb.AppendLine(b.PadStickSteering
            ? "Gamepad: left stick / d-pad left-right = face that way AND thrust."
            : "Gamepad stick steering is off: use the Thrust and Reverse buttons.");
        sb.AppendLine("F10 settings & key remapping   F11 fullscreen");
        return sb.ToString();
    }
}
