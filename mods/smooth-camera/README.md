# Smooth Camera

Smooth Camera replaces the screen-by-screen camera with continuous movement.
Version **0.8.0** requires JK Runtime 1.34 or newer 1.x and the shared Harmony
2.3.6 dependency included in the package.

[Technical guides](docs/index.md) · [Map setup](docs/map-authoring.md)

The camera follows the King with smooth acceleration and braking. Near the top
of a jump, a framing band absorbs small downward movements instead of immediately
dragging the view back down. Long falls resume normal tracking, and a visibility
guard keeps the King on screen.

Neighboring 480 x 360 screens are stitched together with their backgrounds,
platforms, foregrounds, scrolling layers, weather and world entities. By default,
horizontal framing uses the full map width. The view stops at the top and bottom
of the level. Teleports longer than 270 pixels in one tick snap straight to the
destination. Pausing freezes the camera.

Use **Enable** and **Settings** in the Smooth Camera main or pause menu. Enable is
saved and starts on. The Settings page lets you choose vertical and horizontal
modes, framing windows, directional response, anticipation, idle return, focus
controls, presets and per-map profiles. Its preview updates before you commit:
**Apply** saves the draft and **Cancel** throws it away. See
[camera settings](docs/settings.md) for every option.

A map can force smooth presentation in selected `smooth` screens or rectangular
zones even when the player's switch is off. Everywhere else, map policy and the
saved preference decide. The [map authoring guide](docs/map-authoring.md) explains
that permission system.

On an ordinary disabled map, the mod removes its hooks. A map with forced areas,
or a user running the optional diagnostic overlay, keeps dormant observation
hooks but does not request camera-only high-refresh frames in native areas.
Runtime prepares needed hooks before player handoff and keeps them until world
exit, avoiding recompilation on restarts in the same world. Menus and intros do
not run the renderer or high-refresh cadence.

The preference lives in `SmoothCamera.Settings.xml` beside the package data.
Title, intro and ending screens keep native framing. Smooth Camera changes only
presentation: it does not touch player physics, saves, screen events, wind or
achievement flags.

## Camera controls

The existing **Menu up** and **Menu down** bindings also control the camera during
gameplay. Up looks above the king, placing his center 48 logical pixels from the
bottom; Down looks below him, placing his center 48 pixels from the top. Map
boundaries can limit this framing. Transitions remain smooth at the presentation
rate, while input is read at the unchanged native update rate.

Use **Settings > Controls > Bind Focus** to change
**Focus Camera** (also available under **Controls > Smooth Camera**). It defaults
to **F** on the keyboard; controllers start unassigned. The binding editor offers primary
and secondary slots, Clear and Default commands. It supports alternative buttons and two-button chords, saved separately for
each native device profile. Focus smoothly centers the active native screen on
both axes, then follows native screen changes exactly instead of following the
king inside that screen.

- A tap shorter than the configured threshold (250 ms by default) latches the requested view. Movement starts on press.
- Tapping Up or Down while either directional view is latched cancels that view.
  Tapping Focus again cancels a latched Focus view. Tapping a direction from Focus,
  or Focus from a directional view, switches to that view.
- Holding through the configured threshold temporarily selects the requested view. Release
  restores the previously latched view, or normal tracking when none was latched.
- A cancelling tap takes effect on release. If that press becomes a hold, its
  temporary view takes over at the threshold; normal framing never flashes first.
- Opposing simultaneous directions cancel the unfinished gesture and require
  release. An explicit Focus chord takes precedence over directions it contains.
- Pause, Runtime pages, Steam overlay and window focus loss cancel unfinished
  gestures and require release before rearming. Restart, unloading or disabling
  Smooth Camera clears the latch. View modes aren't saved to disk.

Automatic tracking continues underneath temporary views, so returning keeps
its normal framing behavior. These controls are active only while Smooth Camera
is enabled and its compositor is available. Native buttons remain unconsumed.

The **Settings > Horizontal** switch defaults to **off**,
including when loading settings from an older version. When enabled, the camera
also follows horizontally near native side-teleport screens. The destination is
drawn beyond the corresponding left/right edge, with its vertical neighbors.
A single native link serves both exits; two links select their respective sides.
Each side is shown only if its edge has at least two consecutive free collision
cells (16 game pixels vertically). A solid wall, a single-cell gap, or separate
single-cell gaps keep that side hidden and prevent panning toward it. This is a
presentation heuristic; it doesn't change native teleports or prove that the
player can reach or fit through the opening. Actual crossings keep their
coordinate rebase and temporary departure view.

