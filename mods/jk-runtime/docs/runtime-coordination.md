# Runtime coordination

These services coordinate input, extra presentation frames, scene composition,
controller ownership and shared hooks between participating mods. They expose
resources with explicit owners instead of relying on load order.

The assembly identity remains `1.0.0.0`. Packages built with this SDK declare
minimum API **2.0**, and an older Runtime rejects them before loading implementation
code. Runtime 2.0 also accepts existing API 1.0–1.44 packages. See the
[compatibility policy](api-policy.md) for frozen signatures and binary checks.

## Camera targets

Use `Presentation.CameraTargets` for viewer tracking without moving the live
player. This service is available since API 1.39. Discovery uses
`RuntimeApi.Supports("camera-targets-v1")`. Acquire a lease with
`CameraTargets.Acquire(owner, sessionScope)`, then call `Publish` before drawing
each changed frame. Close the scope on viewer exit, failure and level teardown.
Acquisition is exclusive: a second owner throws and leaves the first untouched.
The lease is visible in `RuntimeResources.Inspect()` and can also be disposed early.

`CameraTarget` carries a world-space center, velocity in pixels per second,
zero-based screen and presentation pause state. Coordinates and velocity must be
finite and the screen must be nonnegative. Consumers check the screen against
the loaded world. Native pause and suspended physics are separate from this
presentation pause state.

Consumers read a value snapshot with `TryRead`. False means normal live tracking;
an acquired lease publishes nothing until its first valid frame. `Revision`
changes on each publication, so cached renders can be invalidated.
`Discontinuity` changes on the first frame of a session or `Publish(target, true)`.
Use that explicit cut for every seek, including small or paused seeks. Consumers
reset their tracking history on cuts, even if no frame observed the gap between
two sessions. Pausing alone preserves continuity. All calls use the game thread.

This service does not update the native camera, move bodies, schedule frames or
choose camera smoothing. Replays owns its native screen selection; Smooth Camera
uses the same target for tracking and map policy, then owns its own rendering.

## Input and presentation

`Input.SharedKeyboard.Subscribe` owns a cursor into one bounded physical keyboard
history. The native reader polls the union of subscribed virtual keys once.
Clients receive independent timestamps, foreground-window evidence and reliability
flags. Losing history is explicit; it is never interpreted as a reliable press.
Disposal drops the subscription immediately, without joining a blocked native call.
`HighRateInputSampler` and `KeyboardActionEdges` consume this source. Injected
readers remain available for deterministic conformance tests.

`PresentationScheduling.Register` requests 240 Hz input sampling and/or drawing.
One scheduler owns the native accumulator. Requests change at Tick boundaries;
the game's public `TargetElapsedTime`, fixed simulation delta and native total
time remain unchanged. Disabling the last request returns partial time exactly
once. Each client exposes `Active`, `HighRefresh` and `LastError`. A callback fault
disables that client; cleanup retries are bounded and do not stop other clients.
Re-register after resolving the fault. Dispose registration leases on teardown.

## Scene and camera rendering

`FrameComposition.RegisterScene` publishes one `ISceneCompositor` during normal
activation. A scene may prepare GPU resources beforehand, but must not publish
them or bind a previous attempt's player/EntityManager. `Active` reports current
presentation activity, independently of a saved enabled preference.

The camera renders each visible logical screen into a borrowed target inside
`FrameComposition.ScreenPass`, then calls `ComposeScreen` after native foreground.
Scene effects therefore see world pixels, excluding stationary pause/menu UI.
The camera assembles the processed screens and stationary UI. A registered
presenter's readiness suppresses the scene's independent final blit.
`FrameStyle` applies global tint and mirroring once at output resolution.
An external presenter may supply `RegisterPresenter(ready, projectWorld)` so late
world overlays share its pan and linked-screen projection through `ProjectWorld`.
Without an active projector the default map chart and native camera apply.
See [map topology](geometry-and-mechanics.md#map-topology).

Register additional world actors with `RegisterWorldDraw(draw)` and own its lease
in the attempt's activation scope. A camera opens `WorldPass(translation)` for
each logical screen, then calls `DrawWorld()` after entities and before foreground.
This applies even when no scene effects need a `ScreenPass` target. `ProjectWorld`
inside a world pass uses that screen's explicit canonical translation, never the
late viewport projector. Draw callbacks may repeat for visible screen images;
they must not advance simulation or retain a global "already drawn" flag.
The native fallback remains the actor owner's ordinary draw path. Suppress that
path during `InWorldPass` and while `HasExternalPresentation` owns the final frame
to avoid duplicate actors in the stationary UI layer.
An optional `ISceneFrameCapture` requests a logical full-frame preview before
final styling. The camera allocates/readbacks a capture target only on explicit
requests; ordinary presentation frames retain no capture buffer.
Adapters preserve the viewport, render target, SpriteBatch and nested screen
context on both success and failure. Targets are owned by their allocating scope.

## Player update callbacks

[PlayerUpdates](common-effects.md#run-between-native-player-components) offers
BeforeInput and AfterInput callbacks without moving native components. Prepare
the bridge during world loading and own player registrations at activation.

## Controllers, resources and patches

`Gameplay.PlayerControl` leases reserve exclusive movement on one body. Acquisition
is non-preemptive; a second owner must wait or remain unavailable. An additive
controller must explicitly define which owner it can compose with. Release a
lease only after restoring the native state it protects.

`JumpSlot.RegisterControllerPolicy` composes a reversible base controller below
native charge decoration. Its optional `permitsCharge` predicate declares whether
the installed base retains native jumping (Casual+ does not). It runs only on
recomposition, not each frame. An exclusive graph reservation suspends both; restore
the base graph before reattaching charge. Unknown cleanup state prevents unsafe
composition. `ComponentAttachment` removes a component from both its original and
current native collections, outside component iteration on the game thread.

`OwnedPatches` uses the single already loaded Harmony 2 engine. It never loads a
second engine. Preserve existing Harmony owner IDs when migrating consumers.
Record ownership before patch installation; failed cleanup remains retryable.
`ReplaceCalls` verifies exact native call counts and stack signatures, preserving
labels and exception boundaries. A changed native method refuses the patch.

## Editing and native optimizations

`UI.TextEntryBuffer` supplies text, selection, caret, input capture and edit
operations for custom pages. `Open` acquires text input by default; an outer
page that owns capture passes `false`. Dispose on close. Clipboard work uses one
bounded STA worker and never joins from Update. A stale, closed or timed-out
paste cannot replace newer text. UI acceptance waits for a pending clipboard job.

The Runtime **Optimizations** setting controls native Earthquake XML, Rayman
player lookup and title-screen process-query caches. Native physics is unchanged.
The former SFC preference imports once only when Runtime has no saved preference;
existing Runtime settings, protected recovery files and the old source file are
preserved. Cache preparation runs in loading phases; invalidation follows native
entity mutations, world exit and file watcher recovery. Failures report status
and retain native fallback behavior.

See [durable settings and work](settings-and-commands.md) and
[preparation](preparation.md) for storage/queue ownership. Ordinary getters and
input/frame dispatch perform no persistence I/O. Physical device latency still
requires a real-device measurement; synthetic timing verifies implementation cost.
