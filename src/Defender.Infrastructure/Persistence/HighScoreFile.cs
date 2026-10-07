using Defender.Core.Scoring;

namespace Defender.Infrastructure.Persistence;

public sealed class HighScoreFile : IVersioned
{
    public const int CurrentSchema = 1;
    public int SchemaVersion { get; set; } = CurrentSchema;
    public List<HighScoreEntry> Entries { get; set; } = new();
}

/// <summary>File names and stores in the user data directory. One high-score file per preset ("slot").</summary>
public sealed class Storage(string directory)
{
    public string Directory { get; } = directory;

    public JsonStore<GameSettings> Settings() =>
        new(Path.Combine(Directory, "settings.json"), GameSettings.CurrentSchema, () => new GameSettings());

    public JsonStore<HighScoreFile> HighScores(string slot) =>
        new(Path.Combine(Directory, $"highscores-{Sanitize(slot)}.json"), HighScoreFile.CurrentSchema, () => new HighScoreFile());

    public JsonStore<SuspendFile> Suspend(int slot) =>
        new(Path.Combine(Directory, $"suspend-{Math.Clamp(slot, 1, 3)}.json"), SuspendFile.CurrentSchema, () => new SuspendFile());

    private static string Sanitize(string s) => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
