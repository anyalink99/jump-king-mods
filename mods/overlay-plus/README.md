# Overlay+

[Technical documentation](docs/index.md).

Overlay+ 1.3.0 is a full-window HUD editor that runs directly on a loaded Jump King
map, including fullscreen black bars. Press **F10**, use **Edit Overlay+** in the
pause menu, or hold **Back + Start** on a controller. Editing pauses the map;
closing restores the previous pause state after the controls are released.
There is no desktop editor, browser UI or external settings dialog.

The editor uses Jump King's own frame and five font styles, plus UIApi+'s native
window cursor. Cursor motion is handled by Windows, independently of game draws. Cyrillic and
extended characters use the pixel font shipped with the game. Native assets are
loaded from the installed game and are not redistributed.

## Enable or disable

The native **Overlay+** settings menu has **Enable** in both the main menu and
pause menu. It defaults to on, including for existing layouts. Turning it off
stops run tracking, saves the interrupted run, closes the editor and releases its
input/cursor ownership, graphics resources, gameplay subscription and all native
presentation/adapter patches. Disabled world/attempt loading skips preparation.
Turning it back on in a loaded map prepares fresh resources and resumes tracking
as a continued/practice session when the native attempt is already underway.
This preparation runs only on the explicit menu action, never a delayed frame.

The editor's existing **Hide HUD** is separate: it only hides widgets while
keeping tracking and editing available. Layouts, records and this preference are
preserved by Enable. Its saved field is `ModuleEnabled` in `Layouts.xml`;
the existing `Enabled` field continues to mean HUD visibility.

## Arrange the HUD

- Drag a widget; drag its bottom-right handle to resize. Double-click text to edit.
- Ctrl/Shift-click or draw a selection rectangle for multiple widgets. Ctrl+A
  selects all; Ctrl+D duplicates; Delete removes unlocked widgets.
- Ctrl+G groups, Ctrl+Shift+G ungroups. Alt-click selects one member of a group.
- Ctrl+Z / Ctrl+Y undo and redo. Page Up / Down change drawing order.
- Arrow keys move; Shift makes larger steps; Ctrl+arrows resize. Alt bypasses
  grid and alignment snapping while dragging. Align also distributes selections.
- F3 or right-click adds a widget; F2 previews sample values. Tab hides the tools.
  Drag either tool panel by its heading. Rescue brings widgets back into view.
- Place, Style and Content tabs edit position, anchor, visibility, fonts, colors,
  opacity, frames and each widget's content. Number controls support +/- and
  direct entry. Escape cancels a drag or text entry before closing the editor.

Controller: LB/RB select a widget, D-pad moves it, hold LS to resize, X adds,
Y hides panels. Hold RS and use D-pad to focus controls; RS+A activates them.
Text entry uses the keyboard, including Ctrl+A/C/V and Shift+Enter for new lines.

Window, game area and all four black bars are separate anchor spaces, each with
nine anchors. Missing bars can move a widget inside the game or hide it. Layouts
support map/aspect profiles (4:3, wide and ultrawide), presets, duplication and
single-widget/group templates. Profiles with a matching map/aspect take priority
over a general layout on the next attempt or resize.

## Widgets

Native game timer, Area Splits, keyboard/gamepad controls, single buttons, arbitrary multiline text,
statistics, jump charge, equipped boots/ring, images, panels and external sources.

The timer widget relocates the actual game timer drawing, keeping its native
font, format and visibility. Its Content tab controls the game's timer and
millisecond settings. Removing or hiding the widget restores native placement.
Real/active/segment/session clocks remain available as optional text variables.
HUD elements are drawn directly at window resolution across both the game and
black bars. They never pass through the game's 480x360 scene buffer. Native pause
menus and UIApi+ foreground pages are composed above the HUD in a separate layer,
including their background dimming. Smooth Camera retains its own scene presentation.

