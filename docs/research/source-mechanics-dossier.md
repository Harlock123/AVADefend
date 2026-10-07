# DEFENDER (Williams 1981, Red Label): game mechanics from the original source

Purpose: numeric behaviour and rules for a clean-room reimplementation. No code or assets are copied here. Only numbers and rules are recorded, each with a citation.
Accessed: 2026-10-07.

Status tags:
- **VERIFIED**: the value or rule was read directly in the source. A citation is given.
- **INFERRED**: my reading or arithmetic based on the cited code.
- **UNKNOWN**: not found or not determined.

Citations use the form `file:line` (LABEL). Files are in `mwenge/defender/src/`. Line numbers refer to that repo at the commit listed below. The historicalsource copy has identical logic; only the assembler syntax and comment punctuation differ (I checked this with a normalised diff).

---

## 0. Source ledger

| Item | Details |
|---|---|
| Primary | https://github.com/mwenge/defender, commit `e74006344b16d19245510b5e55780f90d7606bb8` (shallow clone). README: "source code for the Williams arcade game Defender ... Red Label version". The README says the source was taken from historicalsource. |
| Secondary | https://github.com/historicalsource/defender, commit `3fae9d312295c8b515a59f1dc2456fbc16bb9704`. These are the original upper-case `.SRC` files. |
| Files inspected | `phr6.src` (RAM/equates, all of it); `defa7.src` (core, all of it); `defb6.src` (enemies, data, lines 1-1980); `blk71.src` (terrain, player explosion, wave table, all of it); `amode1.src` (scanner, lines 1175-1315); `romc8.src` (CMOS defaults, lines 776-838); `mess0.src` (attract point text); `vsndrm1.src` (sound ROM dispatch, lines 920-1130). |
| Licence | **Neither repo has a LICENSE file.** The GitHub API reports `license: null` for both. The source itself carries Williams copyright strings: `romc8.src:836` "COPYRIGHT 1980 - WILLIAMS ELECTRONICS" and `vsndrm1.src:3` "COPYRIGHT WILLIAMS ELECTRONICS 1980". Treat the code as all rights reserved, Williams/WMS successor. Use it only as a reference for facts and mechanics. Do not copy code, sprite data, terrain tables or sound data. |
| Handling | Files were only read with grep, sed and cat. Nothing was built or executed. One small Python helper of my own (in `scratchpad/tools/`) read the terrain bytes as data to get the min/max altitude. |

Internal name map: `mess0.src:265-282` pairs these names with the attract-mode labels.

| Source name | Game name |
|---|---|
| LANDER | Lander |
| SCHITZO / SCZ | Mutant |
| TIE | Bomber |
| PROBE / PRB | Pod |
| SWARM / MSW | Swarmer |
| UFO | Baiter |
| ASTRO / EARTHLING | Humanoid |

---

## 1. Timing, screen, world coordinates

### Frame and scheduling

- **Frame rate: about 60 Hz.** The sleep comment reads "A=SLEEP TIME X 16MSEC" (`defa7.src:9`), and the sound table uses "SNDTMR IN 16 MSEC" (`defa7.src:661`). VERIFIED.
- **Executive loop, once per frame.** The IRQ increments `TIMER` once per frame (`defa7.src:1960-1963`, at mid-screen). `EXEC` then runs these steps (`defa7.src:3046-3128`). VERIFIED.
  1. Collision check (COLCHK).
  2. Explosion update.
  3. RAND.
  4. Switch processes.
  5. Process dispatch. Every sleeping process's `PTIME` is decremented once per frame, so `NAP n` means n frames.
- **Once per frame inside the IRQ** (`defa7.src:1931-1994`). VERIFIED.
  - Sound sequencer and switch scan (SNDSEQ), player physics (PLAYER), stars (STOUT), and the enemy-shot ("shell") mover (SHELL).
  - Object draw (OPROC) and player draw (PRDISP) run twice, once for each half of the screen, to race the beam.
  - Velocity integration for all objects (VELO): `OX16 += OXV` and `OY16 += OYV` every frame.
- **Rates of other processes.** VERIFIED unless marked.

| Process | Rate | Citation |
|---|---|---|
| Game executive (GEXEC) | every 15 frames | `NAP 15,GEX0`, `defa7.src:1731` |
| Scanner/scan process (SCPROC) | cycle of 2+2+4 = 8 frames; the scanner redraws every 8 frames | `defa7.src:3300-3307` |
| Off-screen object list maintenance (ISCAN/OSCAN) | every 8 frames | same |
| Shell-lifetime countdown (SHSCAN) | every 8 frames | same |
| Lander process | every 6 frames (cruise); every 1 frame (grabbing) | `defb6.src:736, 776` |
| Mutant process | every 3 frames | `defb6.src:901` |
| Swarmer process | every 3 frames | `defb6.src:249` |
| Baiter process | every 6 frames | `defb6.src:46` |
| Bomber squad super-process | every frame; one random bomber is updated per tick | `defb6.src:1027-1116` |
| Humanoid walker | every 2 frames; services one TLIST slot per tick | `defb6.src:294-359` |

### Screen

- **Video memory layout.** The address is `column_byte*256 + y`. Each byte holds 2 pixels as 4-bit nibbles.
  - The screen is `$9800` bytes. Lasers stop at column `$98` = 152 bytes = **304 px wide** (`defa7.src:2802`).
  - Object draw is limited to columns below `$9C` (`defa7.src:2537`).
  - The width is VERIFIED from the code limits. The 304x256 frame buffer is INFERRED from Williams hardware.
- **Y limits.** VERIFIED.
  - `YMIN = 42`. This is the top of the playfield and the minimum Y coordinate (`phr6.src:21`).
  - `YMAX = 240` (`phr6.src:20`).
  - Objects wrap vertically. Going below YMIN puts them at YMAX, and going above YMAX puts them at YMIN (VELO, `defa7.src:2490-2495`; also mutant hop, `defb6.src:888-890`).
