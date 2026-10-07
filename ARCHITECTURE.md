# Architecture

## Solution layout

| Project | Contents | Dependencies |
|---|---|---|
| `src/Defender.Core` | The whole simulation: world state, scrolling, entities, AI, collision, waves, scoring, the scanner model, the render snapshot, input edge detection, the high-score table and suspend data. | None (BCL only) |
| `src/Defender.Infrastructure` | Persistence: versioned atomic JSON, settings, high scores, suspend. Audio: mixer, synthesiser, SDL output. Gamepad: SDL GameController. | Core, Silk.NET.SDL, Ultz.Native.SDL |
| `src/Defender.Avalonia` | Desktop app: window, `GameView`, software renderer, settings panel, keyboard translation. | Core, Infrastructure, Avalonia 12.1.3 |
| `tests/Defender.Tests` | Headless engine tests, persistence and audio tests, and Avalonia headless UI smoke tests. | All of the above, xUnit v3, Avalonia.Headless.XUnit |

Core never references a UI, audio or OS API. Every test except the 3 `[AvaloniaFact]` UI tests runs against Core or Infrastructure without opening a window.

## The engine (`GameSession`)

### Stepping

- `Step(PlayerInput)` advances one 1/60 s tick. This is the only way simulation time passes.
- The engine takes three injected dependencies:
  - an `IRandom` (deterministic xorshift32 with serialisable state);
  - a `Terrain` (seeded);
  - a `WaveTable` (data-driven and validated).
- The clock is external. The host decides how many ticks to run (see the frame loop below), so tests drive time tick by tick.
- `Policy` (`GamePolicy`) is the only place where Classic and Modern differ.
- `Rules` (`GameRules`) holds the operator adjustments: ships, replay score, difficulty, restore wave.

### State machine

`Attract` → `LifeStart` (90 frames, "PLAYER ONE") → `Playing`. From `Playing`, the game moves on in one of these ways:

| From `Playing` | Path |
|---|---|
| Player dies | `Dying` (200 frames) → `LifeStart`, or `GameOver` if no ships remain |
| Wave cleared | `WaveComplete` (bonus count) → `LifeStart` of the next wave |
| Game ends | `GameOver` → `EnterInitials` (if the score qualifies) → `Attract` |

### Tick order within `Playing`

1. Input: reverse, fire, smart bomb, hyperspace.
2. Player physics and camera (BGL).
3. Stars.
4. Lasers, including laser kills.
5. Enemy AI and integration of velocities and positions.
6. Humanoids.
7. Shells.
8. Player collisions: rams, shots, catches.
9. Game executive, every 15 frames: squads, baiters, escalation, wave end.
10. Removal of dead entities.
11. Planet-blow process, particles.
12. Palette.

The original runs its collision check first and its AI as cooperative processes. Our order can shift some outcomes by one frame; no rule changes.

### Entity ownership

`GameSession` owns every list. The rules for creating and destroying entities:

- **Spawns:**
  - The executive creates Landers (in squads) and Baiters.
  - The start of each life creates Pods, Bombers, and Mutants and Swarmers held in reserve.
  - A Pod's kill routine creates Swarmers.
  - A Lander that finishes an abduction becomes a Mutant: the same object changes `Kind`.
- **Deaths:**
  - Kills only set `Dead = true`. Dead entities are removed once, at the end of the tick, so nothing is deleted mid-iteration.
  - Humanoids are fixed slots (10, like the original TLIST). Their state goes to `Dead`.
- **References** between entities are ids and slot indices, never object references. This is what lets suspend data serialise.

### Units

All units are integers, matching the original's fixed-point layout:

| Quantity | Unit |
|---|---|
| World X | Units, 32 per pixel; wraps with `& 0xFFFF` |
| Y | 1/256 px |
| Player velocity | 24-bit; the top 16 bits are units per frame |
| Ship screen X | 1/128 px (like `PLAX16`) |
| Shells | X × 16 for sub-unit aim precision |

No floating-point arithmetic appears in the rules. Doubles are used only for explosion particle directions, which are cosmetic.

