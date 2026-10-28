# Playing with Powers

Enable each mechanic in **Mods > Powers**. The global switches
start off. Maps can authorize them locally; see [map authoring](map-authoring.md).

## Warp Jump

Charge normally. On departure, the mod calculates where the native jump would
first land. The King breaks into compact red, green and blue pixels, disappears
briefly and reassembles at that landing. Falls can warp too.

The real transfer occurs about 133 ms into a 240 ms transition. Arrival uses
the standing sprite, or the native splat pose for a splat landing. Outfit layers and facing are
preserved; scattered pixels bounce off solid terrain.

Custom sprite subclasses are captured through their virtual drawing method,
including Wardrobe+'s Cosmic shader. Captures preserve facing, colour, alpha,
layer offsets and world coordinates, and retain CPU pixels rather than temporary
GPU targets. The material is frozen for the brief transition; normal animated
drawing resumes afterward. Capture work is bounded to 128x128 pixels per custom
layer and 32 layers. Unavailable/oversized/throwing renderers use their static
texture or a simple silhouette instead of cancelling the gameplay mechanic.
Air Dash's outfit echoes share the same capture path. Native plain sprites keep
the existing direct-texture path without an extra GPU capture.

Press and hold Jump during the transition to buffer the next jump. Charge starts
when assembly and any native splat recovery are over, not during the animation.
Releasing before then cancels the buffer, as in vanilla. The original takeoff
press is not reused. Input uses the game's bindings for every device.

The **Warp Jump** checkbox enables the mechanic on unrestricted maps, subject
to explicit screen permission. It defaults to off;
map-authored activation works independently.

### Warp Jump map colours

| RGB | Variant | Activation |
| --- | --- | --- |
| 173, 47, 211 | Solid | Standing on the block arms the next jump or walk-off |
| 173, 48, 211 | Screen | One nonblocking marker enables the rule for that screen |
| 173, 49, 211 | Zone | Nonblocking volume activates on player-hitbox overlap |

Use opaque, exact colours. Screen is metadata, not a solid pixel. Zone affects
only its volume, including entry during flight. Standing still in a Zone does
not teleport; leaving it while grounded removes its activation. An already
started warp completes even when its destination lies outside the Zone.

Each future mechanic uses the same Solid/Zone/Screen scheme.
[The shared registry](../../../docs/modding/block-registry/README.md) owns colours.

### Supported trajectories

Prediction uses native solid, ice, snow, sand, water, quark and slope behavior,
including wall/ceiling collisions, vertical screen transitions and native
side-exit teleport links (single-destination and separate left/right links). Native wind
uses the captured game clock, direction changes, activation latch, NoWind zones
and snow suppression.

Foreign block types, custom teleport implementations and alternative
movement such as Ball form, Casual controls or Jetpack are not predicted.
An unsupported or unfinished forecast leaves ordinary movement intact and logs
the reason. An inactive teleport link nearby does not disable prediction.

Long forecasts run in cooperative slices (2 ms target, checked between native
ticks), while the departure effect plays. A forecast stops after 30 slices,
60 ms of active calculation or 7200 physics ticks,
whichever limit is reached first. Invalid destinations and excessive collision
geometry are rejected. This bounds our work; an individual foreign hook, driver
call or GC pause cannot safely be interrupted mid-call. Sand descent stops at the native
control surface rather than searching for a solid floor underneath. This does
not add support for custom wind patches or moving terrain.

Warp does not fast-forward world timers, moving blocks or triggers. SFC owns
charge measurement when present; Warp uses the resulting velocity.

Set `JK_POWERS_PROFILE=1` to profile the first 12 forecasts per level session.
`Powers.log` records time spent in each shadow-body stage,
landing checks and camera updates, plus counts of caught CLR exceptions.
Counters are kept in memory and written once per completed or failed forecast.
The first continuation tick is measured as a whole; subsequent ticks have stage
timings. The 2 ms slice target and transition constants are unchanged. Timing
instrumentation has some overhead.

