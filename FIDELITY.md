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
| Scanner blips | 2×2 (V) | Optional bold 3×3 blips (M) |
| Control reminder | None (title-screen hints only) | Optional one-line reminder bar below the picture (M) |
| Pause | Allowed. Pause does not alter rules or timing, but the cabinet had no pause (M, disclosed) | Allowed; also auto-pauses when the window loses focus (M) |
| Suspend / resume | None. High scores only, as on the arcade | Three selectable slots; written on close mid-game and **consumed on resume** (M) |
| High scores | Today's (8, resets daily) + All-Time (10; R: the arcade's was 8) | Separate tables of the same shape |

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
| Laser | ≤ 4 shots; edge-triggered; head 8 px/frame, fizzle 6, tail 2; screen space; ends at column $98 or ≤ 5; each frame tests the 16×1 probe LASP1 (right: 12 px behind to 4 px ahead of the head; left: 16 px from the head); one kill each (V) | As described | Hold-to-fire in Modern only (M) | `LaserTests`, `LaserProbe_Is16x1_PlacedPerDirection` | Masks tested at 1-px rather than 2-px resolution; nearest object wins instead of object-list order (R, rarely differs) |
| Smart bomb | 3 at start; +1 per extra life; kills on-screen enemies that are not materialising (appearing objects carry OTYP bit 1; appear runs only on screen); humanoids and shells unaffected; 2 white flashes; 10-frame re-arm (V) | Exact | Flash suppressible in Modern | `SmartBomb_*` | none |
| Hyperspace | Blank 15 frames with all movement frozen; the wave may still end; clear shells; random BGL, facing and Y; velocity 0; appear for 40 frames; **die if rnd > 192** (V) | Exact | Same | `Hyperspace_*` (statistical: 2000 trials, expected 24.6%, bound 21–28%) | Uses a different RNG |
| Enemy shots | ≤ 20 shells (bullets and mines), all screen-space: **mines die once off screen**; aimed to arrive in about 64 frames with ±16 px jitter; adds player velocity on ~53% of shots; life 160 frames (V/I) | As described | Same | `EnemyShot_IsAimed_AndPooledTo20` | none significant |
| Collision | Bounding box, then pixel mask at 2 px horizontal resolution (V) | Bounding box, then pixel mask at 1 px | Same | Laser and ram tests | Slightly finer than the original (R) |
| Lander | Spawns at top with random vx (1..LNDXV+1) and vy = LNDYV; hovers at terrain − 50 (±20 px band); 6-frame AI tick; targets a walking humanoid; descends in the same 32-px band, **shooting every frame**; latches; lifts to Y 50, **shooting every 4 frames**; absorbs at 1 px/frame; becomes a mutant. A lander that reaches the top without its humanoid is removed unscored and returned to the reserve; one that loses its target while descending keeps vx = 0 (V) | As described | Same | `Lander_*` tests, `AuditRegressionTests` 4–6 | none |
| Mutant | Constant SZXV toward the player. Seeks the player's Y when `player − mutant` is within −380..+1412 units; otherwise dodges the player's line. Exactly ±SZRY hop each tick and the shot timer run in the avoid branch, and in the seek branch only when on screen. 3-frame tick (V) | As described | Same | `Mutant_HomesTowardsPlayer`, `AuditRegressionTests` 2, 3, 13 | none |
| Bomber | Squads of ≤ 3, $180 units apart at player X + $8000, Y $50, no materialise; direction alternates per squad. Each frame one of 4 squad slots is picked (empty = no update): nudge −32..+31, damping V/32, keep 16–32 px from the player when on screen. Mines only when on screen, 1 in 8 updates, < 10. Off screen, cruise-altitude random walk with a correction that pushes away from it (V; last point I) | As described | Same | `Bomber_LaysStationaryMines_OnlyOnScreen_UpToCap`, `BomberSquad_*` | Off-screen correction direction (I) |
| Pod / Swarmer | Pod: X high byte $10–$4F, Y rnd/2 + YMIN, vx −32..31, \|vy\| 32–64. On death releases 1..7 swarmers exactly at the pod (cap 20), vy = signed rnd × 2, random first nap. Swarmers overshoot and turn back after 150 px and fire only toward the player. Restored swarmers come in clusters of ≤ 6, 1024–1535 px ahead (V) | As described | Same | `Pod_*`, `Swarmers_*`, `AuditRegressionTests` 14, 18 | none |
| Baiter | Timer UFOTIM (×15 frames), shortened when ≤ 8 or ≤ 3 enemies remain; ≤ 12 alive. On each 18-frame cycle, re-seeks when rnd > UFOSK: vx = player vx ± 2 px/frame unless within 20 px, vy = (player vy ± 1 px/frame)/2 unless within 10 px; a close axis keeps its velocity. First shot timer 8. Not counted for wave end (V) | As described | Same | `Baiter_*`, `AuditRegressionTests` 17 | none |
| Humanoids | 10; spread over 4 quadrants; **re-placed (same count) at every life and wave start**; walk only on screen, 1 slot per 2 frames; 9/256 turn chance; **the laser kills them in any state** (no points) (V) | As described | Same | `StartsWith10Humanoids_*`, `AuditRegressionTests` 1, 7 | none |
| Falling / catch | +8/256 px/frame every 4 frames; lands dead if speed > 0.875 px/frame; safe drop about 49 px; catch 500, set down 500, safe solo landing 250 (V/I) | As described | Same | `SafeFallHeight_IsAbout49Px`, `ShortFall_*`, `LongFall_*`, `ShootingAbductor_*` | none |
| Planet explosion | When the last humanoid is lost: terrain removed, 16 debris bursts, landers become mutants, new squads arrive as mutants. Restored on waves that are multiples of 5 (V) | As described | Flashes suppressible | `LosingLastHumanoid_*`, `Planet_And10Humanoids_RestoredOnEveryFifthWave` | Debris visuals are approximate (R) |
| Waves | WVTAB: 22 byte rows; base values for waves 1–4; inter-wave deltas applied (wave − 4 + difficulty 5) times, up to a ceiling of 15; intra-wave deltas every 10 s, with the count restarting each life; deltas that would pass a limit or overflow a byte are skipped, and LNDYV/SZYV never carry into their high byte (V) | Data-driven `WaveTable`, validated on load | Same | `WaveParams_*`, `WaveTable_RejectsMalformedData`, `AuditRegressionTests` 10 | none |
| Wave end | All Landers, Mutants, Bombers, Pods and Swarmers gone, alive and in reserve; Baiters excluded. Bonus screen: cleared screen, one humanoid counted per 4 frames, then 128 frames (V) | As described | Same | `WaveClears_*`, `WaveBonusScreen_*` | none |
| Scoring | See RESEARCH.md, including points for ramming and 25 when a shot hits the player (V) | Exact | Same | `KillScores_MatchOriginal`, humanoid tests | none |
| Lives | 3 ships; +1 ship and +1 smart bomb every 10,000; HUD shows ≤ 5 ships and ≤ 3 bombs (V) | Exact | Same | `BonusShipAndSmartBomb_Every10000`, `StartsWith3Ships_*` | No hidden cap known |
| Player death | Ship glows (8 steps × 4 frames) **while enemies keep moving**; 2 white frames freeze everything; ~108-frame, 128-piece explosion. Counts go back to the reserves; a death that empties the wave pays the wave bonus first (V) | As described (142 frames) | Flash suppressible | `Ramming_*`, `LosingAllShips_*`, `AuditRegressionTests` 8 | none |
| Scanner | 128 px wide; whole planet centred on the screen middle; 1:16 X scale; Y/8 + 7; 2×2 two-colour blips; window brackets; mini terrain (V) | Exact geometry; colours from our palette | Classic every 8 frames; Modern every frame | visual | Mini-terrain sampling is ours (R) |
| Palette | 16-entry pseudo-palette of BBGGGRRR bytes; cycling laser, A, C and bomber slots; per-wave border colour (V) | The same mechanism with **our own** colour tables and cycle sequences | Cycling slowed when suppressed | visual | Exact colour tables were not copied (R) |
| Sprites | Sizes: ship 16×6, lander 10×8 ×3, mutant 10×8, baiter 12×4, bomber 8×8, pod 8×8, swarmer 6×4, humanoid 4×8, mine 4×3 (V) | **Original pixel art** at those sizes | Same | `SoftwareRenderer_DrawsEveryStateHeadlessly` | Shapes are approximations (R) |
| Flicker | No evidence of sprite flicker in the Red Label code; the star count drops when the CPU is overloaded (V) | No artificial flicker added | Modern suppresses rapid colour cycling | — | none |
| Sound | Williams 6808 + DAC board; about 30 commands; thrust on/off; no speech (V) | **Synthesised** original approximations (no samples); priority mixer | Same | `AudioTests` | Timbre is approximate by design (R) |
| RNG | 3-byte LFSR with a mix step (V) | xorshift32, injectable and serialisable | Same | `Replay_IsDeterministic_*` | Not byte-exact with MAME (R) |
| Attract mode | Logo, scoring legend, high scores, demo flight (V: existence; sequence not studied) | Cycle: title + scoring legend (10 s) → silent demonstration flown by the autopilot in an isolated session (≤ 25 s) → hall of fame (10 s) | Same | `AttractTests` | Sequence and demo behaviour are reconstructions (R) |
| High scores | 2 tables × 8 ("Todays Greatest", reset daily; "All Time Greatest"); initials with Up/Down and Fire (V) | Today's 8 + All-Time 10 per preset, shown side by side; Up/Down and Fire, or type the initials; daily rollover from an injectable clock | Same | `HighScoreBookTests`, `LosingAllShips_*` | All-Time holds 10, not 8 (per the brief) |
| Two players | Alternating turns on death; per-player save area (V, PLSAV/PLRES); "PLAYER n" for 128 frames at each 2-player life start, then 96 frames (1-player: the 96-frame pause only); "PLAYER n / GAME OVER" for 96 frames when one player runs out; P1 score left of the scanner, P2 right (V) | As described; the waiting player's state is a `PlayerState`; each qualifying player enters initials in turn | Same | `TwoPlayerTests`, `AuditRegressionTests` 12 | none |