## Scrolling world and scanner

- `CameraX` (BGL) is the world X of the screen's left edge. Player world X = BGL + screen X.
- Thrust moves the camera. When the ship reverses and slides across the screen, the camera compensates so the ship's speed relative to the world doesn't change.
- An object is on screen when `(X − BGL) & 0xFFFF < 300 px`. The renderer also draws objects that are partly off the left edge.
- The scanner window spans the whole planet, centred on the middle of the screen:
  - X: `((X − (BGL + 150 px − 1024 px)) & 0xFFFF) >> 10`, which gives 2-px columns.
  - Y: `pixelY/8 + 7`.
- The blip list is rebuilt every 8 frames in Classic, as in the original, and every frame in Modern.

## Rendering pipeline

1. `GameSession.BuildSnapshot(FrameSnapshot)` produces a view-frustum snapshot with everything already in screen coordinates:
   - visible sprites, including partly visible ones;
   - lasers, stars, particles and popups;
   - the terrain Y for each of the 304 columns;
   - scanner blips and mini-terrain;
   - the 16-entry palette and the HUD values.

   The snapshot's lists are reused to avoid allocations.
2. `SoftwareRenderer` (pure managed code) rasterises the snapshot into a 292×240 `uint[]`. That is MAME's visible area, offset (12, 7) into the 304×256 game frame. Sprites are palette-indexed. Each palette byte (BBGGGRRR) is converted to RGB through the arcade's resistor-ladder levels.
3. `GameView` copies the buffer into one `WriteableBitmap` and draws it scaled to fit:
   - aspect ratio preserved, centred;
   - nearest-neighbour by default, bilinear as a Modern option;
   - optional integer scaling, aware of the DPI scale factor.

   No Avalonia control exists per entity.
4. The frame loop runs on `TopLevel.RequestAnimationFrame` (vsync-paced). Each frame:
   1. `GameHost.Frame(dt)` samples the devices.
   2. `FixedStepScheduler.Advance(dt)` decides how many whole ticks to run. It caps at 8 ticks per frame and drops the backlog after a stall.
   3. That many ticks run; audio events are dispatched after each one.
   4. The snapshot is rebuilt.

   Rendering is therefore decoupled from the 60 Hz simulation.

We use software rasterisation, not SkiaSharp drawing calls, because the arcade is a 4-bpp framebuffer. Exact pixel and palette semantics are trivial in software, it is testable without a GPU, and at 292×240 it costs well under 1 ms. Avalonia's Skia backend still does the scaling and composition.

## Input

- `InputLevels` holds the device levels. Keyboard levels come from the set of held keys, mapped through `InputBindings` by `Key` name; gamepad levels come from SDL. Both are ORed together.
- `InputEdgeDetector` latches press edges between ticks and consumes each edge exactly once. OS key-repeat therefore never creates presses, and a quick tap between two ticks is never lost.
- Stick steering on the gamepad (`FaceRequest`) is turned into a reverse edge plus held thrust.
- When the window loses focus, all keys are released.

## Audio

- Core emits `SoundId` events per tick plus a `ThrustSoundOn` level.
- `Mixer`:
  - 16 voices;
  - priority-based voice stealing; the thrust loop can never be stolen;
  - rapid retriggers of a sound restart its existing voice;
  - separate volumes for effects and for the ambience/loop;
  - mute;
  - soft clipping.
- `SoundSynth` renders every effect procedurally at start-up. No samples are used.
- `SdlSoundEngine` pushes 512-frame chunks from a background thread through `SDL_QueueAudio`, keeping about 46 ms of latency. No native callback is involved.
- If SDL or the audio device can't be initialised, the game uses `NullSoundEngine`. A status message appears in Settings and on the title screen.

## Persistence

See SAVES.md. In short, `JsonStore<T>`:

- writes atomically: write a temp file, flush, then rename;
- stores a schema version;
- runs migrations;
- moves corrupt or incompatible files aside as `*.bad-<timestamp>` and falls back to defaults.
