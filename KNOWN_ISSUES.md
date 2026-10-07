# Known issues and deferred items

## Platform validation

- **Development and testing took place on Linux aarch64 only** (Arch, Hyprland/Wayland, running through XWayland). The brief named Windows as the first validation platform, but no Windows machine was available.
- **Windows (win-x64):** a self-contained publish **builds**, and the output contains `Defender.exe`, `SDL2.dll` and the Skia and HarfBuzz native libraries. It has **never been run**.
- **macOS:** the osx-arm64 framework-dependent build **compiles** and bundles its native libraries, but has **never been run**. It is not code-signed or notarised, so Gatekeeper will block it until signed or explicitly allowed.
- **Linux x64:** not built and not run. The Linux arm64 build (self-contained) was run.
- **Windows keyboard (unverified risk):** F10 (Settings) and Alt (a default Fire key) are Windows' menu-activation keys. The app marks them handled, but whether that fully prevents Windows' menu mode (a beep or a stalled key) needs checking on Windows. If it is a problem, rebind Fire away from Alt (Left Ctrl and J also fire by default), and use F9, which also opens Settings.
- **First run on a new OS:** `Defender --selftest` prints a pass/fail report without opening a window. CI runs it on the packaged Windows, Linux and macOS builds.
- **Gamepad:** tested against an **SDL virtual controller** (real SDL GameController code path): hot-plug add and remove, default mapping, deadzone and stick steering all pass. **No physical controller was tested**, so vendor-specific mappings, analog feel and the rebinding UI with real hardware are unverified.
- **Audio:** SDL audio opens on the development machine and the mixer is tested. Every effect is peak-limited to 0.9 (the three big explosions used to hard-clip), and levels were checked numerically. Nobody has *listened* yet; `--export-sounds <dir>` writes them all as WAV files for that.
- **Native Wayland:** the window runs through XWayland. Avalonia's native Wayland backend was not enabled or tested.

## Historical uncertainty

The list of open questions is in RESEARCH.md. Briefly:

- The RNG does not match the original byte for byte, so runs cannot be compared exactly against MAME.
- A second, line-by-line audit against the source fixed 18 discrepancies and settled the earlier "inferred" items: ramming scores, a shot hitting the player scores 25, walking humanoids can be shot, and the mutant window, bomber layout and death/restart flow are now as written (see RESEARCH.md).
- Still open: the off-screen bomber altitude correction, which as written pushes bombers away from their cruise height (implemented as written). The smart-bomb question is resolved: materialising enemies are immune, as in ours.
- Reconstructions that remain: the look of explosion debris, and sound timbres (event timing, priorities and the one-at-a-time model now follow the source).
- The terrain profile is deliberately not the arcade's (it is generated under the same constraints).
- The sprite shapes and colour tables are original approximations, not the arcade's.
- High scores: like the original we keep "Today's" (8, reset daily) and "All-Time" tables, but All-Time holds 10 rather than 8, as the brief asked.

## Deferred and missing

- **Attract-mode demo flight.** Implemented, but as a reconstruction: our autopilot flies it, and the original's demo sequence was not studied.
- **2-player play.** Implemented, including the "PLAYER n / GAME OVER" turn-over. Gamepad Start2 defaults to pressing the right stick, which is awkward.
- **Cocktail flip.** Implemented as a setting (rotates the picture 180° on player two's turns), but not seen on screen. The cocktail cabinet's control-panel switching is not modelled.
- **Gamepad rebinding UI.** Implemented. The swap logic and control detection are tested (the latter with a virtual controller); not tried with real hardware.
- **Classic crash-recovery save.** Not implemented. The brief allows it but does not require it.
- **Sound design.** The sounds are synthesised from scratch and only loosely evoke the Williams board. An extended thrust loop and the distinct "lightning" during the planet explosion are approximate.
- **Diagnostics.** The original's diagnostics and audit screens are not implemented.

## Implementation notes

- **Laser hits:** the probe now matches the original's 16×1 placement, but masks are compared at 1-px rather than 2-px resolution, and when two objects share the probe the nearer one dies rather than the first in the original's object list.
- **Off-screen enemies:** they are simulated every frame. The original updated them at 8× velocity every 8 frames. The average motion is the same; only the granularity differs.