- **Scanner area.** VERIFIED.
  - `SCANH = YMIN-34 = 8` (`phr6.src:158`).
  - The scanner starts at screen column `$30` and row 8: `SCANER = $3000+SCANH` (`phr6.src:159`).
  - It is 64 byte-columns wide (`amode1.src:1225`), which is **128 px**, centred on a 304-px screen (columns 48-111, so pixels 96-223).
  - The border's bottom line is at row `SCANH+$20 = 40` (`defa7.src:907`). The scanner is therefore about 32 rows tall (rows 8-39), sitting above the playfield.
- **Visible playfield width: 150 bytes = 300 px.** An object is drawn only if `OX16 - BGL < 150*64` (`defa7.src:2527-2530`). VERIFIED.
- **Ground.**
  - Terrain altitude starts at offset `$E0 = 224` (`blk71.src:103, 381`). VERIFIED.
  - Across the whole planet the terrain Y ranges from **159 to 233**. INFERRED: I decoded the table as data.
  - Humanoids spawn at Y `$E0` (`defa7.src:1529`), and their walking Y is clamped at or below `$E8` (232) (`defb6.src:316, 340`). VERIFIED.

### World X

- **Units.** `OX16` is a 16-bit world X. **32 units = 1 pixel**, and 64 units = 1 byte column. Evidence: the comment "100*32 ;100 PIXEL LEFT BUFFER" (`defa7.src:3334`) and the screen-visibility maths at `defa7.src:2527-2535`. VERIFIED.
- **Planet circumference.** The 16-bit coordinate wraps at **65536 units = 2048 px**. INFERRED, and consistent with the terrain table: 256 bytes = 2048 bits at 1 bit per pixel (`blk71.src:18 TLEN=$100`, `510-525`), and the altitude table is indexed by `OX16>>6` with 1024 entries of 2 px each (`defb6.src:364-380`, `blk71.src:70, 375-400`).
- **Y units.** `OY16` high byte = pixel Y, and the low byte is a fraction. Velocities are in 1/256 px per frame. VERIFIED from VELO and the player code.
- **Scrolling.**
  - `BGL` is the world X of the left edge of the screen (`phr6.src:215` "TERRAIN LEFT POINTER"). `BGLX` holds the previous frame's value.
  - Every frame `BGL += PLAXV(top16) - BGDELT` (`defa7.src:2428-2431`). VERIFIED.
  - Player world X: `PLABX = (PLAX16 >> 2 & ~$1F) + BGL` (`defa7.src:2433-2440`). `PLAX16` is the ship's **screen** X with 128 units per pixel; its high byte is the screen byte column. VERIFIED.
- **Active-object window.** Objects within `[BGL - 100 px, BGL + 400 px)` are on the active list. Others move to an inactive list, which integrates them at 8x velocity every 8 frames (`defa7.src:3311-3374`). VERIFIED.

---

## 2. Player ship (PLAYER, `defa7.src:2339-2476`)

All values below are VERIFIED unless marked.

- **Velocity format.** `PLAXV` is 24-bit. "Velocity" below means its top 16 bits (`PLAXV`, signed), in world units per frame (32 = 1 px).
- **Drag, applied every frame before thrust.** The code adds `-(V)*4` into the 24-bit value at byte offset 1 (`defa7.src:2343-2358`).
  - INFERRED: this is equivalent to **V -= V/64 per frame**.
- **Thrust.** While the thrust input is held (PIA2 bit 1), `PLADIR` is added at byte offset 1 (`defa7.src:2360-2371`).
  - `PLADIR` is **±$0300** (`defa7.src:1249-1250`, `3231-3235`). That is **+3 units per frame²** in top-16 units, which is 3/32 px/frame².
  - INFERRED: the steady state is where 3 = V/64, so **V ≈ 192 units/frame = 6 px/frame ≈ 360 px/s**.
- **Hard clamp:** |V| ≤ `$0100` (256 units = 8 px/frame) (`defa7.src:2421-2428`). With the default thrust and drag this clamp is never reached (INFERRED).
- **Ship screen position and the reverse slide** (`defa7.src:2373-2420`).
  - Target screen column:
    - `base + V/8` byte columns (half-column precision).
    - `base = $20` (column 32, about x = 64 px) when facing right.
    - `base = $70` (column 112, about x = 224 px) when facing left.
    - The `V/8` offset applies only when the velocity has the same sign as the facing. Otherwise the offset is 0.
    - INFERRED: at full speed the offset is 24 columns (48 px). The ship sits at about x = 112 px facing right and about x = 176 px facing left.
  - The ship's screen X moves toward the target by **at most `$100` per frame, which is 1 column = 2 px/frame**.
    - While it is sliding, `BGDELT = ±$40` (64 units = 2 px) is taken off the scroll, so the ship's **world velocity is unchanged** by the slide (`defa7.src:2399-2413`).
    - INFERRED: a full reverse at low speed slides 80 columns (160 px), which takes about 80 frames (1.33 s).
- **Reverse (REV, `defa7.src:3157-3171`).**
  - Triggered on the button **press edge**. It sets `NPLAD = -PLADIR`, so the facing flips. Velocity is not changed directly; drag and thrust do the rest.
  - The next reverse needs the button released, then 5 more frames.
  - The ship uses a separate left-facing picture (PLBPIC).
- **Start position.**
  - Screen column `$20`, Y `$80` (128): `NPLAXC=$2080` (`defa7.src:1269-1271`).
  - Facing right with zero velocity. On a new life `BGL = 0` (`defa7.src:1241-1244`).
- **Vertical motion** (`defa7.src:2442-2475`). The up input is PIA3 bit 0; down is PIA2 bit 7.
  - **No up/down input: vertical velocity is 0 immediately.** There is no vertical inertia.
  - **First frame of a press:** the speed is **1 px/frame** (`±$100`).
  - **While held:** the speed increases by 8/256 px/frame² up to **2 px/frame** (`±$200`). INFERRED: reaching full speed takes 32 frames.
