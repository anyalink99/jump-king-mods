# Smooth Camera

Follow the King continuously across screens instead of cutting at each boundary.
The camera eases through jumps and short landings, catches long falls and keeps
the King visible. It stitches neighboring scenery, weather and world entities.

## Install and start

Version **0.12.2** needs [JK Runtime 2.0+](../jk-runtime/README.md) and the shared
Harmony 2.3.6 supplied in the package. Keep one active copy of each mod and one
shared Harmony engine. For a manual install, close the game and copy the release
package into `Jump King/Content/JKMods/`, with Runtime installed separately.

Open **Mods > Smooth Camera** in the main or pause menu. **Enable** starts on and
is saved. Jump through a vertical screen boundary to try it. Title, intro and
ending screens keep native framing; pausing freezes the camera.

## Camera controls

**Look Up/Down** inherit the game's Menu up/down bindings. **Focus Camera**
defaults to **F** on the keyboard and has no controller default. All three use
**Hold** initially: release to return to automatic tracking. Open **Binds** to
rebind them or choose Press or Both. Map rules may restrict these views.
Read [controls and activation modes](docs/controls.md) for chords, latching and
what happens on pause or focus loss.

## Settings

Open **Settings**, adjust the preview, then choose **Apply** to save or **Cancel**
to discard the draft. You can change each axis, framing windows, response,
look-ahead and idle return, choose a preset or save a personal map profile.
Settings live in `SmoothCamera.Settings.xml` beside the package data.

**Horizontal** starts off. Turn it on to follow usable native side teleports and
Expansion Blocks MultiWarp exits. Runtime's shared map topology places side
branches by their entrance direction and preserves repeated images of self links.
Closed edges don't show a side view. This is a visual clearance heuristic, not a
change to teleport collision. Read [side-link rendering](docs/rendering.md#horizontal-side-links).

**240 Hz** starts on and requests camera presentation up to 240 FPS; the display,
VSync and GPU still limit it. Simulation and input keep their native interval.
It doesn't interpolate the King's physics. Turn it off if you prefer the native
cadence. See [all settings](docs/settings.md) and
[frame cadence](docs/rendering.md#240-hz-presentation).

## Debug dragging

In native Debug mode, left-click places the King at the visible world position;
hold and drag to move him and the camera through the framing window. The mouse
wheel keeps its native screen teleport. Read [Debug dragging](docs/controls.md#debug-dragging)
for boundaries, side exits and Focus behavior.

## Map-controlled activation

Maps can require smooth camera in particular screens or areas, or forbid camera
views. Activation uses Runtime's tags, opaque markers or
`jk-runtime/mechanics.xml`; optional camera recommendations live in
`props/smooth-camera/camera.xml`. A controlled map turns the global switch off
on entry and leaves it off afterward. Read the
[map authoring guide](docs/map-authoring.md) before adding these rules.

## Rendering and compatibility

Replays can supply the camera target; race ghosts leave live tracking alone.
Mapping composes lighting and water before camera assembly, then tint and
mirroring once afterward. Subframe Charge shares Runtime's frame scheduler.
Mods with their own compositor or gameplay changes during Draw need separate
checks. Artwork seams remain visible, and extra screen views cost more to draw.
Read [rendering and compatibility](docs/rendering.md) for version requirements
and fallback behavior.

## Build and checks

From the repository root:

```powershell
.\scripts\check-mods.ps1 -Mod smooth-camera
```

The package is `build/smooth-camera/UPLOAD_TO_WORKSHOP/`; `_INTERNAL` isn't
installable. Building doesn't install or publish it. Read
[validation](docs/validation.md) for integration commands and the
[playtest checklist](docs/validation.md#playtest-checklist).
The [guide index](docs/index.md) links the player, map-author and developer guides.

## Manual installation

Close Jump King, install Runtime, then copy the release DLLs as described above.
Avoid a local copy alongside an existing Workshop copy. See
[Runtime installation](../jk-runtime/README.md#install) for shared dependencies.
