namespace Defender.Core.Scoring;

public sealed record HighScoreEntry(string Initials, int Score, int Wave, DateTime DateUtc);

/// <summary>Top-N table, highest first. Ties keep the earlier entry ahead (arcade convention: a new score must beat, not equal).</summary>
public sealed class HighScoreTable
{
    public const int DefaultCapacity = 10;
    private readonly List<HighScoreEntry> _entries = new();

    public HighScoreTable(int capacity = DefaultCapacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
    }

    public int Capacity { get; }
    public IReadOnlyList<HighScoreEntry> Entries => _entries;

    public bool Qualifies(int score) => score > 0 && (_entries.Count < Capacity || score > _entries[^1].Score);

    /// <summary>Inserts if it qualifies; returns the 0-based rank or -1.</summary>
    public int Insert(HighScoreEntry entry)
    {
        if (!Qualifies(entry.Score)) return -1;
        int idx = _entries.FindIndex(e => entry.Score > e.Score);
        if (idx < 0) idx = _entries.Count;
        _entries.Insert(idx, entry with { Initials = NormalizeInitials(entry.Initials) });
        if (_entries.Count > Capacity) _entries.RemoveAt(_entries.Count - 1);
        return idx;
    }

    public static string NormalizeInitials(string? s)
    {
        var chars = (s ?? "").ToUpperInvariant().Where(c => c is (>= 'A' and <= 'Z') or ' ' or '.' or '-').Take(3).ToArray();
        return new string(chars).PadRight(3);
    }

    public void LoadFrom(IEnumerable<HighScoreEntry> entries)
    {
        _entries.Clear();
        foreach (var e in entries.Where(e => e.Score > 0).OrderByDescending(e => e.Score).Take(Capacity))
            _entries.Add(e with { Initials = NormalizeInitials(e.Initials) });
    }
}
