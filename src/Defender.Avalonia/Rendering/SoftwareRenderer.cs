using Defender.Core;
using Defender.Core.Simulation;

namespace Defender.Avalonia.Rendering;

/// <summary>
/// Rasterises a <see cref="FrameSnapshot"/> into a 292×240 BGRA buffer: the MAME "defender" visible
/// area (set_visarea(12,303,7,246)) of the 304×256 game frame. Pure managed code → unit-testable.
/// </summary>
public sealed class SoftwareRenderer
{
    public const int Width = 292, Height = 240, CropX = 12, CropY = 7;
    public readonly uint[] Pixels = new uint[Width * Height];
    private readonly uint[] _pal = new uint[16];
    private readonly uint[] _byteToArgb = new uint[256];

    public SoftwareRenderer()
    {
        // BBGGGRRR through the board's resistor ladders (williams_v.cpp:340-363, computed levels).
        int[] rg = [0, 38, 81, 118, 137, 174, 217, 255];
        int[] b = [0, 95, 160, 255];
        for (int i = 0; i < 256; i++)
            _byteToArgb[i] = 0xFF000000u | (uint)(rg[i & 7] << 16) | (uint)(rg[(i >> 3) & 7] << 8) | (uint)b[(i >> 6) & 3];
    }

    public bool ShowControlHints { get; set; } = true;
    public string? StatusLine { get; set; }
    public double GameSpeed { get; set; } = 1;
    public bool BoldScanner { get; set; }
    public Func<Core.Scoring.HighScoreBook?>? HighScoreProvider { get; set; }

    public uint Argb(byte paletteByte) => _byteToArgb[paletteByte];

    public void Render(FrameSnapshot s)
    {
        for (int i = 0; i < 16; i++) _pal[i] = _byteToArgb[s.Palette[i]];
        Array.Fill(Pixels, _pal[Pal.Background]);

        switch (s.State)
        {
            case SessionState.Attract: DrawAttract(s); DrawHud(s, attract: true); return;
            case var _ when s.Demo:
                DrawPlayfield(s); DrawHud(s, false);
                CenterText("DEMONSTRATION", 60, _pal[Pal.White]);
                if ((s.StateTimer / 30) % 2 == 0) CenterText("PRESS 1 OR F2 TO START", 214, _pal[Pal.Yellow]);
                return;
            case SessionState.GameOver:
                DrawPlayfield(s); DrawHud(s, false);
                CenterText("GAME OVER", 130, _pal[Pal.White]);
                return;
            case SessionState.EnterInitials: DrawInitials(s); return;
        }
        DrawPlayfield(s);
        DrawHud(s, false);
        if (s.State == SessionState.LifeStart && s.PlayerCount == 2 && s.StateTimer < 128) CenterText(s.CurrentPlayer == 0 ? "PLAYER ONE" : "PLAYER TWO", 120, _pal[Pal.White]);
        if (s.State == SessionState.TurnOver) { CenterText(s.CurrentPlayer == 0 ? "PLAYER ONE" : "PLAYER TWO", 112, _pal[Pal.White]); CenterText("GAME OVER", 124, _pal[Pal.White]); }
        if (s.State == SessionState.WaveComplete) DrawWaveComplete(s);
        if (s.Paused) { CenterText("PAUSED", 116, _pal[Pal.White]); CenterText("ESC / START TO RESUME", 128, _pal[Pal.Grey]); if (StatusLine is { } st) CenterText(st, 140, _pal[Pal.Yellow]); }
    }

    // ----- primitives in game coordinates (304×256) -------------------------------------------------

    private void Plot(int gx, int gy, uint c)
    {
        int x = gx - CropX, y = gy - CropY;
        if ((uint)x < Width && (uint)y < Height) Pixels[y * Width + x] = c;
    }

    private void HLine(int x0, int x1, int y, uint c) { for (int x = x0; x <= x1; x++) Plot(x, y, c); }
    private void VLine(int x, int y0, int y1, uint c) { for (int y = y0; y <= y1; y++) Plot(x, y, c); }

    private void DrawSprite(Sprite spr, int gx, int gy, int appear = 0, uint? mono = null)
    {
        int cx = spr.Width / 2, cy = spr.Height / 2;
        for (int y = 0; y < spr.Height; y++)
            for (int x = 0; x < spr.Width; x++)
            {
                byte p = spr.Pixels[y * spr.Width + x];
                if (p == Sprite.Transparent) continue;
                uint c = mono ?? _pal[p];
                if (appear > 0)
                {
                    // Materialise: pixels converge onto the sprite from a spread-out cloud.
                    int k = 1 + appear / 3;
                    Plot(gx + cx + (x - cx) * k, gy + cy + (y - cy) * k, c);
                }
                else Plot(gx + x, gy + y, c);
            }
    }

