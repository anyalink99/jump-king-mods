# Runtime coordination

The stable assembly identity remains `1.0.0.0`. New packages declare minimum
API 1.25; an older Runtime rejects them before loading implementation code.
The build verifies every previously published public/protected signature through
the complete pre-migration API 1.24 baseline, alongside older frozen consumers.

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
An optional `ISceneFrameCapture` requests a logical full-frame preview before
final styling. The camera allocates/readbacks a capture target only on explicit
requests; ordinary presentation frames retain no capture buffer.
Adapters preserve the viewport, render target, SpriteBatch and nested screen
context on both success and failure. Targets are owned by their allocating scope.

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
