namespace Defender.Infrastructure.Persistence;

/// <summary>OS-appropriate per-user data directory: %APPDATA%\Defender1981, $XDG_CONFIG_HOME/Defender1981 or ~/.config/Defender1981.</summary>
public static class AppPaths
{
    public const string AppFolder = "Defender1981";

    public static string DefaultDataDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolder);
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var root = !string.IsNullOrWhiteSpace(xdg) && Path.IsPathRooted(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(root, AppFolder);
    }
}