Edge checks recognize native stone, ice, snow and slope geometry. Water, sand
and nonblocking platforms don't close an edge. Unknown mod blocks keep the
native side-view fallback rather than running their collision callbacks during
rendering. Results are shared by presentation frames and refreshed after each
native update, so replacement geometry is observed without rescanning every
high-refresh frame.

Horizontal influence grows with the visible portion of a teleport screen, so it
starts smoothly while approaching from above/below. Leaving the neighborhood
returns to full-width framing. The camera rebases across a side teleport without
restarting its spring; the departure view remains visible during a one-way
arrival. When nearby portals describe incompatible layouts, the side view first
slides out of sight before its destination changes. Invalid destination indices
are ignored for presentation. Areas beyond the destination map's top/bottom have
no authored scenery. Native teleport behavior itself remains unchanged.

The **240 Hz** switch under **Settings > General** enables
presentation up to 240 FPS, subject to the display's refresh rate,
VSync and GPU performance. It defaults to on, preserving previous versions'
behavior. Uncheck it to keep smooth following at the native game cadence.
The switch keeps camera framing, controls, physics time and saved bindings;
it doesn't remove the smooth-camera hooks or restart the level.
With 240 Hz enabled, the camera advances on each rendered frame while
native simulation and input keep their original update interval. Extra frames
reuse the captured world. Fractional camera positions are applied at output
resolution, avoiding jumps of several display pixels when the game is enlarged.
The king is part of that cached world, so his screen position can visibly step
relative to the camera during fast falls. Turning 240 Hz off removes the extra
camera-only frames. It doesn't interpolate the king or guarantee stationary
framing during camera acceleration, landing or changes in the framing band.

## Build and checks

From the repository root, using Windows PowerShell:

```powershell
.\scripts\check-mods.ps1 -Mod smooth-camera
.\scripts\check-mods.ps1 -Mod smooth-camera -Integration
```

The first builds Runtime, tests camera motion and installed native hooks, and
checks package discovery and the installed MonoGame scheduler's update cadence.
Integration also renders an isolated native MonoGame
fixture and checks pixels across screen seams, layer ordering, viewport overlays,
render-state restoration and resource disposal. Captures are retained under
`build/smooth-camera/_INTERNAL/graphics/`. Tests neither install the mod nor start
the game or write its saves/settings. An interactive playtest remains necessary.

The distributable is `build/smooth-camera/UPLOAD_TO_WORKSHOP/`. It contains
`SmoothCamera.dll`, `0Harmony.dll` and documentation. The implementation DLL and
test dependencies under `_INTERNAL` aren't installable release files.

## Manual installation

After building, with the game closed, install JK Runtime and put
`SmoothCamera.dll` in `Jump King/Content/JKMods/`. Use a single shared
`0Harmony.dll` from this package; don't keep multiple Harmony versions or a
second copy of a Workshop-installed mod. Restart the game. Building doesn't
install or publish anything.

## Rendering and compatibility

Subframe Charge 0.24+ and Smooth Camera submit independent input/draw requests
to Runtime's single scheduler. Either 240 Hz option can request extra frames;
disabling one client keeps the other. SFC player prediction refreshes the
world atlas each presented frame so the king stays behind native foreground
layers. Both load orders keep one native physics accumulator.

The installed game's draw order is background, world entities, foreground, then
menus/overlays. A guarded Harmony transpiler redirects only those draw calls.
The compositor renders one or two vertical native screen views into a reusable
atlas once per simulation update (or when visible screen coverage changes).
Horizontal portal framing expands the atlas and captures up to two additional
side views. Intermediate presentation frames reuse those captures.
The native render target holds stationary UI. The final blit combines that UI
with the translated atlas, using point sampling at output resolution. Native screen
queries and coordinate transforms are overridden only inside a thread-local
render scope. The native camera field is never assigned, and no entity updates
are replayed. Save workers and physics keep the original screen state.