    public void Text(string s, int gx, int gy, uint c, int scale = 1)
    {
        int cx = gx;
        foreach (char ch in s.ToUpperInvariant())
        {
            if (PixelFont.Glyphs.TryGetValue(ch, out var rows))
                for (int r = 0; r < PixelFont.GlyphHeight; r++)
                    for (int col = 0; col < PixelFont.GlyphWidth; col++)
                        if ((rows[r] >> (PixelFont.GlyphWidth - 1 - col) & 1) != 0)
                            for (int sy = 0; sy < scale; sy++)
                                for (int sx = 0; sx < scale; sx++)
                                    Plot(cx + col * scale + sx, gy + r * scale + sy, c);
            cx += PixelFont.Advance * scale;
        }
    }

    private void CenterText(string s, int gy, uint c, int scale = 1) =>
        Text(s, CropX + (Width - PixelFont.Measure(s, scale)) / 2, gy, c, scale);

    // ----- playfield ----------------------------------------------------------------------------------

    private void DrawPlayfield(FrameSnapshot s)
    {
        foreach (var st in s.Stars) if (st.Y > Arcade.ScannerBottom) Plot(st.X, st.Y, _pal[st.Color & 0xF]);
        uint ground = _pal[Pal.Brown];
        for (int x = 0; x < Arcade.ScreenWidth; x++) if (s.TerrainY[x] >= 0) Plot(x, s.TerrainY[x], ground);

        foreach (var d in s.Sprites)
        {
            if (d.Y + d.Sprite.Height <= Arcade.ScannerBottom) continue;
            DrawSprite(d.Sprite, d.X, d.Y, d.Appear, d.Mono is { } m ? _pal[m] : null);
        }
        foreach (var b in s.Blasts) DrawBlast(b);
        foreach (var l in s.Lasers) DrawLaser(l);
        foreach (var p in s.Particles)   // PLEX: 2×2 squares in colour $B
        {
            uint c = _pal[p.Color & 0xF];
            Plot(p.X, p.Y, c); Plot(p.X + 1, p.Y, c); Plot(p.X, p.Y + 1, c); Plot(p.X + 1, p.Y + 1, c);
        }
        foreach (var t in s.Popups) Text(t.Text, t.X, t.Y, _pal[t.Color]);
    }

    /// <summary>EWRITE (samexap7.src:223-377): each 2-px × 2-row tile keeps its colours; only the spacing scales.</summary>
    private void DrawBlast(BlastDraw b)
    {
        var spr = b.Sprite;
        int xoff = b.CenterCol - b.TopCol, dy = b.CenterRow - b.TopRow;
        int yoff = dy >> 1, flavor = dy & 1;
        for (int py = 0; py < spr.Height; py++)
            for (int px = 0; px < spr.Width; px++)
            {
                byte c = spr.Pixels[py * spr.Width + px];
                if (c == Sprite.Transparent) continue;
                int col = b.CenterCol + ((px >> 1) - xoff) * b.S;
                int row = b.CenterRow - flavor + ((py >> 1) - yoff) * 2 * b.S;
                if (col < 0 || col > 0x98 || row < Arcade.YMin || row > 255) continue;
                Plot(col * 2 + (px & 1), row + (py & 1), _pal[c]);
            }
    }

    private void DrawLaser(LaserDraw l)
    {
        uint beam = _pal[Pal.Laser], tip = _pal[Pal.White];
        int a = Math.Min(l.Fizzle, l.Head), b = Math.Max(l.Fizzle, l.Head);
        HLine(a, b, l.Y, beam);
        Plot(l.Head, l.Y, tip); Plot(l.Head - l.Dir, l.Y, tip);
        // Broken trailing section between the tail eraser and the fizzle front.
        int t0 = Math.Min(l.Tail, l.Fizzle), t1 = Math.Max(l.Tail, l.Fizzle);
        for (int x = t0; x < t1; x++)
            if (((x * 7919) ^ (x >> 2)) % 5 < 2) Plot(x, l.Y, beam);
    }

    // ----- HUD + scanner (top 40 rows) ------------------------------------------------------------------

