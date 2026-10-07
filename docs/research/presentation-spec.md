# Defender (Red Label) presentation spec: explosions, HUD, attract mode

Clean-room behavioural spec taken from the mwenge 6809 source (read only). Paths below are relative to
`research_src/mwenge/src/`. Nothing here copies code, bitmaps or data tables. Only label names, line
references and single constants appear.

Conventions
- Screen address = column*256 + row. One byte column is 2 pixels, so **x_px = column*2** and **y = row**.
  The high nibble of a byte is the left pixel and the low nibble is the right pixel. A 16-bit store (STD/STY/STU)
  writes the same column at two rows: (col,row) and (col,row+1).
- World X (`OX16`) is in 1/64 column units, so **1 px = 32 units**. The screen is $2600 units wide (304 px).
  OY16's high byte is the pixel row, and the low byte is a fraction.
- Times are in frames at 60 Hz. TIMER is incremented once per video frame in the IRQ (defa7.src:1963). The EXEC
  loop runs once per TIMER tick (defa7.src:3048-3050). The attract code's own comments confirm the sleep units:
  `NAPP 60` is "SLEEP 1 SECOND" (amode1.src:313) and `NAPP $FF` is "SLEEP 4 SECS" (amode1.src:255).
- Object velocities are added once per frame in VELO (defa7.src:2480-2499). Y is 8.8 pixels/frame. X is in
  world units/frame, where 32 = 1 px.
- **VERIFIED** means read directly from code. **INFERRED** means derived or computed by me and not stated outright.

---------------------------------------------------------------------------------------------------

## 1. EXPLOSIONS AND APPEARS (samexap7.src, blk71.src PLEX)

### 1.1 Slot pool (VERIFIED)
- Slot size `RAMSIZ` = $40 = 64 bytes (phr6.src:113). The pool runs from $9C00 (phr6.src:207-209) to `RAMEND`
  = $A000 (phr6.src:114). That is 1024/64 = **16 slots** shared by explosions and appears (INFERRED arithmetic).
- Slot header: RSIZE (2 bytes; bit 15 = appear flag, high byte = integer size, low byte = fraction),
  OBDESC (picture), ERASES, CENTER (col,row), TOPLFT (col,row), OBJPTR (phr6.src:107-112). The other 52 bytes
  are an erase list of 2-byte screen addresses that grows down from the slot end. That allows **at most 26
  pieces drawn per slot per frame** (INFERRED, 64-12=52). The code does no bounds check.
- `LSEXPL` remembers the slot used by the most recent explosion (phr6.src:361).

### 1.2 Picture to pieces (VERIFIED, samexap7.src:223-377 EWRITE)
- A picture is W byte columns by H rows (descriptor bytes 0-1). Its data is stored column by column, H bytes
  per column (samexap7.src:230-235).
- A **piece** is one byte column (2 px) by **2 rows**, written as a single 16-bit store (samexap7.src:308-342).
  If H is odd, each column's last piece is a single byte, 2 px by 1 row (samexap7.src:236-241, 344-348).
- Pieces are never scaled. Only the **spacing** between them grows. Each piece keeps the original pixel colours
  of the object's current picture frame (OPICT at the moment the effect starts, samexap7.src:92-93 and 59-60).
  No colour change is applied to explosions (VERIFIED: EWRITE copies the picture data unchanged).

Formula for each frame, with S = integer part of the size (high byte of RSIZE with bit 7 masked,
samexap7.src:225-227). The fraction is ignored when drawing.
```
xoff   = CENTER.col - TOPLFT.col                  (columns)
dy     = CENTER.row - TOPLFT.row
yoff   = dy >> 1 ;  flavor = dy & 1               (samexap7.src:242-247)
piece (i = column 0..W-1, j = 2-row block 0..ceil(H/2)-1):
   col = CENTER.col + (i - xoff) * S              (samexap7.src:248-255, 365)
   row = CENTER.row - flavor + (j - yoff) * 2*S   (samexap7.src:271-281, 311)
```
- With S = 1 this rebuilds the original sprite, which is how an appear ends.
- Clipping (VERIFIED): columns where col < 0 are skipped (samexap7.src:256-268). Drawing stops at the first
  column with col > $98, i.e. x > 304 (samexap7.src:269-270, 366-368). Within a column, leading pieces with row < $2A
  (42, the playfield top just below the scanner) are skipped (samexap7.src:283-293). The column stops at the first
  piece that would pass row 255 (8-bit carry, samexap7.src:312 etc.). Pieces therefore never draw over the scanner.
