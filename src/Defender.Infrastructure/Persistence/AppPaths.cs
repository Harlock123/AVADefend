namespace Defender.Infrastructure.Persistence;

/// <summary>OS-appropriate per-user data directory: %APPDATA%\AVADefend, $XDG_CONFIG_HOME/AVADefend or ~/.config/AVADefend.</summary>
public static class AppPaths
{
    public const string AppFolder = "AVADefend";

    /// <summary>The folder used by builds before the AVADefend rename.</summary>
    public const string LegacyAppFolder = "Defender1981";

    public static string DefaultDataDirectory() => Path.Combine(ConfigRoot(), AppFolder);

    public static string LegacyDataDirectory() => Path.Combine(ConfigRoot(), LegacyAppFolder);

    private static string ConfigRoot()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return !string.IsNullOrWhiteSpace(xdg) && Path.IsPathRooted(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
    }

    /// <summary>
    /// One-time move of an existing pre-rename data folder to the new name, so settings, high scores and
    /// suspend slots carry over. Does nothing if the new folder already exists or the old one doesn't.
    /// Returns a message describing what happened, or null if there was nothing to do.
    /// </summary>
    public static string? MigrateLegacyDataDirectory(string legacyDir, string newDir)
    {
        if (Directory.Exists(newDir) || !Directory.Exists(legacyDir)) return null;
        try
        {
            Directory.Move(legacyDir, newDir);   // same parent folder: a rename
            return $"Moved saved data from {Path.GetFileName(legacyDir)} to {Path.GetFileName(newDir)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not move old saved data ({ex.Message}); starting with defaults.";
        }
    }
}
