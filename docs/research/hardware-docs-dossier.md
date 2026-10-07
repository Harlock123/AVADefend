# DEFENDER (Williams, 1980/81) — Hardware, Presentation & Secondary-Source Dossier

Prepared for: AVADefend (faithful recreation, reference = MAME set `defender`, "Red label")
Date accessed for all sources: 2026-10-07
Method: everything below was fetched and inspected in this session (curl of raw source/API JSON, archive.org OCR text, WebFetch/WebSearch). Items marked **UNVERIFIED** come only from search-engine snippets or a single secondary source and were not confirmed in a primary source. Nothing here is from memory alone unless explicitly marked **(memory, unverified)**.

Confidence key: **H** = read directly in primary source/code; **M** = reliable secondary source or derived by calculation from primary; **L** = snippet / single weak source.

---

## 0. Executive summary (the numbers you will actually use)

| Item | Value | Source | Conf |
|---|---|---|---|
| Main CPU | MC6809E @ 12 MHz/3/4 = **1.000 MHz** | MAME williams.cpp L1531, L1537 | H |
| Sound CPU | **M6808** @ 3.579545 MHz XTAL, internal ÷4 → **894.886 kHz** | williams.cpp L1532, L1540 | H |
| Sound output | single 8-bit DAC **MC1408** (ic6), fed from PIA port A | williams.cpp L1563, L1581 | H |
| Pixel clock | 12 MHz × 2/3 = **8 MHz** (MAME model) | williams.cpp L1556 | H |
| Raster total | **512 × 260** (H × V) | williams.cpp L1556 | H |
| Refresh | 8e6 / (512·260) = **60.096 Hz**; line rate 15.625 kHz | derived from L1556 | M (calc) |
| Visible area (Defender) | x **12..303**, y **7..246** → **292 × 240** | williams.cpp L1601 | H |
| Operator manual figure | "360 x 240 pixels", "non-interlaced … every 16.66 mSec … 60 Hz", 16 colours of 256 | Theory of Operation 16P-3001-301 (Oct 1981) | H |
| Frame buffer | 4 bpp, 16 palette entries, each an 8-bit **BBGGGRRR** byte (c000–c00f) | williams.cpp L337; williams_v.cpp | H |
| Palette weights | R,G bits (LSB→MSB): 1200 Ω, 560 Ω, 330 Ω; B bits: 560 Ω, 330 Ω | williams_v.cpp L340-363 | H |
| R/G levels (0..7) | **0, 38, 81, 118, 137, 174, 217, 255** | computed with MAME's resnet.cpp algorithm | M (calc) |
| B levels (0..3) | **0, 95, 160, 255** | same | M (calc) |
| Controls | 2-way stick (Up/Down), Thrust, Reverse, Fire, Smart Bomb, Hyperspace, 1P/2P start | MAME input port L762-786; setup booklet | H |
| Factory: ships/bonus | 3 ships; bonus ship + smart bomb every **10,000**; 3 smart bombs | setup booklet; source DEFALT table | H |
| High-score tables | "TODAYS GREATEST" (8 entries, RAM) + "ALL TIME GREATEST" (8 entries, CMOS); 3 initials | booklet; source phr6.src/amode1.src | H |
| Speech | **None.** Sound ROM contains a generic hook for an optional "talking" ROM at $EFFD but Defender ships with no speech ROM (MAME loads only `video_sound_rom_1.ic12` at $F800). | vsndrm1.src L35-38, L933-938; MAME ROM_START | H |

---

## 1. MAME driver (mamedev/mame)

**Repository state inspected:** `mamedev/mame` master, commit **36818916698d85746ae488278d7dca48783f4dfc** (commit date 2026-10-07). NOTE: the driver now lives in `src/mame/williams/` (not `src/mame/midway/` — that path 404s). Files read: `src/mame/williams/williams.cpp` (4052 lines), `williams_v.cpp` (623), `williams_m.cpp` (540), `williams.h` (377), `src/emu/video/resnet.cpp`, `resnet.h`. Licence of MAME driver files: BSD-3-Clause (header `// license:BSD-3-Clause`, copyright-holders: Aaron Giles). Using the *numbers/facts* is fine; do not copy code verbatim without honouring BSD-3.

### 1.1 Clocks / CPUs (williams.cpp)
- L1531 `MASTER_CLOCK = XTAL(12'000'000)`; L1532 `SOUND_CLOCK = XTAL(3'579'545)`.
- L1537 `MC6809E(config, m_maincpu, MASTER_CLOCK/3/4)` → 1 MHz.
- L1540 `M6808(config, m_soundcpu, SOUND_CLOCK); // internal clock divider of 4, effective frequency is 894.886kHz`.
- L1543 NVRAM "5101 (Defender)" + battery (1K×4 CMOS; Defender map c400-c4ff, `cmos_4bit_w`).
- L1546/1549 timers every 32 scanlines (VA11 IRQ) and at scanline 240 (count240).
- Theory manual confirms: 12 MHz oscillator divided to 4 MHz and 6 MHz; E and Q 1 MHz clocks for the MPU.
- Wikipedia/KLOV describe the sound CPU as "6800"; MAME uses M6808 (6800-family with internal RAM). There's also a bootleg config `defender_6802snd` (L1611-1618, M6802) used by `defenderom` and `galwars2` only — NOT the reference.