- Every frame the previous frame's pieces are erased (written as 0) from the erase list, then redrawn
  (samexap7.src:198-199, 208-219). The 2-px block at CENTER is also cleared each frame (samexap7.src:369-376).

### 1.3 Explosion (EXST / EXPU), VERIFIED unless noted
- Start condition: the object's screen-relative X (OX16 - BGL) must have a high byte of $26 or less, i.e. the
  object must be on screen. Otherwise **no explosion happens** (samexap7.src:71-74).
- Slot choice: round robin starting at the slot after `LSEXPL`. Slots holding an appear (RSIZE < 0) are skipped.
  A slot with a running explosion is **stolen**: its old pieces are erased and the slot is reused. The search
  gives up when it gets back to `LSEXPL`, so the newest explosion is never stolen. If every other slot holds an
  appear, there is no explosion (samexap7.src:76-89).
- Initial size **$0100 = 1.0** (samexap7.src:90-91).
- Centre: the screen point `CENTMP` is used if it lies inside the picture rectangle. Otherwise the picture centre
  (TOPLFT + (W/2, H/2)) is used (samexap7.src:104-116). For a laser kill, the collision code sets `CENTMP` to the
  exact screen byte where the laser hit the sprite (defa7.src:2993-3008). The explosion therefore **radiates from
  the hit point**. For humanoid deaths CENTMP is set to roughly the sprite centre (defb6.src:826, 941).
- Growth: each frame RSIZE += **$AA** (0.664 per frame, samexap7.src:135). The explosion ends, is erased and
  frees its slot once the high byte exceeds **$30**, i.e. size reaches 49.0 (samexap7.src:137-142).
  - Lifetime is 73 updates. 72 frames are drawn with S stepping 1,2,3,...,48, an integer step about every
    1.5 frames (INFERRED: (0x3100-0x100)/0xAA = 72.3).
  - A piece k columns from the centre sits at k*S columns, so it moves at about 1.33*k px per frame
    (INFERRED: 2 px * 0.664 * k). Vertically it moves 2*k rows per S step. Outer pieces leave the screen within a few
    frames, which gives the "burst of separating squares" look.
- Scrolling: each frame CENTER.col and TOPLFT.col are moved by the background scroll delta (BGLX-BGL, to column
  precision), so the explosion stays fixed in the world (samexap7.src:143-160).
- Global kill: if STATUS bit 2 is set (not in normal play, e.g. STATUS=$7F during player death,
  defa7.src:1370-1371), every explosion is cancelled at once and every appear finishes at once
  (samexap7.src:123-130).
- Off screen: no start (see above). Pieces clip as in 1.2. The explosion is not attached to the object, so it
  keeps going after the object is gone.
- Callers: enemy kills via defb6.src:1188-1191 (EXST wrapper after scoring), ship/bomber etc.
  (defb6.src:189, 395, 464), defa7.src:2717, attract demo (amode1.src:552, 646).

### 1.4 Appear / materialise (APST / EXPU appear branch), VERIFIED unless noted
- Start: the object's real picture is saved in the slot and replaced by the null picture (the object is
  invisible but still moves under VELO). The object is linked into the active list (samexap7.src:23-27).
  If relative X > $2600 (off screen), the appear is **aborted** and the object shows normally at once
  (samexap7.src:28-31, 41-43).
- Slot choice: same round robin from `LSEXPL`, but it does not update LSEXPL. It skips appear slots and may
  steal a running explosion. It aborts if the search returns to LSEXPL (samexap7.src:33-48).
- The appear sound plays only in game (STATUS bit 7 clear) (samexap7.src:49-53). OTYP bit 1 is set while
  appearing and cleared at the end (samexap7.src:54-56, 176-178). It marks the object as not yet real; the
  game uses it to exclude the object from collisions/hyperspace (INFERRED from name "HYPER NOT").