- **Vertical limits.** Up movement is ignored when Y ≤ `YMIN+1` = 43. Down movement is ignored when Y ≥ 238. The ship's Y is the top-left of the image.
- **Ship size and collision.**
  - The image is **8 byte-columns x 6 rows = 16 x 6 px** (`defb6.src:1961-1964` PLAPIC/PLBPIC "8,6"). VERIFIED.
  - Collision is pixel-accurate (section 12).

---

## 3. Laser (LFIRE, LASR/LASL, `defa7.src:2763-2886`)

- **Max simultaneous lasers: 4** (`LFLG`, `CMPA #4`, `defa7.src:2763-2766`). VERIFIED.
- **Fire is edge-triggered; there is no auto-fire.**
  - The switch scan raises an event only when a game switch has been 0 for the last 2 scans and is now 1 (`defa7.src:760, 780-790`).
  - The fire entry in SWTAB has STATUS mask `$E8`. Firing is blocked during game over, when controls are inactive, when objects are inactive, or when the player is dead (`defb6.src:1845-1846`, `defa7.src:3111`).
  - VERIFIED.
- **Origin.** VERIFIED.
  - Right-facing: ship column +7 (+14 px) and row +4 (`LEAX $704,X`, `defa7.src:2792`).
  - Left-facing: ship column +0 and row +4 (`defa7.src:2841`).
- **Growth per frame** (the laser process sleeps 1 frame per step). VERIFIED.
  - The **head advances 4 columns = 8 px per frame**. Each step draws a solid segment in colour 1, with a colour-9 (white) tip (`defa7.src:2799-2810`).
  - A "fizzle" segment behind it advances 3 columns per frame, using a random sparse pattern table (`FISS`, `defa7.src:2889-2902`).
  - The tail eraser advances 1 column per frame.
  - INFERRED: the beam is a lengthening line with a broken trailing section.
- **End of life.** VERIFIED.
  - The head reaches column `$98` (the right edge) or column ≤ 5 (the left edge), **or** the head hits something.
  - The laser lives in **screen space**: it does not scroll with the world (INFERRED from the use of absolute screen addresses).
- **Hit test.** VERIFIED.
  - Each frame the head is tested with an 8x1 collision picture (LASP1 "8,1", `defb6.src:1940`) against the active object list.
  - **The first object hit has its kill routine called, and the laser ends.** One kill per shot.
- **Laser colour** cycles through a 36-entry colour table, one step every 2 frames (COLR/COLTAB, `defa7.src:3024-3042`). VERIFIED.
- **Shot speed in time:** from the right-facing origin (column about 39) to the right edge (152) is about 28 frames. INFERRED.

---

## 4. Smart bomb (SBOMB, `defa7.src:3175-3209`)

- **Starting count = the number of ships per game** (CMOS `NSHIP`, default **3**). `START` loads `A=NSHIP&$F` into `P1LAS` and then stores `D=(A,10)` into `P1SBC`/`P1TRG` (`defa7.src:1142-1147`; default `romc8.src:802`). VERIFIED.
- **+1 smart bomb with every extra life** (`INC PSBC-1,X`, `defa7.src:525-526`). VERIFIED.
- **Display caps:** at most **3** bomb icons (`defa7.src:888-890`) and at most **5** reserve-ship icons (`defa7.src:844-846`). The internal counts can be higher. VERIFIED.
- **Effect.** VERIFIED.
  - Walks the active object list.
  - Every object that is **currently drawn on screen** (`OBJX≠0`) **and** has `OTYP < 2` gets its normal kill routine called, so normal points are scored.
  - Humanoids (`OTYP $10`), score signs (`$11`) and appearing objects (bit 1) are skipped. Landers carrying a humanoid have `OTYP=1` and **are** killed (`defb6.src:739`).
  - Enemy shots and mines live on a separate list and **are not cleared** (INFERRED: SBOMB only walks OPTR).
- **Flash:** colour 0 (the background) is complemented every 2 frames, 4 times, which gives 2 white flashes (`defa7.src:3196-3202`). VERIFIED.
- **Debounce:** the button must be released, then 10 more frames pass (`defa7.src:3203-3208`). VERIFIED.

---

## 5. Hyperspace (HYPER, `defa7.src:3213-3278`)

- **Allowed** only when `STATUS & $FD == 0`, that is, during normal play. The terrain-inactive bit is allowed. VERIFIED.
- **Sequence.** VERIFIED.
  1. The screen is cleared.
  2. Wait 15 frames.
  3. **All enemy shots are deleted.**
  4. `BGL` is set to a random 16-bit value, so the destination is anywhere on the planet.
  5. A random direction is chosen:
     - Facing right: screen column `$20`, `PLADIR=+$300`.
     - Facing left: screen column `$70`, `PLADIR=-$300`.
  6. Y = `HSEED/2 + YMIN`, so 42..169.
  7. Velocity is zeroed.
  8. The ship plays an "appear" effect and then waits `$28` = 40 frames.
- **Death chance.** After the reappear, if the random byte `LSEED > 192` the player dies through the normal death routine (`defa7.src:3275-3277`). VERIFIED.
  - INFERRED: that is a probability of **63/256 ≈ 24.6%** on every hyperspace, regardless of wave.

---

## 6. Scoring

`SCORE` takes A = an exponent code and B = 2 BCD digits (`defa7.src:474-509`).

- INFERRED from the code: points = BCD(B) × 10^(A).
- Examples: A=1, B=$20 gives 200. A=2, B=$10 gives 1000.

