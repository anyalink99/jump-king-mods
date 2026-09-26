# Mechanic boundaries to preserve

These contracts describe how first-party mechanics share Runtime services.
Focused tests cover individual behaviors; combinations also need in-game checks.

## Collision policy belongs to the actor and operation

Ball King uses a corrected `BottomLeft` contour while retaining native collision
callbacks. The native slope instance remains unchanged. Preserve that hybrid
behavior, not a universal repaired triangle. See
[actor-specific geometry](geometry-and-mechanics.md#explicit-geometry-profiles)
and Ball King's geometry adapter (`mods/morph-ball/src/MorphBlockGeometry.cs` in the repository).

Ball King also removes uphill input introduced by native X-slope projection in
specific rolling/release cases. Its sticky contacts, native contact probes,
position-cap corrections and teleport momentum are not ordinary rectangular
walking. A polygon alone does not describe these rules.

Source: native movement (`mods/morph-ball/src/MorphController.NativeMovement.cs` in the repository).

## Screen scope does not determine solidity

Existing Ball King restricted-screen pixels create `BoxBlock` instances as well
as screen rules. Mega Gameplay Expansion's Screen variant records metadata and
returns no block. Preserve both RGB meanings. The generic descriptor needs
separate activation scope and physical-block properties; never make all screen
markers solid or all nonblocking during migration.

Sources: Ball King blocks (`mods/morph-ball/src/BallKingMapBlocks.cs` in the repository),
Mega variants (`mods/mega-gameplay-expansion/src/MapVariantSet.cs` in the repository).

## Input timing is not the native charge timer

SFC rounds physical hold duration using 17 ms; native charge advances by 1/60
second, including its release-update increment. Underwater input frames are
rounded before applying the native charge multiplier. Do not merge these clocks
or quantize the already-scaled strength.

True buffered charge remains native. Eligibility comes from actual native
JumpState visits after physics, not just `IsOnGround`; the latter would lose
ice continuation and recovery semantics. Completed short taps have separate
delivery rules. Explicit pause spans are excluded; a slow frame is not a pause.

Sources: quantizer (`mods/subframe-charge/src/ChargeQuantizer.cs` in the repository),
eligibility timeline (`mods/subframe-charge/src/ChargeTimeline.cs` in the repository),
native integration (`mods/subframe-charge/src/SubframeChargeState.cs` in the repository).

## Enabled, available, active and consuming input differ

Casual keeps native charge; Casual+ owns a separate jump controller and SFC
yields. Casual yields while morphed. Jetpack requires a fresh airborne press
after release; a ball jump or attachment disarms thrust so one held button does
not launch both mechanics. A ball jump sequence is not an SFC charge result.

The distinction between `ThrustEnabled` and `ThrustActive` matters: Warp rejects
an enabled Jetpack controller even before thrust starts because thrust could
change the predicted flight. No Walk Off rejects active thrust, not merely
installed/enabled equipment. Casual+'s held landing-buffer policy also depends
on Jetpack availability. Replacing all these tests with one `Active` boolean
would change play.

Sources: Casual controller (`mods/casual-jumping/src/CasualController.cs` in the repository),
SFC installer (`mods/subframe-charge/src/SubframeChargeInstaller.cs` in the repository),
Jetpack controller (`mods/more-items/src/Jetpack/JetpackController.cs` in the repository),
Warp controller (`mods/mega-gameplay-expansion/src/WarpController.cs` in the repository),
No Walk Off (`mods/mega-gameplay-expansion/src/NoWalkOffController.cs` in the repository).

## Blocking geometry is not necessarily standing support

No Walk Off guards actual grounded displacement after water/material scaling
and X collision, before Y collision. A lower block or slope-only contact is not
flat support. Ice and Snake Ring's native ice movement remain exempt, including
entry onto ice during the same displacement. The second outward input requires
release/neutral; the rule cannot be replaced with a universal velocity clamp.

Ball King and Casual+ each have their own support/grace/buffer policies. A
Runtime support query should report observations and let the controller decide
eligibility rather than enforcing one shared coyote duration.

## Warp is not world fast-forward

Warp starts from the resulting native departure velocity. It must not reround
SFC input or treat predicted flight ticks as elapsed world time. The real
transfer is delayed by the effect; world timers and triggers are not advanced
by the skipped trajectory. Unsupported routes keep ordinary movement.

Its component order is BodyComp, warp transition, input, behavior tree. Finishing
before/after the wrong slot can reuse flight velocity and reintroduce landing
drift. An initiated authored warp can finish after leaving its activation zone.

Source: Warp controller (`mods/mega-gameplay-expansion/src/WarpController.cs` in the repository).

## Physical pause, presentation and recording need distinct contracts

Warp disables body, input and behavior tree during its animation. Its own visual
component keeps updating unless the game is paused. Warp holds a
`PresentationActivity` lease, and Replays retains ticks with that declaration
even while the body is disabled. Controller fixtures cover
completion/cancellation/restore; recorder tests cover the disabled-body gate.
The combined visual result still needs an end-to-end playtest.

Do not hide this behind a generic `IsPaused` API. Runtime provides owned suspension
and explicit recording/presentation policy. Distinguish world pause, player
physics suspension, input capture and replay viewing. Multiple holders must not
restore a shared Enabled flag while another holder still needs suspension.
Warp uses ComponentSuspension, with nested-holder and restored-disabled-state
tests. This is distinct from global NativePause and PresentationActivity.

The pose format retains origin/destination poses and elapsed ticks, not the RGB
dissolve particles. A visual-effect track and a combined end-to-end recording
playback test remain separate work; the pose format does not provide full
visual-effect serialization.

Sources: Warp freeze/advance (`mods/mega-gameplay-expansion/src/WarpController.cs` in the repository),
recording gate (`mods/replays/src/ReplayRecorder.cs` in the repository),
playback clock (`mods/replays/src/ReplaySession.cs` in the repository).

## Restore, replay and persistent state are separate operations

Rewinder restores registered movement state and the native takeoff anchor, not
inventory or world time. More Items stacks persist across worlds; new attempts
reset placed pickup collection, not owned stacks. Do not make all registered
state automatically rewindable.

Replays uses recorded poses and its own owned clock/presentation session. It is
not input resimulation and must not depend on full simulation coverage to play
existing recordings. Physical timestamps remain monotonic across any restore;
SFC invalidates associations using RestoreEpoch, even after a failed/rolled-back
transaction. Native simulation time, real input time and presentation time need
explicit meanings.

Sources: Rewinder (`mods/more-items/src/Rewinder/RewinderController.cs` in the repository),
item API (`mods/more-items/docs/api.md` in the repository), recording (`mods/replays/docs/recording.md` in the repository),
SFC restore invalidation (`mods/subframe-charge/src/SubframeChargeState.Input.cs` in the repository).

## Permission and run history cannot be normalized to a setting

SFC's enabled correction registers modified behavior unless permitted by the
map. Jetpack records actual assisted use, as do successful global Warp transfer
and No Walk Off edge stops. Authored Mega activation has different permission
semantics from a global checkbox. Preserve each trigger during migration.

Removing a modifier or rewinding movement does not erase its contribution to
the attempt. Map permission removes only that mechanic's own restriction, not
other contributors or a community's submission rules. A visible timing overlay
is not proof that correction was enabled.

## Migration acceptance cases

- Native and ball bottom-slope behavior remain distinct without shared mutation.
- Existing Screen colors retain both their collision and activation meaning.
- SFC preserves dry/water, buffered, ice, pause and restore semantics.
- A ball jump cannot activate thrust from the same unreleased input.
- Casual/Jetpack availability and active-thrust cases remain distinct.
- Warp landing order, scoped activation and world-time behavior stay unchanged.
- Warp recording explicitly accounts for suspended-physics presentation ticks.
- Nested input/physics/presentation leases release only their own ownership.
- Restore never refunds unrelated inventory or rewinds hardware evidence/history.
- Disabled consumers create no high-rate readers or simulation work.

Each new shared service should be migrated with one real producer and consumer
and these relevant cases, rather than replacing every mod's integration at once.
