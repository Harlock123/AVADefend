namespace Defender.Core.Simulation;

/// <summary>
/// An explosion of a specific sprite frame (EXST/EXPU, samexap7.src): the sprite's own 2-px × 2-row tiles are
/// spread apart by an integer factor S that grows from 1.0 by $AA (0.664) per frame until it passes $30 (~72
/// frames). Fixed in the world; radiates from the laser hit point when it lies inside the sprite.
/// </summary>
public sealed class Blast
{
    public string SpriteName { get; set; } = "";
    public int TopLeftX { get; set; }     // world units
    public int TopRow { get; set; }       // px
    public int CenterX { get; set; }      // world units (column precision)
    public int CenterRow { get; set; }    // px
    public int Size { get; set; } = 0x100; // 8.8
    public long Serial { get; set; }
}

public sealed partial class GameSession
{
    public const int MaxEffectSlots = 16;      // RAMALS pool: 16 slots of 64 bytes (phr6.src:113-114, 207-209)
    public List<Blast> Blasts { get; } = new();
    private long _blastSerial;

    /// <summary>Starts an explosion of <paramref name="spr"/> drawn at (worldX, yPx). Off-screen objects don't explode.</summary>
    private void StartBlast(Sprite spr, int worldX, int yPx, int? hitScreenX = null, int? hitRow = null)
    {
        int sx = SignedScreenX(worldX);
        if (sx < 0 || sx > 311) return;                           // relative X (units) high byte must be ≤ $26
        int topCol = sx >> 1;
        int wCols = (spr.Width + 1) / 2;
        int cCol = topCol + wCols / 2, cRow = yPx + spr.Height / 2;
        if (hitScreenX is { } hx && hitRow is { } hr && hx >= sx && hx < sx + spr.Width && hr >= yPx && hr < yPx + spr.Height)
        {
            cCol = hx >> 1; cRow = hr;                            // CENTMP: radiate from where the laser hit
        }
        if (Blasts.Count >= MaxEffectSlots)
        {
            // Round-robin stealing: the oldest running explosion goes; the newest is never stolen.
            Blasts.RemoveAt(0);
        }
        int left = WrapX(CameraX + topCol * 2 * Arcade.UnitsPerPixel);
        Blasts.Add(new Blast
        {
            SpriteName = spr.Name, TopLeftX = left, TopRow = yPx,
            CenterX = WrapX(CameraX + cCol * 2 * Arcade.UnitsPerPixel), CenterRow = cRow, Size = 0x100, Serial = ++_blastSerial,
        });
    }

    private void UpdateBlasts()
    {
        foreach (var b in Blasts) b.Size += 0xAA;
        Blasts.RemoveAll(b => (b.Size >> 8) > 0x30);
    }

    // ----- player explosion (PLEX, blk71.src:566-672) -------------------------------------------------------

    private static readonly byte[] PlexFade = [0x7F, 0x3F, 0x37, 0x2F, 0x27, 0x1F, 0x17, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02];
    public const int PlexFrames = 56 + 13 * 4;   // 108

    /// <summary>128 pieces from ship + (8,3) with a fixed-seed diamond velocity spread: the same pattern every death.</summary>
    private void StartPlex()
    {
        Particles.Clear();
        var r = new XorShiftRandom(0x08081732);   // fixed seed, like the original's $0808/$1732 registers
        int x0 = (Player.ScreenPx + 8) << 8, y0 = (Player.PixelY + 3) << 8;
        while (Particles.Count < 128)
        {
            int vx = r.Next(1024) - 512;                          // ±2 px/frame in 1/256 px
            int vy = r.Next(1024) - 512;                          // ±2 rows/frame
            if (Math.Abs(vx) + Math.Abs(vy) >= 724) continue;    // |vx_cols| + |vy|/2 < 1.414 → diamond
            Particles.Add(new Particle { X = x0, Y = y0, Vx = vx, Vy = vy, Life = 1 });
        }
    }

    private void UpdatePlex()
    {
        foreach (var p in Particles)
        {
            if (p.Life == 0) continue;
            int nx = p.X + p.Vx, ny = p.Y + p.Vy;
            if ((ny >> 8) < Arcade.YMin || (ny >> 8) > 255 || nx < 0 || (nx >> 8) > Arcade.ScreenWidth) { p.Life = 0; continue; }
            p.X = nx; p.Y = ny;
        }
    }

    /// <summary>Palette $B during PLEX: white for 56 frames, then 13 fading colours × 4 frames.</summary>
    private byte PlexColor(int framesIntoPlex) =>
        framesIntoPlex < 56 ? (byte)0xFF : PlexFade[Math.Min((framesIntoPlex - 56) / 4, PlexFade.Length - 1)];
}
