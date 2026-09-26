# Smooth Camera

[Technical documentation](docs/index.md).

Experimental continuous camera for Jump King. Version **0.6.0** requires JK Runtime
1.30 or newer 1.x and a shared Harmony 2.3.6 dependency (included in the package).

The camera follows the king with smooth acceleration and braking. A retained
framing band absorbs small downward movements around a jump's apex and landing,
without recentering downward afterward. Sustained falls resume following and a
visibility guard keeps the king in view. It stitches neighboring native 480 x 360 screens, including
backgrounds, platforms, foregrounds, scrolling layers, weather and world entities.
Horizontal framing defaults to the map's full width. At the bottom and top of the
map the view stops at the level boundary. Teleports exceeding 270 pixels in one
tick snap to their destination. Ordinary pauses freeze the camera.

Use the standard **Smooth Camera** checkbox in the main or pause menu.
Checked enables smooth following (default); unchecked restores native screens.
When unchecked, camera hooks are removed; starting an attempt with the camera
disabled does not install them.
Runtime prepares enabled camera hooks before handoff and retains them until
world exit, avoiding patch recompilation on same-world restarts. The renderer
and high-refresh cadence remain inactive throughout menus and intros. Shared
Runtime preparation replaces the camera's private lifecycle hooks. Disabling
the setting removes rendering and scheduler patches immediately.
The existing setting persists in `SmoothCamera.Settings.xml` beside
the package's data directory. Intro, ending and title screens use native framing.
The mod changes presentation; it does not alter player physics, save data,
screen events, wind selection or achievement flags.

## Camera controls

The existing **Menu up** and **Menu down** bindings also control the camera during
gameplay. Up looks above the king, placing his center 48 logical pixels from the
bottom; Down looks below him, placing his center 48 pixels from the top. Map
boundaries can limit this framing. Transitions remain smooth at the presentation
rate, while input is read at the unchanged native update rate.

Use **Bind Focus** in the Smooth Camera main or pause settings to change
**Focus Camera** (also available under **Controls > Smooth Camera**). It defaults
to **F** on the keyboard; controllers start unassigned. The binding editor offers primary
and secondary slots, Clear and Default commands. It supports alternative buttons and two-button chords, saved separately for
each native device profile. Focus smoothly centers the active native screen on
both axes, then follows native screen changes exactly instead of following the
king inside that screen.

- A tap shorter than 250 ms latches the requested view. Movement starts on press.
- Tapping Up or Down while either directional view is latched cancels that view.
  Tapping Focus again cancels a latched Focus view. Tapping a direction from Focus,
  or Focus from a directional view, switches to that view.
- Holding for at least 250 ms temporarily selects the requested view. Release
  restores the previously latched view, or normal tracking when none was latched.
- A cancelling tap takes effect on release. If that press becomes a hold, its
  temporary view takes over at the threshold; normal framing never flashes first.
- Opposing simultaneous directions cancel the unfinished gesture and require
  release. An explicit Focus chord takes precedence over directions it contains.
- Pause, Runtime pages, Steam overlay and window focus loss cancel unfinished
  gestures and require release before rearming. Restart, unloading or disabling
  Smooth Camera clears the latch. View modes are not saved to disk.

Automatic tracking continues underneath temporary views, so returning preserves
its normal framing behavior. These controls are active only while Smooth Camera
is enabled and its compositor is available. Native buttons remain unconsumed.

The separate **Horizontal** checkbox defaults to **off**,
including when loading settings from an older version. When enabled, the camera
also follows horizontally near native side-teleport screens. The destination is
drawn beyond the corresponding left/right edge, with its vertical neighbors.
A single native link serves both exits; two links select their respective sides.
Each side is shown only if its edge has at least two consecutive free collision
cells (16 game pixels vertically). A solid wall, a single-cell gap, or separate
single-cell gaps keep that side hidden and prevent panning toward it. This is a
presentation heuristic; it does not change native teleports or prove that the
player can reach or fit through the opening. Actual crossings retain their
coordinate rebase and temporary departure view.

Edge checks recognize native stone, ice, snow and slope geometry. Water, sand
and nonblocking platforms do not close an edge. Unknown mod blocks retain the
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

The separate **240 Hz** checkbox in the main and pause settings enables
presentation up to 240 FPS, subject to the display's refresh rate,
VSync and GPU performance. It defaults to on, preserving previous versions'
behavior. Uncheck it to keep smooth following at the native game cadence.
The switch preserves camera framing, controls, physics time and saved bindings;
it does not remove the smooth-camera hooks or restart the level.
With 240 Hz enabled, the camera advances on each rendered frame while
native simulation and input retain their original update interval. Extra frames
reuse the captured world. Fractional camera positions are applied at output
resolution, avoiding jumps of several display pixels when the game is enlarged.
The king is part of that cached world, so his screen position can visibly step
relative to the camera during fast falls. Turning 240 Hz off removes the extra
camera-only frames. It does not interpolate the king or guarantee stationary
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
test dependencies under `_INTERNAL` are not installable release files.

## Manual installation

After building, with the game closed, install JK Runtime and put
`SmoothCamera.dll` in `Jump King/Content/JKMods/`. Use a single shared
`0Harmony.dll` from this package; do not keep multiple Harmony versions or a
second copy of a Workshop-installed mod. Restart the game. Building does not
install or publish anything.

## Rendering and compatibility

Subframe Charge 0.24+ and Smooth Camera submit independent input/draw requests
to Runtime's single scheduler. Either 240 Hz option can request extra frames;
disabling one client preserves the other. SFC player prediction refreshes the
world atlas each presented frame so the king stays behind native foreground
layers. Both load orders preserve one native physics accumulator.

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
are replayed. Save workers and physics retain the original screen state.

Only interval reads inside the framework's fixed-step scheduler are redirected
to the presentation interval. Native updates receive the unchanged simulation
delta and clock; the public `TargetElapsedTime` property is preserved for physics
and other mods. Disabling/unloading returns partial elapsed time to the native
accumulator. Camera movement uses a critically damped spring with 12-pixel upward
and 64-pixel downward framing bands. The pause state freezes that motion.
The spring responds faster at high player speeds so ordinary fast falls do not
hit the emergency visibility clamp every simulation tick. A target-crossing
guard prevents a sudden landing from producing a later reverse movement.

Native foreground NPCs and the flying gargoyle are composed with the world.
Native NPC/prop draw callers use explicit scoped camera queries, so screen checks
inlined before enabling Smooth Camera cannot hide adjacent characters. Drawing
those characters does not advance their behavior trees or activate screen events.
Pause UI, timer, location labels, fades and lightning remain fixed in the
viewport, outside the screen passes. Unknown third-party foreground overlays retain their
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
compatibility checks. Smooth Camera does not interpolate player animation/physics.

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
