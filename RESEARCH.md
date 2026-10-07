# Research dossier

Research for the *Defender* (Williams Electronics, 1980/81) recreation. Accessed 2026-10-07.

The full working dossiers, with every value cited to file and line, are archived here:

- [`docs/research/source-mechanics-dossier.md`](docs/research/source-mechanics-dossier.md): game mechanics read from the original Red Label 6809 source code.
- [`docs/research/hardware-docs-dossier.md`](docs/research/hardware-docs-dossier.md): hardware details (from MAME), operator manuals, history, home ports, open-source versions, NuGet checks, and a 25-row source ledger.

This page summarises those dossiers. Where this page and a dossier disagree, the dossier's citation is the authority.

## Coverage and method

**Primary sources**
- **The original source code.** This is the Red Label 6809 assembly, as mirrored in
  - `github.com/mwenge/defender` @ `e74006344b16d19245510b5e55780f90d7606bb8`, and
  - `github.com/historicalsource/defender` @ `3fae9d3`.

  The two copies were checked against each other and contain the same game logic. Both repositories have **no licence**, and the source carries Williams copyright notices. We took **facts only**: numbers, rules and timings. **No code, sprite data, terrain data or sound data was copied.**
- **MAME** `src/mame/williams/williams.cpp` and `williams_v.cpp` @ `36818916`, licensed BSD-3. These give the clocks, screen, visible area and palette resistor network.
- **Williams operator manuals** on archive.org:
  - Later Series Setup Booklet 16P-3001-103 (July 1981)
  - Early Series 16P-3000-103
  - Theory of Operation 16P-3001-301

**Secondary sources:** Wikipedia, KLOV, a *Game Developer* retrospective, and a transcribed Atari 2600 manual. The 2600 manual was used only to identify home-port behaviour that must **not** be imported.

**Gaps (what we did not inspect)**
- We did not watch longplay video or step through frames in MAME. Timings come from the code instead.
- We did not read contemporary trade press (RePlay, Play Meter, Cash Box).
- arcade-history.com returned 403.
- We saw no image of the instruction card or the control panel.
- We did not search freesound.org.

## Reference version

| Item | Decision |
|---|---|
| Baseline | **MAME set `defender` (Red Label)**, the parent set. The MAME driver notes that Red was the last revision, with "much improved enemy AI". The source mirror is also Red Label. |
| Other sets | `defenderg` (Green) and `defenderb` (Blue) differ only in chip types. `defenderw` (White) was the first release. `defenderj` is the Taito licence. |
| Cocktail cabinet | There is no separate ROM set. Screen flip is bit 0 of `video_control_w`. Only Red Label supports cocktail. Not implemented (single-player upright). |
| Early-series differences | Background-sound setting; difficulty on a 0-2 scale; wave limit 10. Not modelled. A claim that early Landers scored 100 is **unverified**. |

## Key findings (summary)

### Hardware and presentation

| Topic | Finding | Source |
|---|---|---|
| CPUs and audio | MC6809E at 1 MHz. Sound board: M6808 driving a single MC1408 8-bit DAC. | MAME |
| Refresh rate | Raster about 60.096 Hz. | MAME |
| Game loop | Runs once per 16 ms frame. | source |
| Visible area | **292×240** = `set_visarea(12,303,7,246)` inside a 304-pixel-wide game frame. | MAME |
| Other resolution claims | Wikipedia says 320×256, which is wrong. The Theory of Operation manual says "360×240". | |
| Palette | 8-bit BBGGGRRR, 16 entries at a time. | MAME + source |
| Palette levels | R/G: 0, 38, 81, 118, 137, 174, 217, 255. B: 0, 95, 160, 255. | MAME resistor network |
| Speech | Defender has **no speech**. | MAME, source |

### World and ship

| Topic | Finding | Source |
|---|---|---|
| World | 16-bit X coordinate at 32 units per pixel, so the **planet is 2048 px around**. | source |
| Terrain | A fixed 2048-step bit table, ±1 px per step. Starts at Y = 224; ranges over Y 159-233. | source |
| Ship drag | V −= V/64 per frame. | source |
| Ship thrust | +3 units/frame². Top speed is about 6 px/frame. | source |
| Ship screen position | Sits at X 64 facing right, or X 224 facing left, plus a speed lead. Reversing slides it across at 2 px/frame. | source |
| Vertical movement | No inertia. 1 px/frame at first, rising to 2 px/frame. | source |
| Laser | One shot per press, at most 4 on screen. The head travels 8 px/frame in screen space. Each shot kills one target. | source |
| Smart bomb | Starts at 3; +1 with each extra life. Kills every enemy on screen with normal scoring. Leaves humanoids and shots alone. 2 white flashes (resolved below). | source |
| Hyperspace | Random destination. **Death chance is 63/256 (about 24.6%) on every use.** | source |