- Initial RSIZE = **$AF00**: appear flag plus size **$2F = 47** (samexap7.src:57-58).
- Each frame RSIZE -= **$100** (one whole size step, samexap7.src:162-163). Drawn sizes are 46, 45, ..., 1, 0,
  which is **47 drawn frames**. On the 48th update the value goes positive and the slot ends: the real picture is
  restored, the hyper-not flag is cleared and the pieces are erased (samexap7.src:164, 171-180). The effect is
  **converging** pieces: they start 46x spread out and collapse into the sprite.
  - At S = 0 every piece lands on the same spot, giving one 2x2 blob for 1 frame (INFERRED from the formula).
- Tracking: every frame TOPLFT is recomputed from the object's current OX16/OY16, so the converging cloud
  follows a moving object (samexap7.src:165-187).
- "Phoney centre" (samexap7.src:188-197). INFERRED reading of the multiply chain:
  `k = (2 * floor(col*218/256)) mod 256`, `CENTER.col = TOPLFT.col + floor(W*k/256)`,
  `CENTER.row = TOPLFT.row + floor(H/2)`. The convergence point is a pseudo-random column inside the sprite
  that changes with screen position, at vertical mid-height. A simple equivalent is a random column inside the
  sprite, fixed per frame by screen X.
- Kill zone: if the object's relative X high byte + $0C has bits $C0 set, i.e. the object is outside
  [-96 px, +416 px) of the screen left, the appear ends at once and the object becomes normal
  (samexap7.src:166-171; px conversion INFERRED).

### 1.5 Player-ship death (defa7.src:1328-1384) and PLEX (blk71.src:566-672)
Pre-explosion (VERIFIED, defa7.src):
1. Scrolling freezes (BGLX=BGL), the ship sprite is erased and the death sound starts (defa7.src:1330-1337).
2. The ship is redrawn as a monochrome silhouette in colour index $B. Each step does: erase (blank) 2 frames,
   draw 2 frames, set colour (defa7.src:1349-1369). The colour sequence is PXCTB: red, red, red, $0F (orange-red),
   $3F (yellow), $7F (pale), $FF (white), $FF, then end (defa7.src:1434). That is 8 colours times 4 frames,
   about 34 frames (INFERRED).
3. STATUS = $7F, which kills all running explosions (see 1.3). The **background (colour 0) flashes white for 2
   frames**, then black (defa7.src:1370-1377). All enemy processes are killed (GNCIDE).
4. PLEX starts at the ship screen position + (4 columns, 3 rows) = +(8 px, 3 px) (defa7.src:1379-1382).

PLEX (VERIFIED unless noted):
- **128 pieces** (`PNBITS` $80, blk71.src:26). Each piece record holds 8.8 X (columns), 8.8 Y (rows), VX, VY
  (blk71.src:21-25).
- All pieces start at the centre with fraction 0 (blk71.src:576-580).
- Velocities come from two 16-bit shift-register RNGs seeded **$0808** and **$1732** on every death, so the
  pattern is the same every time (blk71.src:569-572). RNG step: the new top bit is (bit1 XOR bit2) of the low
  byte after `L ^ (L>>1)`, rotated into a 16-bit right shift (blk71.src:581-586). Any decent RNG works.
  - VX = signed 8.8 in **[-1.0, +1.0) columns/frame**, i.e. ±2 px/frame (high byte is (rand & 1) - 1,
    blk71.src:587-590).
  - VY = signed 8.8 in **[-2.0, +2.0) rows/frame** (high byte is (rand & 3) - 2, blk71.src:603-606).
  - Reject and redraw both if |VX| + |VY|/2 >= **$016A** (1.414) (blk71.src:609-615). In pixel units this is
    |vx_px| + |vy_px| < 2.83: a **diamond** (L1) velocity distribution, uniform inside, with no corners
    (INFERRED geometry). |·| uses ones complement, a negligible bias.
- Per frame (sleep 1 frame per step, blk71.src:626-628):
  1. Erase every piece's previous 2x2 area.
  2. pos += vel (no gravity, no drag).
  3. If the new row < **$2A** (42), or row wraps past 255, or new column > **$98**, the piece is **not moved and
     not drawn**. It is effectively dead: frozen off-screen and never redrawn (blk71.src:637-646).
  4. Draw the piece as a **2x2 px square in colour index $B**. If the X fraction is < 0.5 it is aligned to the byte
     (blk71.src:649-653). Otherwise it is shifted right by 1 px across two bytes (blk71.src:654-660). So X
     resolution is 1 px and Y resolution is 1 row.