| Event | Points | Citation | Status |
|---|---|---|---|
| Lander killed | **150** | `KILP $0115,LHSND` `defb6.src:922` | VERIFIED |
| Mutant killed | **150** | `defb6.src:625` | VERIFIED |
| Baiter killed | **200** | `defb6.src:82` | VERIFIED |
| Bomber killed | **250** | `KILO $0125,TIHSND` `defb6.src:1120` | VERIFIED |
| Pod killed | **1000** | `KILO $0210` `defb6.src:118` | VERIFIED |
| Swarmer killed | **150** | `LDD #$0115` `defb6.src:190` | VERIFIED |
| Attract-screen cross-check | Lander 150, Mutant 150, Pod 1000, Bomber 250, Swarmer 150, Baiter 200 | `mess0.src:265-282, 386-389` | VERIFIED |
| Humanoid caught in mid-air | **500** (P500, "500" sign shown for 50 frames) | `defb6.src:398-411, 506-529` | VERIFIED |
| Caught humanoid set down on ground | **500** (second P500) | `defb6.src:945-962` | VERIFIED |
| Humanoid lands safely on its own | **250** (P250) | `defb6.src:930-938, 958-960` | VERIFIED |
| Enemy shot or mine hits the player | 25 (shell kill routine BKIL `LDD #$25`) | `defa7.src:2700-2701` | VERIFIED code; INFERRED that it fires on player contact, since shells are only collision-tested against the player |
| End-of-wave bonus per surviving humanoid | **100 × min(wave, 5)** | `defa7.src:1826-1836` | VERIFIED |

Score display: at most 6 digits, leading zeros blanked (`defa7.src:561-594`).

Ramming an enemy: COLCHK calls the enemy's kill routine, so the enemy dies and **is scored**, and then the player dies (`defa7.src:3132-3151`, `2993-3011`). INFERRED.

---

## 7. Waves and difficulty

### Wave table (WVTAB, `blk71.src:674-722`)

The table has 22 one-byte rows, in the same order as the RAM ELIST (`phr6.src:382-405`). Each row holds `MAX, MIN, INTRA-delta, INTER-delta, W1, W2, W3, W4`.

| Row (variable) | max | min | intra | inter | W1 | W2 | W3 | W4 |
|---|---|---|---|---|---|---|---|---|
| Landers (LNDRES) | 20 | 0 | 0 | 0 | 15 | 20 | 20 | 20 |
| Bombers (TIERES) | 3 | 0 | 0 | 0 | 0 | 3 | 4 | 5 |
| Pods (PRBRES) | 6 | 0 | 0 | 0 | 0 | 1 | 3 | 4 |
| Mutants (SCZRES) | 10 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Swarmers (SWMRES) | 10 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| WAVTIM (×15 frames between lander squads) | 30 | 0 | 0 | 0 | 30 | 25 | 20 | 16 |
| WAVSIZ (landers per squad) | 5 | 0 | 0 | 0 | 5 | 5 | 5 | 5 |
| LNDXV (lander X speed cap) | $60 | 0 | +3 | +2 | $16 | $1E | $26 | $2E |
| LNDYV MSB | 1 | 0 | 0 | 0 | 0 | 0 | 1 | 1 |
| LNDYV LSB | $FF | 0 | +$10 | 0 | $70 | $B0 | 0 | 0 |
| LDSTIM (lander shot timer) | $80 | $10 | -4 | -2 | $4A | $3A | $2A | $2A |
| TIEXV (bomber X speed) | $30 | 0 | 0 | 0 | $20 | $28 | $2C | $30 |
| SZRY (mutant random Y hop) | 2 | 0 | 0 | 0 | 1 | 1 | 2 | 2 |
| SZYV MSB | 1 | 0 | 0 | 0 | 0 | 0 | 1 | 1 |
| SZYV LSB | $FF | 0 | +8 | +6 | $62 | $E0 | $02 | $12 |
| SZXV (mutant X speed) | $60 | 0 | +8 | +4 | $0C | $1C | $24 | $28 |
| SZSTIM (mutant shot timer) | $FF | 8 | -2 | -2 | $2A | $22 | $1E | $1C |
| SWXV (swarmer X speed) | $60 | 0 | +8 | +2 | $16 | $1E | $20 | $22 |
| SWSTIM (swarmer shot timer) | 40 | 10 | -2 | -1 | 25 | 25 | 25 | 25 |
| SWAC (swarmer Y-accel mask) | $3F | 0 | 0 | 0 | $1F | $1F | $1F | $3F |
| UFOTIM (baiter timer, ×15 frames) | $C0 | $18 | -12 | -4 | $D4 | $C4 | $A4 | $94 |
| UFSTIM (baiter shot timer) | 10 | 3 | -1 | -1 | 15 | 13 | 12 | 10 |
| UFOSK (baiter re-seek threshold) | 200 | 40 | -12 | -8 | 240 | 220 | 200 | 200 |

VERIFIED. The column meanings come from the header comment "MAX,MIN,INTRADELT,INTERDELT / W1,W2,W3,W4".

### How the table is applied (GETWV, `defa7.src:1849-1901`; WDELT, `defa7.src:1908-1927`)

Steps 1-3 are VERIFIED. The rest is noted per item.

1. Base values come from column W(min(wave, 4)).
2. Then the **inter-wave delta** is applied N times, where `N = max(wave-4, 0) + GA1`, capped at `GA2`.
   - CMOS defaults: **GA1 "INITIAL DIFFICULTY" = 5** and **GA2 "DIFFICULTY CEILING" = 15** (`romc8.src:811-812`).
   - So with the defaults even wave 1 gets 5 inter-deltas, and N saturates at 15 from wave 14 on (INFERRED arithmetic).
3. Each delta application is clamped:
   - A positive delta is skipped if the result would exceed max or overflow the byte.
   - A negative delta is skipped if the result would fall below min or underflow.
