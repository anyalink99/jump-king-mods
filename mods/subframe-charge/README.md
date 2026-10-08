# Subframe Charge

Subframe Charge **0.27.1** catches jump presses and releases that happen between
game updates. It measures the real hold time, then feeds the matching strength
through the game's normal release routine. The mod requires **JK Runtime 1.44+**
and the shared Harmony 2.3.6 included in the package.

[Technical guides](docs/index.md) · [Map setup](docs/map-authoring.md)

In Debug Mode, Runtime's right-drag `P:` prompt follows the effective
Quarter-step Charge setting. Prompted jumps report exact frames and percentages
to Jump%; they don't report a physical SFC hold duration.

By default, measured time is rounded to 17 ms steps. Physics, direction, water
scaling and the release-tick increment stay native. A tap that starts and ends
between updates becomes a minimum jump instead of being lost.

When Controls+ converts Jump to **Press**, charging follows the logical toggle at
the native update rate. Physical-tap replay and hold-time correction are suspended
while that conversion is active. A mechanic that temporarily requires native input
also restores the physical sampling path.

## Settings

The native main/pause settings order is **Enabled**, **Quarter-step Charge**, **Show SFC**,
**Subframe Inputs**, **240 Hz**. Runtime owns the separate **Optimizations** setting. The native dependency check
greys out and blocks 240 Hz while Subframe Inputs is off.

### Quarter-step Charge

**Quarter-step Charge** divides each 17 ms hold-frame interval into four 4.25 ms
steps. It requires **Enabled**, but it does not require **Subframe Inputs** or
**240 Hz**. The setting defaults to off for existing installations.

For a dry jump, holds of 12, 12.25, 12.5 and 12.75 frames become charge steps
13, 13.25, 13.5 and 13.75 because the native release increment still applies.
Water applies its 0.5 multiplier after rounding, producing 6.5, 6.625, 6.75 and
6.875. Minimum and full jumps do not change, and snow keeps its native threshold.

The mode follows the native charge lifecycle:

- a buffered hold starts when the native tree accepts charging, so time before
  landing does not count;
- SFC adjusts power only on a native release below automatic maximum;
- a sampled release cannot change a timer that is still held, delay maximum
  charge or extend the native 1/30-second running grace after an edge; and
- ice drift, direction, poses, eligibility, automatic full charge and
  unmeasurable input stay native.

These intermediate powers change gameplay, not just animation. A fractional
power can rebound from a slope-wall contact where a neighboring whole-frame
power slides, although native whole-frame powers can produce the same kind of
response elsewhere. See the
[Babe of Ascension wall investigation](docs/ascension-slope-wall.md).

Player-enabled quarter-step mode marks the run as modified even when the map
allows normal correction through `AllowSubframeCharge`. Explicitly authored
quarter steps use map attribution instead.

Jump% percentages and the gauge receive the exact scaled charge. Frame mode displays
the fractional hold count even with Show SFC off. Its legacy integer public counter
is rounded to the nearest frame for compatibility. Runtime's exact event
properties keep the fraction, while its legacy integer fields are null for
fractional results. A native or unsupported later jump clears that display.

### Input and presentation settings

- **Show SFC** displays the measured charge.
- **Subframe Inputs** buffers all eleven native pad actions: Up, Down, Left,
  Right, Jump, Pause, Confirm, Cancel, Boots, Ring and Restart. Keyboard/mouse
  actions use Runtime's shared physical-key worker with a 1 ms polling target and respect
  Controls+ alternatives/chords. Native title/pause menus use the same subframe input path with either
  presentation rate. Native gamepad backends are also polled between
  physics ticks on the presentation scheduler. Their polling remains subject
  to a blocked driver or Present/VSync. This doesn't reschedule arbitrary
  third-party controls that bypass the native pad API.
- **240 Hz** enables presentation up to 240 FPS and predicted player drawing. Enabling it enables Subframe Inputs; disabling
  Subframe Inputs disables 240 Hz. Optimizations and charge correction remain
  independent. The display, VSync and GPU still limit the visible frame rate.

Runtime imports the former Optimizations preference once if it has no saved
choice. It caches earthquake XML, exact Rayman player lookups and title-screen
OBS/Streamlabs availability. Native physics, sounds and effects remain intact.
The old SFC XML field is retained for compatibility; Runtime owns later changes.
Keyboard focus loss, overlay activation and leaving a fast menu discard pending
gameplay presses. Native gameplay consumes buffered edges once per update;
several presses of the same action within one native update coalesce. A completed
direction tap becomes one minimum native movement tick. SFC continues to own jump
power and completed-tap replay only while its correcting node actually owns the
live player's jump and the sampler supports that device. Enabled alone doesn't
claim input on the title screen, under an alternate controller, or with an
unavailable sampler. Other cases keep independent native one-tick Jump taps.
The title intro and logo keep native update/input ordering; the fast menu pump
takes over only after the actual menu starts. It can't consume the intro's Space.

