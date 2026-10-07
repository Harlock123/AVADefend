using Defender.Core.Audio;
using Defender.Infrastructure.Audio;
using Defender.Infrastructure.Persistence;

namespace Defender.Tests;

[Collection("Environment")]
public class AppPathsTests
{
    [Fact]
    public void UsesAbsoluteXdgConfigHome_IgnoresRelative_FallsBackToDotConfig()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "XDG applies to Linux/macOS");
        var old = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "/tmp/xdg-test");
            Assert.Equal("/tmp/xdg-test/AVADefend", AppPaths.DefaultDataDirectory());
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "relative/path");   // not absolute: ignored per spec
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Assert.Equal(Path.Combine(home, ".config", "AVADefend"), AppPaths.DefaultDataDirectory());
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
            Assert.Equal(Path.Combine(home, ".config", "AVADefend"), AppPaths.DefaultDataDirectory());
        }
        finally { Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", old); }
    }
}

public class LegacyDataMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "defender-migrate-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void OldFolder_IsRenamed_KeepingItsFiles()
    {
        var oldDir = Path.Combine(_root, AppPaths.LegacyAppFolder);
        var newDir = Path.Combine(_root, AppPaths.AppFolder);
        Directory.CreateDirectory(oldDir);
        File.WriteAllText(Path.Combine(oldDir, "highscores-classic.json"), "{}");
        Assert.NotNull(AppPaths.MigrateLegacyDataDirectory(oldDir, newDir));
        Assert.False(Directory.Exists(oldDir));
        Assert.True(File.Exists(Path.Combine(newDir, "highscores-classic.json")));
    }

    [Fact]
    public void ExistingNewFolder_IsNeverOverwritten()
    {
        var oldDir = Path.Combine(_root, AppPaths.LegacyAppFolder);
        var newDir = Path.Combine(_root, AppPaths.AppFolder);
        Directory.CreateDirectory(oldDir);
        Directory.CreateDirectory(newDir);
        File.WriteAllText(Path.Combine(newDir, "settings.json"), "new");
        Assert.Null(AppPaths.MigrateLegacyDataDirectory(oldDir, newDir));
        Assert.Equal("new", File.ReadAllText(Path.Combine(newDir, "settings.json")));
        Assert.True(Directory.Exists(oldDir));     // left alone
    }

    [Fact]
    public void NothingToDo_WhenThereIsNoOldFolder() =>
        Assert.Null(AppPaths.MigrateLegacyDataDirectory(Path.Combine(_root, "nope"), Path.Combine(_root, AppPaths.AppFolder)));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}

/// <summary>The real SDL output path, using SDL's silent "dummy" driver (no sound reaches the speakers).</summary>
[Collection("SDL")]
public class SdlSoundEngineTests
{
    [Fact]
    public void DummyDriver_PumpRunsUnderLoad_AndDisposesCleanly()
    {
        Environment.SetEnvironmentVariable("SDL_AUDIODRIVER", "dummy");
        var engine = SdlSoundEngine.CreateOrFallback();
        try
        {
            Assert.SkipWhen(!engine.IsAvailable && engine.Status.StartsWith("SDL unavailable"), engine.Status);
            Assert.True(engine.IsAvailable, engine.Status);
            engine.Monophonic = true;
            engine.SetLooping(SoundId.Thrust, true);
            for (int i = 0; i < 500; i++) engine.Play((SoundId)(i % (int)SoundId.Count));
            engine.Muted = true;
            for (int i = 0; i < 50; i++) { engine.SetLooping(SoundId.Thrust, i % 2 == 0); Thread.Sleep(2); }
            engine.Muted = false;
            Thread.Sleep(150);                                   // the pump thread keeps mixing without faulting
            Assert.True(engine.IsAvailable);
        }
        finally
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            engine.Dispose();
            Assert.True(sw.ElapsedMilliseconds < 1000, "dispose should stop the pump promptly");
            Environment.SetEnvironmentVariable("SDL_AUDIODRIVER", null);
        }
    }
}