4. **Intra-wave escalation.** The game executive applies the INTRA column to the live values every **40 ticks × 15 frames = 600 frames (10 s)** (`defa7.src:1723-1730`). VERIFIED.
   - These live values are saved and restored across player deaths: PLSAV/PLRES (`defa7.src:1491-1512`, `1579-1585`). INFERRED.

**Effective values with the default GA1=5 (INFERRED arithmetic):**

| Variable | W1 | W2 | W3 | W4 |
|---|---|---|---|---|
| LNDXV | $20 | $28 | $30 | $38 |
| LDSTIM | $40 | $30 | $20 | $20 |
| SZXV | $20 | $30 | $38 | $3C |
| SZYV | $0080 | $00FE | $0120 | $0130 |
| SZSTIM | $20 | $18 | $14 | $12 |
| SWXV | $20 | $28 | $2A | $2C |
| SWSTIM | 20 | 20 | 20 | 20 |
| UFOTIM (ticks) | 192 | 176 | 144 | 128 |
| UFSTIM | 10 | 8 | 7 | 5 |
| UFOSK | 200 | 180 | 160 | 160 |

Counts (landers, bombers, pods) have zero deltas, so they follow the table directly. **Wave 1: 15 landers. Wave 2: 20 landers, 3 bombers, 1 pod. Wave 3: 20, 4, 3. Wave ≥4: 20, 5, 4.**

### Spawning (GEXEC, `defa7.src:1647-1731`)

GEXEC ticks every 15 frames. VERIFIED unless marked.

- **Landers.**
  - A squad is launched when `WAVTMR` reaches 0 or no landers are alive, and also on the first tick.
  - Only when fewer than 8 landers are alive and reserves remain.
  - Squad size is `min(WAVSIZ=5, reserve)`.
  - `WAVTMR` is reloaded from `WAVTIM`. INFERRED: wave 1 gives 30 ticks = 450 frames = 7.5 s between squads.
  - Landers spawn at a random world X (16-bit), Y = `YMIN+2` (the top), with an appear effect (`defb6.src:652-686`).
- **Pods** all spawn at the start of the wave (or life) at random positions, with an appear effect (`defa7.src:1622-1626`, `defb6.src:87-114`).
  - X: high byte `$10-$4F`.
  - X velocity: random -32..+31 units/frame.
  - Y velocity: random, |v| ≥ `$20`.
- **Bombers** spawn in squads of up to 3 (`defa7.src:1628-1639`). VERIFIED; positions INFERRED.
  - Each squad's direction alternates.
  - Start Y is 80; start X is around player world X + `$8000` (half the planet away), with squad members spaced apart (`defb6.src:980-1021`).
- **Wave end.** The wave ends when landers (alive + reserve), bombers, pods, swarmers and mutants (alive + reserve) are all zero (`WVCHK`, `defa7.src:1748-1755`). **Baiters do not count.**
  - When it ends: bonus screen, then `INC PLAS` followed by a restart that decrements it again (net ship count unchanged), and a new wave (`defa7.src:1657-1666`).
- **Baiter timer.** Let `remaining` = the WVCHK sum.
  - If `remaining ≤ 8`, the countdown is capped at `UFOTIM/2+1` ticks. If `remaining ≤ 3`, the cap is `UFOTIM/4+1` (`defa7.src:1667-1677`).
  - When the timer expires, one baiter spawns if fewer than **12** are alive.
  - The timer is then reloaded:
    - `UFOTIM` if `remaining ≥ 4`;
    - otherwise a random value ≤ `UFOTIM/4` (`defa7.src:1678-1691`).
  - INFERRED: with defaults the first baiter comes after about 192×15 = 2880 frames, about 48 s, on wave 1 unless few enemies remain. Intra-wave escalation shortens it by 12 ticks every 10 s, down to a minimum of 24 ticks.
- **Baiter spawn position:** world X = `BGL + random(0..31)*256`, which is within the visible screen. Random Y (`defb6.src:5-24`).

---

## 8. Humanoids

- **Count: 10** at game start (`defa7.src:1146-1147`). VERIFIED.
- **Restore.** VERIFIED; the planet tie-in is INFERRED.
  - The count is reset to 10 when the new wave number is a multiple of **GA4 "RESTORE WAVE #" (default 5)**, so at waves 5, 10, 15 and so on. GA4 = 0 disables restore (`defa7.src:1851-1863`; default `romc8.src:814`).
  - Otherwise survivors carry over between waves (`defa7.src:1498-1501`, PLSAV).
  - The planet returns whenever the humanoid count is non-zero (STCHK, `defa7.src:1319-1324`).
- **Placement.** VERIFIED.
  - If there are more than 7, they are spread evenly over the 4 quadrants of the planet (`$40` high-byte steps), with the remainder placed randomly.
  - X within each quadrant: high byte random `0-$1F`.
  - Y = `$E0`.
  - Random facing (`defa7.src:1517-1577`).
- **Walking.** The walker visits one slot every 2 frames. VERIFIED.
  - A visited humanoid moves 1 px (`$20` units) horizontally.
  - Its Y steps 1 px toward the terrain altitude +4 (left-facing) or +15 (right-facing), capped at 232.
  - There is a 9/256 chance per visit of turning around (`defb6.src:294-359`).
  - Only humanoids on screen walk.
- **Falling** (AFALL, `defb6.src:927-944`).
  - A released humanoid starts with Y velocity 0.
  - Every 4 frames, `OYV += 8` (1/256 px/frame), staying below `$300`. VERIFIED.
  - On reaching the ground:
    - If `OYV > $E0` (0.875 px/frame), it **dies**.
    - Otherwise it lands safely and scores 250.
  - VERIFIED.
  - INFERRED **safe fall height**: v reaches `$E0` after about 112 frames, and the distance fallen is about t²/256, so **≈ 49 px**.
