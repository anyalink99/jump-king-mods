# Input devices and other movement mods

Keyboard, mouse buttons, Xbox/XInput and native DirectInput controls are
supported through Runtime. Steam Input's virtual Xbox devices use XInput.
Current primary/secondary bindings and Controls+ chords are respected.

A 1 ms polling target doesn't guarantee 1 ms hardware accuracy. Device report
rates, drivers, focus loss and scheduling affect available evidence.
Unsupported measurements leave the native jump intact.

## Fractional physics

Inputs + Physics is a separate gameplay option. Native movement and collision
queries run in smaller steps, including queries against custom `IBlock` geometry.
Foreign body callbacks, player components and alternate input/tree controllers
keep native cadence. Collision phases can explicitly opt
into substeps through Runtime's BodyPipeline API. This avoids accelerating fixed
tick timers and MovingPlatform's hard-coded platform update.

Foreign material callbacks also run when their registered contacts change,
including contacts that begin and end between native ticks. The dispatcher uses
the body's registered block types and actual collided block instances; it has no
list of ice or momentum-stop classes. Stable contacts keep native periodic
cadence. Each invocation receives elapsed time since the previous invocation,
and the shared `PlayerValues.ICE_FRICTION` rate follows that duration while the
material callback runs. Exit invalidates a cached callback result immediately.
The callback sees current and previous physical-step collision snapshots.

This preserves contact delivery and time accounting; it can't infer arbitrary
legacy callback intent. A provider that accumulates a fixed literal on every
invocation rather than using `FrameDelta` can still depend on callback count.
Contact reactions and periodic work should be separate in new controllers.
Native-periodic body work can own a normal `BodyPipeline` phase; collision work
can opt into a substep phase. Don't replay callbacks or getters to classify them.

Conveyor has a separate adapter: its stateful `ModifyXVelocity` contact history
advances at native cadence, while intermediate movement reuses its additive belt
speed. Its original exit logic still supplies momentum and resets jumping.
Native gravity runs through the real material handlers before fractional scaling.
Expansion's SpecialHighGravity and Ghost of the Immortal Babe's SpecialRainGravity
keep their fixed extra acceleration at native cadence. The gravity adapter scales
acceleration before the terminal clamp, including UpSideDownCore's reversed clamp.

Presence alone no longer rejects custom blocks, components or Harmony patches.
Controls+ Left, Right and Jump must still use Hold. This isn't a universal
compatibility guarantee: unknown stateful velocity queries and patches on fast
native methods can depend on tick count and need their own adapter. Keep a
reproduction map and exact mod builds when reporting those cases.

Changes to the physics choice apply at the next safe ground contact in the same
attempt. Stand still and release movement and Jump; charging, sliding, paused
play and external control delay the change. Changing Hold to toggle input
during fractional simulation pauses the attempt. Playback/replica world ownership and performance-suspension
leases stop local simulation while retaining the existing owner's control.

Fractional physics always contributes a run modifier. `AllowSubframeCharge`
permits ordinary charge correction only. Prediction/trajectory tools using a
native fixed-step simulation must provide a separate fractional-physics adapter;
the physical trajectory is no longer a native-cadence replay.

## Charge correction

Subframe Charge includes a native 1 ms preset. The third-party
Millisecond Charge 0.1.0 addon targets the older quarter-step implementation;
its private patch is no longer applicable. Use **Substep Charge > 1 ms** instead.

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
