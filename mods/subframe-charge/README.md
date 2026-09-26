# Subframe Charge

[Technical documentation](docs/index.md).

Subframe Charge **0.25.0** measures how long Jump is held between game updates and
uses that duration to select a jump strength through the native release routine. It requires **JK Runtime 1.30+**
and shared Harmony 2.3.6 (included in the package).

By default the measured duration is rounded to 17 ms steps. Native physics, direction,
water scaling and the release-tick increment are retained. A tap entirely
between updates produces the minimum jump rather than disappearing.

## Settings

The native main/pause settings order is **Enabled**, **Quarter-step Charge**, **Show SFC**,
**Subframe Inputs**, **240 Hz**. Runtime owns the separate **Optimizations** setting. The native dependency check
greys out and blocks 240 Hz while Subframe Inputs is off.

**Quarter-step Charge** subdivides each hold-frame interval into four (4.25 ms
on SFC's existing 17 ms clock). It requires Enabled but is independent of Subframe
Inputs and 240 Hz. Existing settings default it to off. For example, 12, 12.25,
12.5 and 12.75 hold frames become dry charge steps 13, 13.25, 13.5 and 13.75.
The native release increment is retained. Water's 0.5 multiplier is applied
after rounding, so those become 6.5, 6.625, 6.75 and 6.875 charge steps. The
minimum and full jump are unchanged; snow still applies its native threshold.
Quarter-step mode also measures buffered holds from the tick when the native
tree actually accepts the charge. Time held before landing is excluded. Power
is adjusted only on a native release below the automatic maximum; a sampled
release cannot change a still-held native timer, delay maximum charge, or extend
the native 1/30-second running-charge leniency after leaving an edge. Ice drift,
direction, charge poses and eligibility remain native. Automatic full charge and
unmeasurable input remain native; whole-step mode keeps its original buffers.
New intermediate trajectories are a gameplay change, not just smoother drawing.
They can also change native slope-wall contact outcomes: some fractional powers
rebound where neighboring whole-frame powers slide. The same native response is
also possible at other whole-frame powers without SFC. See the
[Babe of Ascension wall investigation](docs/ascension-slope-wall.md).
Quarter-step mode marks the run as modified even if the map allows ordinary
SFC timing correction through `AllowSubframeCharge`.

Jump% percentages and gauge receive the exact scaled charge; frame mode displays
the fractional hold count even with Show SFC off. Its legacy integer public counter
is rounded to the nearest frame for compatibility. Runtime's exact event
properties preserve the fraction, while its legacy integer fields are null for
fractional results. A native or unsupported subsequent jump clears that display.

The additional presentation features are:
- **Subframe Inputs** buffers all eleven native pad actions: Up, Down, Left,
  Right, Jump, Pause, Confirm, Cancel, Boots, Ring and Restart. Keyboard/mouse
  actions use Runtime's shared physical-key worker with a 1 ms polling target and respect
  Controls+ alternatives/chords. Native title/pause menus use the same subframe input path with either
  presentation rate. Native gamepad backends are also polled between
  physics ticks on the presentation scheduler. Their polling remains subject
  to a blocked driver or Present/VSync. This does not reschedule arbitrary
  third-party controls that bypass the native pad API.
- **240 Hz** enables presentation up to 240 FPS and predicted player drawing. Enabling it enables Subframe Inputs; disabling
  Subframe Inputs disables 240 Hz. Optimizations and charge correction remain
  independent. The display, VSync and GPU still limit the visible frame rate.

Runtime imports the former Optimizations preference once if it has no saved
choice. It caches earthquake XML, exact Rayman player lookups and title-screen
OBS/Streamlabs availability. Native physics, sounds and effects remain intact.
The old SFC XML field is retained for compatibility; Runtime owns subsequent changes.
Keyboard focus loss, overlay activation and leaving a fast menu discard pending
gameplay presses. Native gameplay consumes buffered edges once per update;
several presses of the same action within one native update coalesce. A completed
direction tap becomes one minimum native movement tick. SFC continues to own jump
power and completed-tap replay only while its correcting node actually owns the
live player's jump and the sampler supports that device. Enabled alone does not
claim input on the title screen, under an alternate controller, or with an
unavailable sampler. Other cases retain independent native one-tick Jump taps.
The title intro and logo keep native update/input ordering; the fast menu pump
takes over only after the actual menu starts. It cannot consume the intro's Space.

Text entry uses Runtime's keyboard capture boundary in all input/presentation
modes. Physical editor keys and Controls+ chords cannot also invoke menu or
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
Disabling Inputs preserves native held movement but suppresses already-consumed
presses until both physical and native snapshots observe release. Its keyboard
worker stops after that handoff (or immediately when nothing is held).

**240 Hz is a presentation/input mode, not 240 Hz physics.** Simulation, timers,
charge steps, saves, world animations and foreign update hooks retain their native
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
contacts and jumps out retain the material's directional rules. No native body
behaviours or foreign callbacks execute during this calculation.
Ground contact suppresses gravity drift and wall jitter.
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
Rendering retains the original pixel grid and point sampling. It does not blur
the image or draw the player above foreground scenery or menus.

Smooth Camera **0.6.0+** and SFC register independent requests with Runtime's
single presentation scheduler in either load order. Either client can request
extra frames; disabling one leaves the other request active. Camera world
capture refreshes for the predicted king. Drawing more world frames costs
GPU/CPU time; improvements on every map or every third-party renderer are not
guaranteed. Mods that advance gameplay inside Draw require separate adapters.

Native update/draw dispatch uses explicit scheduler call sites and Runtime's
non-inlined native boundaries. The permitted simulation step gates the entire
native dispatch, including other mods' update hooks, before any world animation
can advance. This avoids depending on JIT treatment of the small DoUpdate wrapper.

The original charge options remain:

- **Enabled** applies charge correction. When off, **Show SFC** can retain
  measurement without correcting jump power. With both options off, the native
  jump node is retained and SFC attaches no sampler or player observers.
  Subframe Inputs, 240 Hz and Optimizations remain independent. With Inputs on,
  a completed Jump tap between native updates still becomes one minimum held
  tick, even with Enabled off; charge strength otherwise follows native timing.
- **Show SFC** shows timing beside Jump%'s frame count. It does not affect
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

A 1 ms polling target does not guarantee 1 ms hardware accuracy. Device report
rates, drivers, focus loss and scheduling affect available evidence.
Unsupported measurements leave the native jump intact.

Water retains intermediate half-strength steps. The native ice continuation
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
does not supply the Runtime adapter.

Correction marks the run modified unless the map has `AllowSubframeCharge`.
Measurement-only mode does not add that mark.
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
They are not uploaded automatically.

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