Text entry uses Runtime's keyboard capture boundary in all input/presentation
modes. Physical editor keys and Controls+ chords can't also invoke menu or
gameplay actions. Pending worker/native/menu actions are discarded at capture
transitions, and forced close requires release before those keys work again.
Gamepads and the pointer remain available for the on-screen keyboard.

Runtime's MenuTreeSession owns each title tree lifetime. A completed result is
handed to the native parent without rerunning the tree; turning Inputs off keeps
that pending result. Intro/outro phases advance natively, while the active menu
advances between simulation ticks. New native menu lifetimes get new sessions.
Native gameplay Jump edges are preserved when MonoGame's held snapshot arrives
after a worker edge. This keeps native jump eligibility working with both native
and high-refresh presentation; menu edges still use the physical stream only.
Disabling Inputs keeps native held movement but suppresses already-consumed
presses until both physical and native snapshots observe release. Its keyboard
worker stops after that handoff (or immediately when nothing is held).

**240 Hz is a presentation/input mode, not 240 Hz physics.** Simulation, timers,
charge steps, saves, world animations and foreign update hooks keep their native
cadence. The scheduler targets 240 input/menu intervals per second even with
240 Hz presentation off. Keyboard/mouse edges target 1 ms independently of that
scheduler. Charge initiation and takeoff still occur on native simulation ticks
(normally 17 ms); SFC measures the hold between ticks, not 240 Hz jump physics.
The player sprite predicts less than one tick ahead from completed native movement
and outgoing velocity, retaining observed water/material displacement modifiers.
Same-direction deceleration takes effect immediately instead of replaying the
previous fast fall or slope approach. A fresh native walk or takeoff
can still appear before the next body integration. No body position is written
and no speculative physics behaviour runs.
A bounded read-only path clips the drawn hitbox against known native boxes and
slopes, including neighboring screens, ceilings, thin floors and screen edges.
For native slopes, sand and water, a read-only endpoint calculation follows the
game's X-resolution-then-Y order, including slope redirection and current material
speed. Drawing takes a straight path when clear, or the native axis route around
an overhang; a swept guard still prevents tunneling through known thin geometry.
Sand entry immediately uses the native outgoing sink speed, while side/underside
contacts and jumps out keep the material's directional rules. No native body
behaviours or foreign callbacks execute during this calculation.
Ground contact suppresses gravity drift and wall jitter.
An idle supported player stays still even when a nearby slope is known but the
supporting custom block isn't. Fresh takeoff and movement still predict.
Unknown shapes, special quark support, missing geometry and exhausted scan
budgets keep prediction active; they simply provide no additional clipping.
This intentionally permits brief visual correction at unsupported collisions.
Native bounds are read without block callbacks. Slope queries use original native
IL and the live stored shape, so installed collision hooks neither disable drawing
nor execute gameplay side effects for speculative positions. The real simulation
continues to execute its original modded collision pipeline.
Teleports, screen changes, pauses and explicit alternate presentation ownership
still snap to the authoritative pose. There is no interpolation frame of latency.
Runtime diagnostic mode measures path preparation as
`subframe-presentation.collision-path`.
Rendering keeps the original pixel grid and point sampling. It doesn't blur
the image or draw the player above foreground scenery or menus.
HitboxResizer's custom player draw also uses the predicted position, including
the 10 × 14 king on Super Jing World. Its sprite anchor and hitbox stay unchanged.

Smooth Camera **0.6.0+** and SFC register independent requests with Runtime's
single presentation scheduler in either load order. Either client can request
extra frames; disabling one leaves the other request active. Camera world
capture refreshes for the predicted king. Drawing more world frames costs
GPU/CPU time; improvements on every map or every third-party renderer aren't
guaranteed. Mods that advance gameplay inside Draw require separate adapters.

Native update/draw dispatch uses explicit scheduler call sites and Runtime's
non-inlined native boundaries. The permitted simulation step gates the entire
native dispatch, including other mods' update hooks, before any world animation
can advance. This avoids depending on JIT treatment of the small DoUpdate wrapper.

The original charge options remain:

- **Enabled** applies charge correction. When off, **Show SFC** can keep
  measurement without correcting jump power. With both options off, the native
  jump node is retained and SFC attaches no sampler or player observers.
  Subframe Inputs, 240 Hz and Optimizations remain independent. With Inputs on,
  a completed Jump tap between native updates still becomes one minimum held
  tick, even with Enabled off; charge strength otherwise follows native timing.
