# Milestone status

Updated 2026-10-07.

**Status labels**

- **Impl**: implemented.
- **Tested**: covered by automated tests (`dotnet test`; 174 tests passing).
- **Observed**: seen working in the running app on Linux arm64, via screenshots.
- **Untested**: implemented but not exercised.
- **Deferred**: not implemented.

**Platform caveat.** All observation happened on Linux aarch64. Nothing has been run on Windows or macOS.

## M1 — Research and specification: done

- Two dossiers with cited sources and a source ledger (`docs/research/`), summarised in RESEARCH.md.
- Fidelity matrix and the reference-version decision (FIDELITY.md).
- Open questions are listed in RESEARCH.md and KNOWN_ISSUES.md.
- **Gaps:** no longplay or frame-by-frame video analysis; no trade-press review.

## M2 — Skeleton and engine validation: done

- Solution with 4 projects; package versions pinned in `Directory.Packages.props`.
- Fixed 60 Hz loop decoupled from rendering. Status: Impl, Tested, Observed.
- WriteableBitmap pipeline with nearest-neighbour scaling. Status: Observed. A first screenshot test confirmed the expected scroll rate.
- World-wrap tests at both seams. Status: Tested.
- Build instructions in the README were run and verified (build and test). No CI configuration exists yet.

## M3 — Player ship and physics: done

- Thrust, drag, speed lead and the reverse slide, all in exact integer arithmetic. Status: Impl, Tested, Observed.
- Vertical movement with no inertia. Status: Impl, Tested.
- Lasers: edge-triggered, at most 4. Status: Impl, Tested, Observed.
- Scanner. Status: Impl, Observed.
- Pause. Status: Impl, Tested.

## M4 — Humanoids and the abduction chain: done

- Humanoid placement and walking. Status: Impl, Tested.
- Lander behaviour: hover, descend, grab, lift, absorb, then become a Mutant. Status: Impl, Tested.
- Shooting a Lander that carries a humanoid, the humanoid's fall, the catch, setting it down, and scoring for each. Status: Impl, Tested.
- Planet explosion, the Mutant-only state, and restoration every 5th wave. Status: Impl, Tested.
- Humanoids on the scanner. Status: Observed.

## M5 — Complete enemy roster: done

| Feature | Status |
|---|---|
| Bomber and its mines | Impl, Tested |
| Pod releasing 1–7 Swarmers | Impl, Tested |
| Swarmer behaviour | Impl, Untested in isolation; runs in soak tests |
| Baiter, including its timer rules | Impl, Tested |
| Smart bomb | Impl, Tested |
| Hyperspace, including the 24.6% death chance (statistical test) | Impl, Tested |

## M6 — Waves and scoring: done

- Data-driven wave table with validation, inter-wave and intra-wave escalation, wave-clear check, bonus screen. Status: Impl, Tested.
- All score values; bonus ship and smart bomb every 10,000. Status: Impl, Tested.
- High-score table with initials entry and persistence. Status: Impl, Tested.

## M7 — Audio: done, with caveats

- 22 synthesised effects, a looping thrust sound, a 16-voice priority mixer, separate effects and ambience volumes, mute, and silent fallback when audio is unavailable. Status: Impl, Tested (mixer and fallback).
- The device opens on Linux. Status: Observed.
- **Audio quality has not been judged**: nobody listened during development.

## M8 — Modern mode and accessibility: mostly done

| Feature | Status |
|---|---|
| Settings panel (preset, audio, display, accessibility, keyboard remapping) | Impl, Tested (F10 opens, Esc closes and saves); layout observed in a headless render |
| Gamepad through SDL, with hot-plug, deadzone and stick steering | Impl, Tested with an SDL virtual controller; no physical hardware |
| Suspend/resume (Modern) | Impl, Tested: equivalent after a JSON round-trip |
| Flash and colour-cycle suppression, reduced motion | Impl |
| Game speed | Impl; shown on the HUD |
| Hold-to-fire | Impl, Tested |
| Auto-pause on focus loss | Impl |
| Integer scaling and bilinear option | Impl |
| Borderless fullscreen (F11) | Impl, not observed |
| Gamepad rebinding UI | Impl; swap logic Tested; **Untested** with a real controller |

## M9 — Validation, packaging, docs: partial

| Item | Status |
|---|---|
| All 9 documents | Written |
| Windows x64 self-contained publish | **Builds**; never run |
| Linux arm64 self-contained publish | Builds and runs (Observed) |
| macOS | Not built |
| Fidelity validation against MAME footage | Not done |

## Round 2 additions (2026-10-07)

