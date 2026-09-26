# Timing and input

Jump King's scheduler targets 17 ms in the installed .NET Framework build,
while its simulation advances by 1/60 second. SFC rounds the measured hold to
the former and supplies the corresponding native timer steps to JumpState.
The native release update still adds its increment. There is no adaptive
calibration or clamp to within one frame of an observed vanilla result.

Water applies the native 0.5 charge multiplier after input-frame selection,
including the release increment. The 71 nonzero underwater release counts
include powers between dry-land steps. Jump% reports input frames separately
from scaled strength.

## Eligibility and buffers

Eligibility follows actual native JumpState visits after physics, preserving
recovery gating and the shared airborne ice-continuation reference. A timestamp
captured before BodyComp defines the frame boundary; handler execution time
does not shift it.

A press held before charge becomes eligible is buffered. SFC leaves its charge,
release and maximum behavior native and shows `Buffered`. Held buffers have no
age cutoff. A completed missed tap has a separate 50 ms delivery deadline and
is not replayed into a later eligibility interval.

Explicit pause spans are subtracted from measured holds. A sampled release can
correct power before the game's cached input catches up, but native JumpState
still performs takeoff. This does not run physics off-thread or remove all
frame latency.

## Evidence

Runtime aggregates bound alternatives like the game: Jump is released only
when every contributing control is released. Device loss invalidates timing
when it affects a contributing source. A neutral unavailable controller does
not invalidate a keyboard-only hold.

Observation gaps over 50 ms invalidate timing; this is a watchdog, not an
accuracy guarantee. Reacquisition requires neutral Jump. Opaque binding changes
are periodically rediscovered; ordinary rebind events apply immediately.
Foreground gating prevents keyboard sampling while the game lacks focus.

Workers read physical state rather than the native frame cache. DirectInput
opens its own handle using the game's device GUID. It does not consume the
game's joystick events or dispose its handle. Unknown custom pad backends
require a usable physical-binding and device-identity contract.

## Native integration

All references to the shared jump node are replaced and restored together.
Sound and particle registries remain shared with the native node.
Measurement-only mode yields to another mod's custom JumpState.

Jump% integration is optional. It updates frame/percentage values only for
corrected jumps; disabled, buffered and unmeasured jumps keep native values.
The comparison uses Jump%'s counter after its update, not velocity inference.
Game-update jitter can produce a wall-time discrepancy larger than one frame.

Diagnostics are queued off-thread and rotated. An abrupt kill can lose the
last queued batch. Device metadata cannot prove Steam Input configuration,
firmware or driver version.
