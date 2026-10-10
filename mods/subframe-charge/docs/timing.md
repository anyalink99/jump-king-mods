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
doesn't shift it.

A press held before charge becomes eligible is buffered. SFC leaves its charge,
release and maximum behavior native and shows `Buffered`. Held buffers have no
age cutoff. A completed missed tap has a separate 50 ms delivery deadline and
isn't replayed into a later eligibility interval.

Explicit pause spans are subtracted from measured holds. A sampled release can
correct power before the game's cached input catches up, but native JumpState
still performs takeoff. This doesn't run physics off-thread or remove all
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
opens its own handle using the game's device GUID. It doesn't consume the
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
last queued batch. Device metadata can't prove Steam Input configuration,
firmware or driver version.

## Physical and native keyboard snapshots

Keyboard boundary polling uses the same physical source as the
worker. A delayed MonoGame keyboard snapshot is never fed back into that edge
history: doing so could synthesize repeated presses while a key remained held.
No timed debounce is used; distinct physical presses remain distinct.

## Input ownership and predicted drawing

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
