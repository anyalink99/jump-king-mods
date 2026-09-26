# Core API contracts

Reference `JKRuntime.dll`. All public namespaces begin with `JKRuntime`.
There is one API version: `RuntimeApi.Version`, ApiMajor 1 / ApiMinor 30.
Runtime and UI services are supplied by the same assembly.
See the [compatibility policy](api-policy.md) for ABI guarantees and limits.
Start with the [task-oriented SDK guide](getting-started.md) for UI, events,
mechanics, Mapping scenes and state ownership.
The [handbook index](index.md) and [public API map](api-map.md) cover
the full reading path. The exported SDK includes a generated `PublicApi.md`
reference for current signatures, parameter defaults and XML summaries.

Runtime provides native [events, action input and suspension](events-and-input.md),
[mechanic composition and block declarations](geometry-and-mechanics.md),
the shared [native ballistic adapter and conformance harness](simulation.md),
and [bounded diagnostic tracing](diagnostic-workflow.md). These services
have real first-party consumers; none silently replaces a mod's controller.

## Preparation before player handoff

The [preparation guide](preparation.md) owns callback signatures, world/attempt
lifetimes, measurement and fallback rules. Use it before adding startup work;
the [lifecycle guide](lifecycle.md) specifies the full native/SDK sequence.

## Run-modifier attribution

Attribution evidence updates immediately on the game thread. Its companion XML
is persisted asynchronously using immutable, coalesced snapshots;
registration does not wait for a durable disk flush. The native modified-run
flag is unchanged, and incomplete persisted history remains conservatively unknown.

`Gameplay.BodyPipeline(..., markRunModified: true)` automatically records the
behaviour assembly's title as a contributor, after successful native registration.
`false` preserves map-authorized mechanics and records no contribution.

For direct registrations use `Gameplay.RunModifiers.Register`, `RegisterBefore`,
`RegisterAfter` and `Remove`, with the same body/behaviour/anchor arguments and
boolean result as the native methods. These methods still call the native API;
they do not independently set/clear a cheat flag. Pair registrations/removals
through this API, on the game thread, during the active player lifetime.
Transient markers must use it for both calls. Give your behaviour implementation
assembly a descriptive `AssemblyTitle` (the assembly simple name is its stable ID).
Do not pass a behaviour implemented in another mod as your own usage marker.

`RunModifiers.GetContributors()` returns a copy of known display names and, when
needed, an explicit unknown-source entry; a clean run returns an empty array.
Names remain after module teardown so the native post-game screen can show them.
`RuntimeApi.Supports("run-modifier-attribution-v1")` advertises this service.
This is not a way to whitelist a map/mod.

An optional Harmony-backed observer watches native BodyComp
registrations. Existing third-party binaries need no rebuild when their behaviour
assembly maps unambiguously to a native loaded mod. It validates list/count changes,
captures early and transient registrations, and shares the explicit API's history
without double-counting. It does not inject a Harmony dependency into the SDK.
Explicit API calls still work if the observer is unavailable. Registration methods
retain native results/exceptions; attribution errors cannot suppress them.
The API assembly identity is 1.0.0.0, independent of the runtime/file version.
See [compatibility and diagnostics](compatibility.md) for attribution limits.

## Packaging

The [packaging guide](packaging.md) is the primary contract for discovery shells,
manifests, implementation loading, dependency DLLs and payload selection.
Use the [first-package guide](getting-started.md) for a complete build workflow.
Lifecycle attributes come from `JKRuntime.Modules`; the package host owns their
invocation. The [lifecycle guide](lifecycle.md) specifies callback signatures.

## Graph and lifetime

`ModuleDefinition` is the lower-level immutable declaration: ID, Version,
Install, optional Start/Stop, capabilities, ordering and exclusive resource IDs.
`RuntimeApi.Register` returns a process registration lease. Definitions persist
between levels; disposing registration while a level is active is forbidden.
Independent ready modules sort by ordinal ID.

