using Defender.Core.Audio;
using Defender.Core.Scoring;
using Defender.Infrastructure.Audio;
using Defender.Infrastructure.Persistence;

namespace Defender.Tests;

public class AudioTests
{
    private static Mixer NewMixer() => new(SoundSynth.RenderAll());

    [Fact]
    public void AllSoundsSynthesise_NonEmptyAndFinite()
    {
        foreach (SoundId id in Enum.GetValues<SoundId>())
        {
            if (id == SoundId.Count) continue;
            var clip = SoundSynth.Render(id);
            Assert.NotEmpty(clip);
            Assert.All(clip, s => Assert.True(float.IsFinite(s) && Math.Abs(s) <= SoundSynth.MaxPeak + 1e-6f));
        }
    }

    [Fact]
    public void RapidTriggering_NeverExceedsVoicePool_OrThrows()
    {
        var m = NewMixer();
        var buf = new float[256];
        for (int i = 0; i < 2000; i++)
        {
            m.Play((SoundId)(i % (int)SoundId.Count));
            if (i % 7 == 0) m.Mix(buf);
        }
        Assert.InRange(m.ActiveVoices, 1, Mixer.MaxVoices);
        Assert.All(buf, s => Assert.InRange(s, -1f, 1f));
    }

    [Fact]
    public void ThrustLoop_IsIdempotent_SurvivesExplosionFlood_AndStopsCleanly()
    {
        var m = NewMixer();
        m.SetLooping(SoundId.Thrust, true);
        m.SetLooping(SoundId.Thrust, true);
        Assert.Equal(1, m.ActiveVoices);
        for (int i = 0; i < 100; i++) m.Play(SoundId.EnemyExplode);
        var buf = new float[44100];
        m.Mix(buf); // well past the clip length: proves looping
        Assert.Equal(1, m.ActiveVoices);
        m.SetLooping(SoundId.Thrust, false);
        m.Mix(new float[1024]);
        Assert.Equal(0, m.ActiveVoices);
    }

    [Fact]
    public void MutedMixer_OutputsSilence()
    {
        var m = NewMixer();
        m.Muted = true;
        m.Play(SoundId.SmartBomb);
        var buf = new float[512];
        m.Mix(buf);
        Assert.All(buf, s => Assert.Equal(0f, s));
    }

    [Fact]
    public void NullEngine_AcceptsAllCalls()
    {
        using var e = new NullSoundEngine();
        e.Play(SoundId.Fire);
        e.SetLooping(SoundId.Thrust, true);
        e.StopAll();
        Assert.False(e.IsAvailable);
    }
}

public class PersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "defender-tests-" + Guid.NewGuid().ToString("N"));

    public sealed class Doc : IVersioned
    {
        public int SchemaVersion { get; set; }
        public string Name { get; set; } = "default";
    }

    private JsonStore<Doc> Store(Func<Doc, Doc?>? migrate = null) => new(Path.Combine(_dir, "doc.json"), 2, () => new Doc(), migrate);

    [Fact]
    public void MissingFile_YieldsDefaults()
    {
        var r = Store().Load();
        Assert.Equal(LoadStatus.Missing, r.Status);
        Assert.Equal("default", r.Value.Name);
    }

    [Fact]
    public void RoundTrip_PreservesValues_AndWritesSchemaVersion()
    {
        var s = Store();
        s.Save(new Doc { Name = "x" });
        var r = s.Load();
        Assert.Equal(LoadStatus.Loaded, r.Status);
        Assert.Equal("x", r.Value.Name);
        Assert.Contains("\"schemaVersion\": 2", File.ReadAllText(s.FilePath));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void CorruptFile_YieldsDefaults_AndIsQuarantined()
    {
        Directory.CreateDirectory(_dir);
        var s = Store();
        File.WriteAllText(s.FilePath, "{ this is not json");
        var r = s.Load();
        Assert.Equal(LoadStatus.Corrupt, r.Status);
        Assert.NotNull(r.Message);
        Assert.False(File.Exists(s.FilePath));
        Assert.Single(Directory.GetFiles(_dir, "doc.json.bad-*"));
    }

    [Fact]
    public void NewerVersion_IsRejected_OlderIsMigrated()
    {
        Directory.CreateDirectory(_dir);
        var s = Store(old => new Doc { Name = old.Name + "-migrated" });
        File.WriteAllText(s.FilePath, "{\"schemaVersion\":1,\"name\":\"v1\"}");
        var r = s.Load();
        Assert.Equal(LoadStatus.Migrated, r.Status);
        Assert.Equal("v1-migrated", r.Value.Name);

        File.WriteAllText(s.FilePath, "{\"schemaVersion\":99,\"name\":\"future\"}");
        Assert.Equal(LoadStatus.IncompatibleVersion, s.Load().Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}

public class HighScoreTableTests
{
    [Fact]
    public void KeepsTopTen_Sorted_AndRejectsBelowBoundary()
    {
        var t = new HighScoreTable();
        for (int i = 1; i <= 12; i++) t.Insert(new HighScoreEntry("abc", i * 1000, 1, DateTime.UnixEpoch));
        Assert.Equal(10, t.Entries.Count);
        Assert.Equal(12000, t.Entries[0].Score);
        Assert.Equal(3000, t.Entries[^1].Score);
        Assert.False(t.Qualifies(3000)); // must beat, not tie, the 10th place
        Assert.True(t.Qualifies(3001));
        Assert.Equal(-1, t.Insert(new HighScoreEntry("zzz", 2000, 1, DateTime.UnixEpoch)));
        Assert.Equal(0, t.Insert(new HighScoreEntry("top", 99999, 5, DateTime.UnixEpoch)));
        Assert.Equal("TOP", t.Entries[0].Initials);
    }

    [Fact]
    public void TieGoesBelowExistingEntry()
    {
        var t = new HighScoreTable();
        t.Insert(new HighScoreEntry("AAA", 500, 1, DateTime.UnixEpoch));
        Assert.Equal(1, t.Insert(new HighScoreEntry("BBB", 500, 1, DateTime.UnixEpoch)));
    }

    [Theory]
    [InlineData("ab", "AB ")]
    [InlineData("a1b2c3d", "ABC")]
    [InlineData(null, "   ")]
    public void InitialsAreNormalised(string? input, string expected) => Assert.Equal(expected, HighScoreTable.NormalizeInitials(input));
}

public class WavExportTests
{
    [Fact]
    public void ExportsOneValidWavPerSound()
    {
        var dir = Path.Combine(Path.GetTempPath(), "defender-wav-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = WavExport.ExportAll(dir);
            Assert.Equal((int)SoundId.Count, files.Count);
            foreach (var f in files)
            {
                var bytes = File.ReadAllBytes(f);
                Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
                Assert.Equal(bytes.Length - 8, BitConverter.ToInt32(bytes, 4));
                Assert.True(bytes.Length > 1000);
            }
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
