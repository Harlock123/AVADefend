# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing, building and the
diagnostic flags; `ARCHITECTURE.md` explains the engine in depth, `SAVES.md` the files on disk, `FIDELITY.md` and
`RESEARCH.md` how closely it follows the 1981 original, and `KNOWN_ISSUES.md` what is still untested. This file
covers the rules that keep the code working.

## What this is

A recreation of Williams' 1981 arcade game Defender, in C# on .NET 10 and Avalonia 12, with a Classic preset
(arcade-faithful) and a Modern one. The frame is rasterised in software from the simulation's snapshot, sound is
synthesised at start-up and played through SDL2, and gamepads also go through SDL2. One self-contained
executable per platform (Windows, macOS and Linux, x64 and ARM), named `AVADefend`.

## Commands

```bash
dotnet build Defender.slnx                      # needs the .NET 10 SDK (there is no global.json)
dotnet test Defender.slnx                       # engine, persistence, audio and headless Avalonia UI tests
dotnet run --project src/Defender.Avalonia      # play
dotnet run --project src/Defender.Avalonia -- --selftest              # windowless platform check
dotnet run --project src/Defender.Avalonia -- --screenshots screenshots/   # regenerate the README pictures
dotnet run --project src/Defender.Avalonia -- --export-sounds sounds/      # every effect as a WAV
python3 tools/make_icon.py                      # rebuild the icon from the ship sprite
```

Other flags: `--autoplay` (a scripted pilot plays) and `--audio-probe` (no window). The test that opens the
settings panel saves a picture of it if `DEFENDER_SETTINGS_SHOT` names a file.

`Directory.Build.props` turns nullable and implicit usings on, sets `TreatWarningsAsErrors` to false, and
turns on central package versions (`Directory.Packages.props` pins every package). CI builds with
`-warnaserror`, so keep warnings at zero anyway.

## Layout

- `src/Defender.Core/`: the whole game, with no UI, audio or OS references (BCL only).
  - `Simulation/GameSession.cs` is the engine and its state machine; `GameSession.Testing.cs` adds test setups.
  - `Player`, `Enemies`, `Humanoids`, `Terrain`, `WaveTable` and `Blasts` hold the rules; `Arcade.cs` and
    `WorldMath.cs` the units and wrap-around arithmetic; `GameRules` and `GamePolicy` the operator settings and
    the Classic/Modern differences.
  - `FixedStepScheduler.cs` turns frame time into 60 Hz ticks; `FrameSnapshot.cs` is what the renderer draws.
  - `Autopilot`, `AttractDemo` and `AttractDirector` play the attract mode; `Suspend` and `TwoPlayer` do what
    their names say.
  - `Input/` has `InputLevels`, `InputEdgeDetector` and `Bindings`; `Scoring/` the high-score tables.
- `src/Defender.Infrastructure/`: `Persistence/` (atomic, versioned JSON stores and `AppPaths`), `Audio/`
  (`Mixer`, `SoundSynth`, `SdlSoundEngine`, `WavExport`) and `Input/SdlGamepad.cs`.
- `src/Defender.Avalonia/`: `MainWindow` (keys), `GameHost` (devices, scheduler, sound, saves), `GameView`
  (one `WriteableBitmap`), `Rendering/SoftwareRenderer.cs`, `SettingsPanel`, `SelfTest` and `ScreenshotTool`.
- `tests/Defender.Tests/`: xUnit v3 and `Avalonia.Headless.XUnit`. `TestUtil.cs` makes seeded sessions and runs
  ticks.
- `docs/research/`: the research dossiers behind the rules.

## Rules that keep it working

- **Fixed steps.** `GameSession.Step(PlayerInput)` advances one 1/60 s tick and is the only way game time passes.
  `FixedStepScheduler` caps catch-up at 8 ticks per frame. Rules use integers only, in the units listed in
  `ARCHITECTURE.md`.
- **Seeded random source.** Draw gameplay chance from the session's `IRandom` (`XorShiftRandom`), never
  `Random.Shared`. Its state is saved in suspend files, so a resumed game continues exactly. Only `Terrain`
  uses `new Random(seed)`, from the terrain seed that is saved alongside.
- **Core stays pure.** Nothing in `Defender.Core` may reference Avalonia, SDL or the file system; that is what
  lets the tests run without a window.
- **Entities refer to each other by id or slot**, never by object reference, and kills only set `Dead`; the dead
  are removed at the end of the tick. Both are needed for suspend data and safe iteration.
- **Input.** Keys are tracked by Avalonia `Key` name and mapped through the bindings; gamepad levels are ORed in.
  `InputEdgeDetector` turns levels into presses once per tick, so key repeat never fires twice. In
  `PlayerInput`, `Vertical` is -1 for up and +1 for down.
- **Classic and Modern keep separate high-score tables**, and only `GamePolicy` may differ between them.
- **Saves.** Everything lives in `AVADefend` under `SpecialFolder.ApplicationData` on Windows and
  `$XDG_CONFIG_HOME` (or `~/.config`) elsewhere. Change a saved format only with a schema version bump and a
  migration in its `JsonStore`. Tests use their own temporary folders and `NullSoundEngine`.
- **Line endings.** Sources are LF; `src/Defender.Avalonia/app.manifest` is CRLF. Keep each file's endings.

## Making a change

- Add or update tests with every gameplay change; `dotnet test` must pass with no warnings.
- If a change departs from the original, record it in `FIDELITY.md`; if it touches saves, update `SAVES.md`.
- If the change shows on screen, regenerate the screenshots and look at them.
- Update `README.md`, `CONTROLS.md` and `KNOWN_ISSUES.md` to match.

## CI and releases

`.github/workflows/build.yml` builds with warnings as errors and runs the tests on every push and pull request.
It then publishes single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64, and starts
the Windows x64, macOS Arm64 and Linux x64 packages with `--selftest` on machines of each OS. Pushes to `main`
replace the rolling `latest` pre-release; a `v*` tag makes a proper release.