- **Catch** (AKIL1, `defb6.src:398-417`).
  - Player contact with a falling humanoid scores 500 and attaches it.
  - It then rides at player Y+10 and player world X + 4 px (`defb6.src:945-957`).
  - When the terrain altitude is above the humanoid's Y, it is set down and scores another 500 (`defb6.src:954-962`).
  - Laser fire kills a falling humanoid (no points).
  - VERIFIED.
- **Humanoid blip / sprite colour:** `$6666` (colour 6, grey) (`defa7.src:1519`). VERIFIED.
- **Wave bonus:** see section 6. On the bonus screen the multiplier display is capped at 5 (`defa7.src:1809-1816`).

---

## 9. Enemy AI

### Lander (`defb6.src:652-923`)

VERIFIED unless marked.

- **Spawn.**
  - X velocity: random magnitude 1..LNDXV+1 (RMAX), with random sign.
  - Y velocity: `LNDYV` downward.
  - Each lander picks a target humanoid from the target list.
- **Cruise** (every 6 frames).
  - Hover band: aim for Y = terrain altitude - 50.
    - Descend while above it.
    - Hold (vy = 0) within 20 px below it.
    - Climb if lower than that.
  - Animation: 3 frames.
  - Shooting: RMAX(LDSTIM) ticks between shots.
- **Target lost.** If the target is dead or taken, the lander re-targets. If no humanoids are left, the **lander becomes a mutant** (SCZ00).
- **Grab.** When lander X & `$FC00` equals target X & `$FC00` (the same 32-px band):
  - The lander stops.
  - Each frame it moves 1 px in X toward the target and descends at LNDYV until it is at target Y-12.
  - It latches when |dx| ≤ 2 px.
- **Lift.** Both lander and humanoid get vy = -LNDYV.
  - They rise until the lander Y ≤ `YMIN+8` (50).
  - Then the humanoid is pulled up 1 px per frame into the lander.
  - The humanoid dies, and the **lander turns into a mutant** at the same position.
- **Shot while carrying.** The humanoid starts falling, with a scream sound.

### Mutant (`defb6.src:590-626, 828-901`)

VERIFIED unless marked.

- **Spawn.** Randomly placed but outside ±300 px of the screen-left area (`defb6.src:596-605`). INFERRED: this avoids spawning near the player.
- **X movement:** constant speed `SZXV` toward the player's world X.
- **Y movement (every 3 frames).**
  - When the player is within about -12..+44 px in X: seek the player's Y at `SZYV`.
  - When farther away: avoid the player's Y line. If within 8 px vertically, move away at `SZYV`; otherwise vy = 0.
- **Jitter:** a random ±`SZRY` px hop every tick.
- **Shooting:** shot timer RMAX(SZSTIM) ticks. On-screen only, in the seek branch.

### Bomber (`defb6.src:977-1149`)

VERIFIED unless marked.

- **X:** constant `TIEXV` (1-1.5 px/frame). The direction alternates per squad.
- **Each frame**, one random bomber in the squad gets:
  - a random Y-velocity nudge (±1 picture step, which also changes the image);
  - damping of `vy -= vy/32` (INFERRED).
- **Off screen:** cruise altitude random-walks within 64..104 px, and vy is steered by ±`$10`.
- **On screen:** keeps a vertical distance of 16-32 px from the player.
- **Mines.** With probability 1/8 per tick, the selected bomber lays a mine, if fewer than **10** shells exist.
  - Mines are stationary in the world (zero velocity, world-locked by scroll compensation).
  - Lifetime is `(SEED&31)+1` SHSCAN ticks × 8 frames = 8..256 frames.
  - The mine image alternates and its colour cycles every 6 frames.
  - Mines only collide with the player.

### Pod

- Drifts with a constant random velocity (above).
- When killed, releases **RMAX(6) = 1..7 swarmers** (INFERRED from the RMAX semantics: random ≤ 6, then +1) at its position (`defb6.src:118-124`).
- Global swarmer cap: **20** (`defb6.src:146-150`).
- Blip colour `$CCCC` (cycling).

### Swarmer (`defb6.src:144-288`)

VERIFIED unless marked.

- **X:** constant `SWXV` toward the player.
  - The direction is re-evaluated only when the swarmer gets more than 150 px past the player (`defb6.src:240-244`). INFERRED: it overshoots and comes back.
- **Y (every 3 frames).**
  - Accelerates toward the player's Y by a random per-swarmer value (`HSEED & SWAC`).
  - Clamped to ±2 px/frame.
  - Damping about `vy -= vy/64`.
  - Random noise ±16/256 per tick.
- **Shooting:** only when heading toward the player.
  - The shot's X velocity is the swarmer's vx × 8.
  - The shot's Y velocity is dy/32 per frame.
  - Shot timer RMAX(SWSTIM).

### Baiter (`defb6.src:1-83`)

VERIFIED unless marked.

- Image cycles every 6 frames.
- On each full animation cycle (18 frames), it re-seeks if `SEED > UFOSK`:
  - **vx = player vx ± `$40` (2 px/frame) toward the player**, unless within ±20 px in X.
  - **vy = (player vy ± 1 px/frame)/2 toward the player**, unless within ±10 px in Y.
  - The first seek happens at spawn.
- **Shooting:** shot timer RMAX(UFSTIM) ticks of 6 frames.

---

## 10. Planet destruction and restore

- When the humanoid count reaches 0, the TERBLO process starts (`defb6.src:421-494`). VERIFIED.
  - It sets STATUS bit 1 (terrain inactive) and erases the terrain and the scanner terrain.
  - Then 16 iterations of explosion debris at random X near the screen, with random flash colours and the "lightning" sound.
  - It ends with the terrain-blow sound.
- **After that:**
  - New lander squads spawn as mutants (`LANDST` jumps to the mutant start when ASTCNT=0, `defb6.src:654-656`). VERIFIED.
  - Existing landers become mutants on their next target check. INFERRED.
  - Stars "hyper" (re-randomise Y) only while there is no terrain (`defa7.src:2190-2198`). VERIFIED.