### Scoring, waves and enemies

| Topic | Finding | Source |
|---|---|---|
| Enemy scores | Lander 150, Mutant 150, Swarmer 150, Baiter 200, Bomber 250, Pod 1000. Matches the attract-mode text. | source |
| Humanoid scores | Catch 500, set down 500, lands safely alone 250. | source |
| Wave bonus | 100 × min(wave, 5) per surviving humanoid. | source |
| Extra life | Every 10,000 points. | source, manual |
| Wave table | 22 variables. Wave 1 has 15 Landers only. From wave 4 on: 20 Landers, 5 Bombers, 4 Pods. Difficulty 5 and ceiling 15 by default. | source, manual |
| Humanoids | 10 of them. Refilled every 5th wave; survivors carry over in between. | source |
| Baiters | First one after about 48 s on wave 1, sooner when few enemies remain. Up to 12 at once. | source |
| Enemy AI | Hover heights, timings and the abduction state machine are all recorded in the dossier. | source |
| High scores | Two tables, "Today's" and "All-time", **8 entries each**, with 3 initials. | source, manual |

## Disagreements and how they were resolved

- **Smart-bomb flash count.**
  - One dossier reported 2 white flashes; the other reported 4.
  - We re-read `defa7.src` `SBOMB`: `LDA #4 ; SCREEN FLASHES/2`, followed by a loop of 4 × (`COM PCRAM`, `NAP 2`). That is 4 inversions, which gives **2 white flashes**. The comment's "/2" caused the other reading.
  - **We implement 2 flashes.**
- **Screen resolution.** MAME's visible area (292×240) is used. The manual's "360×240" probably describes the full horizontal scan rather than the visible picture.
- **Release date.** Wikipedia gives an AMOA showing in September 1980. A search snippet attributed to Jarvis gives Chicago, 31 October 1980. **Unresolved.** It does not affect gameplay.
- **High-score table size.**
  - The original keeps 2 tables of 8 entries.
  - The project brief asks for a top 10.
  - We keep a top 10 per preset and document the difference (see FIDELITY.md).

## Home ports: do not import

| Port or source | Differences from the arcade |
|---|---|
| Atari 2600 | Fire, smart bomb and hyperspace are chosen by the ship's height; heavy flicker; a city skyline instead of mountains. |
| Circulating `DEFENDER.TXT` | This is the Atari 2600 manual. It lists Swarmer 200 and catch/return 500/1000, which are **not** arcade values. |

None of this behaviour is used.

## Source-fidelity audit (second pass)

After the first implementation we ran a line-by-line comparison of our engine against the original routines. It found 18 discrepancies, all now fixed and covered by `AuditRegressionTests`. It also resolved most open questions:

| Question | Answer from the source |
|---|---|
| Can walking humanoids be shot? | **Yes.** The laser scan does not skip humanoids; `ASTKIL` ignores only contact with the ship (defb6.src:384-397). The hit gives no points and can blow up the planet. |
| Does ramming score the enemy? | **Yes.** COLIDE calls the enemy's own kill routine before the player dies (defa7.src:3132-3152). |
| Does a shot hitting the player score 25? | **Yes.** The shell kill routine `BKIL` scores $25 (defa7.src:2700-2702, 3143-3146). |
| Mutant seek window | `player − mutant` within −380..+1412 units (≈ −12..+44 px) (defb6.src:855-858). |
| Bomber squad layout | Members $180 units apart at player X + $8000, Y = cruise altitude = $50, no materialise; nudge −32..+31 (defb6.src:977-1116). |
| Death and restart (PLSAV/PLRES) | Counts are saved at the hit. Swarmers, mutants, pods and bombers are re-created at the next life; landers stay in reserve; humanoids are re-placed every life (defa7.src:1491-1585). |
| Death timing | 32-frame glow during which enemies keep moving; 2 white frames; ~108 explosion frames; then (2-player only) "PLAYER n" for 128 frames and a 96-frame pause. |

## Open questions

These are also tracked in KNOWN_ISSUES.md.

1. **Random-number generator.** The exact RNG (a 3-byte LFSR) is not replicated. We use xorshift32. This matters only for byte-exact replay against MAME.
2. **Smart bomb vs materialising enemies.** Whether the bomb hits enemies that are still materialising is unresolved. Ours: they are immune.
3. **Bomber off-screen altitude correction.** As written, it pushes bombers *away* from their cruise altitude, so they drift and wrap vertically. We implement it as written; it may be an original quirk.
4. **Attract-mode demo.** The original attract-mode demo flight was not studied.
