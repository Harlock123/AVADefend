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
| Sound model | One sound at a time with the original priorities (V) | Polyphonic mixing; the mono board is an option (M) |
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
| Stars | 16 stars; ½ parallax (2 px per 4 px of scroll); one random star recolours each frame and on about half the frames jumps to a new X; its Y re-randomises only while the planet is gone (V) | As described | Same | visual | none |
| Thrust/drag | V −= V/64; +3/frame²; clamp 256 (V) | Exact 24-bit integer arithmetic | Same | `Thrust_*`, `Drag_*` | none |
| Ship screen X / reverse | Base 64/224 px plus a lead of (V>>2)·128; within one column of the target the ship snaps there with no scroll compensation, otherwise it slides one column (2 px) and the scroll compensates ±$40; the velocity clamp comes after. Reverse on press; re-arms after release + 5 frames (V) | Exact | Same | `Reverse_*`, `ShipLeadsForward_AtSpeed`, `WhileAccelerating_LeadSnapsUncompensated_*` | none |
| Vertical | No inertia; 1 → 2 px/frame (+8/256 per frame); movement gated at Y > 43 / Y < 238 but not clamped, so 42 and 239 are reachable (V) | Exact | Same | `Vertical_*` | none |
| Laser | ≤ 4 shots; edge-triggered; head 8 px/frame, fizzle 6, tail 2; screen space; ends at column $98 or ≤ 5; each frame tests the 16×1 probe LASP1 (right: 12 px behind to 4 px ahead of the head; left: 16 px from the head); one kill each (V) | As described | Hold-to-fire in Modern only (M) | `LaserTests`, `LaserProbe_Is16x1_PlacedPerDirection` | Masks tested at 1-px rather than 2-px resolution; nearest object wins instead of object-list order (R, rarely differs) |
| Smart bomb | 3 at start; +1 per extra life; kills on-screen enemies that are not materialising (appearing objects carry OTYP bit 1; appear runs only on screen); humanoids and shells unaffected; 2 white flashes; re-arms after the flashes + 10 frames, a release, and 10 more frames (V) | Exact | Flash suppressible in Modern | `SmartBomb_*` | none |
| Hyperspace | Blank 15 frames with all movement frozen; the wave may still end; clear shells; random BGL, facing and Y; velocity 0; appear for 40 frames; **die if rnd > 192** (V) | Exact | Same | `Hyperspace_*` (statistical: 2000 trials, expected 24.6%, bound 21–28%) | Uses a different RNG |
| Enemy shots | ≤ 20 shells (bullets and mines), all screen-space; mines die once off screen; mines only while fewer than 10 shells exist. SHOOT: 8-bit screen-column/row differences with ±16 jitter, ×4 → arrives in ~64 frames; the same random byte adds the ship's velocity in ~53% of shots; starts at the shooter; dies at the top edge or past row 255; fired regardless of the ship's state (V) | As described | Same | `EnemyShot_*`, `MineCap_CountsEveryShell` | none |
| Collision | Bounding box, then pixel mask at 2 px horizontal resolution (V) | Bounding box, then pixel mask at 1 px | Same | Laser and ram tests | Slightly finer than the original (R) |
| Lander | Spawns at top with random vx (1..LNDXV+1) and vy = LNDYV; hovers at terrain − 50 (±20 px band); 6-frame AI tick; targets a walking humanoid; descends in the same 32-px band, **shooting every frame**; latches; lifts to Y 50, **shooting every 4 frames**; absorbs at 1 px/frame; becomes a mutant. A lander that reaches the top without its humanoid is removed unscored and returned to the reserve; one that loses its target while descending keeps vx = 0 (V) | As described | Same | `Lander_*` tests, `AuditRegressionTests` 4–6 | none |
| Mutant | Constant SZXV toward the player. Seeks the player's Y when `player − mutant` is within −380..+1412 units; otherwise dodges the player's line. Exactly ±SZRY hop each tick and the shot timer run in the avoid branch, and in the seek branch only when on screen. 3-frame tick (V) | As described | Same | `Mutant_HomesTowardsPlayer`, `AuditRegressionTests` 2, 3, 13 | none |
| Bomber | Squads of ≤ 3, $180 units apart at player X + $8000, Y $50, no materialise; direction alternates per squad. Each frame one of 4 squad slots is picked (empty = no update): nudge −32..+31, damping V/32, keep 16–32 px from the player when on screen. Mines only when on screen, 1 in 8 updates, < 10. Off screen, cruise-altitude random walk with a correction that pushes away from it (V; last point I) | As described | Same | `Bomber_LaysStationaryMines_OnlyOnScreen_UpToCap`, `BomberSquad_*` | Off-screen correction direction (I) |
| Pod / Swarmer | Pod: X high byte $10–$4F, Y rnd/2 + YMIN, vx −32..31, \|vy\| 32–64. On death releases 1..7 swarmers exactly at the pod (cap 20), vy = signed rnd × 2, random first nap. Swarmers overshoot and turn back after 150 px and fire only toward the player. Restored swarmers come in clusters of ≤ 6, 1024–1535 px ahead (V) | As described | Same | `Pod_*`, `Swarmers_*`, `AuditRegressionTests` 14, 18 | none |
| Baiter | Timer UFOTIM (×15 frames), shortened when ≤ 8 or ≤ 3 enemies remain; ≤ 12 alive. On each 18-frame cycle, re-seeks when rnd > UFOSK: vx = player vx ± 2 px/frame unless within 20 px, vy = (player vy ± 1 px/frame)/2 unless within 10 px; a close axis keeps its velocity. First shot timer 8. Not counted for wave end (V) | As described | Same | `Baiter_*`, `AuditRegressionTests` 17 | none |
| Humanoids | 10; placed at Y $E0 spread over 4 quadrants and re-placed every life; walker visits 16 slots, one every 2 frames (a step every 32 frames), only on screen; 9/256 turn chance; the laser kills them in any state (no points); an absorbed humanoid explodes (V) | As described | Same | `StartsWith10Humanoids_*`, `Humanoid_WalksOneStepEvery32Frames`, `Humanoids_ArePlacedAtY224`, `AuditRegressionTests` 1, 7 | none |
| Falling / catch | Every 4 frames: +8/256 px/frame (below $300) and a ground test (terrain altitude ≤ Y, no snap); lands dead if speed > 0.875 px/frame; safe drop about 49 px; catch 500, set down 500, safe solo landing 250 (V/I) | As described | Same | `SafeFallHeight_IsAbout49Px`, `ShortFall_*`, `LongFall_*`, `ShootingAbductor_*` | none |
| Planet explosion | When the last humanoid is lost: terrain and scanner terrain erased; TERBLO runs 16 iterations of 2 explosions at the terrain altitude, a random background colour for 2 frames and the lightning sound, then a random 1-3 frame pause (~60-90 frames); the terrain-blow sound at the end; landers become mutants; restored on waves that are multiples of 5 (V) | As described | Background tint suppressible | `LosingLastHumanoid_*`, `Planet_And10Humanoids_*` | none |
| Waves | WVTAB: 22 byte rows; base values for waves 1–4; inter-wave deltas applied (wave − 4 + difficulty 5) times, up to a ceiling of 15; intra-wave deltas every 10 s, with the count restarting each life; deltas that would pass a limit or overflow a byte are skipped, and LNDYV/SZYV never carry into their high byte (V) | Data-driven `WaveTable`, validated on load | Same | `WaveParams_*`, `WaveTable_RejectsMalformedData`, `AuditRegressionTests` 10 | none |
| Wave end | All Landers, Mutants, Bombers, Pods and Swarmers gone, alive and in reserve; Baiters excluded. Bonus screen: cleared screen, one humanoid counted per 4 frames, then 128 frames (V) | As described | Same | `WaveClears_*`, `WaveBonusScreen_*` | none |
| Scoring | See RESEARCH.md, including points for ramming and 25 when a shot hits the player (V) | Exact | Same | `KillScores_MatchOriginal`, humanoid tests | none |
| Lives | 3 ships; +1 ship and +1 smart bomb every 10,000; HUD shows ≤ 5 ships and ≤ 3 bombs (V) | Exact | Same | `BonusShipAndSmartBomb_Every10000`, `StartsWith3Ships_*` | No hidden cap known |
| Player death | Ship flashes as a silhouette (2 frames off, 2 on) through red→white over ~32 frames while enemies keep moving; 2 white frames cancel every explosion and finish every appear; PLEX: 128 pieces from ship + (8,3) with a fixed-seed diamond velocity spread (< 2.83 px/frame), 2×2 squares, white for 56 frames then 13 fading colours × 4 (108 frames) (V) | As described | Flash suppressible | `Ramming_*`, `LosingAllShips_*`, `AuditRegressionTests` 8 | none |
| Scanner | 128 px wide; whole planet centred on the screen middle; 1:16 X scale; Y/8 + 7; 2×2 two-colour blips; fixed 1-px window ticks at x 152 and 167; the player is a 5-px plus at column $4B + screen column/16 (V) | Exact geometry; colours from our palette | Classic every 8 frames; Modern every frame | `ScannerWindowTicks_*` | Mini-terrain sampling is ours (R) |
| Palette | 16-entry pseudo-palette of BBGGGRRR bytes; cycling laser, A, C and bomber slots; per-wave border colour (V) | The same mechanism with **our own** colour tables and cycle sequences | Cycling slowed when suppressed | visual | Exact colour tables were not copied (R) |
| Sprites | Sizes: ship 16×6, lander 10×8 ×3, mutant 10×8, baiter 12×4, bomber 8×8, pod 8×8, swarmer 6×4, humanoid 4×8, mine 4×3 (V) | **Original pixel art** at those sizes | Same | `SoftwareRenderer_DrawsEveryStateHeadlessly` | Shapes are approximations (R) |
| Flicker | No evidence of sprite flicker in the Red Label code; the star count drops when the CPU is overloaded (V) | No artificial flicker added | Modern suppresses rapid colour cycling | — | none |
| Sound | Williams 6808 + DAC board, **one sound at a time**: a new script plays only if its priority ≥ the current one; thrust is heard only when nothing else plays. Triggers: distinct kill sounds per enemy and shot sounds per shooter; appear sound on materialise and on hyperspace return (no sound on entry); lander pick-up and suck; lightning per planet burst, terrain-blow at the end; no wave-start/bonus/game-over jingles; no speech (V) | **Synthesised** original approximations (no samples) triggered at the original events; Classic uses the monophonic priority model | Modern mixes polyphonically (optional mono) | `AudioTests`, `MonophonicBoardTests`, `EachEnemyKind_HasItsOwnHitSound`, `Hyperspace_IsSilentOnEntry_*` | Timbre is approximate by design (R) |
| RNG | 3-byte LFSR with a mix step (V) | xorshift32, injectable and serialisable | Same | `Replay_IsDeterministic_*` | Not byte-exact with MAME (R) |
| Attract mode | Logo page (~960 frames: logo traced, "PRESENTS", title assembling from 15 converging strips, copyright) → Hall of Fame (600) → scripted demo (~2279: lander abducts the humanoid, ship shoots it, catches and sets down the humanoid for 500, then each enemy appears, rises into the beam, explodes and re-appears in its slot with name and points) → logo page (V, amode1.src) | Same order, timings, positions and script, with **our own** logo text, title graphic and notice. Not reproduced: coin-skip, credits line, start lock until the first logo page completes. A start hint line is shown (the arcade had none) | Same | `AttractTests` | none for behaviour; artwork deliberately differs |
| High scores | 2 tables × 8 ("Todays Greatest", reset daily; "All Time Greatest"); initials with Up/Down and Fire (V) | Today's 8 + All-Time 10 per preset, shown side by side at the HALDIS positions (Classic shows 8 all-time rows like the arcade, Modern 10); Up/Down and Fire, or type the initials; daily rollover from an injectable clock | Same | `HighScoreBookTests`, `LosingAllShips_*` | All-Time holds 10, not 8 (per the brief) |
| Two players | Alternating turns on death; per-player save area (V, PLSAV/PLRES); "PLAYER n" for 128 frames at each 2-player life start, then 96 frames (1-player: the 96-frame pause only); "PLAYER n / GAME OVER" for 96 frames when one player runs out; P1 score left of the scanner, P2 right (V) | As described; the waiting player's state is a `PlayerState`; each qualifying player enters initials in turn | Same | `TwoPlayerTests`, `AuditRegressionTests` 12 | none |

| Explosions / appears | EXST: the sprite's own 2-px × 2-row tiles spread by S = 1.0 + 0.664/frame for ~72 frames, world-fixed, radiating from the laser hit point; only for on-screen objects; 16 effect slots. APST: tiles converge from S = 46 to 0 over 47 frames while the invisible object keeps moving; on-screen only (V) | As described | Same | `Appear_Lasts47DrawnFrames_*`, `Explosion_SpreadsFor72Frames_*` | none |
| HUD | Score: fixed 6-digit field at (30,28) [P2 (226,28)], 8 px per digit, leading zeros blanked but "00" kept, colour 1 (cycles with the laser); ships at (30,20) step 12 (max 5); bombs at (82,27) step 4 rows (max 3); colour-5 border: 2-row line at y 40-41, 2-px scanner sides, white window marker x 152-167 on the border (V) | As described | Modern: steady score colour; adds HIGH/WAVE in the free right-hand area (M) | visual | Score colour cycling is from the code; verify against a MAME capture |
