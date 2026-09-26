# Mega Gameplay Expansion

[Technical documentation](docs/index.md).

Custom-map mechanics and an experimental universal gimmick library. Requires
**JK Runtime 1.32 or newer**. Includes Warp Jump, No Walk Off and Air Dash;
their global checkboxes default to off and are pinned by default.
Map-authored activation works independently.

Current version: **0.12.0**.

## Gimmick library

Open **Gimmick library** in main or pause settings to browse mechanics by map,
region, provider or behaviour. Search supports combined filters, exact colours,
a palette and saved queries. Applying an override requires a loaded map.
Edit a draft, choose its scope, then use **Apply**. Pinning an entry creates a
shortcut without enabling it.

**Active & reset** lists MGE overrides and built-in modes. **Restore original map
state** releases MGE's map overrides while keeping configurations, pins and global
mod settings. Neither bulk action changes third-party settings. Those remain
separately editable in **Mod settings**. If you used bulk reset in 0.9.1 or 0.9.2,
read the [recovery note](docs/gimmick-controls.md#library-menu).

New materials use semantic placement: surfaces retain terrain shape, while
nonblocking media fill empty space around solids and slopes. Native Wind has
direction, strength and scope controls. Reflected `IBlockBehaviour` fields are
read-only diagnostics; use **Browse this mod's materials** to apply a block
instead of forcing its collision handler.

Discovery uses native contracts and reflection, without per-mod adapters.
Supported unseen materials can be constructed from registered factories;
arbitrary map controllers and assets cannot. Unsupported entries explain why
they cannot be applied. See [discovery and control](docs/gimmick-library.md) and
[search, placement and wind](docs/gimmick-controls.md) for details.

**Binds** edits Air Dash's primary and secondary bindings, including two-button
chords. **Default** restores following Jump. Text editing captures keyboard input
so typing cannot trigger gameplay controls.

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

The **Warp Jump** checkbox enables the mechanic everywhere. It defaults to off;
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
[The shared registry](../../docs/modding/block-registry/README.md) owns colours.

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

The mod profiles the first 12 forecasts per level session.
`MegaGameplayExpansion.log` records time spent in each shadow-body stage,
landing checks and camera updates, plus counts of caught CLR exceptions.
Counters are kept in memory and written once per completed or failed forecast.
The first continuation tick is measured as a whole; subsequent ticks have stage
timings. The 2 ms slice target and transition constants are unchanged. Timing
instrumentation has some overhead.

Use **JK Runtime 1.5.1 or newer** for the restart slowdown fix. The slowdown
previously attributed to subscription order was reproduced by clearing the
native inventory cache: repeated Snake Ring checks in prediction reread the
inventory file. Runtime fills missing read-cache entries without writing
saves or replacing current inventory/settings. Keep the bounded diagnostics
available to verify repeated restarts in the installed game.

Successful global use marks the run modified unless the map has
`AllowMegaGameplayExpansion`. Map-authored activation does not add that mark.

## No Walk Off

Hold a direction to walk to the edge. The King stops at the last whole-pixel
position with one pixel of support and stays there without drifting. Release
that direction, then press it again to walk off. Jumping remains unrestricted;
turning back lets you retreat and arms the next approach normally.

No Walk Off only intervenes in verified ordinary walking. One motion observer
compares commanded speed, actual displacement and post-material momentum.
Wind, ice/Snake Ring inertia, conveyor offsets and other nonstandard motion
release the guard even while a direction is held. JK Runtime observes supported
arithmetic in the actual modifier calls: water, low gravity and unfamiliar pure
speed scaling do not need per-material coefficients. Protection resumes after an ordinary movement
cycle has been observed. Unknown altered movement is left untouched rather than
treated as walking. Unsupported arithmetic or a changed movement patch graph
releases protection and is available through Runtime's observation diagnostics.

JumpKingPlus low-gravity walking is supported, including its legacy speed mode
and water combinations. Top-facing JumpKingPlus one-way platforms count as
support while standing on their top edge; upward passage remains unchanged.
One-way ice still follows the inertia rule above.

Snake Ring makes grounded movement use the ice model on every surface. Its
acceleration and fractional displacement explain the fine approaches previously
made with tiny inputs. No Walk Off does not replace that model.

| RGB | Variant | Activation |
| --- | --- | --- |
| 173, 50, 211 | Solid | While supported by this terrain |
| 173, 51, 211 | Screen | One nonblocking marker affects the whole screen |
| 173, 52, 211 | Zone / area | While the player's hitbox overlaps this volume |

The **No Walk Off** checkbox applies the same rule globally. An ordinary
neighbouring platform does not inherit Solid activation; leaving a Zone removes
its rule. Joined surfaces do not create artificial edges. A lower platform
does not count as support at the current height.
At a flat-to-descending-slope seam, the King stops at the last flat support;
release and press again to enter the slope. The slope itself is not flat support.

The mechanic guards grounded movement only. It does not catch an airborne King,
stop native slope sliding, change teleport destinations, or control Ball form
and active Jetpack thrust. Direction is read through native input, including
keyboard, mouse bindings, gamepads and Controls+ chords. An analog stick must
return to neutral before a second outward input can release the edge stop.

Warp Jump still handles actual departures; a stopped walk does not start a warp.
Global edge stops use the same `AllowMegaGameplayExpansion` permission and
modified-run policy as Warp Jump. Merely enabling the checkbox does not mark a
run until it changes movement. See [implementation](docs/no-walk-off.md).

## Air Dash

By default, press the bound **Jump** button again in flight to dash horizontally.
A held left/right input chooses the dash direction, including against the
current flight. Without a directional override it follows horizontal motion;
a vertical jump uses the King's facing direction. Opposing held directions
cancel each other and use that same fallback. There
is one dash per flight, refreshed on landing. The activation press is consumed;
release and press Jump again, even during the dash, to use the native buffered
jump. Holding the activation press does not also buffer a jump.

To use another button, open **Controls+ → Mega Gameplay Expansion → Air Dash**.
Primary/secondary buttons and two-button chords are supported, including mouse
buttons on the keyboard profile. Bindings are saved per device profile in
`MegaGameplayExpansion.Settings.xml`. Reset the row to follow that device's
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
pauses with the game. [Audio credits and edit recipe](assets/audio/README.md).

| RGB | Variant | Activation |
| --- | --- | --- |
| 173, 53, 211 | Solid | Jumping from this terrain grants one dash for that flight; walking off does not |
| 173, 54, 211 | Screen | One nonblocking marker allows activation on that screen |
| 173, 55, 211 | Zone / area | Allows activation while the hitbox overlaps the volume |

The **Air Dash** checkbox enables it globally. Zones/screens check scope at
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

Global use follows `AllowMegaGameplayExpansion` and modified-run attribution.
Authored use does not add a global-use marker. See [implementation](docs/air-dash.md).

## Development

The implemented library is documented in [its guide](docs/gimmick-library.md).
The read-only installed-map audit runs with
`python mods/mega-gameplay-expansion/tools/audit_gimmicks.py` from the repository
root. Audit tests run with
`python -m unittest discover -s mods/mega-gameplay-expansion/tests -p test_gimmick_audit.py`.

```powershell
.\mods\mega-gameplay-expansion\build.ps1
.\mods\mega-gameplay-expansion\build.ps1 -Tier Integration
```

Default builds run the fast regression tier, including all Dash and No Walk Off
cases and a representative native trajectory grid. Integration retains the full
2160-case trajectory sweep, installed-map/wind fixtures and native audio checks.
Gimmick checks use a separate unknown-provider DLL and real Harmony patches;
Integration also checks native pin menus and renders the library offscreen.
To refresh the Runtime SDK as well, use
`scripts/check-mods.ps1 -Mod mega-gameplay-expansion`. See [testing](../../docs/testing.md).

Output: `build/mega-gameplay-expansion/UPLOAD_TO_WORKSHOP/MegaGameplayExpansion.dll`.
No separate Harmony or Runtime DLL is included in this package.

`MegaGameplayExpansion.log` is beside the DLL and rotates at 512 KiB.
See [implementation](docs/implementation.md) for update order, cancellation
and test coverage. `render-preview.ps1` renders the effect offscreen for review.

### Workshop artwork

Run `build-preview.ps1` to regenerate `workshop-preview.png` (256x256, under
34 KiB). It loads the installed game's idle and airborne sprites, uses the
mod's RGB particle and dash-trail code, and draws the shared Litter Lover title.
For readability in a still, the idle sprite's disassembly phase varies across
its silhouette; this is a cover composition, not a single gameplay frame.
The script also writes a nearest-neighbour 1024x1024 review copy under
`build/mega-gameplay-expansion/preview/`. No installation or game launch is needed.
