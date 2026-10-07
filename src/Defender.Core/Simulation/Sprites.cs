namespace Defender.Core.Simulation;

/// <summary>
/// Palette-indexed sprite. Pixels hold a 0-15 pseudo-palette index, or <see cref="Transparent"/>.
/// The same pixels drive rendering and pixel-mask collision (the original tests image overlap,
/// defa7.src:2907-3020). All artwork here is original, drawn to the documented dimensions.
/// </summary>
public sealed class Sprite
{
    public const byte Transparent = 0xFF;

    public Sprite(string name, params string[] rows)
    {
        Name = name;
        Height = rows.Length;
        Width = rows.Max(r => r.Length);
        Pixels = new byte[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                char c = x < rows[y].Length ? rows[y][x] : '.';
                Pixels[y * Width + x] = c is '.' or ' ' ? Transparent : (byte)Convert.ToInt32(c.ToString(), 16);
            }
    }

    private Sprite(string name, int w, int h, byte[] px) { Name = name; Width = w; Height = h; Pixels = px; }

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public bool Solid(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height && Pixels[y * Width + x] != Transparent;

    public Sprite Mirror()
    {
        var px = new byte[Pixels.Length];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                px[y * Width + x] = Pixels[y * Width + (Width - 1 - x)];
        return new Sprite(Name + "-mirror", Width, Height, px);
    }

    /// <summary>Pixel-mask overlap of two sprites at integer screen positions.</summary>
    public static bool Overlap(Sprite a, int ax, int ay, Sprite b, int bx, int by)
    {
        int x0 = Math.Max(ax, bx), x1 = Math.Min(ax + a.Width, bx + b.Width);
        int y0 = Math.Max(ay, by), y1 = Math.Min(ay + a.Height, by + b.Height);
        if (x0 >= x1 || y0 >= y1) return false;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (a.Solid(x - ax, y - ay) && b.Solid(x - bx, y - by)) return true;
        return false;
    }

    /// <summary>Does a horizontal 1-px-high segment [x0,x1) at row y touch the sprite?</summary>
    public bool HitsSegment(int sx, int sy, int x0, int x1, int y)
    {
        if (y < sy || y >= sy + Height) return false;
        int a = Math.Max(x0, sx), b = Math.Min(x1, sx + Width);
        for (int x = a; x < b; x++) if (Solid(x - sx, y - sy)) return true;
        return false;
    }
}

/// <summary>Palette slots (defb6.src:1876-1891 CRTAB layout).</summary>
public static class Pal
{
    public const byte Background = 0, Laser = 1, Red = 2, Green = 3, Yellow = 4, WaveBlue = 5, Grey = 6,
        Brown = 7, Purple = 8, White = 9, CycleA = 0xA, DeathGlow = 0xB, CycleC = 0xC, BomberD = 0xD, BomberE = 0xE, BomberF = 0xF;
}

public static class Sprites
{
    // Ship 16x6 (PLAPIC "8,6" bytes → 16x6 px).
    public static readonly Sprite ShipRight = new("ship",
        "66..............",
        "C669............",
        "CC66999999......",
        "C6669999999999..",
        ".66999922.......",
        "..666...........");
    public static readonly Sprite ShipLeft = ShipRight.Mirror();

    // Lander 10x8, three animation frames.
    public static readonly Sprite[] Lander =
    [
        new("lander0", "...3333...", "..333333..", ".34334334.", "..333333..", "...4444...", "..4.44.4..", ".4..44..4.", "4...44...4"),
        new("lander1", "...3333...", "..333333..", ".33433433.", "..333333..", "...4444...", "..4.44.4..", "..4.44.4..", ".4..44..4."),
        new("lander2", "...3333...", "..333333..", ".43343343.", "..333333..", "...4444...", "...4444...", "..4.44.4..", "..4.44.4.."),
    ];

    public static readonly Sprite Mutant = new("mutant",
        "..C.CC.C..", "...CCCC...", ".CC3CC3CC.", "CCCCCCCCCC", "..CC33CC..", ".C.C..C.C.", "C..C..C..C", "...C..C...");

    public static readonly Sprite Baiter = new("baiter",
        "...333333...", ".3333333333.", "33C33C33C333", ".3333333333.");

    public static readonly Sprite Bomber = new("bomber",
        "DDDDDDDD", "DEEEEEED", "DEFFFFED", "DEF..FED", "DEF..FED", "DEFFFFED", "DEEEEEED", "DDDDDDDD");

    public static readonly Sprite Pod = new("pod",
        "8..88..8", ".8.88.8.", "..2882..", "88822888", "88822888", "..2882..", ".8.88.8.", "8..88..8");

    public static readonly Sprite Swarmer = new("swarmer", ".2222.", "244442", "224422", ".2..2.");

    public static readonly Sprite Humanoid = new("humanoid", ".66.", ".66.", "6886", "6886", ".88.", ".88.", ".6.6", ".6.6");

    public static readonly Sprite Mine = new("mine", "C..C", ".CC.", "C..C");

    public static readonly Sprite Shot = new("shot", ".AA.", "A99A", ".AA.");

    public static readonly Sprite ShipIcon = new("shipicon", "C66.......", "C66999999.", ".6699999..", "..66......");
    public static readonly Sprite BombIcon = new("bombicon", ".22222", "222222", ".22222");

    /// <summary>Chunk of exploding ground for the planet explosion (our own art standing in for TEREX).</summary>
    public static readonly Sprite TerrainChunk = new("terrainchunk", ".7474.", "774477", "47..74", "774477", ".7474.");

    private static Dictionary<string, Sprite>? s_byName;

    /// <summary>Lookup by name so effects that reference a sprite can be serialised.</summary>
    public static Sprite ByName(string name)
    {
        s_byName ??= new[] { ShipRight, ShipLeft, Mutant, Baiter, Bomber, Pod, Swarmer, Humanoid, Mine, Shot, TerrainChunk }
            .Concat(Lander).ToDictionary(s => s.Name);
        return s_byName.TryGetValue(name, out var s) ? s : TerrainChunk;
    }

    /// <summary>Laser collision probe LASP1: 8 bytes × 1 row = 16×1 px (see GameSession.UpdateLasers).</summary>
    public const int LaserProbeWidth = 16;
}
