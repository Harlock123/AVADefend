namespace Defender.Core.Simulation;

public readonly record struct ScannerBlip(int X, int Y, byte Upper, byte Lower);

public sealed partial class GameSession
{
    // ----- stars (defa7.src:2073-2198) ---------------------------------------------------------------

    private void RandomizeStars()
    {
        foreach (var s in Stars)
        {
            s.X = Rng.Next(Arcade.ScreenWidth + 8);
            s.Y = Rng.Range(Arcade.YMin + 1, 168);
            s.Color = (byte)Rng.Range(2, 9);
        }
    }

    /// <summary>Stars shift 2 px opposite to travel per 128 world units (4 px) of scroll: half parallax.</summary>
    private void UpdateStars(int scrollUnits)
    {
        _starScrollAcc += scrollUnits;
        int shift = 0;
        while (_starScrollAcc >= 128) { shift -= 2; _starScrollAcc -= 128; }
        while (_starScrollAcc <= -128) { shift += 2; _starScrollAcc += 128; }
        int span = Arcade.ScreenWidth + 8;
        if (shift != 0)
            foreach (var s in Stars) s.X = ((s.X + shift) % span + span) % span;
        // SBLNK (defa7.src:2160-2197): one star per frame changes colour; on about half the frames it also jumps
        // to a new X, and only while the planet is gone does its Y re-randomise.
        var t = Stars[Rng.Next(Stars.Count)];
        t.Color = (byte)((t.Color + 1) & 7);
        if ((Rng.NextByte() & 1) == 0) t.X = Rng.Next(span);
        if (!PlanetActive) t.Y = Rng.Range(Arcade.YMin + 1, 168);
    }

    // ----- palette (BBGGGRRR bytes; defb6.src:1876-1891 layout, our own colour choices) ----------------

    private static readonly byte[] DefaultPalette =
        [0x00, 0xFF, 0x07, 0x38, 0x3F, 0xC0, 0xA4, 0x15, 0xC7, 0xFF, 0x3F, 0x00, 0xC7, 0xC0, 0x3F, 0x07];
    private static readonly byte[] CycleColors = [0x07, 0x1F, 0x3F, 0x38, 0xF8, 0xC0, 0xC7, 0xFF, 0x3C, 0x2F];
    private static readonly byte[] LaserCycle = [0xFF, 0x3F, 0x07, 0xC7, 0xF8, 0x38, 0xFF, 0xC0];
    private static readonly byte[] WaveColors = [0xC0, 0x38, 0x07, 0x15, 0x3F, 0xC7, 0xA4, 0xF8];
    private static readonly byte[] DeathGlow = [0x07, 0x07, 0x07, 0x0F, 0x3F, 0x7F, 0xFF, 0xFF];
    private static readonly (byte, byte, byte)[] BomberCycle = [(0xC0, 0xC0, 0x3F), (0xC0, 0x3F, 0x07), (0x3F, 0xC0, 0x07)];

    private void ResetPalette() => DefaultPalette.CopyTo(Palette, 0);

    private void UpdatePalette()
    {
        bool calm = Policy.SuppressFlashes;
        if (Frame % (calm ? 8 : 2) == 0) Palette[Pal.Laser] = LaserCycle[(int)(Frame / (calm ? 8 : 2)) % LaserCycle.Length];
        if (Frame % (calm ? 30 : 6) == 0)
        {
            Palette[Pal.CycleA] = CycleColors[Rng.Next(CycleColors.Length)];
            Palette[Pal.CycleC] = CycleColors[Rng.Next(CycleColors.Length)];
            var (d, e, f) = BomberCycle[(int)(Frame / (calm ? 30 : 6)) % 3];
            Palette[Pal.BomberD] = d; Palette[Pal.BomberE] = e; Palette[Pal.BomberF] = f;
        }
        Palette[Pal.WaveBlue] = WaveColors[(Math.Max(Wave, 1) - 1) % WaveColors.Length];
        Palette[Pal.DeathGlow] = State == SessionState.Dying ? DeathGlow[Math.Min(StateTimer / 4, DeathGlow.Length - 1)] : (byte)0xFF;
        // Background complement flash (smart bomb, death); planet explosion bursts tint it a random colour.
        Palette[Pal.Background] = FlashFrames > 0 && (FlashFrames / 2) % 2 == 1 ? (byte)0xFF : (byte)0x00;
        if (_planetFlashFrames > 0)
        {
            _planetFlashFrames--;
            if (!Policy.SuppressFlashes) Palette[Pal.Background] = PlanetFlashColor;
        }
    }
}
