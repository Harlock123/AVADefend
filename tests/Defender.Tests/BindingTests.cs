using Defender.Avalonia;
using Defender.Core.Input;

namespace Defender.Tests;

public class BindingTests
{
    [Fact]
    public void PadRebind_MakesControlPrimary_AndRemovesItElsewhere()
    {
        var b = InputBindings.CreateDefault();
        SettingsPanel.ApplyPadBinding(b, LogicalButton.Hyperspace, PadControl.X);   // X was a Reverse binding
        Assert.Equal(PadControl.X, b.Gamepad[LogicalButton.Hyperspace][0]);
        Assert.DoesNotContain(PadControl.X, b.Gamepad[LogicalButton.Reverse]);
        Assert.NotEmpty(b.Gamepad[LogicalButton.Reverse]);                          // still has Left shoulder
    }

    [Fact]
    public void PadRebind_StealingSoleControl_NeverLeavesTwoActionsOnOneControl()
    {
        var b = InputBindings.CreateDefault();
        SettingsPanel.ApplyPadBinding(b, LogicalButton.SmartBomb, PadControl.A);    // A was Fire's only control
        var owners = b.Gamepad.Where(kv => kv.Value.Contains(PadControl.A)).Select(kv => kv.Key).ToList();
        Assert.Equal([LogicalButton.SmartBomb], owners);
        Assert.NotEmpty(b.Gamepad[LogicalButton.Fire]);
    }

    [Fact]
    public void Sanitize_RepairsMissingAndInvalidEntries()
    {
        var b = new InputBindings { PadDeadzone = float.NaN };
        b.Keyboard[LogicalButton.Fire] = ["", " "];
        b.Gamepad[LogicalButton.Fire] = [(PadControl)999];
        var s = b.Sanitized();
        Assert.Equal(InputBindings.CreateDefault().Keyboard[LogicalButton.Fire], s.Keyboard[LogicalButton.Fire]);
        Assert.Equal(InputBindings.CreateDefault().Gamepad[LogicalButton.Fire], s.Gamepad[LogicalButton.Fire]);
        Assert.Equal(0.2f, s.PadDeadzone);
        Assert.Equal(Enum.GetValues<LogicalButton>().Length, s.Keyboard.Count);
    }
}

public class KeyboardRebindTests
{
    [Fact]
    public void StealingBothKeysOfAnAction_LeavesItBound_AndKeysUnique()
    {
        var b = InputBindings.CreateDefault();
        InputBindings.Rebind(b.Keyboard, LogicalButton.Fire, "Enter");  // Hyperspace: [Enter, L]
        InputBindings.Rebind(b.Keyboard, LogicalButton.Fire, "L");      // Hyperspace would be empty
        Assert.NotEmpty(b.Keyboard[LogicalButton.Hyperspace]);
        var all = b.Keyboard.SelectMany(kv => kv.Value).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal("L", b.Keyboard[LogicalButton.Fire][0]);
        Assert.Equal(b, b.Sanitized() is { } s && s.Keyboard.All(kv => kv.Value.SequenceEqual(b.Keyboard[kv.Key])) ? b : null);
    }
}