### 1.2 Screen (williams.cpp)
- L1556 `m_screen->set_raw(MASTER_CLOCK*2/3, 512, 6, 298, 260, 7, 247);` (base Williams)
- L1601 Defender override: `m_screen->set_visarea(12, 304-1, 7, 247-1);` → **292 × 240 visible**.
- Derived: H freq = 8 MHz/512 = 15.625 kHz; V = 15625/260 = **60.096 Hz**. Manual says "60 Hz", "16.66 mSec", non-interlaced.
- Orientation: `ROT0` (horizontal monitor) for all Defender sets (L3968-3971).
- Pixel aspect: 292×240 displayed on a 4:3 tube ⇒ pixels ≈ 1.096:1 (wider than tall) — derived, M. The manual's "360×240" is the full addressable/nominal width; Wikipedia's "320×256" figure (citing Retro Gamer Oct 2008) conflicts with both — treat Wikipedia's number as **unreliable**.
- Frame buffer layout (williams_v.cpp header L9-12): 4 bpp, "inverted X/Y order": byte at offset 0 = pixels (0,0),(1,0); offset 256 = pixels (2,0),(3,0)… i.e. column-major, 2 pixels/byte, high nibble = left pixel (screen_update L200-222).
- Defender and Stargate have **no blitter** (header L44; williams_v.cpp L14-15): CPU writes framebuffer directly.
- Cocktail: `defender_state::video_control_w` (williams_m.cpp L335-338) sets `m_cocktail = BIT(data,0)`; driver comment (L29-33) says "only red can run in a cocktail table". There is **no separate cocktail ROM set** in MAME; cocktail is a hardware/PROM variant (decoder.2/.3) of red label.

