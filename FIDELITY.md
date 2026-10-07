# Fidelity

## Reference version

The baseline is the **MAME `defender` set (Red Label, Williams 1980/81)**. Its mechanics come from the Red Label source code, which was used for facts only; its hardware characteristics come from MAME. See RESEARCH.md for the evidence and sources.

### Labels

Every row in the matrix below has one of these labels:

- **V (verified):** read directly in the original source, MAME or the manual.
- **I (inferred):** derived from that code by reasoning or arithmetic.
- **R (reconstruction):** our assumption, used where the evidence is silent.
- **M (modern addition):** not in the original.

## Presets

Classic and Modern run the **same engine, wave tables, physics and scoring**. Every difference is an explicit field on `GamePolicy` (Core) or a presentation setting. **Modern mode cannot change wave content, physics or scoring.**

| | Classic | Modern |
|---|---|---|
| Fire | One shot per press (V) | Same by default. Optional **hold-to-fire**, 1 shot per 8 frames (M, opt-in, disclosed in the settings) |
| Scanner refresh | Every 8 frames, like the original SCPROC (V) | Every frame (M) |
| Flashes / colour cycling | As the original (V/I) | Optional suppression: no background flashes, and colour cycling slowed to 1/4–1/5 speed (M) |
| Game speed | 100% | 50–100%, an accessibility option. Shown on the HUD as `M 0.7X` (M) |
| Scaling | Nearest-neighbour | Nearest-neighbour or optional bilinear (M) |
| Pause | Allowed. Pause does not alter rules or timing, but the cabinet had no pause (M, disclosed) | Allowed; also auto-pauses when the window loses focus (M) |
| Suspend / resume | None. High scores only, as on the arcade | One suspend slot, written on close mid-game and **consumed on resume** (M) |
| High scores | Own top-10 table (R: the arcade had two tables of 8) | Separate top-10 table |

## Fidelity matrix

Columns:

- **System:** the game system.
- **Original behaviour:** what the arcade does, with its evidence label.
- **Implementation:** what we built.
- **Mode:** whether Classic and Modern differ. "Same" means both presets behave identically.
- **Verification:** the test that checks it.
- **Uncertainty:** what remains unknown.

