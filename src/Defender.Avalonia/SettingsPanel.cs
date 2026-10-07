using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Defender.Core;
using Defender.Core.Input;

namespace Defender.Avalonia;

/// <summary>In-game settings and key remapping. Built in code; all changes apply on close.</summary>
public sealed class SettingsPanel : Border
{
    private readonly GameHost _host;
    private readonly Action _close;
    private readonly StackPanel _root = new() { Spacing = 6 };
    private LogicalButton? _capturing;
    private LogicalButton? _capturingPad;
    private HashSet<PadControl> _padHeldAtStart = new();
    private readonly DispatcherTimer _padTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private TextBlock? _captureHint;

    public SettingsPanel(GameHost host, Action close)
    {
        _host = host;
        _close = close;
        Background = new SolidColorBrush(Color.FromArgb(240, 8, 8, 24));
        BorderBrush = Brushes.SteelBlue;
        BorderThickness = new Thickness(2);
        Padding = new Thickness(16);
        Margin = new Thickness(24);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Stretch;
        MaxWidth = 720;
        Child = new ScrollViewer { Content = _root };
        _padTimer.Tick += (_, _) => PollPadCapture();
    }

    public void Refresh()
    {
        var s = _host.Settings;
        _root.Children.Clear();
        _capturing = null;
        StopPadCapture();
        Header("SETTINGS  (Esc / F10 to close)");

        Header("Preset");
        var mode = new ComboBox { ItemsSource = new[] { "Classic (1981 arcade rules)", "Modern (same rules + conveniences)" }, SelectedIndex = s.Mode == GameMode.Modern ? 1 : 0, IsEnabled = _host.CanChangeMode };
        mode.SelectionChanged += (_, _) => s.Mode = mode.SelectedIndex == 1 ? GameMode.Modern : GameMode.Classic;
        _root.Children.Add(mode);
        if (!_host.CanChangeMode) Note("Preset can only be changed between games.");
        Note("Classic and Modern keep separate high-score tables.");

        Header("Audio");
        Slider("Effects volume", s.EffectsVolume, v => s.EffectsVolume = (float)v);
        Slider("Thrust / ambience volume", s.AmbienceVolume, v => s.AmbienceVolume = (float)v);
        Check("Mute all audio", s.AudioMuted, v => s.AudioMuted = v);
        Note("Audio: " + _host.Audio.Status);

        Header("Display");
        Check("Integer scaling (sharpest pixels)", s.IntegerScaling, v => s.IntegerScaling = v);
        Check("Show control hints (title screen; Modern: reminder bar below the picture)", s.ShowControlHints, v => s.ShowControlHints = v);
        Check("Cocktail table: rotate the picture for player two", s.CocktailFlip, v => s.CocktailFlip = v);

        Header("Modern preset only");
        Check("Smooth (bilinear) scaling", s.SmoothScaling, v => s.SmoothScaling = v);
        Check("Bold scanner blips (easier to read on large displays)", s.BoldScanner, v => s.BoldScanner = v);
        Check("Suppress flashes and fast colour cycling", s.FlickerSuppression, v => s.FlickerSuppression = v);
        Check("Reduced motion (no full-screen flashes)", s.ReducedMotion, v => s.ReducedMotion = v);
        Check("Hold fire to repeat (rate-limited; original is one shot per press)", s.HoldToFire, v => s.HoldToFire = v);
        var speed = new ComboBox { ItemsSource = new[] { "100% (arcade speed)", "85%", "70%", "50%" } };
        double[] speeds = [1.0, 0.85, 0.7, 0.5];
        speed.SelectedIndex = Math.Max(0, Array.IndexOf(speeds, s.GameSpeed));
        speed.SelectionChanged += (_, _) => s.GameSpeed = speeds[Math.Max(0, speed.SelectedIndex)];
        Labeled("Game speed (shown on HUD)", speed);
        var slot = new ComboBox { ItemsSource = new[] { 1, 2, 3 }.Select(n => $"Slot {n}: {_host.DescribeSlot(n)}").ToArray(), SelectedIndex = s.SuspendSlot - 1 };
        slot.SelectionChanged += (_, _) => s.SuspendSlot = Math.Max(0, slot.SelectedIndex) + 1;
        Labeled("Suspend slot (closing mid-game saves here; selecting a full slot resumes it)", slot);

        Header("Gamepad");
        Note(_host.Gamepad?.Status ?? "Gamepad support disabled");
        Check("Stick / d-pad left-right steers (face + thrust)", s.Bindings.PadStickSteering, v => s.Bindings.PadStickSteering = v);
        Slider("Analog deadzone", s.Bindings.PadDeadzone, v => s.Bindings.PadDeadzone = (float)v, 0.05, 0.9);

        Header("Keyboard bindings (click, then press a key; Backspace clears to default)");
        _captureHint = new TextBlock { Foreground = Brushes.Gold };
        _root.Children.Add(_captureHint);
        foreach (var b in Enum.GetValues<LogicalButton>())
        {
            var btn = new Button { Content = string.Join(" / ", s.Bindings.Keyboard[b]), MinWidth = 260 };
            var lb = b;
            btn.Click += (_, _) => { _capturing = lb; _captureHint.Text = $"Press a key for {lb} (Esc cancels)…"; };
            Labeled(b.ToString(), btn);
        }
        Header("Gamepad bindings (click, then press a pad control; Esc cancels)");
        if (_host.Gamepad?.Connected != true) Note("Connect a controller to rebind it.");
        foreach (var b in Enum.GetValues<LogicalButton>())
        {
            var btn = new Button { Content = string.Join(" / ", s.Bindings.Gamepad[b]), MinWidth = 260, IsEnabled = _host.Gamepad?.Connected == true };
            var lb = b;
            btn.Click += (_, _) => StartPadCapture(lb);
            Labeled(b.ToString(), btn);
        }

        var reset = new Button { Content = "Reset all bindings to defaults" };
        reset.Click += (_, _) => { s.Bindings = InputBindings.CreateDefault(); Refresh(); };
        _root.Children.Add(reset);

        var done = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right };
        done.Click += (_, _) => _close();
        _root.Children.Add(done);
    }

    /// <summary>While waiting for a rebind, the next key goes to the binding (adds it as the primary key).</summary>
    public bool TryCaptureKey(string key)
    {
        if (_capturing is not { } b) return false;
        var s = _host.Settings;
        if (key != "Escape")
        {
            if (key == "Back") s.Bindings.Keyboard[b] = InputBindings.CreateDefault().Keyboard[b];
            else
            {
                InputBindings.Rebind(s.Bindings.Keyboard, b, key); // a key drives exactly one action
            }
            s.Bindings = s.Bindings.Sanitized();
        }
        _capturing = null;
        Refresh();
        return true;
    }

    private void StartPadCapture(LogicalButton b)
    {
        _capturingPad = b;
        // Ignore controls already held (e.g. A used to click) until they are released.
        _padHeldAtStart = _host.Gamepad?.PressedControls().ToHashSet() ?? new();
        if (_captureHint is not null) _captureHint.Text = $"Press a gamepad control for {b} (Esc cancels)…";
        _padTimer.Start();
    }

    private void StopPadCapture()
    {
        _capturingPad = null;
        _padTimer.Stop();
    }

    private void PollPadCapture()
    {
        if (_capturingPad is not { } b || _host.Gamepad is not { Connected: true } pad) { StopPadCapture(); return; }
        var held = pad.PressedControls();
        _padHeldAtStart.IntersectWith(held);
        var fresh = held.FirstOrDefault(c => !_padHeldAtStart.Contains(c), (PadControl)(-1));
        if ((int)fresh < 0) return;
        ApplyPadBinding(_host.Settings.Bindings, b, fresh);
        StopPadCapture();
        Refresh();
    }

    /// <summary>Makes <paramref name="control"/> the primary pad binding for <paramref name="b"/>, removing it elsewhere.</summary>
    public static void ApplyPadBinding(InputBindings bindings, LogicalButton b, PadControl control)
    {
        InputBindings.Rebind(bindings.Gamepad, b, control);
        bindings.Gamepad = bindings.Sanitized().Gamepad;
    }

    /// <summary>Esc while waiting for a pad control cancels the capture.</summary>
    public bool TryCancelPadCapture()
    {
        if (_capturingPad is null) return false;
        StopPadCapture();
        if (_captureHint is not null) _captureHint.Text = "";
        return true;
    }

    private void Header(string text) => _root.Children.Add(new TextBlock { Text = text, FontWeight = FontWeight.Bold, Foreground = Brushes.Gold, Margin = new Thickness(0, 8, 0, 0) });
    private void Note(string text) => _root.Children.Add(new TextBlock { Text = text, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap });

    private void Check(string label, bool value, Action<bool> set)
    {
        var c = new CheckBox { Content = label, IsChecked = value };
        c.IsCheckedChanged += (_, _) => set(c.IsChecked == true);
        _root.Children.Add(c);
    }

    private void Slider(string label, double value, Action<double> set, double min = 0, double max = 1)
    {
        var sl = new Slider { Minimum = min, Maximum = max, Value = value, Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        sl.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) set(sl.Value); };
        Labeled(label, sl);
    }

    private void Labeled(string label, Control c) =>
        _root.Children.Add(new DockPanel
        {
            Children = { new TextBlock { Text = label, Width = 250, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White }, c },
        });
}