- **Restore** is by the humanoid restore rule (section 8): waves that are multiples of GA4 (default 5).

---

## 11. Lives

- **Ships per game:** CMOS `NSHIP`, default **3** (`romc8.src:802`). VERIFIED.
- **Bonus life:** CMOS `REPLAY`, default BCD `$0100` in the "x100" digits, which is **every 10,000 points** ("REPLAY @10,000", `romc8.src:801`). VERIFIED.
  - Each time the score passes the next level, the level increases by REPLAY.
  - The award is **+1 ship and +1 smart bomb**, with the free-ship sound (`defa7.src:511-532`).
- **Display caps:** 5 ships and 3 bombs (section 4). There is no stated maximum on the internal counts (8-bit). UNKNOWN beyond that.

---

## 12. Enemy shots and collision

### Shots ("shells")

VERIFIED unless marked.

- **Pool:** at most **20** shells total, counting enemy bullets and mines (`defa7.src:2557-2560`).
  - Mines also require fewer than 10 (`defb6.src:1136-1138`).
  - A shell can only be fired from an on-screen object with Y > YMIN.
- **Default lifetime:** 20 SHSCAN ticks (×8 frames = 160 frames). Mines have their own lifetime (`defa7.src:2589-2591`).
- **Death:** a shell dies when it leaves the screen (column ≥ `$98` or Y ≤ YMIN).
- **Coordinates:** shells are kept in screen columns and are compensated for scroll every frame (`defa7.src:2609-2643`).
- **Aimed shot (SHOOT, `defb6.src:534-570`), used by landers, mutants and baiters.**
  - `vx = (player_screen_col + rand(-16..15) - shooter_col) * 4 / 256` columns per frame.
  - `vy = (player_y + rand(-16..15) - shooter_y) * 4 / 256` px per frame.
  - INFERRED: the shot reaches the aim point in **about 64 frames (≈1.07 s)**, so its speed is proportional to distance.
  - In about 53% of shots (`SEED > 120`), the player's X velocity is added (lead / world compensation).
- **Shot image:** the bullet ("fireball") is a 2x3-byte mini image, colour cycling.

### Collision (COLIDE, `defa7.src:2907-3020`)

VERIFIED.

- A bounding-box test, followed by a **pixel-mask overlap** test on byte columns (2 px) and rows: any nonzero byte in both images counts.
- Player against enemies: checked every frame (COLCHK, `defa7.src:3132-3153`).
- Player against shells: checked separately.
- Humanoids do not kill the player, except a falling one, which is caught.

---

## 13. Terrain and stars

### Terrain

- A fixed table of **256 bytes = 2048 bits**. **Each bit is a 1-px horizontal step that moves the ground 1 px up (bit = 1) or down (bit = 0)** (`blk71.src:384-397, 510-525`). VERIFIED.
- The profile starts at Y = 224 ($E0) and, per my decode, returns to 224 after 2048 px, with Y in the range 159..233 (INFERRED).
- The **circumference is 2048 px**, matching the 16-bit world.
- An altitude lookup table of 1024 entries, one per 2 px, is used for humanoid walking, falling and lander hover (`blk71.src:375-400`).
- Ground colour: nibble colour 7 ("BROWN" = `$15`) (`blk71.src:252, 266`; `defb6.src:1883`). VERIFIED.
- The scanner uses a separate pre-computed mini-terrain table (MTERR, 64 columns × 3 bytes) (`amode1.src:1199-1226`, `1280-1311`). VERIFIED.

### Stars

VERIFIED unless marked.

- **16 stars** (`phr6.src:541`).
- X: random in 0..`$9B` columns. Y: 43..168 (`defa7.src:2073-2093`).
- **Parallax:** stars move 1 column (2 px) for every 128 world units (4 px) of scroll, opposite to the direction of travel. INFERRED: half the terrain scroll speed (`defa7.src:2097-2115`).
- They wrap at the screen edges.
- Each frame, one random star changes colour (nibble +1, through colours 0-7) and is sometimes repositioned (`defa7.src:2159-2198`).
- The number of active stars drops to 3 when the CPU is overloaded (`defa7.src:3062-3065`).

---

## 14. Scanner (`amode1.src:1180-1276`)

VERIFIED unless marked.

- **Size:** 64 byte-columns (128 px) wide, rows about 8-39.
- **Window:** the scanner's left edge is world X `BGL + 150 px - 1024 px`, so the **whole 2048-px planet is shown, centred on the middle of the visible screen**.
- **Scale.**
  - X: `(OX16 - left) >> 10`, which is 1 scanner column (2 px) per 32 world px, a **1:16** scale.
  - Y: `OY16/8 + 7`, a **1:8** scale.
- **Blips:** each is 1 byte wide × 2 rows (2x2 px), using the object's `OBJCOL` word. The top byte is the upper row and the second byte the lower row.

| Object | OBJCOL | Colours (upper row / lower row) | Citation |
|---|---|---|---|
| Lander | `$4433` | yellow / green | `defb6.src:659` |
| Mutant | `$CC33` | cycling / green | `defb6.src:594` |
| Baiter | `$3333` | green | `defb6.src:7` |
| Pod | `$CCCC` | cycling | `defb6.src:88` |
| Swarmer | `$2424` | red/yellow per pixel | `defb6.src:153` |
| Bomber | `$8888` | purple | `defb6.src:997` |
| Humanoid | `$6666` | grey | `defa7.src:1519` |

- **Player blip:** white (colour 9) pattern (`amode1.src:1244-1259`).
- **Screen-window markers:** "screen marker" brackets in colour 9 (`defa7.src:922-928`, `amode1.src:1228-1235`).
- **Border colour:** colour 5. Its palette value is set per wave from `WCTAB[wave&7]` = `$81,$28,$07,$16,$2F,$84,$15` (waves 1-7); the 8th entry falls on the preceding 0 byte (`defa7.src:1262-1266, 1436`, `906-921`). VERIFIED. INFERRED that the border is the visible wave-colour cue.

