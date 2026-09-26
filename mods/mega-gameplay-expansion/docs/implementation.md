# Warp Jump implementation

## Prediction and transfer

An isolated native BodyComp simulates the initial velocity until first landing.
It uses game collision/movement routines but omits rendering, sound and gameplay
events. The work budget is bounded. Failed forecasts are not retried every tick
of the same activation.

The first simulated tick resumes at X movement: the live body has already
cached collision/water state, applied wind and cached velocity. Repeating that
prefix would double wind and erase water-entry history. Later ticks run the
complete isolated body pipeline. Wind uses the captured native statistics clock
and advances one clock tick per simulated update; it does not freeze the force
or alter the real game's clock. Sand permits controls without IsOnGround, so a
descending sand contact is also a valid endpoint.

Collision queries use private screen copies with 64-pixel broad-phase cells.
The selected blocks retain their native order and native intersection methods;
no live screen array is modified. The job yields after a 2 ms target or 128
ticks, whichever comes first, with checks between native ticks. There are also
7200-tick, 30-slice and 60-ms active-work ceilings, plus query-count
and collision-index size limits. These are cooperative limits, not
hard real-time guarantees about a single native call or GC pause.

Native side-exit teleports run at their original body-pipeline stage through
Runtime's optional `INativeFlightTeleports` world contract. The forecast follows
single links and distinct left/right links, preserving native horizontal wrap,
Y truncation, source-index arithmetic, the TeleportedPlayer context flag and
forced camera selection. Only the shadow body and its private screen change;
the live camera moves once at Warp transfer. Invalid destinations are rejected
before relocation. Foreign teleport hooks are not simulated by this adapter.
There is no separate crossing-count limit: looping routes use the same finite
tick/work/slice budgets as any other unfinished forecast.

Departure begins while the job is pending. If needed, the invisible interval
waits for a destination. Pause stops calculation as well as animation. Pending
Runtime snapshots retain an immutable seed and restart the isolated job on
restore; failure, cancellation and unload restore controls and the launch state.
The log includes forecast tick count, slices, total work and largest slice.

The real player remains at takeoff during 100 ms of disassembly and a 33 ms
invisible interval. Position then changes to the forecast landing, followed by
about 107 ms of assembly. Pause freezes the transition; visual updates are
capped at 50 ms so a hitch does not consume the entire effect.

Only the player's body and behavior-tree components are suspended; their previous
enabled states are restored. Native InputComponent keeps polling: a fresh held
Jump press remains buffered, and release cancels it. Warp neither consumes that
token nor forces an externally disabled input component on. Charge starts when
the native tree resumes and permits it, including after ordinary splat recovery.
Animation time does not alter charge or landing calculations.

The owned transition component runs after BodyComp and before input/BT.
Finishing assembly allows native ground controls to run in the same tick,
avoiding a stale horizontal-velocity step. Ice momentum is not manually zeroed.

## Pixels and sound

Both endpoints capture their native layered sprite, tint, origin and facing.
Departure uses the current pose; arrival uses idle or the native layered splat
sprite when the forecast's LastVelocity.Y equals PlayerValues.MAX_FALL, exactly
as the installed FailState tests it. Arrival selection creates a new image, so
a completed forecast cannot mutate a pending snapshot's pose. Scatter is radial, capped
at 12 px. Native texels transition to RGB fragments and return to the exact
sprite, including dark armour and equipment.

Paths use native blocking collision queries with independent-axis bounce and
damping. Water does not repel particles. Neighbouring screens are included.
Overlapping texels search for exposed cells within 8 px per axis; deeply buried
ones are hidden during scatter. Assembly reverses precomputed paths.

Sprite reads are cached by texture/source rectangle. A device-owned white
texture supplies fragment colours and is disposed with the device. Unknown
sprite subclasses retain ordinary flight.

Completion calls the native landing-sound selector once and acknowledges the
ground node to avoid a duplicate sound. Audio failure is logged without
leaving controls locked.

## Cancellation and state

Before transfer, cancellation resumes the original flight. After transfer,
it retains native landing state and the selected idle/splat pose. Native input
tokens survive cancellation and restore. Unload releases owned resources.
If a foreign mod replaces material behaviours during the effect, cleanup logs
the mismatch, restores basic body pose/momentum and releases owned suspensions;
it does not overwrite that mod's replacement material state.
Runtime snapshots include the plan, phase and animation age; normal disk saves
do not serialize a pending warp.

## Map variants

Register one MapVariantSet per mechanic. Solid, Zone and Screen share the
same effect controller. Reserve all three colours before adding factories.
Zone overlap is independent of terrain but does not add multiple colours to
one collision pixel or imply support for foreign movement.

## Tests

The suite compares native trajectories across dry/water scenes, ground
materials, directions and obstacles. It also reads the installed native map,
tests non-mutating refusal and checks actual Entity component dispatch for
landing handoff. Separate cases cover delayed transfer, cancellation, snapshots,
sound, particle collisions and all three map variants.

These are automated native-method and model checks. Physical input behavior
and the appearance of the transition still need in-game review.
