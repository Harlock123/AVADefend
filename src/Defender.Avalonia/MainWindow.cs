using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Defender.Core;
using Defender.Core.Simulation;

namespace Defender.Avalonia;

public sealed class MainWindow : Window
{
    private readonly GameHost _host;
    private readonly GameView _view;
    private readonly Border _help;
    private readonly SettingsPanel _settings;
    private WindowState _restoreState = WindowState.Normal;

    public MainWindow() : this(new GameHost()) { }

    public MainWindow(GameHost host)
    {
        _host = host;
        Title = Branding.WindowTitle;
        Width = SoftwareRenderer3x.W; Height = SoftwareRenderer3x.H;
        MinWidth = 292; MinHeight = 240;
        Background = Brushes.Black;
        _view = new GameView(host) { Focusable = true };
        _help = BuildHelp();
        _settings = new SettingsPanel(host, () => CloseSettings()) { IsVisible = false };
        Content = new Grid { Children = { _view, _help, _settings } };
        if (host.Settings.Fullscreen) SetFullscreen(true);

        AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: false);
        AddHandler(KeyUpEvent, OnKeyUp, handledEventsToo: false);
        Deactivated += (_, _) =>
        {
            _host.ReleaseAllKeys();
            // Modern: auto-pause when the window loses focus (suspend). Classic keeps running like a cabinet.
            if (_host.Settings.Mode == GameMode.Modern && _host.Session.State == SessionState.Playing && !_host.Session.Paused)
                _host.Session.Step(new Core.Input.PlayerInput { PausePressed = true });
        };
        Opened += (_, _) => _view.Focus();
        Closing += (_, _) => { _host.SuspendIfPlaying(); _host.SaveSettings(); _host.Dispose(); };
    }

    public GameView View => _view;

    private static string KeyName(Key k) => k switch { Key.Enter => "Enter", _ => k.ToString() };

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_settings.IsVisible)
        {
            if (_settings.TryCaptureKey(KeyName(e.Key))) { e.Handled = true; return; }
            if (e.Key is Key.Escape or Key.F10) { CloseSettings(); e.Handled = true; }
            return;
        }
        switch (e.Key)
        {
            case Key.F11: SetFullscreen(WindowState != WindowState.FullScreen); e.Handled = true; return;
            case Key.F1: _help.IsVisible = !_help.IsVisible; e.Handled = true; return;
            case Key.F10: OpenSettings(); e.Handled = true; return;
        }
        if (_help.IsVisible && e.Key == Key.Escape) { _help.IsVisible = false; e.Handled = true; return; }
        // Initials can be typed directly; letters must not also act as game buttons there.
        if (_host.Session.State == SessionState.EnterInitials && e.Key is >= Key.A and <= Key.Z)
        {
            _host.Session.TypeInitial((char)('A' + (e.Key - Key.A)));
            e.Handled = true;
            return;
        }
        _host.KeyDown(KeyName(e.Key));
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _host.KeyUp(KeyName(e.Key));
        e.Handled = true;
    }

    private void SetFullscreen(bool on)
    {
        if (on) { _restoreState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState; WindowState = WindowState.FullScreen; }
        else WindowState = _restoreState;
        _host.Settings.Fullscreen = on;
    }

    private void OpenSettings()
    {
        _host.ReleaseAllKeys();
        if (_host.Session.State == SessionState.Playing && !_host.Session.Paused)
            _host.Session.Step(new Core.Input.PlayerInput { PausePressed = true });
        _settings.Refresh();
        _settings.IsVisible = true;
    }

    private void CloseSettings()
    {
        _settings.IsVisible = false;
        _host.ApplySettings();
        _host.SaveSettings();
        _view.Focus();
    }

    private Border BuildHelp()
    {
        var text = new TextBlock
        {
            FontFamily = new FontFamily("monospace"),
            FontSize = 14,
            Foreground = Brushes.White,
            Text = ControlsHelp.Text(_host.Settings.Bindings),
        };
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 0, 0, 0)),
            BorderBrush = Brushes.SteelBlue, BorderThickness = new Thickness(2),
            Padding = new Thickness(16), HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Child = text, IsVisible = false,
        };
    }
}

internal static class SoftwareRenderer3x
{
    public const int W = 292 * 3, H = 240 * 3;
}