Controls default to Left, Right, Jump, Boots and Ring. Select an action, learn a
physical key/controller button, change labels, hide bindings, display hold duration
and choose row, free keyboard or controller arrangement. Free keyboard arrangement
exposes each key's position and size. Each arrangement remembers its own size;
switching back restores it. The default minimum flash is zero: keyboard/mouse
state is refreshed at draw time; controller state comes from the game's current
input tick without extra driver polls. Released keys have no extra visual hold. Optional minimum
flash is measured from the press, never added after a long hold. It observes Controls+ bindings without
changing them. Custom sources accept `Key:A`, `Pad:A`, `Mouse:Left`,
`Device:<identifier>|<button-code>` or `Action:mod.action`.

Text variables can be inserted from Content: `{time}`, `{game_time}`, `{real_time}`,
`{active_time}`, `{segment}`, `{session}`, `{attempt}`, `{area}`, `{screen}`,
`{height}`, `{jumps}`, `{falls}`, `{pb}`, `{sum_best}`, `{delta}`, `{map}`,
`{campaign}`, `{category}` and `{status}`. Unsupported glyphs use `?`; no system
fonts are substituted.

## Area splits and records

Splits are strictly the map's native named **Areas**, in authored order. Entering
and landing in the next unlocked Area finishes the previous segment; victory
finishes the final Area. Falls and re-entry do not create extra splits. Overlap
uses the native unlock boundary. The built-in 27 Areas are filtered by actual
start mode: **Main Babe 10 / New Babe Plus 9 / Ghost of the Babe 8**. Continue uses
the saved campaign flags. GoTB preserves its non-monotonic authored route. Custom
maps keep their own complete Area list; a map without Areas shows that explicitly.

The HUD offers cumulative or segment times, comparison deltas, PB, best segments,
sum of best and a scrolling current-Area view. Runs contains dates, notes,
completed/interrupted status, segments, flags and comparisons with PB, previous
or a selected run. Export history as XML and CSV. With Replays installed, save a
replay, open a linked recording or select it as a ghost from the same panel.

Game time follows the native attempt clock; real time includes pause; active time
excludes pause. Layout editing alone does not invalidate a run. Continued/joined
sessions, skipped Areas, state restoration, clock reversal, replay playback,
foreign teleports and changed gameplay modifiers are recorded as practice.
Practice never replaces records. Interrupted eligible runs can contribute golds.
Records are partitioned by map revision, campaign, category and the observed
modifier/equipment fingerprint. Stable modified runs have separate records. This
is a local tracker, not an anti-cheat verifier; arbitrary foreign mod settings
which expose no state cannot be completely fingerprinted.

## Compatibility and files

Requires **JK Runtime 1.30 or newer compatible 1.x** and the shared
Harmony 2.3.6 engine. Replays 2.1+, Smooth Camera 0.6+ and Jump% are optional. The native
timer draw is redirected only when a visible timer widget owns it. The original
timer method and other mods' postfixes still run. This build requires Runtime
with `UIApi.Supports("window-cursor-v1")`. Jump% with optional
Subframe Charge can be selected under External. Other mods can register text or
drawing providers through `OverlayPlusApi.dll`; see [provider API](docs/api.md).
There is no safe universal relocation of arbitrary unregistered foreign drawing.

Everything is configured inside the game. Data lives separately in
`Jump King/Content/OverlayPlus`: `Layouts.xml`, `Runs/<route>.xml`, `Images`,
`Imports` and `Exports`. Choose local images or shared layout files using the
in-game browser. Supplying a new image/shared file requires copying that asset
into its folder; it does not require a settings editor. Saves use atomic
replacement, retained `.bak` files and preserved corrupt/recovered copies.
Checkpoints are saved every ten seconds and at split, editor close and attempt end.
Run checkpoints use detached in-memory copies; XML encoding and disk writes run
on JK Runtime's managed save worker, with at most 128 pending destinations and
coalescing of newer checkpoints. Accepted work drains on ordinary process exit.
Unused external providers are not polled.
Run tracking never changes native gameplay saves. Timer visibility and precision
controls use the game's own general settings property. On a crash, up to ten seconds of unsaved
tracking may be absent. Unknown data schemas are preserved and rejected.

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