`ModuleContext` supplies Track, Publish, Require<T> and TryGetCapability.
`RuntimeScope` supplies owned IDisposable resources, deferred cleanup and child
scopes. These owners have different cleanup/failure contracts: use the
[lifetime table](lifecycle.md#lifetime-choice) and
[dependency rules](lifecycle.md#dependencies-and-failure) rather than assuming
that every owner can be retried. The lifecycle guide is authoritative for
activation order, partial starts and teardown.

## Shared services

The [API map](api-map.md) routes each service to its primary contract. Core
capability `jk.ui:1:0` publishes UiServices. `jk.game.readonly:1:0` publishes
GameContract only when structural validation succeeds. The fingerprint is
separate evidence; an unknown hash does not establish native compatibility.

Cooperative controller capabilities include `player.form`, `player.thrust` and
`player.movement`. Declare optional integrations explicitly and retain an
absence path; the capability graph does not infer them from installed DLLs.

### Settings and threading

Use [settings and commands](settings-and-commands.md) for validation, persistence,
game-thread application, immediate execution without a pump and failure recovery.
Module and live-state APIs belong to the game thread. Physical input workers
have a separate [input contract](events-and-input.md) and do not edit game state.

### Gameplay ordering

BodyPipeline uses native semantic phases and explicit priorities/owner IDs.
First-party pre-wind policy order is Ball 0 → Jetpack 100 → Casual 200 → SFC 300;
it is independent of registration/Workshop order. Disposal removes only owned
instances. The native modified-behavior policy is preserved per map permission.

BodyPipeline registration, removal and disposal are game-thread operations.
Unknown BodyPhase values are rejected. Starting disposal closes registration;
all owned releases are attempted even if one fails. Failed entries remain
available for a later disposal retry, and successful disposal is idempotent.

JumpSlot detaches the registered charge policy before changing the base
controller. Acquire a `SuspendChargePolicy(owner)` lease
inside `Recompose`, before replacing native jumping, and dispose it only after
restoring the controller's native graph. Keep it in the controller's lifetime;
failed graph cleanup must retain the lease for retry. Independent leases compose.
The policy is reapplied only when the final reservation is released. It can be
registered, refreshed or unloaded while suspended, regardless of module load order.
`ChargePolicySuspended` also suppresses native charge observations.

Acquiring/releasing a reservation outside `Recompose` automatically composes the
policy, but graph edits still belong inside `Recompose`. Reentrant policy callbacks
and off-thread mutations are rejected. Failed policy acquisition runs its cleanup;
successful cleanup permits retry. Failed detach prevents controller mutation and
requires restart because ownership is uncertain. Both controller and reattachment
errors are preserved. Foreign ownership loss is never silent successful restoration.

JumpResult distinguishes physical hold, native buffered input and unavailable
evidence. Its outcome records whether upward velocity was actually produced.
Do not fabricate durations or equate a completed native node with every possible
later airborne trajectory. External Jump% integration remains in SFC.

JumpEvents subscriptions, publication and subscription disposal are game-thread
operations. Runtime observes native jump boundaries; SFC adds physical-hold timing evidence.
See [native events and input](events-and-input.md) for event ordering and
coverage, and [geometry profiles](geometry-and-mechanics.md#explicit-geometry-profiles)
for the separation of native collision from actor-specific contours.

### State and time

The [state and time guide](state-and-time.md) owns participant validation,
restore ordering, undo/rollback, RestoreEpoch and clock-lease contracts.
Snapshots cover registered state in the current level. Monotonic physical input
must be invalidated after a restore, including a rolled-back restore; its
timestamps are never rewound.

## Diagnostics and compatibility

GetModules/GetOrder/GetErrors, PackageHost.Errors, Commands.LastError and
ExportDiagnostics expose status. Runtime reports loaded Harmony ABIs and owners
without installing Harmony, reordering foreign patches or promising universal
generic-method support. Useful third-party Controls+ discovery, Jump% integration
and BallKingGeometryApi remain intentional extension points.

See compile-and-package-checked [RuntimeExample](../examples/RuntimeExample.cs)
and [UI example](../examples/ExampleIntegration.cs). Native/private state contracts
must be reverified against the installed game after updates.


## Runtime coordination

See [runtime coordination](runtime-coordination.md) for shared keyboard
observations, presentation scheduling, scene/camera composition, exclusive player
control, owned Harmony patches, embedded text entry and native optimizations.