- Coordinates are screen-relative. The background has stopped scrolling, so the debris does not drift with the
  world.
- Colour/fade: palette entry $B is driven from PXCOL (blk71.src:672). The first colour **$FF (white) is held for
  56 frames** (blk71.src:624). Each later colour is held **4 frames** (blk71.src:667): $7F, $3F (yellow), $37, $2F,
  $27, $1F, $17 (yellow, then orange, then red), 7 (red), 6, 5, 4, 3, 2 (red getting darker). Colour 0 ends PLEX
  (blk71.src:632-634).
  **Total = 56 + 13*4 = 108 frames, about 1.8 s** (INFERRED sum).
  Byte colour format is BBGGGRRR (Williams standard; e.g. $07 red, $3F yellow, $FF white). PLEX itself only
  confirms the order.
- After PLEX: score refresh, wave check, then next player or "PLAYER n / GAME OVER" (defa7.src:1383-1414).

### 1.6 Compared with our code
- `Enemies.SpawnExplosion` emits N radial particles with a single colour and a 30-40 frame life. The original
  instead **spreads the sprite's own 2x2 tiles** about the hit point with size 1.0 + 0.664/frame for 72 frames,
  keeping the tile colours, clipped at y < 42.
- `AppearFrames = 32` should be **47 drawn frames + 1** (sizes 46 to 0), converging rather than fading.
- The player explosion (`GameSession.cs:247`) uses 128 white radial particles at speed 3. The original uses
  128 pieces with uniform diamond velocity (max L1 2.83 px/frame), 2x2 px, and the white(56f) to yellow to red to
  dark fade, 108 frames, preceded by about 34 frames of monochrome ship flashing and a 2-frame white screen flash.

---------------------------------------------------------------------------------------------------

## 2. HUD (top of screen), VERIFIED unless noted

Constants: phr6.src:151-159. Drawing code: defa7.src:546-596 (SCRTRN), 833-858 (LDISP), 862-874 (TDISP),
877-902 (SBDISP), 906-929 (BORDER). Scanner: amode1.src:1182-1270 (SCNR).

### 2.1 Score digits
- P1 score top-left `P1DISP` = $0F1C, i.e. **x=30, y=28**. P2 `P2DISP` = $711C, i.e. **x=226, y=28** (phr6.src:153-154).
- 6 BCD digits, left to right, **advance 4 columns = 8 px** (`DIGLTH`, phr6.src:155; defa7.src:592).
  The digit field covers x 30..77 (P1) and 226..273 (P2).
- Leading zeros are blanked, but the **last two digits are always drawn**, so a zero score shows "00"
  (defa7.src:571-579).
- Glyph cell: **3 byte columns (6 px) by 8 rows** (descriptor $0308, mess0.src:448-457). Glyphs are drawn in
  **colour index 1** (all font nibbles are 1). Text routine spacing: advance = glyph width + 1 column, so 8 px per
  normal glyph. Narrow glyphs (I, space, punctuation) have widths of 1-2 columns. Line spacing is 10 rows
  (mess0.src:745-749, 812-815).
- Note (VERIFIED): colour index 1 is the "laser" colour. In game, process COLR (started defa7.src:1284) cycles
  palette entry 1 through COLTAB (defa7.src:3037-3042, 35 entries) every 2 frames (defa7.src:3024-3033). So any
  colour-1 graphics, including the score font, cycle with the laser. Please check this against a MAME capture
  before copying it, because it may surprise players.
- When 2 players are active, both scores are drawn. The P2 HUD only appears if PLRCNT = 2 (defa7.src:837-844,
  883-890, 871-873).

### 2.2 Reserve ships ("lasers")
- P1 at `P1LAT` $0F14, i.e. **x=30, y=20**. P2 at `P2LAT` $7114, i.e. **x=226, y=20** (phr6.src:156-157).
- Icon `PLAMIN` is **5 columns by 4 rows = 10x4 px** (defb6.src:1966).
- Spacing **+6 columns = 12 px**. **Max 5 shown** (defa7.src:845-856). The area cleared first is 32 columns by 6 rows
  (64x6 px) (defa7.src:848-849).