JK Runtime's inventory read cache prevents the old restart slowdown. The issue
was previously blamed on subscription order, but clearing the cache reproduced
it: repeated Snake Ring checks during prediction reread the inventory file.
Runtime fills missing cache entries without writing saves or replacing current
inventory/settings. Keep the bounded diagnostics available when checking
repeated restarts in the installed game.

Successful global use marks the run modified unless the map has
`AllowPowers`. Map-authored activation does not add that mark.
## Air Dash

By default, press the bound **Jump** button again in flight to dash horizontally.
A held left/right input chooses the dash direction, including against the
current flight. Without a directional override it follows horizontal motion;
a vertical jump uses the King's facing direction. Opposing held directions
cancel each other and use that same fallback. There
is one dash per flight, refreshed on landing. The activation press is consumed;
release and press Jump again, even during the dash, to use the native buffered
jump. Holding the activation press does not also buffer a jump.

To use another button, open **Controls+ → Powers → Air Dash**.
Primary/secondary buttons and two-button chords are supported, including mouse
buttons on the keyboard profile. Bindings are saved per device profile in
`Powers.Settings.xml`. Reset the row to follow that device's
current Jump bindings again, including future Jump rebinds.

When the activated Dash binding is separate from Jump, Jump keeps its native
buffer behavior: you can hold it before or during the dash without another
release/press. Only an activation sharing a held Jump button consumes that
buffer. Input is observed on all registered devices, not just the last-used
one. Reconnection, pause and snapshot restoration require a fresh custom press.

The tuning is **80 pixels at 6 pixels per physics tick**: 14 ticks,
roughly 238 ms on the normal game clock, with a final 2-pixel step. Y stays fixed
and wind/water/sand speed multipliers do not curve or shorten this segment.
On an unobstructed completion, the King carries half the dash velocity: horizontal
speed 3, vertical speed 0. Native gravity, wind and material behavior resume on
the next tick. The King can therefore travel farther than the 80-pixel dash;
the pre-dash flight velocity is not restored. Walls stop the dash and use native X
collision resolution: a 6-pixel impulse bounces back at 3 pixels per tick.
Slopes retain the game's native normal/projection, not a new ball-style contour.
The sweep checks every pixel, including thin walls. Native screen caps and
side teleports end the dash early.

The effect uses layered-sprite echoes, a tapered cyan/violet wake, a launch ring
and impact sparks. It does not recolour the real King or replace native poses.
The embedded 300 ms whoosh is an 8-bit edit of SoundReality's
[Whoosh Velocity](https://pixabay.com/sound-effects/film-special-effects-whoosh-velocity-383019/).
It starts with actual dash movement, follows native SFX mute/master volume and
pauses with the game. [Audio credits and edit recipe](../assets/audio/README.md).

| RGB | Variant | Activation |
| --- | --- | --- |
| 173, 53, 211 | Solid | Jumping from this terrain grants one dash for that flight; walking off does not |
| 173, 54, 211 | Screen | One nonblocking marker allows activation on that screen |
| 173, 55, 211 | Zone / area | Allows activation while the hitbox overlaps the volume |

The **Air Dash** checkbox enables it globally on unrestricted maps, subject
to explicit screen permission. Zones/screens check scope at
activation; an already started dash finishes outside that scope. Landing on a
different solid clears the previous Solid grant. All three variants share the
same per-flight dash, so overlapping rules cannot grant extra dashes.

Dash follows native Jump bindings by default, or the custom Controls+ row.
It does not recalculate charge and can follow an SFC jump. Ball form, Casual
movement, active Jetpack thrust and a running Warp presentation do not accept
dash input. Warp automatically acts on departure, so enabling both does not
make its frozen presentation interruptible. Disabling a globally started dash
or unloading during an unfinished dash restores its saved velocity. A completed
dash keeps its outgoing momentum. Pause stops the dash and its effect;
Runtime snapshots retain remaining distance, momentum and availability.

Global use follows `AllowPowers` and modified-run attribution.
Authored use does not add a global-use marker. See [implementation](air-dash.md).