---

## 15. Colour palette

VERIFIED unless marked.

- **16-entry pseudo palette** (`PCRAM`), copied to the hardware colour RAM every frame (`defa7.src:1969-1980`).
- **Format:** 8-bit **BBGGGRRR**. INFERRED from the values: `$07` = red, `$28` = green, `$2F` = yellow, `$81` = blue, `$FF` = white.

**Defaults (CRTAB, `defb6.src:1876-1891`):**

| Index | Use | Value |
|---|---|---|
| 0 | background | `$00` |
| 1 | laser (cycled) | — |
| 2 | red | `$07` |
| 3 | green | `$28` |
| 4 | yellow | `$2F` |
| 5 | blue, per-wave | `$81` |
| 6 | grey | `$A4` |
| 7 | brown | `$15` |
| 8 | purple | `$C7` |
| 9 | white | `$FF` |
| A | bomb cycler | — |
| B | monochrome (player death glow) | — |
| C | cycler | — |
| D, E, F | bomber colours | — |

**Cycling:**

- Laser colour 1 steps through a 36-entry table every 2 frames (`defa7.src:3024-3042`).
- Colours A and C get a random table colour every 6 frames (`defb6.src:1213-1228`).
- Bomber colours D, E, F rotate through the triples (`$81,$81,$2F`), (`$81,$2F,$07`), (`$2F,$81,$07`) every 6 frames (`defb6.src:1195-1209`).

**Flashes:**

- Smart bomb: background flash (section 4).
- Planet explosion: random background flashes.
- Player death:
  - The ship is redrawn monochrome in colour B, stepping through `7,7,7,$F,$3F,$7F,$FF,$FF` every 4 frames with a black background (`defa7.src:1341-1377, 1434`).
  - Then a one-frame white flash.
  - Then a 128-piece explosion that fades through `$FF,$7F,$3F,$37,$2F,$27,$1F,$17,7,6,5,4,3,2,0` (`blk71.src:566-672`).

---

## 16. Sound commands

The main CPU sends a 6-bit sound number, written inverted and masked to `$3F` (`defa7.src:696-704`).

The main CPU uses scripts with priorities. The format is `priority, then (repeat, frames, sound#)...`, with frame counts in 16 ms units (`defa7.src:659-691`). VERIFIED.

| Script | Priority | Sound # | Used for |
|---|---|---|---|
| CNSND coin | $FF | $19 | coin |
| RPSND free ship | $FF | $1E | extra life |
| PDSND player death | $F0 | $11 ×2, then $17 | player death |
| ST1SND | $F0 | $0A | 1-player start |
| ST2SND | $F0 | $0B | 2-player start |
| TBSND terrain blow | $E8 | $14, $11, $17 | planet destroyed |
| SBSND smart bomb | $E8 | $11 ×6, $17 | smart bomb |
| ACSND | $E0 | $08 | humanoid caught |
| ALSND | $E0 | $1F | humanoid landed |
| AHSND | $E0 | $11 | humanoid hit; also the planet "lightning" |
| ASCSND | $D8 | $1A | humanoid scream (falling) |
| APSND | $D0 | $15 | enemy appear |
| PRHSND | $D0 | $05 | pod hit |
| SCHSND | $D0 | $17 | mutant hit |
| UFHSND | $D0 | $07 | baiter hit |
| TIHSND | $D0 | $01 | bomber hit |
| LHSND | $D0 | $06 | lander hit |
| LPKSND | $D0 | $0B | lander picks up humanoid |
| LSKSND | $C8 | $0E | lander "suck" |
| SWHSND | $C0 | $07 | swarmer hit |
| LASSND | $C0 | $14 | laser |
| LGSND | $C0 | $18 | lander grab (defined; use not traced) |
| LSHSND | $C0 | $03 | lander shoot |
| SSHSND | $C0 | $09 | mutant shoot |
| USHSND | $C0 | $03 | baiter shoot |
| SWSSND | $C0 | $0C | swarmer shoot |

Direct writes:

- Thrust on: `$16`.
- Thrust off: `$0F`.
- `$13` on death and game over ("OFF SOUND").
- `$12` once per frame while a humanoid is being pulled into a lander (`defa7.src:737-752, 1385, 1429`; `defb6.src:820`).

VERIFIED.

**Sound-board dispatch** (`vsndrm1.src:940-958, 1003-1013, 1107-1121`). VERIFIED code; the exact mapping is INFERRED. It is consistent: APPEAR=$15, THRUST=$16, SCREAM=$1A and BGEND=$13 all line up with the main-CPU uses.

| Input | Routine |
|---|---|
| $01-$0D | "GWAVE" waveform sounds, from a 13-entry parameter table |
| $0E | SP1 |
| $0F | BG1 |
| $10 | BG2INC |
| $11 | LITE |
| $12 | BON2 |
| $13 | BGEND |
| $14 | TURBO |
| $15 | APPEAR |
| $16 | THRUST |
| $17 | CANNON |
| $18 | RADIO |
| $19 | HYPER |
| $1A | SCREAM |
| $1B | ORGANT |
| $1C | ORGANN |
| $1D-$20 | "VARI" sounds: SAW, FOSHIT, QUASAR, CABSHK |

The synthesis parameters are Williams data. Do not copy them; design sounds by ear.

---

## Open items / UNKNOWN

- Exact game behaviour of RAND (the 3-byte LFSR plus a multiplicative mix, `defa7.src:945-962`). It matters only for exact replay; a modern RNG is fine.
- Mutant sprite colours: the sprite nibbles were not decoded (deliberately; that is asset data).
- No maximum on the internal life or bomb counter was found.
- Whether rammed enemies score was inferred, not traced at runtime.
- The attract mode and high-score entry were not studied.
