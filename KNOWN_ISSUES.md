# Known issues and deferred items

## Platform validation

- **Development and testing took place on Linux aarch64 only** (Arch, Hyprland/Wayland, running through XWayland). The brief named Windows as the first validation platform, but no Windows machine was available.
- **Windows (win-x64):** a self-contained publish **builds**, and the output contains `Defender.exe`, `SDL2.dll` and the Skia and HarfBuzz native libraries. It has **never been run**.
- **macOS:** not built and not run.
- **Linux x64:** not built and not run. The Linux arm64 build (self-contained) was run.
- **Gamepad:** the code is written against SDL GameController and compiles, but **no physical controller was tested**. Hot-plug handling is therefore untested.
- **Audio:** SDL audio opens on the development machine and the mixer is tested. Nobody listened to the sounds during development, so their quality is unjudged.
- **Native Wayland:** the window runs through XWayland. Avalonia's native Wayland backend was not enabled or tested.

## Historical uncertainty

The list of open questions is in RESEARCH.md. Briefly:

- The RNG does not match the original byte for byte, so runs cannot be compared exactly against MAME.
- A second, line-by-line audit against the source fixed 18 discrepancies and settled the earlier "inferred" items: ramming scores, a shot hitting the player scores 25, walking humanoids can be shot, and the mutant window, bomber layout and death/restart flow are now as written (see RESEARCH.md).
- Still open: whether a smart bomb hits enemies that are still materialising (ours: no), and the off-screen bomber altitude correction, which as written pushes bombers away from their cruise height (implemented as written).
- Reconstructions that remain: planet-explosion debris visuals and the bonus-screen timing.
- The terrain profile is deliberately not the arcade's (it is generated under the same constraints).
- The sprite shapes and colour tables are original approximations, not the arcade's.
- High scores: like the original we keep "Today's" (8, reset daily) and "All-Time" tables, but All-Time holds 10 rather than 8, as the brief asked.

## Deferred and missing

- **Attract-mode demo flight.** Implemented, but as a reconstruction: our autopilot flies it, and the original's demo sequence was not studied.
- **2-player play.** Implemented, including the "PLAYER n / GAME OVER" turn-over. Gamepad Start2 defaults to pressing the right stick, which is awkward.
- **Cocktail flip.** Implemented as a setting (rotates the picture 180° on player two's turns), but not seen on screen. The cocktail cabinet's control-panel switching is not modelled.
- **Gamepad rebinding UI.** Implemented, but untested on hardware (no controller available). The swap logic is unit-tested.
- **Classic crash-recovery save.** Not implemented. The brief allows it but does not require it.
- **Sound design.** The sounds are synthesised from scratch and only loosely evoke the Williams board. An extended thrust loop and the distinct "lightning" during the planet explosion are approximate.
- **Diagnostics.** The original's diagnostics and audit screens are not implemented.

## Implementation notes

- **Laser hits:** we test the whole segment the beam's head swept that frame, at 1 px resolution. The original tests an 8×1 probe at 2 px resolution. The result can differ by a pixel at the edges.
- **Off-screen enemies:** they are simulated every frame. The original updated them at 8× velocity every 8 frames. The average motion is the same; only the granularity differs.