### 2.3 Smart bombs
- P1 at `P1SBD` $291B, i.e. **x=82, y=27**. P2 at `P2SBD` $8B1B, i.e. **x=278, y=27** (phr6.src:151-152).
- Icon `SBPIC` is **3 columns by 3 rows = 6x3 px** (defb6.src:1969), using colours 9 and $C.
- Stacked **vertically**, **+4 rows** each, **max 3** (rows 27, 31, 35) (defa7.src:892-902). The area cleared first
  is 3 columns by 11 rows (defa7.src:895-896).

### 2.4 Scanner frame (BORDER, defa7.src:906-929). SCANH = YMIN-34 = 8 (phr6.src:158, 21)
All border lines use **colour index 5** (byte $55, both pixels), except the window marker in colour 9.
- **Line across the whole screen under the scanner: rows 40-41 (2 px thick)**, columns 0..$9B, i.e. full width
  (defa7.src:906-911).
- Left side: column $2F, i.e. **x=94-95**, rows 8-39. Right side: column $70, i.e. **x=224-225**, rows 8-39
  (defa7.src:912-916).
- Top: **row 7** (1 px), columns $2F..$70, i.e. x=94..225 (defa7.src:917-921).
- "Scanner screen marker": **colour 9 (white)**, 2 rows thick, columns $4C..$53, i.e. **x=152..167**, at rows 7-8
  and at rows 40-41 (over the bottom line) (defa7.src:922-928).
- Colour 5 is the wave colour, set at each player start from WCTAB indexed by (wave & 7) (defa7.src:1262-1266,
  1436). Order: blue $81, green $28, red $07, $16, yellow $2F, $84, $15. Wave 8 reads the byte just before WCTAB
  (the 0 terminating PXCTB), which gives a black border (INFERRED from table adjacency; worth checking against
  MAME). Default CRTAB value: entry 5 = $81 blue (defb6.src:1881).
- The scanner interior (64 columns by 32 rows at column $30, row 8, i.e. **x=96..223, y=8..39**) is cleared by
  TDISP (defa7.src:862-866).

### 2.5 Scanner contents (bonus detail, amode1.src:1182-1270)
- Scanner left edge in world units = BGL - $6D40 (amode1.src:1202-1204). Blip column = $30 + ((OX16 - left)
  >> 10) (64 columns cover the whole 65536-unit world). Row = 7 + (pixelY >> 3). The 2-byte OBJCOL is written as
  2 px by 2 rows: the upper byte is the top row and the lower byte the second row (amode1.src:1262-1272).
- Player blip: column $4B + (screenCol >> 4), row 7 + (screenY >> 3). It is a white **3x3 plus sign**
  (amode1.src:1243-1258).
- Bezel ticks: single white pixels, 2 rows tall, at x=152 (left pixel of column $4C) and x=167 (right pixel of
  column $53), rows 9-10 and 38-39 (amode1.src:1225-1232).
- Mini-terrain: 64 columns of 2 bytes each from a 3-bytes-per-entry table (amode1.src:1205-1224). Data not
  reproduced here.

### 2.6 Compared with our DrawHud
- Ours draws 1-px lines. The original sides are 2 px wide at x=94-95 / 224-225 and the bottom line is 2 rows
  (40-41) across the full width.
- Ours puts ship icons at y=10, x0=18. The original has **x=30, y=20**, step 12, max 5.
- Ours puts bombs at x=84, y=16+5i. The original has **x=82, y=27+4i**, max 3.
- Ours right-aligns the score at y=28. The original uses a **fixed 6-digit field at x=30** (P2 x=226), y=28,
  leading zeros blanked, minimum "00".
- The original HUD has no "HIGH" or "WAVE" text.
- The white window ticks (rows 7-8 and 40-41, x=152..167) are fixed in the original, not tied to the player.

---------------------------------------------------------------------------------------------------

## 3. ATTRACT MODE (amode1.src, mess0.src)

