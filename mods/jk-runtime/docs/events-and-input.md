# Events and input

See [lifecycle](lifecycle.md) for subscription ownership and
[state/time](state-and-time.md) for restore epochs and monotonic timestamps.

Runtime 1.4 observes native gameplay without requiring Subframe Charge. It adds
two lightweight component boundaries around the player's body; it does not
replace the controller or patch the native update method. Without subscribers,
these boundaries do not inspect charge, geometry or player statistics.

## Observation order

`GameplayEvents.Subscribe(owner, callback)` returns a game-thread lease. Track
it with the level's `ModuleContext`. Events carry a process sequence number,
gameplay tick, attempt identity, restore epoch and Stopwatch timestamp.

- Before BodyComp: establish tick identity and the previous support/screen state.
- After BodyComp: report support transitions and the native teleport flag.
- After the player's update: report changes to the native charge timer, jump
  callbacks and screen changes. Charge-ended precedes jump at this boundary.
- PauseManager observations continue while player updates are paused.
- State transactions report started, completed or failed, including rollback.
- Feature-owned teleports call `NotifyTeleport` at the actual transfer point,
  after updating the camera. A forecast is not a teleport event.

Charge events describe boundary observations, not every intermediate instruction
inside JumpState. A press and release completed in one update may produce a jump
without a separately observed charging pose. Event timestamps are delivery
timestamps, not physical press/release times. Pause and restore events can occur
between gameplay ticks. Restore events use screen -1 and zero vectors because
the transaction service does not presume that its participants represent a player.

SFC still publishes `JumpEvents` at its established post-native hook, preserving
Jump% integration. Runtime enriches the corresponding gameplay event with that
same result and does not publish a second legacy result. Without SFC, native
jump callbacks produce `JumpEvidence.Unavailable`: Runtime does not invent ms.
Multiple native callbacks remain separate observations. Other jump providers
should use distinct provider IDs; this bus is not exclusive controller ownership.

`JumpResult.CorrectedFrameCount` and `PredictedFrameCount` report
fractional hold frames before surface scaling and the native release increment.
The extended constructor accepts these after the existing arguments. The original
constructor fills them from its integer counts. Fractional producers pass null
for the corresponding legacy `CorrectedFrames` / `PredictedFrames`; integer and
exact values must agree when both are provided. `CorrectedTimer` remains the
surface-scaled charge time, not a count of input frames. No event changes physics.

With `JumpEvidence.BufferedHold`, hold milliseconds start at the native
charge-acceptance tick, excluding the earlier physical press while airborne or
otherwise ineligible. Exact frame counts may be fractional. `BufferedNative`
remains an uncorrected native buffer; an early sampled release alone does not
mean that the native input has released or that takeoff can be rescheduled.

A failing gameplay subscriber is disabled and recorded by owner. Other
subscribers continue. Recursive publication is rejected. Observers must not
change the action they observe; use the command service for a later mutation.
None of these events changes modified-run attribution.

## Logical actions

`ActionInputs.Read("native.jump")` reads the game's cached logical binding once
per gameplay tick. Its `ActionFrame` contains Down, Pressed and Released. The
first observation after level start establishes a baseline, not a fresh press.
Custom action readers register with an owned ID and must read cached state, not
poll drivers. Recursive reads are rejected. This view never overrides the native
InputComponent, whose buffering and eligibility semantics remain authoritative.

## Native input publication

`NativeInputFrames.Publish(pad, held, pressed)` publishes both native state fields
after device polling and before controller selection/gameplay. `pressed` is a
subset of `held`; include a one-tick held pulse for a completed action if the
producer's policy allows replay. Jump eligibility needs a held Jump and a
pressed Jump on the same native update. Preserve native Jump edges when a
physical sampler leads the message-based native snapshot.

These game-thread APIs do not install hooks or poll hardware. A single producer
must own the publication boundary. Consumers keep using the native getters;
already compiled/inlined getters see the same complete snapshot. Producers should
patch the controller update call site, not rely on tiny getter/callee hooks.
Focus, binding changes, expiry and action replay policy remain the producer's
responsibility. Do not combine unrelated physical and cached keyboard samples
into the same edge history.

`using (NativeInputFrames.BeginMenu(menu, edges))` owns one menu update. Entry
replaces stale native menu input; disposal clears it even after an exception.
It never restores an old consumed Confirm/Cancel. Nested frames are rejected
before changing state. The owner drains its edge queue separately; menu
consumption must not publish those presses to gameplay afterwards.

