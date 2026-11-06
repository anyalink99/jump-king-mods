# Charge, input and presentation settings

Open **Mods > Subframe Charge** in the main or pause menu. Start with the
[README](../README.md) for a short explanation of the switches; this page covers
their dependencies, exact timing and presentation limits.

The native main/pause settings order is **Enabled**, **Substep Charge**,
**Mode**, **Smooth Rendering**, **Show SFC**.
Runtime owns the separate **Optimizations** setting. Smooth Rendering is available
in All Inputs and Inputs + Physics. Charge disables the switch and extra rendering;
its saved preference returns when either input mode is selected again.

## Substep Charge

Choose **Substep Charge** to select Off or a finer charge interval. The selector
is grey and cannot be changed while **Enabled** is off. Neither
**All Inputs** nor **Smooth Rendering** is required.

| Substep Charge | Physical hold interval |
| --- | --- |
| Off (default) | 17 ms |
| Half | 8.5 ms |
| Quarter | 4.25 ms |
| Eighth | 2.125 ms |
| 1 ms | 1 ms |

Existing installations keep their previous enabled state. An old enabled
Quarter-step Charge setting becomes enabled substeps with Quarter-step selected.
The XML key `QuarterStepCharge` is retained for saved settings and pinned controls;
`ChargeStep` stores the selection. Turning substeps off retains that selection.

The interval controls rounding of measured hold time, not input accuracy or
takeoff latency. Player updates decide when a jump starts and releases: native
ticks in Charge/All Inputs, fractional ticks in Inputs + Physics.
Debug Mode's Prompted Jump uses the same effective interval. Each logical pixel
of vertical dragging selects one substep; with substeps off, four pixels select
one frame. Map-authored intervals also apply to the prompt.

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
[Babe of Ascension wall investigation](ascension-slope-wall.md).

Player-enabled substep mode marks the run as modified even when the map
allows normal correction through `AllowSubframeCharge`. Explicitly authored
substeps use map attribution instead.

Jump% percentages and the gauge receive the exact scaled charge. Frame mode displays
the fractional hold count even with Show SFC off. Its legacy integer public counter
is rounded to the nearest frame for compatibility. Runtime's exact event
properties keep the fraction, while its legacy integer fields are null for
fractional results. A native or unsupported later jump clears that display.

## Input and presentation settings

Mode has three choices:

| Mode | Input | Player physics |
| --- | --- | --- |
| Charge | Native controls, with the existing charge sampler | Native cadence |
| All Inputs | Ordered physical edges and responsive menus | Native cadence |
| Inputs + Physics | Separate timestamped movement/jump frames | Fractional native player steps |

Existing `SubframeInputs=false` migrates to Charge; `true` migrates to All Inputs.
The `HighRefresh` choice is preserved independently. Physics is never enabled by
migration. Entering or leaving Inputs + Physics waits for stable ground, no
movement or charge, and released Left, Right and Jump. The player must be under
local native control. The change applies after a completed world update in the
same attempt; paused or unfocused play waits until gameplay resumes. The menu
shows the selected mode without a waiting message. Choosing the active mode
again cancels the request. Choosing Charge disables Smooth Rendering immediately,
even while the physics handoff is pending.

The physics mode targets 240 player steps per wall-clock second. It keeps the
game's original speed, scales gravity/wind/friction and retains the direction
buffer's duration. Saves, world animations, equipment and unrelated components
keep their native cadence. Collision timing changes intentionally, so the mode
marks the run modified even on maps allowing ordinary charge correction.

The mode preserves foreign tick callbacks and alternate controllers at native
cadence while advancing body movement in smaller steps. Custom geometry alone
doesn't disable physics. Left, Right and Jump must use Hold; switching them to
toggle during simulation pauses the attempt. See [compatibility](compatibility.md)
for stateful material queries and patches that need their own adapter.

Multiplayer Expansion's two-client Debug mode suspends local fractional
simulation. Saved preferences aren't changed. Integrations can hold the same
temporary override through `ModEntry.SuspendPerformanceFeatures()`; all owners
must release their handles before local simulation resumes.

- **Show SFC** displays the measured charge.
- **All Inputs** buffers all eleven native pad actions: Up, Down, Left,
  Right, Jump, Pause, Confirm, Cancel, Boots, Ring and Restart. Keyboard/mouse
  actions use Runtime's shared physical-key worker with a 1 ms polling target and respect
  Controls+ alternatives/chords. Native title/pause menus use the same subframe input path with either
  presentation rate. Native gamepad backends are also polled between
  physics ticks on the presentation scheduler. Their polling remains subject
  to a blocked driver or Present/VSync. This doesn't reschedule arbitrary
  third-party controls that bypass the native pad API.
- **Smooth Rendering** requests presentation up to 240 FPS in either input mode. It doesn't change
  the input or physics selection. With native physics it predicts player drawing;
  with fractional physics it draws the latest calculated position directly.
  The display, VSync and GPU still limit the visible frame rate.

Runtime imports the former Optimizations preference once if it has no saved
choice. It caches earthquake XML, exact Rayman player lookups and title-screen
OBS/Streamlabs availability. Native physics, sounds and effects remain intact.
The old SFC XML field is retained for compatibility; Runtime owns later changes.
Keyboard focus loss, overlay activation and leaving a fast menu discard pending
gameplay presses. Native gameplay consumes buffered edges once per update;
several presses of the same action within one native update coalesce. A completed
direction tap becomes one minimum player movement tick. Sequential opposite
presses keep their order; simultaneous opposite holds retain native cancellation.
A stale native keyboard snapshot can't overwrite a fresh physical hold or release.
SFC continues to own jump
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

With All Inputs, predicted drawing looks less than one simulation tick ahead without changing
your body's position. Teleports and pauses reset that prediction. Unsupported
geometry can still cause a brief visual correction. Smooth Camera and SFC share
one presentation scheduler; either can request extra frames.

Read [input ownership and predicted drawing](timing.md#input-ownership-and-predicted-drawing)
for native timing, collision clipping and performance limits.

## Correction and measurement

- **Enabled** applies charge correction. When off, **Show SFC** can keep
  measurement without correcting jump power. With both options off, the native
  jump node is retained and SFC attaches no sampler or player observers.
  Mode and Optimizations remain independent of correction; Smooth Rendering requires
  an input mode. With All Inputs,
  a completed Jump tap between native updates still becomes one minimum held
  tick, even with Enabled off; charge strength otherwise follows native timing.
- **Show SFC** shows timing beside Jump%, or independently when Jump% isn't installed. It doesn't affect
  correction or logging.

Without Last Jump Value / Jump%, SFC uses the same upper-left position and outlined
text style. The native pause menu hides the standalone overlay. With Jump%
installed, its visibility and positioning still control the combined text. SFC displays:

| Label | Meaning |
| --- | --- |
| `SFC: 200 ms` | Measured hold duration |
| `SFC: 200 ms (would 12f)` | Correction is off and the predicted input count differs |
| `SFC: Buffered` | Buffered charge in progress, or an uncorrected native buffered launch |
| `SFC: 200 ms (buffered)` | Substep buffered release, measured from native charge acceptance |
| `SFC: Not supported` | This jump has no reliable timing measurement |
| `SFC: -` | No measurement yet, or another controller owns jumping |

Automatic maximum jumps show press-to-takeoff time with `(max)`, not an
invented release time. Buffered jumps keep native power and Jump% values when
Substep Charge is off, correction is disabled, or the native maximum is
reached. Corrected substep buffers publish exact power and frame counts.
