using Defender.Core.Scoring;
using Defender.Infrastructure.Persistence;

namespace Defender.Tests;

public class HighScoreBookTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "defender-hsbook-" + Guid.NewGuid().ToString("N"));
    private DateTime _now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Insert_PlacesInBothTables_TodayHoldsEight()
    {
        var b = new HighScoreBook(() => _now);
        for (int i = 1; i <= 12; i++) b.Insert("AAA", i * 100, 1);
        Assert.Equal(10, b.AllTime.Entries.Count);
        Assert.Equal(HighScoreBook.TodayCapacity, b.Today.Entries.Count);
        Assert.Equal(1200, b.Today.Entries[0].Score);
    }

    [Fact]
    public void TodayTable_ResetsWhenLocalDateChanges_AllTimeKept()
    {
        var b = new HighScoreBook(() => _now);
        b.Insert("AAA", 5000, 3);
        _now = _now.AddDays(1);
        Assert.True(b.Qualifies(10)); // empty today table: any positive score qualifies
        Assert.Empty(b.Today.Entries);
        Assert.Single(b.AllTime.Entries);
    }

    [Fact]
    public void LowScore_CanQualifyForToday_ButNotAllTime()
    {
        var b = new HighScoreBook(() => _now);
        var old = Enumerable.Range(1, 10).Select(i => new HighScoreEntry("OLD", 100_000 + i, 9, _now.AddDays(-30)));
        b.Load(old, [], null);
        Assert.True(b.Qualifies(500));
        var (all, today) = b.Insert("NEW", 500, 1);
        Assert.Equal((-1, 0), (all, today));
    }

    [Fact]
    public void Load_DropsTodayEntries_FromAnotherDate()
    {
        var b = new HighScoreBook(() => _now);
        var e = new HighScoreEntry("YDA", 999, 2, _now.AddDays(-1));
        b.Load([e], [e], DateOnly.FromDateTime(_now.ToLocalTime()).AddDays(-1));
        Assert.Empty(b.Today.Entries);
        Assert.Single(b.AllTime.Entries);
    }

    [Fact]
    public void V1File_MigratesToV2()
    {
        Directory.CreateDirectory(_dir);
        var store = new Storage(_dir).HighScores("classic");
        File.WriteAllText(store.FilePath,
            "{\"schemaVersion\":1,\"entries\":[{\"initials\":\"ABC\",\"score\":4200,\"wave\":3,\"dateUtc\":\"2026-01-01T00:00:00Z\"}]}");
        var r = store.Load();
        Assert.Equal(LoadStatus.Migrated, r.Status);
        Assert.Equal(2, r.Value.SchemaVersion);
        Assert.Equal(4200, Assert.Single(r.Value.Entries).Score);
        Assert.Empty(r.Value.Today);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
