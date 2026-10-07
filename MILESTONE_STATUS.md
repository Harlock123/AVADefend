# Milestone status

Updated 2026-10-07.

**Status labels**

- **Impl**: implemented.
- **Tested**: covered by automated tests (`dotnet test`; 95 tests passing).
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
| Gamepad through SDL, with hot-plug, deadzone and stick steering | Impl, **Untested** (no hardware) |
| Suspend/resume (Modern) | Impl, Tested: equivalent after a JSON round-trip |
| Flash and colour-cycle suppression, reduced motion | Impl |
| Game speed | Impl; shown on the HUD |
| Hold-to-fire | Impl, Tested |
| Auto-pause on focus loss | Impl |
| Integer scaling and bilinear option | Impl |
| Borderless fullscreen (F11) | Impl, not observed |
| Gamepad rebinding UI | Deferred: keyboard only for now |

## M9 — Validation, packaging, docs: partial

| Item | Status |
|---|---|
| All 9 documents | Written |
| Windows x64 self-contained publish | **Builds**; never run |
| Linux arm64 self-contained publish | Builds and runs (Observed) |
| macOS | Not built |
| Fidelity validation against MAME footage | Not done |

## Next concrete tasks

1. Run the win-x64 build on Windows: smoke test, audio, gamepad.
2. Compare against MAME recordings: lander timing, the baiter's first appearance, the reverse slide.
3. Attract-mode demo flight, and the original two-table high-score presentation as an option.
4. A gamepad rebinding UI.
5. CI: GitHub Actions running `dotnet test` on windows-latest, ubuntu-latest and macos-latest.
