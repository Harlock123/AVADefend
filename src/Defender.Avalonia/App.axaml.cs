using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Defender.Avalonia;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
            var host = new GameHost();
            if (desktop.Args?.Contains("--autoplay") == true) host.Autopilot = new Core.Simulation.Autopilot();
            desktop.MainWindow = new MainWindow(host);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
