namespace Defender.Core.Scoring;

/// <summary>
/// The arcade kept two tables: "TODAYS GREATEST" (reset daily) and "ALL TIME GREATEST", 8 entries each
/// (source + operator manual). We keep All-Time at 10 (project brief) and Today at the original 8.
/// The date comes from an injectable clock so rollover is testable.
/// </summary>
public sealed class HighScoreBook
{
    public const int TodayCapacity = 8;

    public HighScoreBook(Func<DateTime>? clockUtc = null)
    {
        ClockUtc = clockUtc ?? (() => DateTime.UtcNow);
        TodayDate = LocalDate(ClockUtc());
    }

    public Func<DateTime> ClockUtc { get; }
    public HighScoreTable AllTime { get; } = new();
    public HighScoreTable Today { get; } = new(TodayCapacity);
    public DateOnly TodayDate { get; private set; }

    private static DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(utc.ToLocalTime());

    /// <summary>Clears Today's table when the local date has changed. Returns true if it rolled over.</summary>
    public bool RollDate()
    {
        var now = LocalDate(ClockUtc());
        if (now == TodayDate) return false;
        TodayDate = now;
        Today.LoadFrom([]);
        return true;
    }

    public bool Qualifies(int score)
    {
        RollDate();
        return AllTime.Qualifies(score) || Today.Qualifies(score);
    }

    /// <summary>Inserts into both tables where it qualifies; returns the ranks (-1 = not placed).</summary>
    public (int AllTimeRank, int TodayRank) Insert(string initials, int score, int wave)
    {
        RollDate();
        var e = new HighScoreEntry(initials, score, wave, ClockUtc());
        return (AllTime.Insert(e), Today.Insert(e));
    }

    public void Load(IEnumerable<HighScoreEntry?>? allTime, IEnumerable<HighScoreEntry?>? today, DateOnly? todayDate)
    {
        AllTime.LoadFrom(allTime);
        Today.LoadFrom(todayDate == TodayDate ? today : []);
    }

    public int Best => AllTime.Entries.Count > 0 ? AllTime.Entries[0].Score : 0;
}
