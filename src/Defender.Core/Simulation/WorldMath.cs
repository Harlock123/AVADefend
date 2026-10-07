namespace Defender.Core.Simulation;

/// <summary>
/// Wrap-around arithmetic for the 16-bit planet (world X in units, 0..65535; 32 units per pixel). These are the
/// only helpers the engine uses for wrapping, so the seam tests exercise the real code path.
/// </summary>
public static class WorldMath
{
    /// <summary>World X wrapped into 0..65535 (the 16-bit OX16 register wrap).</summary>
    public static int WrapUnits(int x) => x & Arcade.WorldMask;

    /// <summary>Shortest signed distance from <paramref name="from"/> to <paramref name="to"/>, in units (−32768..32767).</summary>
    public static int DeltaUnits(int from, int to) => (short)(ushort)((to - from) & Arcade.WorldMask);

    /// <summary>Generic ring helpers (pixels or any other period).</summary>
    public static int Wrap(int x, int width)
    {
        int r = x % width;
        return r < 0 ? r + width : r;
    }

    public static int Delta(int from, int to, int width)
    {
        int d = Wrap(to - from, width);
        return d > width / 2 ? d - width : d;
    }
}
