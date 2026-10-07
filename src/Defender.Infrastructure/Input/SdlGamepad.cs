using Defender.Core.Input;
using Silk.NET.SDL;

namespace Defender.Infrastructure.Input;

/// <summary>
/// Gamepad input through SDL2's GameController API (uses SDL's controller database for consistent
/// Xbox-style layout across vendors). Supports hot-plug: devices are opened on SDL add events and
/// closed on removal. Poll from the UI thread once per frame.
/// </summary>
public sealed unsafe class SdlGamepad : IDisposable
{
    private readonly Sdl? _sdl;
    private GameController* _pad;
    private int _padInstance = -1;

    public SdlGamepad()
    {
        _sdl = SdlProvider.Api;
        if (_sdl is null) { Status = "SDL unavailable: " + SdlProvider.Error; return; }
        try
        {
            if (_sdl.InitSubSystem(Sdl.InitGamecontroller) != 0) { Status = "Gamepad init failed: " + _sdl.GetErrorS(); _sdl = null; return; }
            Status = "No gamepad connected";
            TryOpenAny();
        }
        catch (Exception ex)
        {
            Status = "Gamepad unavailable: " + ex.Message;
            _sdl = null;
        }
    }

    public string Status { get; private set; } = "";
    public bool Connected => _pad != null;
    public event Action<string>? StatusChanged;

    private void TryOpenAny()
    {
        if (_sdl is null || _pad != null) return;
        int n = _sdl.NumJoysticks();
        for (int i = 0; i < n; i++)
        {
            if (_sdl.IsGameController(i) != SdlBool.True) continue;
            var p = _sdl.GameControllerOpen(i);
            if (p == null) continue;
            _pad = p;
            _padInstance = _sdl.JoystickInstanceID(_sdl.GameControllerGetJoystick(p));
            SetStatus("Gamepad: " + (_sdl.GameControllerNameS(p) ?? "controller"));
            return;
        }
    }

    private void SetStatus(string s) { Status = s; StatusChanged?.Invoke(s); }

    /// <summary>Pumps SDL events (hot-plug) and ORs the current pad state into <paramref name="levels"/>.</summary>
    public void Poll(InputBindings bindings, ref InputLevels levels)
    {
        if (_sdl is null) return;
        Event e;
        while (_sdl.PollEvent(&e) != 0)
        {
            switch ((EventType)e.Type)
            {
                case EventType.Controllerdeviceadded:
                    TryOpenAny();
                    break;
                case EventType.Controllerdeviceremoved when e.Cdevice.Which == _padInstance:
                    _sdl.GameControllerClose(_pad);
                    _pad = null; _padInstance = -1;
                    SetStatus("Gamepad disconnected");
                    TryOpenAny();
                    break;
            }
        }
        if (_pad == null) return;

        float dz = bindings.PadDeadzone;
        float lx = Axis(GameControllerAxis.Leftx);
        bool Pressed(PadControl c) => IsPressed(c, dz);
        bool Any(LogicalButton b) => bindings.Gamepad.TryGetValue(b, out var list) && list.Any(Pressed);

        levels.Thrust |= Any(LogicalButton.Thrust);
        levels.Reverse |= Any(LogicalButton.Reverse);
        levels.Up |= Any(LogicalButton.Up);
        levels.Down |= Any(LogicalButton.Down);
        levels.Fire |= Any(LogicalButton.Fire);
        levels.SmartBomb |= Any(LogicalButton.SmartBomb);
        levels.Hyperspace |= Any(LogicalButton.Hyperspace);
        levels.Pause |= Any(LogicalButton.Pause);
        levels.Start |= Any(LogicalButton.Start);
        if (bindings.PadStickSteering && levels.FaceRequest == 0)
        {
            // Stick/D-pad horizontal = "face this way and thrust" (documented in CONTROLS.md).
            if (lx < -0.5f || Btn(GameControllerButton.DpadLeft)) levels.FaceRequest = -1;
            else if (lx > 0.5f || Btn(GameControllerButton.DpadRight)) levels.FaceRequest = 1;
        }
    }

    /// <summary>State of one physical control (sticks/triggers thresholded by the deadzone).</summary>
    public bool IsPressed(PadControl c, float deadzone)
    {
        if (_pad == null) return false;
        float lx = Axis(GameControllerAxis.Leftx), ly = Axis(GameControllerAxis.Lefty);
        return c switch
        {
            PadControl.A => Btn(GameControllerButton.A),
            PadControl.B => Btn(GameControllerButton.B),
            PadControl.X => Btn(GameControllerButton.X),
            PadControl.Y => Btn(GameControllerButton.Y),
            PadControl.LeftShoulder => Btn(GameControllerButton.Leftshoulder),
            PadControl.RightShoulder => Btn(GameControllerButton.Rightshoulder),
            PadControl.LeftTrigger => Axis(GameControllerAxis.Triggerleft) > 0.4f,
            PadControl.RightTrigger => Axis(GameControllerAxis.Triggerright) > 0.4f,
            PadControl.Back => Btn(GameControllerButton.Back),
            PadControl.Start => Btn(GameControllerButton.Start),
            PadControl.DpadUp => Btn(GameControllerButton.DpadUp),
            PadControl.DpadDown => Btn(GameControllerButton.DpadDown),
            PadControl.DpadLeft => Btn(GameControllerButton.DpadLeft),
            PadControl.DpadRight => Btn(GameControllerButton.DpadRight),
            PadControl.LeftStickUp => ly < -deadzone,
            PadControl.LeftStickDown => ly > deadzone,
            PadControl.LeftStickLeft => lx < -deadzone,
            PadControl.LeftStickRight => lx > deadzone,
            _ => false,
        };
    }

    /// <summary>All controls currently held (used by the rebinding UI).</summary>
    public IReadOnlyList<PadControl> PressedControls(float deadzone = 0.5f) =>
        Enum.GetValues<PadControl>().Where(c => IsPressed(c, deadzone)).ToList();

    private bool Btn(GameControllerButton b) => _sdl!.GameControllerGetButton(_pad, b) != 0;
    private float Axis(GameControllerAxis a) => _sdl!.GameControllerGetAxis(_pad, a) / 32767f;

    public void Dispose()
    {
        if (_sdl is null) return;
        if (_pad != null) _sdl.GameControllerClose(_pad);
        _pad = null;
        _sdl.QuitSubSystem(Sdl.InitGamecontroller);
    }
}