### 3.1 Overall cycle (VERIFIED flow)
```
power-on / game over
   └─ HALLOF (initial entry if qualified) ──> HALDIS (Hall of Fame, ~10 s)
                                                  └─> LEDRET (instructions/scoring demo, ~38 s)
                                                          └─> AMODES (logo / "presents" page, ~16 s)
                                                                  └─> HALDIS ...
```
- HALLOF goes straight to AMODES on the very first power-on (PWRFLG = 0) (amode1.src:126-127). After the
  initials step it goes to HALDIS (amode1.src:258).
- HALDIS ends in LEDRET (amode1.src:419-421). LEDRET ends in AMODES (amode1.src:672-673). The AMODES copyright
  page ends in HALDIS (amode1.src:885-893).
- **A coin (credit increase)** on the logo page or the Hall of Fame jumps straight to the instructions demo
  (checked every 10 frames: amode1.src:891-892, 414-415). A **high-score reset** redraws the Hall of Fame
  (amode1.src:416-417).
- **Start buttons** (defa7.src:1100-1175): they work any time STATUS bit 7 is set (attract or game over) and
  credits >= 1 (P1) or >= 2 (P2). They also need PWRFLG, which is set when the logo page first reaches its
  copyright line (amode1.src:880-881, defa7.src:1126-1127). Start kills every attract process, clears the screen
  and begins the game at once. A 2-player game prompts "PLAYER ONE" at $3C80 (x=120, y=128) for 128 frames
  (defa7.src:1289-1303).
- The **credits line** shows on every attract page when credits > 0. "CREDITS:" is at $28E5 (x=80, y=229) and the
  number at $48E5 (x=144, y=229). It refreshes every 16 frames (amode1.src:956-968).
- **No "press start" prompt** is drawn by the attract code. The "PRESS ONE/ONE OR TWO PLAYER START" messages in
  mess0 (mess0.src:280-281) are not referenced anywhere else in this source (VERIFIED by grep).

### 3.2 Logo / "presents" page (AMODES, amode1.src:715-893)
1. Clear the screen. Start colour processes COLR and TIECOL. Palette $C = yellow $3F (amode1.src:715-728).
2. **Company logo drawn as an animated pen stroke** (LOGO, amode1.src:734-790). A cursor walks a stroke table
   starting at pixel (116, 64) and plots pixels in palette **$F** (cycled by TIECOL every 6 frames: yellow / red,
   defb6.src:1195-1209). Speed is **3 stroke bytes per 2 frames** on the first pass (each byte is up to 2 pixel
   steps). After the first full pass it switches to 10 bytes per 2 frames, starts the next stage (PRES), and
   keeps redrawing the logo in a loop. The first pass takes about 225 frames (INFERRED: about 338 table bytes / 3 * 2).
   **Copyrighted trademark artwork. Do not replicate.** Use your own logo or wordmark drawn with the same
   "pen trace" behaviour.
3. **Two-line credit text** at $3258 (x=100, y=88). Line 1 is the company name. Line 2 ("PRESENTS") is 2 line
   feeds lower (y=108), indented 12 columns (x=124). It is rewritten every 5 frames (amode1.src:795-801;
   mess0.src:278-279). Replace the company name with your own.
4. **Title materialises** (DEFEND, amode1.src:805-837). After **48 frames**, the title bitmap is cut into 15
   vertical strips. Each strip is 4 columns (8 px) wide, placed at world X $0C00 + i*$100 (x = 96 + 8i), Y=152.
   Each strip runs an APPEAR (sizes 46 to 0, 47 frames), all at once. After another **46 frames** the whole title
   (60 columns by 24 rows = **120x24 px**) is stamped every frame at $3090 (**x=96, y=144**) until the appear slots
   are idle (amode1.src:839-845, 896-905). The title bitmap is run-length data (DEFNNN, amode1.src:909-950) in 3
   colours: shadow colour 2, letters colour $C, background. **The DEFENDER wordmark is trademark artwork. Use
   your own title graphic.** The "title assembles from converging fragments" behaviour can be kept.
5. **40 frames later** the letter colour $C starts cycling (CBOMB: white 3 frames, then a random COLTAB colour,
   every 9 frames; defb6.src:1213-1227) (amode1.src:846-847).
