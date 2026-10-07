# Defender (1981) — unofficial recreation

This is a recreation of Williams Electronics' 1981 arcade game *Defender*, built with .NET 10, C# and Avalonia. It reproduces the original's mechanics, using the Red Label arcade source code as evidence:

- the five-button controls;
- momentum physics on a wrap-around planet 2048 pixels wide;
- the scanner (the minimap across the top of the screen);
- abductions of humanoids, which turn Landers into Mutants;
- the planet exploding when every humanoid is lost;
- smart bombs and the risky hyperspace jump;
- the original wave tables.

The game is played offline by a single player. The art and sound are original work; nothing is taken from the arcade ROMs.

It has two presets, **Classic** and **Modern**. Both use the same rules. Modern adds conveniences that are disclosed in the game: pause on focus loss, suspend/resume, optional hold-to-fire, flash suppression, and adjustable speed.

**Documentation**

| File | Contents |
|---|---|
| RESEARCH.md | What we found out about the original, and from which sources |
| FIDELITY.md | How closely each system matches the original, and how Classic and Modern differ |
| ARCHITECTURE.md | How the code is organised |
| CONTROLS.md | Keyboard and gamepad controls |
| SAVES.md | Settings, high-score and suspend files |
| THIRD_PARTY.md | Licences and where every asset came from |
| KNOWN_ISSUES.md | Untested platforms, uncertain behaviour, missing features |
| MILESTONE_STATUS.md | Progress against the project plan |

> **Unofficial fan project.** It is not affiliated with or endorsed by Williams Electronics or its successors, and it is not cleared for public distribution. See THIRD_PARTY.md.

## Prerequisites

- **.NET SDK 10.0.** Verified with 10.0.400 and runtime 10.0.11.
- **Linux:** an X11 or XWayland session. Every other native library the game needs ships in its NuGet packages: SDL2 (audio and gamepad), Skia and HarfBuzz.
- **Windows and macOS:** no extra requirements. Note that the game has **not been run** on either (see KNOWN_ISSUES.md).

## Build, test, run

```bash
dotnet build Defender.slnx                      # Debug build of all projects
dotnet test Defender.slnx                       # 174 tests (engine, persistence, audio mixer, headless Avalonia UI)
dotnet run --project src/Defender.Avalonia      # play
```

Diagnostic flags:

```bash
dotnet run --project src/Defender.Avalonia -- --autoplay      # a scripted pilot plays (for screenshots and soak runs)
dotnet run --project src/Defender.Avalonia -- --audio-probe   # report the SDL audio status and play every sound, no window
dotnet run --project src/Defender.Avalonia -- --export-sounds sounds/   # write every effect as a WAV file to audition
dotnet run --project src/Defender.Avalonia -- --selftest      # windowless platform check: engine, renderer, native libs, data dir, audio, gamepad
```

In the game:

| Key | Action |
|---|---|
| **1** or **F2** | Start a 1-player game |
| **2** or **F3** | Start a 2-player game (players alternate on death) |
| **F1** | Show the controls |
| **F10** | Settings and key remapping |
| **F11** | Fullscreen |

## Publish

```bash
# Windows x64, self-contained (builds; never run, see KNOWN_ISSUES.md)
dotnet publish src/Defender.Avalonia -c Release -r win-x64 --self-contained true -o publish/win-x64

# Linux, self-contained (linux-arm64 has been run; linux-x64 is the same command, untested)
dotnet publish src/Defender.Avalonia -c Release -r linux-arm64 --self-contained true -o publish/linux-arm64

# Framework-dependent, for any RID (needs the .NET 10 runtime installed).
# osx-arm64 builds and bundles its native libraries; it has not been run and is unsigned (Gatekeeper will block it).
dotnet publish src/Defender.Avalonia -c Release -r osx-arm64 --self-contained false -o publish/osx-arm64
```

The executable is called `Defender` (`Defender.exe` on Windows).

## Continuous integration

`.github/workflows/ci.yml` builds and tests on Windows, Linux and macOS, publishes artifacts, and runs `--selftest` on each packaged build (a real runtime check of the native libraries on every OS). It has been written and checked as valid YAML, but it has **not run yet**: it runs once the repository is pushed to GitHub.

## Repository map

```
src/Defender.Core            simulation (no UI/OS dependencies)
src/Defender.Infrastructure  persistence, SDL audio, SDL gamepad
src/Defender.Avalonia        desktop app, software renderer, settings UI
tests/Defender.Tests         xUnit v3 + Avalonia headless tests
docs/research                full research dossiers with citations
```