| System | Original behaviour (evidence) | Implementation | Mode | Verification | Uncertainty |
|---|---|---|---|---|---|
| Frame rate | 60 Hz executive; raster about 60.1 Hz (V) | Fixed 60 Hz ticks, decoupled from rendering (`FixedStepScheduler`) | Same; Modern speed ≤ 1.0 | `SchedulerAndWrapTests` | 0.16% raster drift ignored |
| Screen | 304-px-wide frame; visible area 292×240 at (12,7) (V, MAME) | Game coordinates 304×256, cropped to 292×240 | Same | `UiSmokeTests` aspect ratio | none |
| World wrap | 16-bit X, 32 units/px, 2048 px (V/I) | Integer world units masked with `&0xFFFF` | Same | `WorldWrap_IsSeamless_InBothDirections`, `Wrap_*`, `Delta_*` | none |
| Terrain | Fixed 2048 × ±1 px steps, Y 159–233, same every game (V) | **Generated** with the same constraints from a fixed seed. The Williams table is not copied | Same | `Terrain_*` tests | The profile differs from the arcade's (R, deliberate) |
| Stars | 16 stars; ½ parallax (2 px per 4 px of scroll); one random star recolours each frame (V/I) | As described | Same | visual | Colour-cycling details are approximate |
| Thrust/drag | V −= V/64; +3/frame²; clamp 256 (V) | Exact 24-bit integer arithmetic | Same | `Thrust_*`, `Drag_*` | none |
| Ship screen X / reverse | Base 64/224 px plus a lead of V/4 px. Slide 2 px/frame with scroll compensation. Reverse on press only, 5-frame re-arm (V) | Exact | Same | `Reverse_*`, `ShipLeadsForward_AtSpeed`, `ScrollSpeed_*` | Re-arm uses a 5-frame cooldown rather than "release + 5" (R, equivalent with edge input) |
| Vertical | No inertia; 1 → 2 px/frame (+8/256 per frame); Y 43–238 (V) | Exact | Same | `Vertical_*` | none |
| Laser | ≤ 4 shots; edge-triggered; head 8 px/frame; screen space; one kill each (V) | Exact. The fizzle and tail rates are visual only | Hold-to-fire in Modern only (M) | `LaserTests` | The original tests an 8×1 probe; ours sweeps the head segment (I) |
| Smart bomb | 3 at start; +1 per extra life; kills on-screen enemies that are not materialising; humanoids and shells unaffected; 2 white flashes; 10-frame re-arm (V) | Exact | Flash suppressible in Modern | `SmartBomb_*` | none |
| Hyperspace | Blank 15 frames; clear shells; random BGL, facing and Y; velocity 0; appear for 40 frames; **die if rnd > 192** (V) | Exact | Same | `Hyperspace_*` (statistical: 2000 trials, expected 24.6%, bound 21–28%) | Uses a different RNG |
| Enemy shots | ≤ 20 shells (bullets and mines); aimed to arrive in about 64 frames with ±16 px jitter; adds player velocity on ~53% of shots; life 160 frames (V/I) | As described | Same | `EnemyShot_IsAimed_AndPooledTo20` | none significant |
| Collision | Bounding box, then pixel mask at 2 px horizontal resolution (V) | Bounding box, then pixel mask at 1 px | Same | Laser and ram tests | Slightly finer than the original (R) |
| Lander | Spawns at top with random vx (1..LNDXV+1) and vy = LNDYV; hovers at terrain − 50 (±20 px band); 6-frame AI tick; targets a walking humanoid; descends in the same 32-px band; latches; lifts to Y 50; absorbs at 1 px/frame; becomes a mutant (V) | As described | Same | `Lander_Descends_Grabs_Lifts_AndBecomesMutant` | If the carried humanoid is shot, the lander resumes cruising (R) |
| Mutant | Constant SZXV toward player; Y seeks player inside the −12..+44 px window, otherwise dodges the player's line; random ±SZRY hop; 3-frame tick (V/I) | As described | Same | `Mutant_HomesTowardsPlayer` | Window sign convention (I) |
| Bomber | Squads of ≤ 3; direction alternates per squad; one random member is updated per frame; mines with 1/8 chance; ≤ 10 mines; mines are world-fixed with 8–256-frame life (V/I) | As described | Same | `Bomber_LaysStationaryMines_UpToCap` | Nudge magnitude and squad spacing (R) |
| Pod / Swarmer | Pod drifts; on death releases 1..7 swarmers (cap 20); swarmers overshoot and turn back after 150 px; aimed shots only while heading toward the player (V/I) | As described | Same | `Pod_Releases1To7Swarmers`, `Swarmers_CappedAt20` | none |
| Baiter | Timer UFOTIM (×15 frames), shortened when ≤ 8 or ≤ 3 enemies remain; ≤ 12 alive; matches player velocity ± 2 px/frame; re-seeks when rnd > UFOSK; does not count toward wave end (V) | As described | Same | `Baiter_*` (first appears at about 192 executive ticks ≈ 48 s) | none |
| Humanoids | 10; spread over 4 quadrants; walk only on screen, 1 slot per 2 frames; 9/256 turn chance (V) | As described | Same | `StartsWith10Humanoids_*` | none |
| Falling / catch | +8/256 px/frame every 4 frames; lands dead if speed > 0.875 px/frame; safe drop about 49 px; catch 500, set down 500, safe solo landing 250 (V/I) | As described | Same | `SafeFallHeight_IsAbout49Px`, `ShortFall_*`, `LongFall_*`, `ShootingAbductor_*` | none |
| Planet explosion | When the last humanoid is lost: terrain removed, 16 debris bursts, landers become mutants, new squads arrive as mutants. Restored on waves that are multiples of 5 (V) | As described | Flashes suppressible | `LosingLastHumanoid_*`, `Planet_And10Humanoids_RestoredOnEveryFifthWave` | Debris visuals are approximate (R) |
| Waves | WVTAB: 22 variables; base values for waves 1–4; inter-wave deltas applied (wave − 4 + difficulty 5) times, up to a ceiling of 15; intra-wave deltas every 10 s; deltas that would pass a limit are skipped (V) | Data-driven `WaveTable`, validated on load | Same | `WaveParams_EffectiveValues_*`, `WaveTable_RejectsMalformedData`, `Wave1_*`, `Landers_ArriveInSquadsOf5_*` | none |
| Wave end | All Landers, Mutants, Bombers, Pods and Swarmers gone, alive and in reserve; Baiters excluded (V) | As described, followed by a bonus screen | Same | `WaveClears_*` | Bonus-screen timing is approximate (R) |
| Scoring | See RESEARCH.md (V) | Exact | Same | `KillScores_MatchOriginal`, humanoid tests | Points on ramming and for a shot hitting the player (25) are I |
| Lives | 3 ships; +1 ship and +1 smart bomb every 10,000; HUD shows ≤ 5 ships and ≤ 3 bombs (V) | Exact | Same | `BonusShipAndSmartBomb_Every10000`, `StartsWith3Ships_*` | No hidden cap known |
| Player death | Ship glows (8 steps × 4 frames), white flash, 128-piece explosion (V); world freezes (I) | Glow, flash, 96-particle burst; survivors return to reserves (R) | Flash suppressible | `Ramming_*`, `LosingAllShips_*` | Exact restart flow (R) |
| Scanner | 128 px wide; whole planet centred on the screen middle; 1:16 X scale; Y/8 + 7; 2×2 two-colour blips; window brackets; mini terrain (V) | Exact geometry; colours from our palette | Classic every 8 frames; Modern every frame | visual | Mini-terrain sampling is ours (R) |
| Palette | 16-entry pseudo-palette of BBGGGRRR bytes; cycling laser, A, C and bomber slots; per-wave border colour (V) | The same mechanism with **our own** colour tables and cycle sequences | Cycling slowed when suppressed | visual | Exact colour tables were not copied (R) |
| Sprites | Sizes: ship 16×6, lander 10×8 ×3, mutant 10×8, baiter 12×4, bomber 8×8, pod 8×8, swarmer 6×4, humanoid 4×8, mine 4×3 (V) | **Original pixel art** at those sizes | Same | `SoftwareRenderer_DrawsEveryStateHeadlessly` | Shapes are approximations (R) |
| Flicker | No evidence of sprite flicker in the Red Label code; the star count drops when the CPU is overloaded (V) | No artificial flicker added | Modern suppresses rapid colour cycling | — | none |
| Sound | Williams 6808 + DAC board; about 30 commands; thrust on/off; no speech (V) | **Synthesised** original approximations (no samples); priority mixer | Same | `AudioTests` | Timbre is approximate by design (R) |
| RNG | 3-byte LFSR with a mix step (V) | xorshift32, injectable and serialisable | Same | `Replay_IsDeterministic_*` | Not byte-exact with MAME (R) |
| Attract mode | Logo, scoring legend, high scores, demo flight | Title, scoring legend, Hall of Fame. No demo flight (the `--autoplay` flag is a diagnostic) | Same | visual | Demo flight deferred |
| High scores | 2 tables × 8 entries, initials entered with Up/Down and Fire (V) | 1 top-10 table per preset; Up/Down and Fire, or type the initials | Same | `HighScoreTableTests`, `LosingAllShips_*` | Count differs (per the brief) |
