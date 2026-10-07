namespace Defender.Core.Simulation;

/// <summary>
/// Wrap-around arithmetic for the cylindrical planet. World X is an integer in [0, width).
/// </summary>
public static class WorldMath
{
    public static int Wrap(int x, int width)
    {
        int r = x % width;
        return r < 0 ? r + width : r;
    }

    public static double Wrap(double x, double width)
    {
        double r = x % width;
        return r < 0 ? r + width : r;
    }

    /// <summary>Shortest signed distance from <paramref name="from"/> to <paramref name="to"/> on the ring.</summary>
    public static int Delta(int from, int to, int width)
    {
        int d = Wrap(to - from, width);
        return d > width / 2 ? d - width : d;
    }

    public static double Delta(double from, double to, double width)
    {
        double d = Wrap(to - from, width);
        return d > width / 2 ? d - width : d;
    }
}
