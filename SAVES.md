# Saves and settings

## Locations

| OS | Directory |
|---|---|
| Windows | `%APPDATA%\Defender1981\` |
| Linux | `$XDG_CONFIG_HOME/Defender1981/`, or `~/.config/Defender1981/` if that variable is not set |
| macOS | `~/.config/Defender1981/` (the same .NET resolution as Linux) |

## Files

| File | Contents | Written |
|---|---|---|
| `settings.json` | Preset (Classic or Modern), volumes, mute, display options, accessibility options, key and gamepad bindings, deadzone | When Settings closes, and when the app exits |
| `highscores-classic.json` | Classic tables: All-Time top 10 and Today's top 8 (with its local date). Each entry has initials, score, wave and a UTC date | When initials are committed |
| `highscores-modern.json` | Modern tables, same shape | When initials are committed |
| `suspend-1.json` | **Modern only.** The complete simulation state, including RNG state, terrain seed, every entity and every counter | When the window closes during a game |

### High scores

The two presets keep separate tables, so Modern conveniences such as hold-to-fire and slower speed never post scores to the Classic table.

Today's table is cleared when the local date changes, whether that is noticed at load or mid-session.

### Suspend

- The suspend file is read and **deleted** on the next launch in Modern mode. The game then resumes paused.
- Because the file is consumed, it cannot be reloaded repeatedly as a save state.
- Classic mode never writes a suspend file, matching the arcade, where only high scores persist.

## Format

Every file is indented JSON with camelCase names and enums stored as strings. Each one carries a `schemaVersion` field.

Writes are atomic:

1. The data is written to a temporary file (`.<name>.<guid>.tmp`) in the same directory.
2. The temporary file is flushed to disk.
3. It is renamed over the target file.

A crash during a save leaves the old file intact.

## Validation, corruption and migration

| Situation | What happens |
|---|---|
| File missing | Defaults are used silently |
| File corrupt (bad JSON, empty, unreadable) | Moved aside as `<name>.bad-<yyyyMMddHHmmss>`. Defaults are used, and a message appears on the title-screen status line and in Settings. Nothing is deleted. |
| `schemaVersion` older than the current version | Passed to a migrator (`JsonStore` `migrate` callback) and upgraded. High-score files are at **v2**: the v1→v2 migrator keeps the All-Time entries and starts an empty Today table (tested in `HighScoreBookTests.V1File_MigratesToV2`). All other files are at v1. |
| `schemaVersion` newer than the current version | Treated as incompatible: quarantined like a corrupt file, and defaults are used |

Values are also checked after loading:

- **Settings** are clamped: volumes 0–1, game speed 0.5–1.0, deadzone 0.05–0.9.
- **Bindings** with missing or empty actions fall back to the default for that action.
- **High-score entries** with a score ≤ 0 are dropped, and initials are normalised.
- **Suspend data** is checked for consistency (array sizes, value ranges, allowed states). Inconsistent data is ignored with a message.

## Versioning strategy

When a format changes incompatibly:

1. Increase `CurrentSchema` for that file.
2. Add a migration from the previous version.
3. If migration isn't possible, rely on the quarantine-and-default path.