6. **Copyright line** drawn as a tiny 1-px-wide font bitmap at $3BD0 (**x=118, y=208**), 80x8 px
   (amode1.src:851-866). Replace with your own notice. Do not reproduce the Williams notice. amode1.src:868-879
   is an anti-tamper check on the logo colour. **Do not port it.**
7. Hold for **60 × 10 = 600 frames (10 s)**, or until a coin is inserted (amode1.src:882-892).
   Whole page is about 225 + 48 + 46 + 40 + 600 = **about 960 frames (16 s)** (INFERRED).

### 3.3 Hall of Fame display (HALDIS, amode1.src:378-475)
- Screen cleared. Player scores redrawn in the HUD positions (SCORES, amode1.src:1003-1008). Text colour 1
  starts black, then COLR cycles it (amode1.src:384, 412).
- Title graphic at $3038 (**x=96, y=56**), in yellow (palette $C = $3F) (amode1.src:405-411).
- Headings (mess0.src:301-310 layout instructions, placed at $3854):
  "HALL OF FAME" at **x=112, y=84**. "TODAYS" at column $22 (**x=68, y=104**). "ALL TIME" at column $60
  (**x=192, y=104**). "GREATEST" on the next line (y=114) at x=60 and x=190.
- Underlines in colour 1, 2 rows at **y=123-124**: left x=60..123, right x=190..251 (amode1.src:390-397).
- Two tables of **8 rows**, row pitch **10 px**, starting **y=134**. Today's starts at **x=48**, all-time at
  **x=178** (amode1.src:403-410, 423-457). Each row has: rank digit (1-8), initials 5 columns (10 px) right of the
  rank, then a 6-character score 19 columns (38 px) right of the rank, with leading zeros as spaces (mess0.src:311-315;
  amode1.src:429-441).
- Duration: **600 frames (10 s)**, checked every 10 frames. A coin goes to instructions (amode1.src:413-421).

### 3.4 Instructions / scoring demonstration (LEDRET, amode1.src:477-673)
Setup (VERIFIED): world at BGL=0 with no scrolling, terrain and scanner on, border drawn, HUD scores, credits.
Colour processes COLR, CBOMB, TIECOL and the scanner process run. The text process redraws all revealed labels
every 6 frames (amode1.src:477-493, 703-713). The scanner window is centred via PLAXC=$1030 (amode1.src:528-529).
Initial objects:
- Humanoid at **(x=240, y=219)**, blip colour 6 (amode1.src:494-497; XMAN/YMAN amode1.src:93-94).
- Ship (facing right) at **(x=64, y=80)**, no scanner blip (amode1.src:498-501; XSHIP/YSHIP amode1.src:95-96).
- Lander at **(x≈237, y=64)**, falling at **0.625 px/frame** ($A0), materialising with APPEAR (amode1.src:502-513).

Timeline (frame counts are the NAPP values):
| t (frames) | event | ref |
|---|---|---|
| 0 | lander appears and descends | amode1.src:502-513 |
| +230 | lander reaches the humanoid. Both rise at 0.6875 px/frame ($FF50) | amode1.src:513-519 |
| +160 | ship fires: the laser starts 14 px right / 4 px below the ship's top-left | amode1.src:519-521, 676-680 |
| +21 | laser removed. **Lander explodes** (normal EXST). Ship flies to the humanoid at (+2 px, +0.83 px)/frame. Humanoid free-falls (vy += 8/256 every 2 frames) | amode1.src:540-562 |
| +90 (45 x 2) | catch: a "500" score sprite appears at (x≈255, y=144). Ship and humanoid descend together at 0.75 px/frame | amode1.src:563-587 |
| +80 | humanoid set down and stops. "500" moves to (224, 224). Ship turns left and flies up-left at (-2, -1.5) px/frame | amode1.src:588-603 |
| +96 | ship faces right and stops. "500" removed | amode1.src:604-613 |
| then 6 times | **enemy roster**, see below | amode1.src:619-668 |
| +255+255 | end, back to logo page | amode1.src:670-673 |

