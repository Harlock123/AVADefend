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
            Assert.Equal("/tmp/xdg-test/Defender1981", AppPaths.DefaultDataDirectory());
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "relative/path");   // not absolute: ignored per spec
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Assert.Equal(Path.Combine(home, ".config", "Defender1981"), AppPaths.DefaultDataDirectory());
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
            Assert.Equal(Path.Combine(home, ".config", "Defender1981"), AppPaths.DefaultDataDirectory());
        }
        finally { Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", old); }
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