| Feature | Status |
|---|---|
| Original two-table high scores (Today's 8 with daily rollover, All-Time 10), file schema v1→v2 migration | Impl, Tested, Observed |
| Attract cycle with silent demonstration flight | Impl, Tested, Observed |
| Gamepad rebinding UI; swap-safe rebinding for keyboard and gamepad | Impl; logic Tested; no hardware test |
| 2-player alternating play (2 / F3 to start) | Impl, Tested; 2P HUD observed in a headless render |
| CI workflow for Windows, Linux, macOS | Written and YAML-validated; **never run** (not pushed) |

## Round 3 additions

| Feature | Status |
|---|---|
| Modern control-reminder bar below the picture (never over the game) | Impl, Tested (shown only in Modern) |
| Cocktail flip: picture rotated 180° on player two's turns (setting, both presets) | Impl; not observed |
| Three selectable suspend slots (Modern); slot descriptions in Settings; picking a full slot on the title screen resumes it | Impl, Tested |
| Worst-case performance test: 45 enemies, 20 shells, firing | Tested: sim + snapshot + render average 0.045 ms, worst 0.73 ms per frame (Debug, this machine) |

## Round 4: source-fidelity audit

A line-by-line comparison against the original routines found 18 discrepancies. All are fixed, each with a regression test (`AuditRegressionTests`). Highlights: walking humanoids can be shot; mutants shoot from their avoid branch and their seek window was mirrored; landers shoot while descending and lifting; humanoids are re-placed every life; enemies keep moving during the death glow; mines only appear and live on screen; wave speeds never carry across a byte; the 2-player turn-over message and life-start timings. Status: Impl, Tested, gameplay Observed via autoplay.

## Round 5

| Item | Status |
|---|---|
| Gamepad path tested end-to-end with an SDL virtual controller (hot-plug, mapping, deadzone, steering, rebinding detection) | Tested |
| Smart bomb vs materialising enemies resolved from source (immune); appear effect now only on screen | Impl, Tested |
| Wave-bonus screen timing from source (4 frames per humanoid, 128-frame hold, cleared screen); 128-piece death explosion | Impl, Tested |
| `--export-sounds` WAV export; peak limiter (removed hard clipping in 3 effects); explosion levels raised | Impl, Tested; levels measured, not listened to |
| macOS osx-arm64 framework-dependent publish | Builds; not run; unsigned |

## Round 6

| Item | Status |
|---|---|
| Laser collision probe matches LCOL (16×1, direction-dependent placement) | Impl, Tested |
| Application icon (window + Windows .exe) from our own sprite; generator script in `tools/` | Impl; window icon load Tested |
| Modern bold scanner blips | Impl |

## Round 7: independent correctness review

A fresh read-only review found 8 defects plus 3 minor ones; all are fixed with regression tests.

| Defect | Severity | Fix |
|---|---|---|
| Toggling thrust while muted leaked mixer voices; the 17th press crashed the app (reproduced) | High | Voices advance while muted; fading loops are revived; allocation can't throw; the frame loop reports rather than crashes on any exception |
| Gamepad kept driving the game behind the Settings panel; settings/focus-loss didn't pause non-playing states | Medium-high | Host freeze (any state) while Settings is open or Modern focus is lost; presses made meanwhile are discarded |
| Malformed but valid JSON (null lists or entries, bad target index, bad rules) crashed startup | Medium | Null-tolerant high-score loading; structural validation of suspend data; catch-all at the resume boundary |
| OS key-repeat filled the initials | Medium-low | Repeats are ignored |
| 2-player initials screen showed the wrong player's score | Low-medium | Shows the score being entered |
| Planet-explosion effect leaked into the next life or turn | Low | Reset at each life |
| A temporarily unreadable save file was quarantined, then overwritten with defaults | Low | Left untouched and write-protected for the session |
| Closing during a death, a turn-over or initials lost the game or the entry | Low | Suspend covers death and turn-over; initials are committed on close |
| F1 help stale after rebinding; speed combo misread; Backspace-reset could double-bind | Minor | Fixed |

## Round 8: second source-fidelity audit

A second line-by-line audit (player, humanoids, shells, scanner, stars, sound triggers) found 10 discrepancies plus sound-trigger differences; all fixed with tests (`AuditTwoRegressionTests`, `MonophonicBoardTests`, `ScrollFidelityTests`). The largest: ship-slide snaps were wrongly scroll-compensated, which changed scroll feel during acceleration; humanoids walked 1.6× too fast; smart bombs re-armed too early (a double tap spent two); enemy shot aim used pixels instead of byte columns; the planet explosion ran twice as long. The Classic sound model is now the original one-at-a-time priority board.

Also added: `--selftest` (CI runs it on every packaged build) and F9 as a second Settings key.

## Next concrete tasks

1. Run the win-x64 build on Windows: smoke test, audio, gamepad.
2. Compare against MAME recordings: lander timing, the baiter's first appearance, the reverse slide.
3. Push to GitHub so the CI workflow (`.github/workflows/ci.yml`, written but never run) builds and tests on Windows, Linux and macOS.
4. Test with a real gamepad (vendor mappings and feel; the code path itself is now tested virtually).