Text editors own physical keyboard actions while open. Runtime provides
`KeyboardInputGate` for action producers that bypass the native pad layer. Use
one gate per keyboard stream before both sampling publication and queued delivery;
discard pending edges when `Suppress(unfilteredHeld)` returns true. Capture
transitions invalidate old input, including a complete open/close between polls.
After close each stream must observe its own release. Gamepads are not subject
to this keyboard gate. See [shared text entry](ui-pages.md) for the full contract.

Presentation frequency must not select a different input provider. SFC 0.17
uses the same menu path whenever Subframe Inputs is enabled, independently of
its 240 Hz drawing option. Disabling Subframe Inputs restores native dispatch
at the next scheduler boundary.

## Physical evidence

`SharedActionSampler.Acquire(owner, actionId, bindingAuthority)` shares one
physical sampler between consumers of the same action. `Start()` is explicit.
Only one binding authority may configure an action; SFC owns `native.jump`.
Other consumers may join and drain their own queue, but must not change SFC's
bindings. Each receives the same original transition timestamps. Joining does
not replay old edges. `Reset()` clears only the calling client's queue.

Queues are bounded to 256 entries. Overflow, reconfiguration and loss of the
binding authority invalidate measurement rather than returning plausible-looking
durations. The underlying keyboard/mouse, XInput and DirectInput readers retain
their independent health, focus, reconnect and binding epochs. A hung driver
cannot block the game thread or the other backend workers. No duration is
guaranteed accurate to 1 ms by Windows scheduling or by a device's report rate.

Sharing is per logical action, not a promise that unrelated action IDs have one
combined device read. The compatibility `HighRateInputSampler` API remains
available, but new consumers should use the shared service for the same action.
The final client releases its sampler. Without requesting clients, Runtime
does not start high-rate workers. An SFC observation overlay is itself a client,
even when SFC's correction checkbox is off.

SFC keeps its existing 17 ms quantizer, underwater scaling, ice eligibility,
minimum taps and native buffered jumps. NativePause supplies explicit pause
observations; a long frame is not treated as a pause.

## Suspended components

`ComponentSuspension.Acquire(owner, component)` disables a component until its
last cooperating owner releases it, then restores the captured Enabled value.
It does not advance time or infer presentation intent. Warp separately owns a
`PresentationActivity` lease so Replays keeps its animation ticks.

The optional restoredEnabled argument is for an explicit state-restore handoff;
it supplies the first holder's baseline, not permission to override another
holder. Direct foreign writes to Enabled cannot be arbitrated by this API.
Never use it to unpause the global game or erase another controller's state.

## Pause and area observations

`NativePause.Subscribe` supplies pause **state observations** with monotonic
timestamps, including repeated identical values on observed updates. When a native
pause manager exists, subscription immediately calls the callback with its current
state before returning the lease; that initial callback's exception propagates.
Later throwing listeners are removed through the diagnostic observer. Detect
transitions by comparing consecutive values, or use `GameplayEvents.PauseChanged`.
Subscribe during the active player lifetime and dispose with its owner; subscribing
before the pause manager exists does not itself arrange a future pump attachment.
Do not infer pause
from a slow frame. `AreaEntryObserver.ValidateContract()` checks the native
location-component members. An instance's `Update()` finds and retains the
current `LocationComp`, invokes its location/screen checks and sets its pending
new-screen flag when needed. This supports area notifications while a controller
advances movement outside the normal location update. It changes native state;
it is not a general map-region query or an event subscription. Create a fresh
observer for each player lifetime, call it on the game thread and do not retain
its cached component across attempts. Runtime does not schedule its updates.

## Backend and compatibility APIs

`HighRateInputSampler`, `IHighRateInput`, DirectInput/XInput bindings/readers,
`BindingSnapshot`, `PhysicalBindings`, `InputDiagnostics` and `InputLog` remain
public for existing consumers and backend integrations. Prefer the shared sampler
for a common logical action. Directly constructing another reader can create a
second polling/device owner; it is not equivalent to joining the shared stream.

These APIs distinguish configured buttons, physical transitions, logical actions
and backend health. Dispose readers you create, retain source/epoch information,
and treat gaps/overflow/device loss as unavailable evidence. Never read or mutate
live game objects from a physical polling worker. A feature flag advertises the
API; it does not guarantee the device or exact timing resolution is available.
The generated SDK reference lists the lower-level overloads and parameter defaults.
