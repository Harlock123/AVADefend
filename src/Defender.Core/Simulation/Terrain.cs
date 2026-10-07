namespace Defender.Core.Simulation;

/// <summary>
/// The planet surface. Matches the original's *format and constraints* (2048 one-pixel steps, each
/// exactly 1 px up or down; starts and ends at Y=224; altitude stays within 159..233 — blk71.src:384-397)
/// but the profile itself is generated here from a seed rather than copied from the Williams table.
/// The original's profile is fixed for every game, so we likewise use one fixed seed by default.
/// </summary>
public sealed class Terrain
{
    public const int DefaultSeed = 1981;
    public const int MinY = 159, MaxY = 233;
    private readonly byte[] _y = new byte[Arcade.WorldPixels];

    public Terrain(int seed = DefaultSeed)
    {
        Seed = seed;
        var rng = new Random(seed);
        for (int attempt = 0; ; attempt++)
        {
            if (TryGenerate(rng)) break;
            if (attempt > 1000) throw new InvalidOperationException("Terrain generation failed");
        }
    }

    public int Seed { get; }

    /// <summary>Ground line Y at world pixel x (wraps).</summary>
    public int HeightAtPixel(int px) => _y[px & (Arcade.WorldPixels - 1)];

    public int HeightAtUnits(int worldX) => HeightAtPixel((worldX & Arcade.WorldMask) / Arcade.UnitsPerPixel);

    private bool TryGenerate(Random rng)
    {
        // Mountain ranges: alternating up/down runs, more jagged where it climbs high.
        int n = Arcade.WorldPixels;
        int y = Arcade.TerrainStartY;
        int i = 0;
        bool up = true;
        while (i < n)
        {
            int remaining = n - i;
            int distHome = y - Arcade.TerrainStartY; // >0 means below start (cannot happen much), <0 above
            // Must be able to return to the start height in the remaining steps.
            int run = up ? rng.Next(3, 38) : rng.Next(3, 34);
            if (rng.Next(6) == 0) run = rng.Next(1, 4); // small jaggies
            for (int k = 0; k < run && i < n; k++)
            {
                remaining = n - i;
                int needed = Math.Abs(y - Arcade.TerrainStartY);
                bool goUp = up;
                if (remaining <= needed) goUp = y > Arcade.TerrainStartY; // forced return home
                else if (remaining == needed + 1 && remaining % 2 == 1) goUp = y > Arcade.TerrainStartY;
                if (goUp && y <= MinY) goUp = false;
                if (!goUp && y >= MaxY) goUp = true;
                y += goUp ? -1 : 1;
                _y[i++] = (byte)y;
            }
            _ = distHome;
            up = !up;
            // Bias towards lowlands so humanoids have room and peaks are occasional.
            if (y > 214 && rng.Next(3) == 0) up = true;
            if (y < 175) up = false;
        }
        // Step format: each entry differs from its neighbour by exactly 1 and the loop closes.
        if (_y[n - 1] != Arcade.TerrainStartY) return false;
        int prev = Arcade.TerrainStartY;
        for (int k = 0; k < n; k++)
        {
            if (Math.Abs(_y[k] - prev) != 1 || _y[k] < MinY || _y[k] > MaxY) return false;
            prev = _y[k];
        }
        return true;
    }
}