### 1.3 Palette (williams_v.cpp L340-363)
```
resistances_rg[3] = { 1200, 560, 330 };  resistances_b[2] = { 560, 330 };
compute_resistor_weights(0, 255, -1.0, 3,rg,..., 3,rg,..., 2,b,...)
r = bits 0,1,2 ; g = bits 3,4,5 ; b = bits 6,7     (BBGGGRRR)
```
I reproduced MAME's `compute_resistor_weights` (resnet.cpp L55-196; `combine_weights` rounds +0.5, resnet.h L181-184) in Python:
- weights R/G = 37.615, 80.603, 136.782; B = 94.551, 160.449
- **R/G level table [0..7] = 0, 38, 81, 118, 137, 174, 217, 255**
- **B level table [0..3] = 0, 95, 160, 255**
MAME comment: real hardware has pull-ups/transistors; MAME uses "relative resistor weights" only (so this is MAME's approximation, which is fine since MAME is the reference).

### 1.4 Memory map / I/O (williams.cpp L331-376, L494-507)
- 0000-BFFF RAM (video RAM 0000-97FF); C000-CFFF banked ROM/I-O view; D000-FFFF ROM.
- c000-c00f colour registers (16 × BBGGGRRR), c3fc watchdog, c400-c4ff CMOS, c800 video counter, cc00 PIA1 (coin/service, sound cmd), cc04 PIA2 (player).
- Sound command: 6 bits from PIA1 port B to sound board (L360-365).

### 1.5 Input ports (L762-786) — exact names
IN0: bit0 **Fire** (BUTTON1), bit1 **Thrust** (BUTTON2), bit2 **Smart Bomb** (BUTTON3), bit3 **Hyperspace** (BUTTON4), bit4 Start2, bit5 Start1, bit6 **Reverse** (BUTTON5), bit7 Joystick Down (2-way). IN1: bit0 Joystick Up. IN2: Auto Up/Manual Down (toggle), Advance, coin 3/1/2, High Score Reset, Tilt.

### 1.6 ROM sets & revisions (L1985-2076, L3968-3985)
| Set | MAME title | Main ROMs | PROMs | Notes |
|---|---|---|---|---|
| `defender` (parent) | Defender (Red label) | defend.1,.4 (2K), .2,.3 (4K), banked .9,.12,.8,.11,.7,.10,.6 | decoder.2 + decoder.3 | final version; cocktail-capable |
| `defenderg` | Defender (Green label) | defeng01..12 | decoder.1 | |
| `defenderb` | Defender (Blue label) | wb01, defeng02, wb03 + same banked as green | decoder.1 | |
| `defenderw` | Defender (White label) | rom1..rom12 | decoder.1 | first ROM set; rom10 "hand-repaired with startrkd rom" |
| `defenderj` | T.T Defender (Taito license) | df1-1.e3 … | | |
All share sound ROM `video_sound_rom_1.ic12` CRC fefd5b48 at $F800 (2 KB). No "defenderc" cocktail set exists in MAME. Many bootlegs (defndjeu, tornado1/2, zero, zero2, defenderom, defcmnd, defence, nextcent, defenseb, startrkd, attackf, galwars2) — ignore.

**Revision differences per MAME comment (L29-33):** "white was first ROM set, then green/blue the only difference was the chips used, the final version was red, only red can run in a cocktail table. (red also has *much* improved enemy AI and is harder to play)". All GAME() entries say year 1980. Comment at L3968: "developers left Williams in 1981 and formed Vid Kidz".
- mwenge/defender README: order White → Blue → Green → Red (note: differs slightly from MAME comment ordering of green/blue; both agree white first, red last).
- **UNVERIFIED (search snippet only):** "early edition had landers worth 100 points instead of 150". Not confirmed in any primary source I read.
- Manual evidence of revision differences (see §2.3): early-series booklet 16P-3000-103 has adjustment 20 = BACKGROUND SOUND on/off and difficulty 0/1/2; later-series 16P-3001-103 R-T (July 1981) marks 20 "NOT USED", difficulty 0/5/10, ceiling 5-30 default 15. The red-label source's CMOS defaults match the later booklet (GA1=5, GA2=15, GA3 "UNUSED").

---

## 2. Operator manuals (archive.org)

Downloaded OCR text (`_djvu.txt`) of:
- `arcademanual_Defender-Later-Series-Setup-Booklet` — doc no. **16P-3001-103 R-T, July 1981**
- `arcademanual_Defender-Early-Series-Setup-Booklet` — **16P-3000-103** (no date in OCR)
- `arcademanual_DefenderSetupBookletUSA` (same as early text)
- `ArcadeGameManualDefender` — **Theory of Operation, later system boards, 16P-3001-301, October 1981**
- (seen, not read: `arcademanual_defender_2` owner's manual with schematics; `arcademanual_Defender-Theory-Early/Later`)
Copyright: Williams Electronics manuals; archive.org hosting, no open licence. Use facts only.

### 2.1 Player controls (later booklet, verbatim)
- "UP-DOWN Switch - maneuvers player ship."
- "REVERSE Switch - reverses player ship direction."
- "THRUST Switch - controls player ship speed."
- "FIRE Switch - activates laser gun."
- "HYPERSPACE Switch - warps rocket to another quadrant, danger of possible annihilation."
- "SMART BOMB Switch - destroys all alien ships on screen. A maximum of 3* per play." (* = adjustable)
- GAME PLAY: "Destroy alien ships and missiles. Rescue humanoids, pick them up, and return to surface. Destroy all enemy ships for humanoid bonus and additional alien waves. Bonus ships and Smart Bombs provided every 10,000* points."
- "HIGH SCORE SIGNATURE — Use UP/DOWN to select letters and FIRE button to lock in letter."
- Switch test lists the player panel as: UP, DOWN, REVERSE, 1-PLAYER START, 2-PLAYER START, HYPERSPACE, SMART BOMB, THRUST, FIRE.
- Physical panel layout: Wikipedia — joystick for left hand (Space-Invaders-like), buttons for right hand (Asteroids-like); exact button positions on the panel were **not** confirmed from a panel photo/diagram in this session (GAP). (memory, unverified: left-hand stick with Reverse button beside it; Thrust/Fire to the right; Smart Bomb & Hyperspace — verify against a control-panel overlay image before finalising.)

### 2.2 Game adjustments — later series (factory value)
| Fn | Factory | Description |
|---|---|---|
| 8 | 10,000 | BONUS SHIP LEVEL (0 = no bonus ships) |
| 9 | 3 | SHIPS PER GAME |
| 10 | 3 | COINAGE SELECT |
| 11/12/13 | 1/4/1 | LEFT/CENTER/RIGHT COIN MULT |
| 14/15/16 | 1/0/0 | COINS FOR CREDIT / COINS FOR BONUS / MINIMUM COINS |
| 17 | 0 | FREE PLAY (1 = free play) |
| 18 | 5 | STARTING DIFFICULTY: 0=LIB; 5=MOD; 10=CONS |
| 19 | 15 | PROGRESSIVE WAVE DIFFICULTY LIMIT, 5-30 (05=LIB; 15=MOD; 25=CONS) |
| 20 | 1 | NOT USED |
| 21 | 5 | PLANET RESTORE WAVE NUMBER |
| 22-27 | 0 | NOT USED |
| 28 | 0 | SPECIAL FUNCTION (15 = reset all-time HS / auto-cycle; 35 = zero audits; 45 = restore factory settings) |
Audits 1-7: coins L/C/R, total paid, ships won, play time (minutes), total ships played.
Credits: 20 or fewer retained through power-down.

### 2.3 Early series differences (16P-3000-103)
- Fn 18 STARTING DIFFICULTY factory **0**, scale "0=LIB; 1=MOD; 2=CONS".
- Fn 19 factory **10**, "5=LIB; 10=MOD; 15=CONS", range 4-25.
- Fn 20 **BACKGROUND SOUND 0=OFF 1=ON** (factory 1).
- High-score reset text: "To reset the high score to the factory setting and erase signatures, depress HIGH SCORE RESET in game over mode." (no TODAY/ALL-TIME distinction mentioned in early booklet).

### 2.4 High score tables
- Later booklet: "To reset the 'TODAYS GREATEST' signatures to factory settings, turn game OFF and ON or depress HIGH SCORE RESET in game over mode. To also reset 'ALL TIME GREATEST' signatures to factory settings, set Function 28 to 15 and depress ADVANCE in AUTO-UP."
- Source (red label, mwenge/defender `phr6.src` L173-180, L572; `amode1.src` L137, L218): ALL TIME table = 8 × 12-nibble entries in CMOS (`CRHSTD`); TODAYS table = `THSTAB RMB 12*8` in RAM → **8 entries each**, 3 initials + 6-digit BCD score. Attract text strings include "HALL OF FAME", "TODAYS", "GREATEST" (`mess0.src`).
- Factory all-time defaults (`romc8.src` L782-797, red label source): DRJ 21270, SAM 18315, LED 15920, PGD 14285, CRB 12520, MRS 11035, SSR 8265, TMH 6010. (Factual; recommend using your own placeholder initials anyway.)
- Hall-of-fame entry: letters in light blue (`LDB #$85`), Toccata tune for all-time top score (`#$3D`), "Phantom" tune for today's top (`#$3E`) (amode1.src L148-154).

### 2.5 Diagnostics (later booklet) — relevant to presentation
- Sound test: "Test sequences sounds 1 through 31, skipping 19, 27, and 28."
- Monitor test patterns: cross hatch, red, green, blue, colour bars (8 bars: red, green, blue, white, black, yellow, cyan, magenta).
- Colour RAM test sequences: light/normal/dark red, green, blue.

---

## 3. Gameplay rules & scoring

### 3.1 Scoring — confirmed in red-label source (mwenge/defender, `defb6.src`)
SCORE routine (`defa7.src` L474-477): `A` = exponent, `B` = BCD 0-99; odd exponent shifts one nibble. Decoded:
| Event | Source value | Points |
|---|---|---|
| Lander killed | `KILP $0115,LHSND` (L922) | **150** |
| Mutant ("SCHITZO") | `KILP $0115,SCHSND` (L625) | **150** |
| Swarmer | `LDD #$0115` in MSWKIL (L190) | **150** |
| Baiter ("UFO") | `KILP $0120,UFHSND` (L82) | **200** |
| Bomber ("TIE") | `KILO $0125,TIHSND` (L1120) | **250** |
| Pod ("PROBE") | `KILO $0210,PRHSND` (L118) | **1000** |
| Humanoid popup "250" | P250 `$0125` with C25P1 sprite | 250 |
| Humanoid popup "500" | P500 `$0150` with C5P1 sprite | 500 |
Attract-mode text strings "150/200/250/1000" (mess0.src) match.
Secondary (search snippet; instruction card not seen as image): catching a falling humanoid 500, returning to ground 500, humanoid landing safely on its own 250. Consistent with the 250/500 popup processes in source. Conf M.

### 3.2 End-of-wave bonus — confirmed in source (`defa7.src` L1786-1840)
Screen: "ATTACK WAVE n" / "COMPLETED" / "BONUS X m", one humanoid icon (ASTP3) drawn per surviving humanoid. Bonus per humanoid = **100 × min(wave, 5)** (i.e. 100,200,300,400,500, then 500 thereafter).
(The Atari-2600 manual transcription says the same: "100 bonus points for each surviving humanoid, multiplied by the number of the wave … thereafter … 500".)

### 3.3 Humanoids / planet
- 10 humanoids at start (Atari manual transcription; Wikipedia). Lander carrying humanoid to the top → humanoid becomes Mutant. All humanoids lost → planet explodes, level fills with mutants. Planet restored every 5th wave (factory adjustment Fn 21 = 5, "PLANET RESTORE WAVE NUMBER"). Conf H for restore wave (manual), M for the rest.
- Planet explosion visual (source `defb6.src` L434-482 "TERRAIN BLOW"): random background colours from `COLTAB` flashed via palette entry 0 with "lightning bolt" sound, 16 iterations. Conf H (code read), exact timing not computed.
- Baiters appear if you take too long on a wave (Atari manual; Wikipedia). Exact timer not extracted (GAP — needs source dive in defb6 UFO process).
- All aliens except Landers wrap vertically (Atari-2600 manual text — may not apply to arcade; **verify**).

### 3.4 Smart bomb (source `defa7.src` L3175-3205)
Requires a bomb in stock and no bomb already active; decrements count, redraws icons, plays SBSND; kills every object with nonzero on-screen X (`OBJX`) and type < 2 (i.e. only on-screen enemies); then flashes the screen: `COM PCRAM` (palette entry 0, the background) toggled 8 times with `NAP 2` between (= **4 white/black flashes**, ~2 process ticks each); then waits for button release (debounce) before re-arming. Conf H.

### 3.5 Hyperspace
"warps rocket to another quadrant, danger of possible annihilation" (manual). Source HYPER routine at `defa7.src` L3207+ (not fully analysed; GAP: exact death probability).

---

## 4. Historical

| Fact | Source(s) | Conf |
|---|---|---|
| Team: Eugene Jarvis (lead), Larry DeMar, Sam Dicker, Paul Dussault | Wikipedia; therealsark02 README ("Eugene Jarvis, Sam Dicker, Larry Demar, Paul Dussault, et al.") | M |
| Sound ROM: "DEFENDER SOUNDS REV. 1.0 BY SAM D 10/80 … ORIGINATION DATE 10/24/80, RELEASE 10/31/80, PROGRAMMER: SAM DICKER", "COPYRIGHT WILLIAMS ELECTRONICS 1980" | vsndrm1.src L1-6 | H |
| Jarvis build note "DR J. 1/21/81" in info.src | mwenge/defender | H |
| AMOA show: Wikipedia: "unveiled … at the AMOA trade show in September 1980" (cit. Kent 2001). Search snippet (attributed to Jarvis, likely arcade-history.com — page 403'd for me): shown at "AMOA Chicago on October 31, 1980", released ~November 15, 1980. | conflicting | L/M — **unresolved**; the sound ROM release date 10/31/80 fits the October date |
| Release: Wikipedia: Feb 1981 (JP), Mar 1981 (NA), late 1981 (EU). MAME year 1980. | conflicting | M |
| Reverse button: Jarvis intended one-direction scrolling; Steve Ritchie convinced him to scroll both ways; "Changing the program to make it go backwards was a pain…" | Wikipedia (cites Kent 2001; Retro Gamer Oct 2008; JoyStik Sept 1982) | M |
| Operators "afraid of this game … all the buttons" at AMOA | Gamasutra/Game Developer "The History of Defender", Loguidice & Barton, 2009-07-14 | M |
| DeMar coded the attract mode before the show; attract "programmed in just five hours" | Wikipedia; KLOV | M |
| Terrain is a one-pixel line because hardware couldn't do more; stars move slower than ship for depth (parallax) | Wikipedia (Retro Gamer 2008) | M |
| Dicker's particle explosion algorithm | Wikipedia; source file `samexap7.src` ("SAM's explosions") | H/M |
| Japan export: reverse button embedded on top of joystick | Wikipedia | M |
| 55,000+ units, >$1B | Wikipedia | M |
| KLOV says "commercial failure initially at a 1981 Chicago trade show" — conflicts with 1980 dating | KLOV | L |
Gap: no first-hand Jarvis interview text on the scanner design was found; the Game Developer "Eugeneology" interview contains nothing on Defender's design.

---

## 5. Sound

### 5.1 Hardware
M6808 (MAME) / "6800" (manual/KLOV) + 2 KB sound ROM at $F800 + PIA 6821 + MC1408 8-bit DAC → on-board amp, mono. 6-bit command from main CPU via PIA (manual: "handshake is not implemented in DEFENDER"). MAME output gain 0.25 to a single speaker.

### 5.2 Sound commands used by the game (red-label source `defa7.src` L660-688, table format "SNDPRI, N×(REPCNT, SNDTMR in 16 ms, SND#)")
| Name (source comment) | Priority | Sound # (hex) |
|---|---|---|
| COIN | FF | 19 |
| FREE SHIP | FF | 1E |
| PLAYER DEATH | F0 | 11 then 17 |
| START 1 / START 2 | F0 | 0A / 0B |
| TERRAIN BLOW (planet) | E8 | 14, 11, 17 |
| SMART BOMB | E8 | 11 ×4, 17 |
| ASTRO CATCH | E0 | 08 |
| ASTRO LAND | E0 | 1F |
| ASTRO HIT | E0 | 11 |
| ASTRO SCREAM | D8 | 1A |
| APPEAR | D0 | 15 |
| PROBE HIT / SCHITZO HIT / UFO HIT / TIE HIT / LANDER HIT | D0 | 05 / 17 / 07 / 01 / 06 |
| LANDER PICK UP | D0 | 0B |
| LANDER SUCK | C8 | 0E |
| SWARM HIT | C0 | 07 |
| LASER | C0 | 14 |
| LANDER GRAB | C0 | 18 |
| LANDER SHOOT / UFO SHOOT | C0 | 03 |
| SCHITZO SHOOT | C0 | 09 |
| SWARM SHOOT | C0 | 0C |
| Hall-of-fame tunes | — | 3D "TOCCATA", 3E "PHANTOM" (amode1.src) |
Sound ROM special routines (vsndrm1.src L1003-1006): SP1 (spinner), BG1, BG2INC (background/thrust drones), LITE, BON2, BGEND, TURBO, APPEAR, THRUST, CANNON, RADIO, HYPER, SCREAM, ORGANT, ORGANN (organ tune/note), plus GWAVE/VARI tables. Thrust has its own routine (THRUST) and a background-sound flag (BG1FLG/BG2FLG).
Manual sound test: sounds 1-31 except 19, 27, 28.

### 5.3 Speech
**Defender has no speech.** Evidence: MAME's `defender` sound region contains only `video_sound_rom_1.ic12` (2 KB @ $F800) — no speech ROMs (contrast Sinistar's speech ROMs elsewhere in the driver); the sound ROM source checks `TALK EQU $EFFD` for "presence of talking program" (L933-938) — a generic Williams hook that is absent on Defender boards. No manual mentions speech. Conf H.

### 5.4 Open-licensed sound candidates (for "resembling" placeholders)
| Asset | Author | Licence | URL | Notes |
|---|---|---|---|---|
| Retro Sounds (2 wav, explosion/spaceship) | artisticdude | CC0 | https://opengameart.org/content/retro-sounds-0 | "reminded me of some retro arcade game" |
| Laser fire (tir.mp3) | farfadet46 | CC0 | https://opengameart.org/content/laser-fire-0 | Bfxr-generated |
| 512 Sound Effects (8-bit style) | SubspaceAudio | CC0 | https://opengameart.org/content/512-sound-effects-8-bit-style | 20.6 MB zip |
| Sci-fi Sounds (70 ogg) | Kenney | CC0 | https://opengameart.org/content/sci-fi-sounds | search snippet only, page not fetched (L) |
Freesound was not searched directly (GAP). Recommendation: synthesise procedurally (noise bursts, square/saw sweeps through an 8-bit DAC model at ~894 kHz-derived sample steps) rather than sampling recordings of the arcade board — recordings of the original are Williams' copyrighted output.

---

## 6. Home ports — what NOT to import
- **Atari 2600** (Bob Polaro, 1982): single button — fire/smart bomb/hyperspace chosen by ship height (smart bomb by flying below city line, hyperspace by flying off top); ship disappears when firing (flicker); city skyline instead of mountains; 5 humanoids per stage (snippet); landers can abduct only one humanoid at a time; its manual's scoring/humanoid text (the "DEFENDER.TXT" on ctrl-alt-rees.com is the **Atari 1982 manual**, not the arcade card: Swarmer listed at 200, humanoid catch/return 500/1000 — **do not import**).
- **Atari 8-bit / 5200** (Steve Baker, 1982): close to arcade, some flicker/slowdown; different title/option screen.
- **Atarisoft** ports (C64, VIC-20, Apple II, TI-99/4A, Intellivision) and others — differing graphics/controls.
Sources: Wikipedia; atarimagazines.com CVA v1n2; atariprotos.com (snippets). Conf M/L.

---

## 7. Open-source / derivative implementations
| Repo | Licence (GitHub API) | What it is | Ripped/copyrighted assets? |
|---|---|---|---|
| historicalsource/defender | none | Original Williams 6809 source (12 .SRC) | Is itself Williams' copyrighted source; no licence — **reference only, do not copy code/graphics data** |
| historicalsource/williams-soundroms | none | Sound ROM sources | same |
| mwenge/defender | none (README only) | Buildable red-label source + notes, ROM board images | same; source used heavily in this dossier for facts |
| therealsark02/defender | BSD-2-Clause | Atari ST/STE/Falcon port "based on the published 6809 assembler source code and artistic assets" | **Yes**: derived from original code/assets; includes 22 wav samples |
| jeffnyman/defender-redlabel | MIT | Go implementation "of the extracted, reassembled and rebuilt ROM" | **Likely yes**: spritesheet.png + 19 wavs (named landerdie.wav etc.) |
| jeffnyman/defender-retro | NOASSERTION | build system for the source | source-derived |
| w3arycod3r/fpga-defender | MIT | VHDL recreation for DE10-Lite | sprite_data.mif present — provenance not checked |
| LanceJZ/Defender-the-Remake | MIT | C remake (3D models, PNGs, wavs) | own-made models apparently; not verified |
| ranchoarcade/WilliamsSynthUnity | MIT | C# reproduction of the Williams sound board | no audio files; may embed sound-ROM-derived parameters (not checked) |
| mbackschat/williams-sound-explorer | NOASSERTION | browser tool for Williams sounds | not checked |
| GarethDaviesLondon/defender-release | Unlicense | HTML5 homage | 33 files, JS only, no media files |
| Defendguin (GNU directory) | GPL (per directory listing, not verified) | Linux-themed C/SDL clone | own assets |
Note: an MIT/BSD repo licence does **not** relicense Williams' code/graphics/sound it contains.

---

## 8. Visuals

### 8.1 Sprite sizes — from red-label source (`defb6.src` L1893-1980). Descriptor = `FCB width_in_bytes, height`; 1 byte = 2 horizontal pixels (4 bpp).
| Object (source name) | bytes × lines | **pixels W × H** |
|---|---|---|
| Player ship (PLAPIC/PLBPIC, left/right) | 8 × 6 | **16 × 6** |
| Lander (LNDP1-3, 3 anim frames) | 5 × 8 | **10 × 8** |
| Mutant (SCZP1 "SCHITZO") | 5 × 8 | **10 × 8** |
| Baiter (UFOP1-3 "UFO", 3 frames) | 6 × 4 | **12 × 4** |
| Bomber (TIEP1-4 "TIE", 4 frames) | 4 × 8 | **8 × 8** |
| Pod (PRBP1 "PROBE") | 4 × 8 | **8 × 8** |
| Swarmer (SWPIC1) | 3 × 4 | **6 × 4** |
| Humanoid (ASTP1-4, 2 frames × facing L/R) | 2 × 8 | **4 × 8** |
| Bomber mine (BMBP1-2) | 2 × 3 | **4 × 3** |
| Laser segment (LASP1) | 8 × 1 | **16 × 1** |
| Reserve-ship icon (PLAMIN) | 5 × 4 | **10 × 4** |
| Smart-bomb icon (SBPIC) | 3 × 3 | **6 × 3** |
| "250"/"500" popups (C25P1/C5P1) | 6 × 6 | **12 × 6** |
| Explosion pics (ASXP1, SWXP1, BXPIC) | 4 × 8 | 8 × 8 |
| Terrain explosion (TEREX) | 8 × 6 | 16 × 6 |
(Parallax forum / urchlay notes claim "10x8 … split into two 5x8 halves" — that refers to a different port's representation; the source table above is authoritative.) Conf H for the dimensions; the bitmap data is Williams-copyrighted — redraw, don't copy.

### 8.2 Colour RAM defaults (`defb6.src` CRTAB L1874-1892), RGB via §1.3 levels
| idx | byte | name | RGB (MAME levels) |
|---|---|---|---|
| 0 | 00 | SPACE | 0,0,0 |
| 1 | 00 | LASER (special, animated) | — |
| 2 | 07 | RED | 255,0,0 |
| 3 | 28 | GREEN | 0,174,0 |
| 4 | 2F | YELLOW | 255,174,0 |
| 5 | 81 | BLUE | 38,0,160 |
| 6 | A4 | GRAY | 137,137,160 |
| 7 | 15 | BROWN | 174,81,0 |
| 8 | C7 | PURPLE | 255,0,255 |
| 9 | FF | WHITE | 255,255,255 |
| A | — | BOMB CYCLER | cycled |
| B | — | MONOCHROME | |
| C | — | CYCLER | cycled |
| D-F | — | TIE1-3 (bomber colours, table TCTAB starts $81,$81,$2F) | cycled |
(RGB = derived calculation, Conf M; byte values Conf H.)

### 8.3 Terrain
- Drawn by `BGOUT` in `blk71.src` writing nibble patterns `$7007`/`$0770` → **colour 7 = BROWN ($15 ≈ RGB 174,81,0)**: a thin jagged mountain line (Wikipedia: "a line only a pixel wide"). Conf H for colour index, M for appearance.
- Planet-destroyed state removes terrain (status bit "TERRAIN INACTIVE", phr6.src L314).

### 8.4 Starfield
`STINIT` (`defa7.src` L2071-2093): **16 stars** (SNUM=16), random X < $9C (byte column), Y between YMIN (42) and $A8 (168); colours stepped by `+$11 & $77` per star (cycling palette nibbles 0-7). `STOUT` moves stars by the terrain scroll delta at reduced rate (derived from BGL/BGLX difference, masked) → slower than foreground = parallax. Stars are suppressed when status bit 5 set. Conf H for counts; exact parallax ratio not computed (GAP).

### 8.5 Screen / HUD / scanner layout (source `phr6.src` L150-159, `defa7.src` BORDER L904-929)
- Playfield Y range: **YMIN = 42 … YMAX = 240** (screen lines above 42 are the HUD).
- Scanner: `SCANH = YMIN-34 = 8` (top line), height 32 lines (bottom border line at SCANH+$20 = 40), framed by a border in colour 5 (**BLUE**, `$5555`) spanning byte columns $30–$71 (≈ pixels 96–226 of the 304-px addressable row, i.e. top-centre); a **white** (colour 9, `$9999`) screen-window marker drawn at columns $4C–$54 top and bottom (bracket showing the visible portion). Bottom separator line drawn across the full width in blue. Conf H for constants; pixel conversion M.
- Player 1 score at `P1DISP $0F1C` (byte col 15 → px ~30, line 28); Player 2 at `$711C` (byte col $71 → px ~226, right of scanner). Ship-reserve and smart-bomb icons near `P1SBD $291B` / `P2SBD $8B1B` (left of scanner for P1, right for P2). Conf M (derived addresses).
- Wave transition screen: "ATTACK WAVE n COMPLETED", "BONUS X n" + humanoid icons.

### 8.6 Smart-bomb flash — see §3.4 (background palette entry 0 complemented 8 times → 4 white flashes).

---

## 9. .NET packages (nuget.org API, queried 2026-10-07)

| Package | Latest stable | Published | Licence | TFMs | Notes |
|---|---|---|---|---|---|
| **Avalonia** 12.x | **12.1.3** | 2026-09-22 | MIT | net8.0, **net10.0** | 12.0.0 released 2026-04-07 (also net8.0+net10.0) → **Avalonia 12 supports net10.0** (and net8.0; no netstandard) |
| **Avalonia** 11.x | **11.3.22** | 2026-09-11 | MIT | (netstandard2.0/net6/net8 per deps) | still maintained |
| Silk.NET.SDL | 2.23.0 | 2026-01-23 | MIT | netcoreapp3.1, net5/6, netstandard2.0/2.1 | **SDL2** binding; native via Ultz.Native.SDL 2.32.10 (SDL is zlib) |
| **SDL3-CS** (edwardgushchin) | **3.4.18** | 2026-10-03 | **Zlib** (LICENSE file in package) | net7/8/9/**10** | bindings only; natives in **SDL3-CS.Native 3.4.2** (2026-03-17; win/linux/osx x64+arm64) |
| **ppy.SDL3-CS** | **2026.1002.1** | 2026-10-02 | MIT | net8.0, net8.0-android34.0 (usable from net10) | bundles SDL3 natives for win x86/x64/arm64, osx x64/arm64, linux x86/x64/arm/arm64, iOS |
| Silk.NET.OpenAL | 2.23.0 | 2026-01-23 | MIT (binding) | netstandard2.0/2.1 etc. | |
| **Silk.NET.OpenAL.Soft.Native** | 1.23.1 | 2024-04-23 | **LGPL-2.0-or-later** | — | bundles libopenal.so / soft_oal.dll / dylib → LGPL obligations (dynamic link OK, must allow relinking/replacement, ship licence + source offer) |
| OpenAL.Soft (other package) | 1.23.1 | 2024-06-24 | none declared | | avoid (no licence metadata) |
| NAudio | 3.1.0 | 2026-09-07 | MIT | net9.0, net9.0-windows… | meta-package pulls NAudio.Wasapi/WinMM/Asio/WinForms — **playback back-ends are Windows APIs**; NAudio.Core 3.1.0 is portable for DSP/decoding only (Linux output not available — M, based on dependency list) |
OpenAL Soft upstream (kcat/openal-soft) COPYING = GNU Library GPL v2; README: "OpenAL Soft is an LGPL-licensed…"; default HRTF data Apache-2.0. SDL upstream (libsdl-org/SDL) = Zlib.
**Recommendation:** SDL3 via SDL3-CS (+SDL3-CS.Native) or ppy.SDL3-CS for both gamepad (SDL_Gamepad) and audio (SDL_AudioStream push of a software-mixed 8-bit/float stream) — all zlib/MIT, avoids LGPL entirely. Silk.NET 3.0 not yet on nuget (no 3.x versions listed for Silk.NET.OpenAL).

---

## 10. Gaps / open items
1. Original **instruction card** image (with exact scoring card layout/wording) not viewed — scores confirmed from source instead.
2. Physical **control panel layout** (button order) not confirmed from an image.
3. AMOA 1980 date conflict (Sept vs Oct 31, Chicago) unresolved; arcade-history.com blocked (403).
4. Baiter spawn timer, hyperspace death odds, exact star parallax ratio, wave composition tables — present in source (`defb6.src`/`defa7.src` wave table, phr6 "WAVE TABLE") but not extracted here.
5. "Landers 100 pts in early edition" — unverified.
6. Freesound not searched; Kenney sci-fi page not fetched.
7. Wikipedia resolution "320×256" contradicts MAME (292×240 visible) and manual (360×240 nominal) — use MAME.

---

## 11. Source ledger

| # | Title | Author/Org | URL | Accessed | Type | Platform/version | What I inspected | Claims supported | Conf | Licensing |
|---|---|---|---|---|---|---|---|---|---|---|
| S1 | williams.cpp | MAME (Aaron Giles et al.) | https://github.com/mamedev/mame/blob/36818916698d85746ae488278d7dca48783f4dfc/src/mame/williams/williams.cpp | 2026-10-07 | Emulator source | commit 3681891 | L1-140, 325-380, 488-525, 670-700, 760-800, 1525-1630, 1980-2080, 3965-3995 | clocks, screen, visarea, inputs, map, ROM sets, revision notes | H | BSD-3-Clause |
| S2 | williams_v.cpp | MAME | …/src/mame/williams/williams_v.cpp | 2026-10-07 | Emulator source | same | L1-140, 195-222, 336-363 | framebuffer layout, palette resistors | H | BSD-3-Clause |
| S3 | williams_m.cpp | MAME | …/src/mame/williams/williams_m.cpp | 2026-10-07 | Emulator source | same | L335-345 | cocktail bit | H | BSD-3-Clause |
| S4 | resnet.cpp / resnet.h | MAME | …/src/emu/video/resnet.cpp | 2026-10-07 | Emulator source | same | L55-196; h L167-184 | palette level calc | H | BSD-3-Clause |
| S5 | Defender Later Series Setup Booklet 16P-3001-103 R-T (July 1981) | Williams Electronics | https://archive.org/details/arcademanual_Defender-Later-Series-Setup-Booklet | 2026-10-07 | Operator manual (OCR text) | later PCBs | full text | controls, adjustments, HS tables, diagnostics | H | © Williams; archive hosting |
| S6 | Defender Early Series Setup Booklet 16P-3000-103 | Williams | https://archive.org/details/arcademanual_Defender-Early-Series-Setup-Booklet | 2026-10-07 | Operator manual (OCR) | early PCBs | full text diff vs S5 | early adjustment differences | H | © Williams |
| S7 | Defender Setup Booklet USA | Williams | https://archive.org/details/arcademanual_DefenderSetupBookletUSA | 2026-10-07 | Operator manual (OCR) | early | grep | same as S6 | H | © Williams |
| S8 | Theory of Operation, Later System Boards 16P-3001-301 (Oct 1981) | Williams | https://archive.org/details/ArcadeGameManualDefender | 2026-10-07 | Service manual (OCR) | later boards | grep (video, clocks, sound) | 360×240, 60 Hz non-interlaced, 16/256 colours, clocks | H | © Williams |
| S9 | Defender Owner's Manual | Williams (uploaded by The Manual Librarian) | https://archive.org/details/arcademanual_defender_2 | 2026-10-07 | Manual w/ schematics | — | item page only | existence | M | © Williams |
| S10 | mwenge/defender | GitHub user mwenge ("robert" per README shell prompt) | https://github.com/mwenge/defender | 2026-10-07 | Original source + notes | Red label | README; defa7, defb6, phr6, amode1, mess0, romc8, blk71, vsndrm1, info | sprites, colours, scoring, bonus, HS tables, sounds, scanner, stars, smart bomb | H | No licence; Williams © source — facts only |
| S11 | historicalsource/defender, williams-soundroms | historicalsource | https://github.com/historicalsource/defender | 2026-10-07 | Source archive | — | tree listing | provenance | H | none |
| S12 | Defender (1981 video game) | Wikipedia | https://en.wikipedia.org/wiki/Defender_(1981_video_game) | 2026-10-07 | Encyclopedia | rev. as of access | first 100k chars via WebFetch (twice) | history, team, ports, design anecdotes | M | CC BY-SA |
| S13 | DEFENDER.TXT (transcribed manual) | "The Morbid Guy"; orig. © 1982 Atari | https://ctrl-alt-rees.com/archive/www.cyou.com-~richard/DEFENDER.TXT | 2026-10-07 | Atari 2600 manual transcription | Atari 2600 | full via WebFetch | bonus formula (consistent), port rules NOT to import | M | © Atari |
| S14 | Defender – KLOV | Arcade Museum | https://www.arcade-museum.com/Videogame/defender--williams | 2026-10-07 | Database | — | WebFetch summary | cabinets, controls, 6809/6800 | M | © KLOV |
| S15 | Feature: The History of Defender | Bill Loguidice & Matt Barton, Game Developer | https://gamedeveloper.com/game-platforms/feature-the-history-of-i-defender-i- | 2026-10-07 | Retrospective (2009-07-14) | — | visible excerpt | AMOA reaction quote | M | © Informa |
| S16 | Eugeneology: An Interview with Eugene Jarvis | Game Developer | https://www.gamedeveloper.com/production/eugeneology-an-interview-with-eugene-jarvis | 2026-10-07 | Interview | — | full via WebFetch | nothing on Defender design (negative result) | — | © |
| S17 | arcade-history.com game 614 | arcade-history | https://www.arcade-history.com/game/614/ | 2026-10-07 | Database | — | **403 blocked** | (Oct 31 1980 AMOA claim only via search snippet) | L | — |
| S18 | therealsark02/defender README | therealsark02 | https://github.com/therealsark02/defender | 2026-10-07 | Port source | Atari ST | README, tree | credits; contains original-derived assets | H | BSD-2 (repo) |
| S19 | jeffnyman/defender-redlabel | Jeff Nyman | https://github.com/jeffnyman/defender-redlabel | 2026-10-07 | Port | Go | README, tree | ROM-derived; wavs+spritesheet | H | MIT (repo) |
| S20 | GitHub repo search "defender williams" | GitHub API | https://api.github.com/search/repositories?q=defender+williams | 2026-10-07 | Index | — | top 30 | reimplementation list/licences | M | — |
| S21 | OpenGameArt: Retro Sounds / Laser fire / 512 Sound Effects | artisticdude / farfadet46 / SubspaceAudio | URLs in §5.4 | 2026-10-07 | Asset pages | — | pages via WebFetch | CC0 licences | H | CC0 |
| S22 | nuget.org registration & flat-container APIs | NuGet | https://api.nuget.org/v3/registration5-semver1/{id}/{ver}.json | 2026-10-07 | Package metadata | — | versions, published, licence, TFMs, deps; nupkg contents for ppy.SDL3-CS, SDL3-CS, SDL3-CS.Native, Silk.NET.OpenAL.Soft.Native | §9 table | H | — |
| S23 | kcat/openal-soft COPYING & README | OpenAL Soft | https://github.com/kcat/openal-soft | 2026-10-07 | Licence text | master | first lines | LGPL | H | LGPL-2 |
| S24 | GitHub repo metadata (edwardgushchin/SDL3-CS, ppy/SDL3-CS, dotnet/Silk.NET, AvaloniaUI/Avalonia, libsdl-org/SDL) | GitHub API | https://api.github.com/repos/… | 2026-10-07 | Metadata | — | licence spdx | Zlib / MIT | H | — |
| S25 | Web search snippets (atarimagazines CVA v1n2, atariprotos, strategywiki (403), mentalfloss, slackware.uk urchlay notes) | various | in text | 2026-10-07 | Snippets | — | snippets only | port differences, early-edition lander 100 claim | L | — |
