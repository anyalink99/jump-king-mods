# Side links, frame cadence and compatibility

Smooth Camera assembles the visible map without updating neighboring screens a
second time. Physics, saves, screen events and achievements keep their native
rules. This guide explains portal views, extra presentation frames and the
limits of combining render mods. For everyday adjustments, use
[camera settings](settings.md).

## Horizontal side links

The **Settings > Horizontal** switch defaults to **off**,
including when loading settings from an older version. When enabled, the camera
also follows horizontally near native side teleports and Expansion Blocks
MultiWarp exits. The destination is
drawn beyond the corresponding left/right edge, with its vertical neighbors.
A single native link serves both exits; two links select their respective sides.
Mega Mapping Expansion's `SideLink` declarations use those same native links,
including destinations above screen 255.
Each side is shown only if its edge has at least two consecutive free collision
cells (16 game pixels vertically). A solid wall, a single-cell gap, or separate
single-cell gaps keep that side hidden and prevent panning toward it. This is a
presentation heuristic; it doesn't change native teleports or prove that the
player can reach or fit through the opening. Actual crossings keep their
coordinate rebase and temporary departure view.

Link discovery and edge observations come from JK Runtime's shared `MapTopology`
contract. Self links keep repeated screen images and continuous movement in
either direction, without a vertical camera cut.

MultiWarp destinations are selected at the king's height, including offset
screen numbers. This works in ordinary gameplay and debug mode. Native links
take precedence where both exist. If overlapping MultiWarp blocks disagree on
a destination, the preview stays hidden; an actual crossing still preserves the
camera's coordinate change. Expansion Blocks remains optional, and the camera
doesn't change its collision or teleport behavior.

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

## 240 Hz presentation

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

## Rendering and compatibility

Replays 2.5 or newer publishes a Runtime camera target during viewing. Smooth
Camera follows the recorded subject and evaluates map ranges and zones at that
position while the live player stays suspended. Playback pause freezes camera
motion; seeking reframes immediately, including while paused. Closing the viewer
restores live tracking. Race ghosts do not take control of the camera.

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
