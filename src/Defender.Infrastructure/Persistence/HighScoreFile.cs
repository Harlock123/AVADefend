using Defender.Core.Scoring;

namespace Defender.Infrastructure.Persistence;

/// <summary>
/// v1: all-time entries only. v2 (current): adds today's table and its local date.
/// </summary>
public sealed class HighScoreFile : IVersioned
{
    public const int CurrentSchema = 2;
    public int SchemaVersion { get; set; } = CurrentSchema;
    public List<HighScoreEntry> Entries { get; set; } = new();
    public List<HighScoreEntry> Today { get; set; } = new();
    public string? TodayDate { get; set; }

    /// <summary>v1 → v2: keep the all-time entries; today's table starts empty.</summary>
    public static HighScoreFile? Migrate(HighScoreFile old) =>
        old.SchemaVersion == 1 ? new HighScoreFile { Entries = old.Entries ?? new(), Today = new(), TodayDate = null } : null;

    public DateOnly? ParsedTodayDate => DateOnly.TryParseExact(TodayDate, "yyyy-MM-dd", out var d) ? d : null;
}

/// <summary>File names and stores in the user data directory. One high-score file per preset ("slot").</summary>
public sealed class Storage(string directory)
{
    public string Directory { get; } = directory;

    public JsonStore<GameSettings> Settings() =>
        new(Path.Combine(Directory, "settings.json"), GameSettings.CurrentSchema, () => new GameSettings());

    public JsonStore<HighScoreFile> HighScores(string slot) =>
        new(Path.Combine(Directory, $"highscores-{Sanitize(slot)}.json"), HighScoreFile.CurrentSchema, () => new HighScoreFile(), HighScoreFile.Migrate);

    public JsonStore<SuspendFile> Suspend(int slot) =>
        new(Path.Combine(Directory, $"suspend-{Math.Clamp(slot, 1, 3)}.json"), SuspendFile.CurrentSchema, () => new SuspendFile());

    private static string Sanitize(string s) => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
