# Charge, input and presentation settings

Open **Mods > Subframe Charge** in the main or pause menu. Start with the
[README](../README.md) for a short explanation of the switches; this page covers
their dependencies, exact timing and presentation limits.

The native main/pause settings order is **Enabled**, **Enable Substep Charge**,
**Charge Step**, **Show SFC**, **Subframe Inputs**, **240 Hz**.
Runtime owns the separate **Optimizations** setting. The native dependency check
greys out and blocks 240 Hz while Subframe Inputs is off.

## Substep Charge

Turn on **Enable Substep Charge**, then choose **Charge Step**. The selector is
grey and cannot be changed while substeps or **Enabled** are off. Neither
**Subframe Inputs** nor **240 Hz** is required.

| Charge Step | Physical hold interval |
| --- | --- |
| Half-step | 8.5 ms |
| Quarter-step (default) | 4.25 ms |
| Eighth-step | 2.125 ms |
| 1 ms | 1 ms |

Existing installations keep their previous enabled state. An old enabled
Quarter-step Charge setting becomes enabled substeps with Quarter-step selected.
The XML key `QuarterStepCharge` is retained for saved settings and pinned controls;
`ChargeStep` stores the selection. Turning substeps off retains that selection.

The interval controls rounding of measured hold time, not input accuracy or
takeoff latency. Native updates still decide when a jump starts and releases.
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

Multiplayer Expansion's two-client Debug mode temporarily disables **Subframe
Inputs** and **240 Hz**. Both controls show Off and can't be changed until that mode
ends. Your saved choices return afterward. Charge correction, Substep Charge
and Show SFC remain independent. Integrations can hold the same temporary override
through `ModEntry.SuspendPerformanceFeatures()` and dispose it when finished;
multiple owners must all release their handles before performance modes resume.

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

**240 Hz changes presentation and input sampling; physics stays native.**
Predicted drawing looks less than one simulation tick ahead without changing
your body's position. Teleports and pauses reset that prediction. Unsupported
geometry can still cause a brief visual correction. Smooth Camera and SFC share
one presentation scheduler; either can request extra frames.

Read [input ownership and predicted drawing](timing.md#input-ownership-and-predicted-drawing)
for native timing, collision clipping and performance limits.

## Correction and measurement

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
| `SFC: 200 ms (buffered)` | Substep buffered release, measured from native charge acceptance |
| `SFC: Not supported` | This jump has no reliable timing measurement |
| `SFC: -` | No measurement yet, or another controller owns jumping |

Automatic maximum jumps show press-to-takeoff time with `(max)`, not an
invented release time. Buffered jumps keep native power and Jump% values when
Substep Charge is off, correction is disabled, or the native maximum is
reached. Corrected substep buffers publish exact power and frame counts.