    private void DrawHud(FrameSnapshot s, bool attract)
    {
        // BORDER (defa7.src:906-929): colour 5 (the wave colour); 2-row line across the screen under the scanner,
        // 2-px scanner sides, 1-row top; white visible-window marker over the top and bottom lines.
        uint frame = _pal[Pal.WaveBlue], w = _pal[Pal.White];
        int left = Arcade.ScannerLeft - 2, right = Arcade.ScannerLeft + Arcade.ScannerWidth;   // x 94 and 224
        for (int x = 0; x < Arcade.ScreenWidth; x++) { Plot(x, 40, frame); Plot(x, 41, frame); }
        for (int y = Arcade.ScannerTop; y < Arcade.ScannerBottom; y++) { Plot(left, y, frame); Plot(left + 1, y, frame); Plot(right, y, frame); Plot(right + 1, y, frame); }
        HLine(left, right + 1, Arcade.ScannerTop - 1, frame);
        for (int x = 152; x <= 167; x++) { Plot(x, 7, w); Plot(x, 8, w); Plot(x, 40, w); Plot(x, 41, w); }

        // Scanner contents.
        foreach (var t in s.ScannerTerrain) { Plot(t.X, t.Y, _pal[t.Color]); Plot(t.X + 1, t.Y, _pal[t.Color]); }
        foreach (var b in s.Scanner)
        {
            if (b.X < Arcade.ScannerLeft || b.X > right - 2) continue;
            if (BoldScanner && s.Mode == GameMode.Modern)
            {
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = 0; dx <= 2; dx++)
                        if (b.X + dx < right && b.Y + dy > Arcade.ScannerTop - 1)
                            Plot(b.X + dx, b.Y + dy, _pal[dy < 1 ? b.Upper : b.Lower]);
                continue;
            }
            Plot(b.X, b.Y, _pal[b.Upper]); Plot(b.X + 1, b.Y, _pal[b.Upper]);
            Plot(b.X, b.Y + 1, _pal[b.Lower]); Plot(b.X + 1, b.Y + 1, _pal[b.Lower]);
        }
        if (!attract && s.State != SessionState.EnterInitials)
        {
            // Visible-window ticks inside the scanner (MTX) and the player's 3x3 plus.
            foreach (int x in new[] { s.ScannerWindowLeft, s.ScannerWindowRight })
            {
                Plot(x, Arcade.ScannerTop + 1, w); Plot(x, Arcade.ScannerTop + 2, w);
                Plot(x, Arcade.ScannerTop + 30, w); Plot(x, Arcade.ScannerTop + 31, w);
            }
            if (s.PlayerVisible)
            {
                int px = s.ScannerPlayerX, py = s.ScannerPlayerY;
                Plot(px, py - 1, w); Plot(px - 1, py, w); Plot(px, py, w); Plot(px + 1, py, w); Plot(px, py + 1, w);
            }
        }

