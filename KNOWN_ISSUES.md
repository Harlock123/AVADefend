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
- These details are inferred from the code, not observed in play:
  - points when ramming an enemy;
  - 25 points when a shot hits the player;
  - the sign convention of the mutant's "seek" window.
- These are reconstructions:
  - bomber nudge size and squad spacing;
  - how enemies come back after the player dies (PLSAV);
  - timing of the death and bonus screens;
  - planet-explosion debris visuals.
- Whether walking humanoids can be shot is unknown. Ours cannot.
- The terrain profile is deliberately not the arcade's (it is generated under the same constraints).
- The sprite shapes and colour tables are original approximations, not the arcade's.
- High scores: we keep one top-10 table per preset. The original had two tables of 8 ("Today's" and "All-time"), and "Today's" resets daily.

## Deferred and missing

- **Attract-mode demo flight.** Not implemented. `--autoplay` is a diagnostic flag, not the arcade demo.
- **2-player alternating play.** Not implemented. The arcade supported it.
- **Cocktail flip.** Not implemented.
- **Gamepad rebinding UI.** Gamepad bindings can be edited in `settings.json`, but the in-game UI only rebinds the keyboard.
- **Suspend slots.** Only one suspend slot is exposed. The storage layer supports slots 1–3.
- **Classic crash-recovery save.** Not implemented. The brief allows it but does not require it.
- **Sound design.** The sounds are synthesised from scratch and only loosely evoke the Williams board. An extended thrust loop and the distinct "lightning" during the planet explosion are approximate.
- **Diagnostics.** The original's diagnostics and audit screens are not implemented.

## Implementation notes

- **Laser hits:** we test the whole segment the beam's head swept that frame, at 1 px resolution. The original tests an 8×1 probe at 2 px resolution. The result can differ by a pixel at the edges.
- **Off-screen enemies:** they are simulated every frame. The original updated them at 8× velocity every 8 frames. The average motion is the same; only the granularity differs.