- **Show SFC** shows timing beside Jump%'s frame count. It doesn't affect
  correction or logging.

The overlay requires Last Jump Value / Jump%. It displays:

| Label | Meaning |
| --- | --- |
| `SFC: 200 ms` | Measured hold duration |
| `SFC: 200 ms (would 12f)` | Correction is off and the predicted input count differs |
| `SFC: Buffered` | Buffered charge in progress, or an uncorrected native buffered launch |
| `SFC: 200 ms (buffered)` | Quarter-step buffered release, measured from native charge acceptance |
| `SFC: Not supported` | This jump has no reliable timing measurement |
| `SFC: -` | No measurement yet, or another controller owns jumping |

Automatic maximum jumps show press-to-takeoff time with `(max)`, not an
invented release time. Buffered jumps keep native power and Jump% values when
Quarter-step Charge is off, correction is disabled, or the native maximum is
reached. Corrected quarter-step buffers publish exact power and frame counts.

## Input and compatibility

Keyboard, mouse buttons, Xbox/XInput and native DirectInput controls are
supported through Runtime. Steam Input's virtual Xbox devices use XInput.
Current primary/secondary bindings and Controls+ chords are respected.

A 1 ms polling target doesn't guarantee 1 ms hardware accuracy. Device report
rates, drivers, focus loss and scheduling affect available evidence.
Unsupported measurements leave the native jump intact.

Water keeps intermediate half-strength steps. The native ice continuation
and direction buffer remain available. Casual Jumping's Vanilla and Casual
modes can use SFC; Casual+ and Ball King's ball jumps use their own controllers.
Runtime suspends the charge policy for any controller reserving native
jumping, then restores it without changing SFC's enabled setting.
SFC yields to an undeclared replacement JumpState instead of treating that
intentional ownership as a missing-node crash. Malformed native graphs still fail.

For ConveyorBlockMod (Workshop 3330536917), Runtime adapts
the conveyor's exact-type jump lookup on belt exit while
preserving belt momentum and native reset conditions. SFC discards
cancelled charge evidence when a block resets the active jump. Updating SFC alone
doesn't supply the Runtime adapter.

Player-enabled ordinary correction marks the run modified unless the map has
`AllowSubframeCharge`. Map-authored correction uses authored attribution.
Measurement-only mode doesn't add that mark.
Switching off both settings also removes the hidden measurement
node, trajectory probe and high-rate sampler; **Show SFC** can explicitly restore
measurement while correction remains off.

## Diagnostics

Detailed jump diagnostics are included in every normal build. No replacement DLL
or diagnostic switch is needed. Unsupported launches save recent input/charge
history and the next 30 player frames. See [automatic diagnostics](docs/diagnostic-build.md).

`SubframeCharge.log` is beside the loaded DLL. For Workshop item 3794767640:

`steamapps/workshop/content/1061090/3794767640/SubframeCharge.log`

Play with the affected device, close the game and send the log with the rough
time of the problem. Include rotated logs if available. Logs contain Jump
timing, device identifiers, bindings and build IDs, not general keyboard typing.
They aren't uploaded automatically.

See [timing and input details](docs/timing.md) and
[testing](docs/testing.md) for limitations and reproduction steps.

## Build

```powershell
.\mods\subframe-charge\build.ps1
```

Output: `build/subframe-charge/UPLOAD_TO_WORKSHOP/SubframeCharge.dll`.
For a local installation, use `install.ps1` with the required Runtime.

`build-macro.ps1` builds `TOOLS/FixedJumpMacro.exe`. F8 sends a Space hold;
F9 exits. The optional argument is the requested duration in milliseconds
(default 28, range 1–590). Check measured SFC time rather than assuming an OS
sleep produces an exact physical duration.


Keyboard boundary polling uses the same physical source as the
worker. A delayed MonoGame keyboard snapshot is never fed back into that edge
history: doing so could synthesize repeated presses while a key remained held.
No timed debounce is used; distinct physical presses remain distinct.

## Map-controlled activation

Add the relevant exact tag to `Tags` in `level_settings.xml`:

- `JKRuntime.MapControlled:subframe-charge.timing`

| Mechanic | ID | On RGB | Off RGB | Local RGB |
| --- | --- | --- | --- | --- |
| Charge correction | `subframe-charge.timing` | `(173,107,211)` | `(173,108,211)` | `(173,109,211)` |

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

[Map authoring: tags, exact colours, complete XML and Subframe Charge rules](docs/map-authoring.md)
contains the full instructions within this mod.