        DrawPlayerPanel(s, 0, attract);
        if (s.PlayerCount == 2 && !attract) DrawPlayerPanel(s, 1, attract);
        else if (s.Mode == GameMode.Modern)
        {
            // Modern addition (not on the arcade HUD): high score and wave in the free right-hand area.
            Text("HIGH", 232, 10, _pal[Pal.Grey]);
            Text(s.HighScore.ToString(), 232, 19, _pal[Pal.White]);
            if (!attract) Text($"WAVE {s.Wave}", 232, 29, _pal[Pal.Grey]);
            Text(GameSpeed < 1 ? $"{GameSpeed:0.##}X" : "", 284, 10, _pal[Pal.Green]);
        }
    }

    /// <summary>LDISP/SBDISP/SCRTRN (defa7.src:546-902) positions: P1 at x 30, P2 at x 226.</summary>
    private void DrawPlayerPanel(FrameSnapshot s, int p, bool attract)
    {
        int baseX = p == 0 ? 30 : 226, bombX = p == 0 ? 82 : 278;
        for (int i = 0; i < Math.Min(s.PlayerLives[p], 5); i++) DrawSprite(Sprites.ShipIcon, baseX + i * 12, 20);
        for (int i = 0; i < Math.Min(s.PlayerBombs[p], 3); i++) DrawSprite(Sprites.BombIcon, bombX, 27 + i * 4);
        if (attract && s.PlayerScores[p] == 0) return;
        // Six-digit field, 8 px per digit; leading zeros blanked but the last two digits always shown.
        string digits = Math.Clamp(s.PlayerScores[p], 0, 999_999).ToString("D6");
        int firstShown = Math.Min(digits.TakeWhile(c => c == '0').Count(), 4);
        // Colour 1 (it cycles with the laser) as in the source; Modern uses a steady colour for readability.
        uint c = s.Mode == GameMode.Modern ? _pal[Pal.Yellow] : _pal[Pal.Laser];
        if (s.PlayerCount == 2 && p != s.CurrentPlayer && s.Mode == GameMode.Modern) c = _pal[Pal.Grey];
        for (int i = firstShown; i < 6; i++) Text(digits[i].ToString(), baseX + i * 8, 28, c);
    }

    private void DrawWaveComplete(FrameSnapshot s)
    {
        uint w = _pal[Pal.White];
        CenterText("ATTACK WAVE " + s.Wave, 80, w);
        CenterText("COMPLETED", 92, w);
        CenterText("BONUS X " + s.WaveBonusPerHumanoid, 112, w);
        int n = s.HumanoidsBonusCounted;
        int x0 = CropX + (Width - n * 8) / 2;
        for (int i = 0; i < n; i++) DrawSprite(Sprites.Humanoid, x0 + i * 8, 128);
    }

    // ----- attract / initials ----------------------------------------------------------------------------

    private void DrawAttract(FrameSnapshot s)
    {
        uint w = _pal[Pal.White], g = _pal[Pal.Grey];
        int t = s.StateTimer;
        CenterText(Branding.Title, 52, _pal[Pal.Laser], 3);
        CenterText(Branding.Disclaimer, 78, g);

        if (s.AttractPhase == AttractPhase.Title)
        {
            // Scoring legend.
            CenterText("SCORING", 96, w);
            var rows = new (Sprite spr, string name, string pts)[]
            {
                (Sprites.Lander[(t / 12) % 3], "LANDER", "150"), (Sprites.Mutant, "MUTANT", "150"),
                (Sprites.Baiter, "BAITER", "200"), (Sprites.Bomber, "BOMBER", "250"),
                (Sprites.Pod, "POD", "1000"), (Sprites.Swarmer, "SWARMER", "150"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                int y = 110 + i * 14;
                DrawSprite(rows[i].spr, 92, y);
                Text(rows[i].name, 112, y + 1, g);
                Text(rows[i].pts, 200 - PixelFont.Measure(rows[i].pts), y + 1, w);
            }
            DrawSprite(Sprites.Humanoid, 94, 196);
            Text("HUMANOID CATCH 500  LAND 500", 112, 197, g);
        }
        else
        {
            CenterText("HALL OF FAME", 92, w);
            var book = HighScoreProvider?.Invoke();
            DrawTable("TODAYS", "GREATEST", book?.Today.Entries, 8, CropX + 14);
            DrawTable("ALL TIME", "GREATEST", book?.AllTime.Entries, 10, CropX + 156);
        }
        if ((t / 30) % 2 == 0) CenterText("PRESS 1 OR 2 PLAYER START", 214, _pal[Pal.Yellow]);
        if (ShowControlHints) CenterText("F1 CONTROLS   F10 SETTINGS   F11 FULLSCREEN", 230, g);
        if (StatusLine is { } st) Text(st.Length > 48 ? st[..48] : st, CropX + 2, Arcade.ScannerBottom + 4, _pal[Pal.Grey]);
    }

    /// <summary>One column of the hall of fame (the arcade showed Today's and All-Time side by side).</summary>
    private void DrawTable(string title1, string title2, IReadOnlyList<Core.Scoring.HighScoreEntry>? entries, int rows, int gx)
    {
        uint w = _pal[Pal.White], g = _pal[Pal.Grey];
        Text(title1, gx + (122 - PixelFont.Measure(title1)) / 2, 106, _pal[Pal.Yellow]);
        Text(title2, gx + (122 - PixelFont.Measure(title2)) / 2, 115, _pal[Pal.Yellow]);
        for (int i = 0; i < rows; i++)
        {
            var e = entries is not null && i < entries.Count ? entries[i] : null;
            string line = e is null ? $"{i + 1,2}  ---       " : $"{i + 1,2} {e.Initials} {e.Score,7}";
            Text(line, gx, 128 + i * 9, e is null ? g : (i == 0 ? _pal[Pal.Yellow] : w));
        }
    }

    private void DrawInitials(FrameSnapshot s)
    {
        uint w = _pal[Pal.White];
        CenterText(s.InitialsPlayer == 0 ? "PLAYER ONE" : "PLAYER TWO", 60, _pal[Pal.Yellow]);
        CenterText("YOU HAVE QUALIFIED FOR", 80, w);
        CenterText("THE HALL OF FAME", 92, w);
        CenterText("SCORE " + s.InitialsScore, 110, _pal[Pal.Laser]);
        CenterText("UP/DOWN TO CHOOSE, FIRE TO ENTER", 130, _pal[Pal.Grey]);
        CenterText("(OR TYPE THEM)", 140, _pal[Pal.Grey]);
        int x0 = CropX + (Width - 3 * 18) / 2;
        for (int i = 0; i < 3; i++)
        {
            uint c = i == s.InitialsCursor && (s.StateTimer / 8) % 2 == 0 ? _pal[Pal.Yellow] : w;
            Text(s.Initials[i].ToString(), x0 + i * 18, 160, c, 2);
            if (i == s.InitialsCursor) HLine(x0 + i * 18, x0 + i * 18 + 9, 176, _pal[Pal.Yellow]);
        }
    }
}
