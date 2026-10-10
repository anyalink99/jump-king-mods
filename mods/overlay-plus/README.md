# Overlay+

Build a HUD over the game and its black bars. Add timers, controls, text, images
and area splits, then arrange them in the game with mouse, keyboard or controller.

## Install and open

Published **1.3.0** needs [JK Runtime 1.30+](../jk-runtime/README.md), including
Runtime 2.0, with `UIApi.Supports("window-cursor-v1")`, and the shared Harmony
2.3.6 engine. Packages rebuilt with the current SDK need Runtime 2.0+.
Replays, Smooth Camera and Jump% are optional. Keep one active mod copy.

Press **F10**, choose **Edit Overlay+** in Pause, or hold **Back + Start** on a
controller. The editor pauses the map. Press **F3** or right-click to add a
widget, drag it into place, then close the editor to restore the prior pause state.
Everything is configured in the game.

## Arrange the HUD

Drag to move; use the bottom-right handle to resize. Double-click text to edit.
Ctrl+Z/Ctrl+Y undo and redo. Place, Style and Content edit layout and appearance.
Separate game/window/black-bar anchors and map/aspect profiles keep layouts useful
across window sizes. Read the [editor guide](docs/editor.md#arrange-the-hud) for
keyboard/controller controls, grouping and templates.

## Widgets

Add the native timer, Area Splits, input displays, equipment, text, images or
external data. HUD drawing stays at window resolution, below menus and Runtime
pages. Native timer visibility and precision remain the game's own settings.
The [widget guide](docs/editor.md#widgets) lists sources, variables and input-display rules.

## Area splits and records

Splits follow the map's named Areas in authored order. Runs can show PB, golds,
comparisons and history, with XML/CSV export and optional replay links.
Continued, restored or otherwise interrupted gameplay is tracked as practice
and doesn't replace records. This local tracker isn't an anti-cheat verifier.
Read [record eligibility](docs/editor.md#area-splits-and-records).

## Enable or disable

**Enable** starts on. Turning it off saves the interrupted run and releases the
editor and tracking resources. **Hide HUD** only hides widgets; tracking continues.
Layouts and preferences survive both. Read [enable behavior](docs/editor.md#enable-or-disable).

## Compatibility and files

Data lives in `Jump King/Content/OverlayPlus`, separately from the mod package.
Atomic saves retain backups; a crash can lose up to ten seconds of tracking.
Copy images and shared layouts into the matching folders before browsing them.
The [storage guide](docs/editor.md#compatibility-and-files) gives paths and recovery rules.
Other mods can register data through [OverlayPlusApi](docs/api.md).

## Build and install

```powershell
.\scripts\check-mods.ps1 -Mod overlay-plus -Integration
.\scripts\check-mods.ps1 -Mod overlay-plus,smooth-camera,replays -Integration
.\mods\overlay-plus\install.ps1
```

The installer validates Runtime and Overlay+, waits for you to close the game by
refusing to replace loaded DLLs, chooses an existing Workshop folder or local
`Content/JKMods`, retains rollback backups and preserves settings. It does not
update unrelated mod packages. The release directory is
`build/overlay-plus/UPLOAD_TO_WORKSHOP`; only `OverlayPlus.dll`,
`OverlayPlusApi.dll` and `0Harmony.dll` are distributable DLLs. Install JK Runtime
separately when copying a package manually. Keep one active Overlay+ copy.

See [validation](docs/validation.md) for automated coverage and live-playtest limits.

Runtime owns patch cleanup. Replays ghost settings are saved before live
selection changes.
Text editing acquires Runtime's shared input capture, so typing cannot feed
native or Subframe Charge gameplay controls. Closing or cancelling releases it.
Focused checks use only selected dependencies; combined checks above pass fresh
camera and replay assemblies explicitly. The provider API and saved schemas
remain compatible with 1.1.2.

The [guide index](docs/index.md) separates editor use, integration and maintenance.
