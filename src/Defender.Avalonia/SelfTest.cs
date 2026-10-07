using System.Diagnostics;
using System.Runtime.InteropServices;
using Defender.Avalonia.Rendering;
using Defender.Core.Simulation;
using Defender.Infrastructure.Audio;
using Defender.Infrastructure.Input;
using Defender.Infrastructure.Persistence;

namespace Defender.Avalonia;

/// <summary>
/// `--selftest`: a windowless platform check meant for the first run on a new OS. Exercises the engine,
/// renderer, native libraries, audio, gamepad subsystem and the data directory, and prints a report.
/// Exit code 0 = all required checks passed (audio/gamepad absence is reported but not fatal).
/// </summary>
internal static class SelfTest
{
    public static int Run()
    {
        int failures = 0;
        void Check(string name, Func<string> body, bool required = true)
        {
            try { Console.WriteLine($"[PASS] {name}: {body()}"); }
            catch (Exception ex)
            {
                if (required) failures++;
                Console.WriteLine($"[{(required ? "FAIL" : "WARN")}] {name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine($"Defender self-test — {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}, .NET {Environment.Version}");
        Check("Simulation (2 minutes of autopilot play)", () =>
        {
            var s = new GameSession(rng: new XorShiftRandom(1981));
            var pilot = new Autopilot();
            var snap = new FrameSnapshot();
            var r = new SoftwareRenderer();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 60 * 120; i++) { s.Step(pilot.Next(s)); s.BuildSnapshot(snap); r.Render(snap); }
            return $"{sw.Elapsed.TotalMilliseconds / (60 * 120):0.000} ms/frame incl. render; score {s.Score}, wave {s.Wave}";
        });
        Check("Native Skia (SkiaSharp)", () => { using var b = new SkiaSharp.SKBitmap(8, 8); return "loaded " + SkiaSharp.SkiaSharpVersion.Native; });
        Check("Data directory writable", () =>
        {
            var dir = AppPaths.DefaultDataDirectory();
            var probe = Path.Combine(dir, ".selftest.tmp");
            AtomicFile.WriteAllText(probe, "ok");
            File.Delete(probe);
            return dir;
        });
        Check("Audio (SDL)", () =>
        {
            using var a = SdlSoundEngine.CreateOrFallback();
            if (!a.IsAvailable) throw new InvalidOperationException(a.Status);
            return a.Status;
        }, required: false);
        Check("Gamepad subsystem (SDL)", () =>
        {
            using var p = new SdlGamepad();
            if (p.Status.StartsWith("SDL unavailable") || p.Status.StartsWith("Gamepad init failed") || p.Status.StartsWith("Gamepad unavailable"))
                throw new InvalidOperationException(p.Status);
            return p.Status;
        }, required: false);
        Console.WriteLine(failures == 0 ? "RESULT: OK" : $"RESULT: {failures} required check(s) failed");
        return failures == 0 ? 0 : 1;
    }
}