Enemy roster loop. Order: **Lander, Mutant, Baiter, Bomber, Pod, Swarmer** (amode1.src:1157-1162):
1. The enemy materialises (APPEAR) at **(x=248, y=160)** and rises at **0.75 px/frame** ($FF40) (amode1.src:621-635).
2. After **95 frames**, the ship fires its laser. After another **23 frames**, the laser is removed and the enemy
   **explodes** where it is (amode1.src:636-646).
3. The same enemy then **re-materialises (APPEAR) at its fixed display slot** and stays still (amode1.src:647-656).
   Slot positions (px, from world X/32 and Y):
   - Top row: Lander (72, 96), Mutant (136, 96), Baiter (204, 98).
   - Bottom row: Bomber (75, 152), Pod (139, 152), Swarmer (207, 154).
   (amode1.src:1151-1170, INFERRED conversion)
4. **32 frames** later its name label is revealed. **32 frames** after that the next enemy starts
   (amode1.src:657-665).
- Labels (TEXTAB positions, amode1.src:1142-1157; message layout mess0.src:258-273). Each label is the name,
  then a line feed (10 px) and a small indent, then the points value:
  - "SCANNER" at **(134, 48)** under the scanner. Shown from the start, because TEXPTR starts at the first entry.
  - Lander at (56, 112) with 150. Mutant at (120, 112) with 150. Baiter at (190, 112) with 200.
  - Bomber at (56, 168) with 250. Pod at (128, 168) with 1000. Swarmer at (184, 168) with 150.
  - The points line is indented 6 columns (12 px). Pod uses 0 columns, swarmer 8 (mess0.src:258-273).
- Total demo length is about 230+160+21+90+80+96 + 6*(95+23+32+32) + 510 = **about 2279 frames (38 s)**
  (INFERRED sum, excluding the initial 47-frame appear, which overlaps).
- Demo laser (LASRS, amode1.src:675-701): the beam extends **4 columns (8 px) per frame** in colour 1, with a white
  tip byte. A "fizzle" pattern trails 3 columns per frame behind it. A tail eraser clears 1 column per frame from
  the start point.

### 3.5 High-score initial entry (HALLOF, amode1.src:117-258), for completeness
- Qualifies if the score beats today's 8th place (amode1.src:134-136). Screen layout: "PLAYER ONE/TWO" at
  (124, 56). A 4-line instruction block at (40, 88), lines 10 px apart with blank lines between groups
  (mess0.src:288-294). Initials at x=140/156/172, y=172. Underlines 8 px wide at y=183-184 under each initial
  (amode1.src:166-173, 262-288).
- First initial starts as 'A' and the others blank. Stick up/down cycles space, A..Z with wrap-around
  (amode1.src:343-353). Hold-to-repeat: first step after 3 frames, then delays 32, 21, 15, 12, 11, 10, 10...
  (delay = delay/2 + 5 starting from 55) (amode1.src:355-371).
- The active underline blinks: colour D alternates with black every 15 frames (amode1.src:315-322).
- Fire needs the button released for at least 5 frames before a press counts (amode1.src:189-206). Timeout is
  40 s for the first initial and 20 s for each later one (amode1.src:179-181, 207-208, 310-313).
- The score goes into today's table and, if good enough, the all-time table (top 8 each). If nobody qualified,
  there is a 255-frame wait (amode1.src:252-257).

### 3.6 Copyright and IP notes
- **Do not reproduce:** the Williams logo stroke data (LGOTAB), the DEFENDER wordmark bitmap (DEFDAT), the
  copyright bitmap (CPRTAB), the font bitmaps (mess0 NUMBR*/LETTR*), sprite data, or the strings naming
  "WILLIAMS"/"ELECTRONICS INC." Also leave out the anti-tamper logo-colour check.
- **Can reproduce as behaviour:**
  - the page order and timings
  - the pen-trace logo animation (with your own artwork)
  - the title built from 15 converging strips
  - the scripted rescue demo and the enemy-roster demo with labels and point values (game facts)
  - the Hall of Fame layout and the initials-entry rules
  - coin-skips-to-demo behaviour and the credits line
  - a cycling text colour
- Our `AttractDirector` (Title 600, then autopilot Demo up to 1500, then Hall 600) differs in order (original:
  HoF, then scripted demo of about 2279 frames, then logo page of about 960 frames) and in content: the original
  demo is **scripted**, not an autopilot game.
