using Avalonia;
using Defender.Core.Audio;
using Defender.Infrastructure.Audio;

namespace Defender.Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--audio-probe")) return AudioProbe();
        if (args.Contains("--selftest")) return SelfTest.Run();
        int shots = Array.IndexOf(args, "--screenshots");
        if (shots >= 0) return ScreenshotTool.Run(shots + 1 < args.Length ? args[shots + 1] : "screenshots");
        int ex = Array.IndexOf(args, "--export-sounds");
        if (ex >= 0)
        {
            var dir = ex + 1 < args.Length ? args[ex + 1] : "sounds";
            foreach (var f in WavExport.ExportAll(dir)) Console.WriteLine(f);
            return 0;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>Diagnostic: plays every effect once and reports whether SDL audio opened.</summary>
    private static int AudioProbe()
    {
        using var audio = SdlSoundEngine.CreateOrFallback();
        Console.WriteLine($"Audio: available={audio.IsAvailable} status=\"{audio.Status}\"");
        if (!audio.IsAvailable) return 1;
        audio.SetLooping(SoundId.Thrust, true);
        System.Threading.Thread.Sleep(800);
        audio.SetLooping(SoundId.Thrust, false);
        for (var id = (SoundId)0; id < SoundId.Count; id++)
        {
            if (id == SoundId.Thrust) continue;
            Console.WriteLine($"  {id}");
            audio.Play(id);
            System.Threading.Thread.Sleep(350);
        }
        return 0;
    }
}
