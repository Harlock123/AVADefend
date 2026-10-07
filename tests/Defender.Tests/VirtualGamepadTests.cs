using Defender.Core.Input;
using Defender.Infrastructure;
using Defender.Infrastructure.Input;
using Silk.NET.SDL;
using SdlProvider = Defender.Infrastructure.SdlProvider;

namespace Defender.Tests;

/// <summary>
/// Exercises the real SDL GameController path with an SDL *virtual* controller: hot-plug add/remove,
/// default button/axis mapping, deadzone and stick steering. Skips only if native SDL cannot load.
/// </summary>
[Collection("SDL")]
public unsafe class VirtualGamepadTests
{
    // SDL_GameControllerButton / SDL_GameControllerAxis index order for an automatically mapped virtual pad.
    private const int BtnA = 0, BtnB = 1, BtnStart = 6, AxisLeftX = 0, AxisLeftY = 1, AxisTriggerRight = 5;

    private static InputLevels Poll(SdlGamepad pad, InputBindings b)
    {
        var l = new InputLevels();
        pad.Poll(b, ref l);
        return l;
    }

    [Fact]
    public void HotPlug_Mapping_Deadzone_AndSteering_WithVirtualController()
    {
        var sdl = SdlProvider.Api;
        Assert.SkipWhen(sdl is null, "native SDL2 not available: " + SdlProvider.Error);
        using var pad = new SdlGamepad();
        var b = InputBindings.CreateDefault();
        Poll(pad, b);
        Assert.False(pad.Connected);

        int index = sdl!.JoystickAttachVirtual(JoystickType.Gamecontroller, 6, 15, 0);
        Assert.True(index >= 0, "attach virtual: " + sdl.GetErrorS());
        Joystick* joy = null;
        try
        {
            Poll(pad, b);                                   // device-added event → opened
            Assert.True(pad.Connected, pad.Status);
            joy = sdl.JoystickOpen(index);
            Assert.True(joy != null);

            sdl.JoystickSetVirtualButton(joy, BtnA, 1);
            var l = Poll(pad, b);
            Assert.True(l.Fire);                            // A = Fire
            Assert.False(l.SmartBomb);
            sdl.JoystickSetVirtualButton(joy, BtnA, 0);
            sdl.JoystickSetVirtualButton(joy, BtnB, 1);
            Assert.True(Poll(pad, b).SmartBomb);            // B = Smart Bomb
            sdl.JoystickSetVirtualButton(joy, BtnB, 0);
            sdl.JoystickSetVirtualButton(joy, BtnStart, 1);
            Assert.True(Poll(pad, b).Pause);                // Start = Pause
            sdl.JoystickSetVirtualButton(joy, BtnStart, 0);

            sdl.JoystickSetVirtualAxis(joy, AxisTriggerRight, short.MaxValue);
            Assert.True(Poll(pad, b).Thrust);               // right trigger = Thrust
            sdl.JoystickSetVirtualAxis(joy, AxisTriggerRight, 0);

            // Deadzone 0.2: a 10% push is ignored, a 40% push moves.
            sdl.JoystickSetVirtualAxis(joy, AxisLeftY, -3277);
            Assert.False(Poll(pad, b).Up);
            sdl.JoystickSetVirtualAxis(joy, AxisLeftY, -13107);
            Assert.True(Poll(pad, b).Up);
            sdl.JoystickSetVirtualAxis(joy, AxisLeftY, 0);

            // Stick steering: full right = face right (and the engine adds thrust).
            sdl.JoystickSetVirtualAxis(joy, AxisLeftX, short.MaxValue);
            Assert.Equal(1, Poll(pad, b).FaceRequest);
            b.PadStickSteering = false;
            Assert.Equal(0, Poll(pad, b).FaceRequest);
            sdl.JoystickSetVirtualAxis(joy, AxisLeftX, 0);

            // Rebinding UI support: pressed controls are reported.
            sdl.JoystickSetVirtualButton(joy, BtnA, 1);
            Poll(pad, b);
            Assert.Contains(PadControl.A, pad.PressedControls());
            sdl.JoystickSetVirtualButton(joy, BtnA, 0);
        }
        finally
        {
            if (joy != null) sdl.JoystickClose(joy);
            sdl.JoystickDetachVirtual(index);
        }
        Poll(pad, b);                                       // device-removed event → closed
        Assert.False(pad.Connected);
        Assert.Contains("disconnected", pad.Status);
    }
}
