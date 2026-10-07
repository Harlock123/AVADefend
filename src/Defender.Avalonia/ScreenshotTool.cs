using Defender.Avalonia.Rendering;
using Defender.Core.Input;
using Defender.Core.Scoring;
using Defender.Core.Simulation;
using SkiaSharp;

namespace Defender.Avalonia;

/// <summary>
/// `--screenshots &lt;dir&gt;`: renders deterministic README screenshots straight from the game's renderer (fixed seeds,
/// autopilot play, 3× nearest-neighbour). Regenerate them whenever the visuals change.
/// </summary>
internal static class ScreenshotTool
{
    private const int Scale = 3;

    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var written = new List<string>();
        var renderer = new SoftwareRenderer { ShowControlHints = true };
        var snap = new FrameSnapshot();

        void Save(string name)
        {
            renderer.Render(snap);
            using var bmp = new SKBitmap(SoftwareRenderer.Width, SoftwareRenderer.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            unsafe
            {
                fixed (uint* p = renderer.Pixels)
                    Buffer.MemoryCopy(p, (void*)bmp.GetPixels(), renderer.Pixels.Length * 4L, renderer.Pixels.Length * 4L);
            }
            using var big = bmp.Resize(new SKImageInfo(SoftwareRenderer.Width * Scale, SoftwareRenderer.Height * Scale),
                                       new SKSamplingOptions(SKFilterMode.Nearest));
            var path = Path.Combine(dir, name + ".png");
            using (var f = File.Create(path)) big.Encode(f, SKEncodedImageFormat.Png, 100);
            written.Add(path);
        }

        // ---- gameplay moments from a seeded autopilot game ----
        var wanted = new Dictionary<string, Func<GameSession, bool>>
        {
            ["gameplay"] = s => s.State == SessionState.Playing && s.Lasers.Count > 0 &&
                                s.Enemies.Count(e => !e.Dead && e.Appear == 0 && s.OnScreen(e.X)) >= 3,
            // A lander visibly carrying its humanoid up, well inside the cropped 292-px picture.
            ["abduction"] = s => s.State == SessionState.Playing &&
                                 s.Enemies.Any(e => e.Kind == EnemyKind.Lander && e.Phase == LanderPhase.Lift &&
                                                    s.SignedScreenX(e.X) is > 60 and < 250 && e.PixelY is > 100 and < 160),
            ["explosion"] = s => s.State == SessionState.Playing && s.Blasts.Count >= 2 && s.Blasts.All(b => b.Size < 0x800),
            ["ship-explosion"] = s => s.State == SessionState.Dying && s.StateTimer == 70,
            ["wave-bonus"] = s => s.State == SessionState.WaveComplete && s.StateTimer == 100,
        };
        var pilot = new Autopilot();
        var game = new GameSession(rng: new XorShiftRandom(1981));
        game.StartGame();
        for (int f = 0; f < 60 * 60 * 15 && wanted.Count > 0; f++)
        {
            game.Step(pilot.Next(game));
            if (game.State is SessionState.Attract or SessionState.EnterInitials) { game.StartGame(); continue; }
            foreach (var (name, when) in wanted.ToList())
            {
                if (!when(game)) continue;
                game.BuildSnapshot(snap);
                Save(name);
                wanted.Remove(name);
            }
        }

        // ---- two players ----
        var two = new GameSession(rng: new XorShiftRandom(7));
        two.StartGame(2);
        var pilot2 = new Autopilot();
        for (int f = 0; f < 60 * 60 * 10; f++)
        {
            two.Step(pilot2.Next(two));
            if (two.State == SessionState.Playing && two.CurrentPlayer == 1 && two.ScoreOf(0) > 0 && two.StateTimer > 200) break;
        }
        two.BuildSnapshot(snap);
        Save("two-players");

        // ---- attract pages ----
        var book = new HighScoreBook();
        foreach (var (i, sc) in new[] { ("LJW", 104_350), ("ACE", 61_200), ("EJ ", 48_900), ("LD ", 31_250), ("SAM", 12_000) })
            book.Insert(i, sc, sc / 10_000 + 1);
        renderer.HighScoreProvider = () => book;
        var attract = new GameSession(rng: new XorShiftRandom(3), highScores: book);
        var director = new AttractDirector();
        var marks = new Dictionary<int, string>
        {
            [700] = "attract-title",
            [AttractDirector.TitleFrames + 300] = "hall-of-fame",
            [AttractDirector.TitleFrames + AttractDirector.HallFrames + 505] = "demo-rescue",
            [AttractDirector.TitleFrames + AttractDirector.HallFrames + AttractDemo.TotalFrames - 300] = "demo-scoring",
        };
        for (int f = 1; f <= marks.Keys.Max(); f++)
        {
            attract.Step(default);
            director.Step();
            if (!marks.TryGetValue(f, out var name)) continue;
            attract.BuildSnapshot(snap);
            if (director.Phase == AttractPhase.Demo) director.Demo!.Build(snap, attract.Terrain);
            snap.Demo = director.Phase == AttractPhase.Demo;
            snap.AttractPhase = director.Phase;
            snap.AttractTimer = director.PhaseTimer;
            Save(name);
        }

        foreach (var w in written) Console.WriteLine(w);
        return 0;
    }
}
