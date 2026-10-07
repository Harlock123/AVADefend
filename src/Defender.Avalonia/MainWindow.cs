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
    private readonly TextBlock _hintBar;
    private WindowState _restoreState = WindowState.Normal;
    private readonly HashSet<Key> _keysDown = new();

    public MainWindow() : this(new GameHost()) { }

    public MainWindow(GameHost host)
    {
        _host = host;
        Title = Branding.WindowTitle;
        try { Icon = new WindowIcon(global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://AVADefend/Assets/defender-256.png"))); }
        catch (Exception) { /* icon is cosmetic */ }
        Width = SoftwareRenderer3x.W; Height = SoftwareRenderer3x.H;
        MinWidth = 292; MinHeight = 240;
        Background = Brushes.Black;
        _view = new GameView(host) { Focusable = true };
        _help = BuildHelp();
        _settings = new SettingsPanel(host, () => CloseSettings()) { IsVisible = false };
        _hintBar = new TextBlock
        {
            FontFamily = new FontFamily("monospace"), FontSize = 12, Foreground = Brushes.Gray,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center, Margin = new Thickness(4, 2),
        };
        var dock = new DockPanel();
        DockPanel.SetDock(_hintBar, Dock.Bottom);
        dock.Children.Add(_hintBar);
        dock.Children.Add(_view);
        Content = new Grid { Children = { dock, _help, _settings } };
        RefreshHintBar();
        if (host.Settings.Fullscreen) SetFullscreen(true);

        AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: false);
        AddHandler(KeyUpEvent, OnKeyUp, handledEventsToo: false);
        Deactivated += (_, _) =>
        {
            _host.ReleaseAllKeys();
            _keysDown.Clear();
            // Modern: auto-pause when the window loses focus. Classic keeps running like a cabinet.
            if (_host.Settings.Mode != GameMode.Modern) return;
            if (_host.Session.State is SessionState.Playing or SessionState.LifeStart) _host.Session.SetPaused(true);
            else if (_host.Session.State is not (SessionState.Attract or SessionState.EnterInitials)) _host.FocusHold = true;
        };
        Activated += (_, _) => _host.FocusHold = false;
        Opened += (_, _) => _view.Focus();
        Closing += (_, _) => { _host.Session.CommitPendingInitials(); _host.SuspendIfPlaying(); _host.SaveSettings(); _host.Dispose(); };
    }

    public GameView View => _view;

    private static string KeyName(Key k) => k switch { Key.Enter => "Enter", _ => k.ToString() };

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        bool repeat = !_keysDown.Add(e.Key);   // OS key-repeat (or a key still held from play)
        if (_settings.IsVisible)
        {
            if (e.Key == Key.Escape && _settings.TryCancelPadCapture()) { e.Handled = true; return; }
            if (_settings.TryCaptureKey(KeyName(e.Key))) { e.Handled = true; return; }
            if (e.Key is Key.Escape or Key.F10 or Key.F9) { CloseSettings(); e.Handled = true; }
            return;
        }
        switch (e.Key)
        {
            case Key.F11: SetFullscreen(WindowState != WindowState.FullScreen); e.Handled = true; return;
            case Key.F1:
                if (_help.Child is TextBlock t) t.Text = ControlsHelp.Text(_host.Settings.Bindings); // reflect rebinding
                _help.IsVisible = !_help.IsVisible; e.Handled = true; return;
            case Key.F10 or Key.F9: OpenSettings(); e.Handled = true; return; // F9 too: F10 is a Windows menu key
        }
        if (_help.IsVisible && e.Key == Key.Escape) { _help.IsVisible = false; e.Handled = true; return; }
        // Initials can be typed directly; letters must not also act as game buttons there.
        if (_host.Session.State == SessionState.EnterInitials && e.Key is >= Key.A and <= Key.Z)
        {
            if (!repeat) _host.Session.TypeInitial((char)('A' + (e.Key - Key.A)));
            e.Handled = true;
            return;
        }
        _host.KeyDown(KeyName(e.Key));
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _keysDown.Remove(e.Key);
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
        _host.SettingsOpen = true;   // freezes the game in any state; pad presses are discarded
        _settings.Refresh();
        _settings.IsVisible = true;
    }


    /// <summary>Modern: a one-line control reminder below the picture (never drawn over the game).</summary>
    private void RefreshHintBar()
    {
        var s = _host.Settings;
        _hintBar.IsVisible = s.Mode == GameMode.Modern && s.ShowControlHints;
        string K(Core.Input.LogicalButton b) => s.Bindings.Keyboard.GetValueOrDefault(b)?.FirstOrDefault() ?? "?";
        _hintBar.Text = $"Thrust {K(Core.Input.LogicalButton.Thrust)}  Reverse {K(Core.Input.LogicalButton.Reverse)}  " +
                        $"Fire {K(Core.Input.LogicalButton.Fire)}  Bomb {K(Core.Input.LogicalButton.SmartBomb)}  " +
                        $"Hyperspace {K(Core.Input.LogicalButton.Hyperspace)}  Pause {K(Core.Input.LogicalButton.Pause)}  F1 help";
    }

    private void CloseSettings()
    {
        _settings.IsVisible = false;
        _host.SettingsOpen = false;
        _host.ApplySettings();
        _host.SaveSettings();
        _host.TryResume();
        RefreshHintBar();
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
