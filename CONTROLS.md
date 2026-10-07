# Controls

## The original cabinet

The cabinet has a 2-way **Up/Down joystick** and five buttons: **Thrust**, **Reverse**, **Fire**, **Smart Bomb** and **Hyperspace**. There are also 1P and 2P start buttons. (Source: operator manual 16P-3001-103.)

- **Reverse** flips the direction the ship faces. It does not change velocity directly; the ship keeps drifting and then decelerates.
- **Thrust** accelerates the ship in the direction it faces.
- **Fire** shoots **once per press**. Holding the button does not repeat (verified in the source).

No standard keyboard or gamepad layout matches this exactly. The defaults below keep every control as a separate, simultaneously usable input. In Settings (F10) you can rebind them, and every action can have several keys.

## Keyboard (defaults)

| Action | Keys | Notes |
|---|---|---|
| Up / Down | ↑ / ↓, or W / S | |
| Thrust | Space, Z | Hold |
| Reverse | X, ← or → | Each press flips the ship once. ← and → both just *flip*: they do not choose a direction, matching the arcade's single Reverse button. |
| Fire | Left Ctrl, Left Alt, J | One shot per press. Modern mode has an optional hold-to-fire setting. |
| Smart Bomb | Left Shift, K | |
| Hyperspace | Enter, L | Risky: about a 25% chance of dying on arrival, as on the arcade. |
| Start 1-player game | 1, F2 | |
| Start 2-player game | 2, F3 | Players alternate on each death |
| Pause | Esc, P | |
| Controls overlay | F1 | |
| Settings / remap | F10 or F9 | F9 is an alternative because F10 is a menu key on Windows |
| Fullscreen (borderless) | F11 | |

### Why this layout

- The left hand covers thrust, reverse and bombs (Z/X, Space, Shift).
- The right hand covers altitude (arrows), with fire on Ctrl/Alt or on J/K/L.
- Left Alt and Left Ctrl are both fire keys so that the game is still playable on keyboards where pressing three keys at once (*key ghosting*) is a problem.

## Gamepad (SDL GameController, Xbox-style names)

| Action | Default |
|---|---|
| Up / Down | Left stick or D-pad, up/down |
| Steer (stick steering on) | Left stick or D-pad, left/right: **face that way and thrust** |
| Thrust | Right trigger |
| Reverse | Left bumper, X |
| Fire | A |
| Smart Bomb | B, Left trigger |
| Hyperspace | Y, Right bumper |
| Pause | Start |
| Start 1-player game | Back / View |
| Start 2-player game | Right stick press (rebindable) |

### Mapping rationale

Most players expect a stick to mean "go that way", so stick steering is on by default:

- Pushing left or right turns the ship to face that way (if it isn't already) and holds thrust.
- The arcade's separate thrust and reverse inputs are still available on the right trigger and left bumper.

To play "pure arcade", turn off stick steering in Settings and use the trigger and bumper.

### Settings

- **Deadzone:** 0.2 by default; adjustable in Settings.
- **Hot-plug:** controllers can be connected or disconnected at any time.
- **Rebinding:** in Settings (F10), under **Gamepad bindings**, click an action, then press a control on the pad. This needs a connected controller and has not been tested on real hardware (see KNOWN_ISSUES.md). Bindings are also stored in `settings.json` (`bindings.gamepad`).

## Remapping the keyboard

1. Press F10 to open Settings, then go to **Keyboard bindings**.
2. Click an action, then press the new key.
   - Backspace restores that action's defaults.
   - Esc cancels.
3. Assigning a key removes it from any other action, so one key never drives two actions. If that leaves the other action with no key at all, it receives this action's old key (a swap).
4. **Reset all bindings to defaults** restores every keyboard binding.

Bindings are saved when Settings closes.

## Entering initials

After a qualifying score, you can enter your initials in either of two ways:

- Press Up/Down to choose each letter, then Fire to confirm it.
- Type the letters on the keyboard.