Only interval reads inside the framework's fixed-step scheduler are redirected
to the presentation interval. Native updates receive the unchanged simulation
delta and clock; the public `TargetElapsedTime` property is preserved for physics
and other mods. Disabling/unloading returns partial elapsed time to the native
accumulator. Classic camera movement uses a critically damped spring with 12-pixel upward
and 64-pixel downward framing bands. The pause state freezes that motion.
The spring responds faster at high player speeds so ordinary fast falls don't
hit the emergency visibility clamp every simulation tick. A target-crossing
guard prevents a sudden landing from producing a later reverse movement.

Native foreground NPCs and the flying gargoyle are composed with the world.
Native NPC/prop draw callers use explicit scoped camera queries, so screen checks
inlined before enabling Smooth Camera can't hide adjacent characters. Drawing
those characters doesn't advance their behavior trees or activate screen events.
Pause UI, timer, location labels, fades and lightning remain fixed in the
viewport, outside the screen passes. Unknown third-party foreground overlays keep their
native behavior. Mods which draw screen-space UI inside `EntityManager.Draw`,
change state during `Draw`, or implement their own camera/compositor may need
adapters. Visual seams in independently authored screen artwork remain visible.
Adjacent screen events still activate according to the original game rules.

Mega Mapping Expansion 0.3+ supplies Runtime's scene-composition adapter.
Lighting and water compose each visible logical screen before camera assembly;
frame tint/mirroring apply once to the assembled world and stationary UI. Both
mods can stay enabled. Older Mapping versions use the native-camera fallback
while active; their checkbox can release that fallback.
Known checkbox changes are read immediately from a cached delegate. Patch
metadata is rescanned at most once per second for independently added/removed
compositors, instead of deserializing Harmony metadata every simulation tick.
Other custom rendering mods and full base-game/DLC/Workshop playthroughs have
not been validated. Rendering up to four visible views increases world draw cost on
simulation updates; intermediate camera frames reuse those views. Mods that
replace the game scheduler or mutate gameplay during rendering need separate
compatibility checks. Smooth Camera doesn't interpolate player animation/physics.

## Playtest checklist

- Tap and hold Up, Down and Focus; try cancelling a latch with the opposite
  direction, holding Focus over a latched direction, and changing the native
  screen while focused. Confirm release restores the earlier view.
- Rebind directions and Focus on keyboard/controller, pause during a press,
  alt-tab, open the Steam overlay, restart and disable/re-enable Smooth Camera.
- Jump upward and fall through several screen boundaries; check the king,
  platforms, NPCs and foreground overlap at the seam.
- Pause, resume, toggle camera mode, restart, return to the menu and load a
  different map. Verify that the HUD stays fixed and framing resets.
- Try a side teleport, bottom/top boundaries, rain/snow, scrolling backgrounds,
  NPC dialogue and an ending. Check screen events and movement against Screens.
- Enable horizontal camera, approach a portal screen from above/below, cross both
  exits and try a one-way arrival. Check the seam, player sprite and stationary HUD.
- Try installed gameplay/appearance mods individually before combining render mods.
- Check repeated short jumps and landings for unwanted downward camera movement,
  then a long fall for responsive tracking. Compare 60 Hz and high refresh displays.

## Map-controlled activation

Add the relevant exact tag to `Tags` in `level_settings.xml`:

- `JKRuntime.MapControlled:smooth-camera.tracking`

| Mechanic | ID | On RGB | Off RGB | Local RGB |
| --- | --- | --- | --- | --- |
| Camera tracking | `smooth-camera.tracking` | `(173,83,211)` | `(173,84,211)` | `(173,85,211)` |

Colours are opaque nonblocking screen metadata. On enables the screen; Off denies
activation; Local permits only local triggers or XML zones. On a tagged map,
unmarked screens are off and manual enabling is locked. Entry saves global enable
Off; it stays off after leaving until the player enables it again.

**Screen markers and XML Screens declarations are alternatives for the same
On/Off/Local rule: use either one, without duplicating it in the other.** XML also
supports ranges, zones and supported parameters. If both are used for the same
screen/mechanic, they must agree; XML doesn't override a marker. The map tag
remains separate and isn't added automatically by either method.

Optional screen/zone declarations belong in `jk-runtime/mechanics.xml` at the
map root. Screens start at 1; coordinates use 480 x 360 screen-local pixels.
Pixel and XML declarations must agree.

[Map authoring: tags, exact colours, complete XML and Smooth Camera rules](docs/map-authoring.md)
contains the full instructions within this mod.
