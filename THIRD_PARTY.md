# Third-party notices and asset provenance

This project is an **unofficial fan recreation**. It is not made, endorsed or licensed by Williams Electronics, WMS, Midway or Warner Bros. Interactive, or by any of their successors. "Defender" is used only to identify the game being studied. All names shown to users are defined in `src/Defender.Avalonia/Branding.cs` so they can be replaced. **This project has not been cleared for public distribution.** See "Distribution status" at the end of this file.

## NuGet dependencies

All versions are pinned in `Directory.Packages.props`. Licences were checked against each package's nuspec on 2026-10-07.

| Package(s) | Version | Licence | Use |
|---|---|---|---|
| Avalonia, Avalonia.Desktop, .Skia, .Themes.Fluent, .X11, .Win32, .Native, .FreeDesktop(.AtSpi), .HarfBuzz, .Remote.Protocol | 12.1.3 | MIT | UI framework |
| Avalonia.BuildServices | 11.3.2 | MIT | build-time telemetry and tooling from Avalonia |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | No SPDX expression in the nuspec (it uses the deprecated licence-URL field); upstream ANGLE is BSD-3-Clause | Windows GL backend (transitive) |
| SkiaSharp (+ NativeAssets.*) | 3.119.4 | MIT. The native Skia library inside it is BSD-3-Clause | rendering backend |
| HarfBuzzSharp (+ NativeAssets.*) | 8.3.1.3 | MIT. The native HarfBuzz library is "Old MIT" | text shaping (transitive) |
| MicroCom.Runtime | 0.11.6 | MIT | transitive |
| Tmds.DBus.Protocol | 0.94.1 | MIT | Linux desktop integration (transitive) |
| Microsoft.Extensions.DependencyModel / Microsoft.DotNet.PlatformAbstractions | 9.0.9 / 3.1.6 | MIT (.NET) | transitive |
| Silk.NET.SDL, Silk.NET.Core, Silk.NET.Maths | 2.23.0 | MIT | SDL2 bindings for audio and gamepad |
| Ultz.Native.SDL | 2.32.10 | Zlib (SDL2) | the native SDL2 library for win, linux and osx (x64 and arm64) |
| xunit.v3 / xunit.runner.visualstudio / Microsoft.NET.Test.Sdk / Avalonia.Headless.XUnit | 3.2.2 / 3.1.5 / 18.10.1 / 12.1.3 | Apache-2.0 / Apache-2.0 / MIT / MIT | tests only (not distributed) |

### Licences we deliberately avoided

**OpenAL Soft** (`Silk.NET.OpenAL.Soft.Native`) is LGPL-2.0-or-later. We used SDL2 for audio instead, so that every redistributed native library has a permissive licence.

### Obligations

To meet the licence terms of MIT, BSD-3-Clause and Zlib, a binary distribution must include:

- this file;
- the licence texts of the packages above. They are inside each nupkg and in the upstream repositories.

## Assets

Every audiovisual asset in the game was made for this project. **Nothing was extracted from the ROMs, from MAME, or from recordings of the arcade machine.**

| Asset | Origin | Where |
|---|---|---|
| Sprites (ship, lander ×3, mutant, baiter, bomber, pod, swarmer, humanoid, mine, shot, HUD icons) | Original pixel art, drawn at the sizes documented in the source and in RESEARCH.md | `src/Defender.Core/Simulation/Sprites.cs` |
| 5×7 font | Original | `src/Defender.Avalonia/Rendering/PixelFont.cs` |
| Application icon (`.ico`, 256-px PNG) | Generated from our own ship sprite by `tools/make_icon.py` | `src/Defender.Avalonia/Assets/` |
| Terrain profile | Generated procedurally (seed 1981), following the original's format constraints. The Williams terrain table was **not** copied | `src/Defender.Core/Simulation/Terrain.cs` |
| Colour tables and cycle sequences | Our own choices, in the same 8-bit BBGGGRRR format | `World.cs` |
| Palette to RGB levels | Computed from the MAME resistor-network description (MAME is BSD-3-Clause; only the numbers were used) | `SoftwareRenderer.cs` |
| All sounds | **Synthesised at runtime** by our own code (sweeps, warbles, filtered noise). No samples are used | `src/Defender.Infrastructure/Audio/SoundSynth.cs` |

We found CC0 sample packs on OpenGameArt (listed in `docs/research/hardware-docs-dossier.md` §5.4) but did not use them.

## Reference material (facts only, not redistributed)

| Source | Licence status | How we used it |
|---|---|---|
| Original Defender 6809 source mirrors: github.com/mwenge/defender and github.com/historicalsource/defender | **No licence**; Williams copyright notices in the files | Read to establish numbers, rules and timings. No code, data tables, sprites or sounds were copied. The wave table's numbers appear in `WaveTable.cs` as game parameters, with citations. |
| MAME `williams.cpp` / `williams_v.cpp` | BSD-3-Clause | Screen geometry, clocks and palette levels (facts) |
| Williams operator manuals (archive.org) | © Williams | Controls, factory settings, high-score format |
| Open-source ports (therealsark02/defender, jeffnyman/defender-redlabel, and others) | The repositories carry BSD-2 or MIT, but they appear to bundle assets derived from the original | **Not used.** We did not take any of their assets |

## Distribution status

This repository contains no original Williams code or assets. However:

- The game recreates a copyrighted game's design and uses its name.
- The wave parameters are facts taken from an unlicensed copy of the source.

**Nobody has done a legal review.** Do not treat this project as cleared for public distribution.
