# Material contracts and movement evidence

Runtime 1.32 exposes six optional services. They are distinct from
[isolated simulation](simulation.md): arbitrary movement or collision callbacks
are never replayed to answer a query. The SDK includes a compiled
[InteropExample.cs](../examples/InteropExample.cs).

Feature IDs: `contact-evidence-v1`, `support-predicates-v1`, `movement-stages-v1`,
`method-validity-v1`, `material-capabilities-v1`, `movement-trace-v1`.
API availability is not a promise of coverage for a particular foreign mod.

## Ownership and preparation

Call `MovementObservation.Prepare` in `OnWorldReady` or `BeforeAttempt` only
when needed, inside a named startup measurement. Own its scope in the supplied
`RuntimeScope`. It resolves seven fixed native stages and installs shared dormant
hooks; it does not scan assemblies, construct handlers or find players.
At activation, own `scope.Observe(body)` in the active module lifetime.

Observers share one capture per body, start enabled, and have recording disabled.
Without enabled consumers, stage hooks bypass capture. Preparation scopes and
observers both retain the hooks. Release body observers at attempt end; disposing
only the preparation scope does not invalidate a still-owned observer. Final
release removes only Runtime hooks; failed cleanup is retryable.

Prepare material descriptors early, but publish their owned registrations only
at activation. Do not retain players across attempts in descriptors or delegates.
Mutable services, observation reads and capture belong to the game thread.
A detached `MovementTrace` may be formatted/written on a caller-owned worker.
See [preparation](preparation.md) and [lifecycle](lifecycle.md).

## Actual stages and contacts

`MovementObserver.Read(stage)` returns a value-type `MovementSample`:

| Stage | Native ExecuteBehaviour interval |
| --- | --- |
| `Wind` | `WindVelocityUpdateBehaviour` |
| `XMovement` | `UpdateXPositionFromVelocityBehaviour` |
| `XCollision` | `ResolveXCollisionBehaviour` |
| `YMovement` | `UpdateYPositionFromVelocityBehaviour` |
| `YCollision` | `ResolveYCollisionBehaviour` |
| `Gravity` | `ApplyGravityBehaviour` |
| `Materials` | `ExecuteBlockBehaviours` |

Position and velocity before/after describe changes during the actual invocation.
A delta does not prove a mod's intent. Foreign nodes between these methods lie
outside the intervals. Observation neither reorders physics nor marks the run
modified. `Sequence` increases across all publications on that body; zero means
unobserved. It is not a global frame ID or game tick. Compare it to reject stale
data: skipped/disabled stages retain their previous samples.

`Completed` is false on exceptions, which still propagate. `Covered` additionally
requires the reviewed patch graph and no reentry. Uncovered samples still expose
actual before/after evidence; they must not be treated as certified movement.

For X/Y collision, `Contact` identifies the **last accepted actual query**:
native/additional path, additional handler type, original probe rectangle,
resolver direction and count of accepted queries. Native direction is +1 for
positive velocity, otherwise -1. Backing out of overlap can accept several
probes; the count is not a number of distinct surfaces. `None` means no accepted
query in this invocation. Original native queries and additional callbacks run
exactly once per original call site.

This identifies the accepting path, not a unique colliding block. Runtime avoids
duplicating virtual collision-info getters or enumerating foreign collections.
A witnessed landing cannot answer whether a hypothetical next position is
supported. Use the support contract for that question.

## Material declarations and pure support

`RuntimeApi.Materials` is an exact-type registry. Register `MaterialCapabilities`
with an owner ID, block type, `SpeedCapability`, optional `SupportPredicate`,
geometry-profile ID, state-participant ID and `CaptureState` delegate. Duplicate
type ownership fails. Dispose the returned lease to remove the declaration.
`Generation` changes on registration/removal; `Resolve(type)` is a constant-time
lookup returning an immutable descriptor or null. `Inspect()` allocates a detached
owner/descriptor inventory without invoking providers, for explicit browsers and
diagnostics rather than Update.

Speed values (`Unknown`, `Identity`, `Scaling`, `Additive`, `Custom`) are provider
declarations, not [observed arithmetic](motion-observation.md). Geometry/state IDs
link to existing services without implicitly registering geometry, state or
simulation coverage. A provider must make its native behavior match its contract.

`SupportQuery` contains detached body/surface rectangles, velocity, an initial
overlap flag and a provider-specific 64-bit state token. A predicate returns
`Supported`, `Unsupported` or `Unknown`. The type-based query overload accepts
already captured values. The block-based overload reads its rectangle and calls
the optional capture delegate once, then evaluates the predicate. Missing state
capture means token zero, not enabled support.

Capture must be bounded and read-only. The predicate must use only the input and
immutable constants. Runtime isolates exceptions and rejects recursive queries
and registry mutation, but cannot prove arbitrary provider code pure or interrupt
a slow delegate. Reading globals in a predicate violates this contract. Neither
overload invokes `Intersects`, `AdditionalYCollisionCheck` or player updates.

Share one `SupportBudget(maximum)` by reference across a bounded operation's
candidates. Maximum is 1–4096; each admitted query consumes one unit, including
missing coverage. Exhaustion/default zero returns `Unknown` without provider
calls. Invalid rectangles, non-finite velocity, missing declarations, exceptions,
invalid results and reentry also return unknown. This limits call count, not
execution time. Spatially restrict candidates before querying.

`SupportPredicates.TopFace` accepts token 1, no initial overlap, nonnegative Y
velocity, feet exactly on the surface top and horizontal overlap. All other
cases are unsupported. Capture switch/direction state explicitly when using it.

MGE preserves native flat/slope precedence, then its reviewed JumpKingPlus rule,
then checks declared materials with at most 64 queries per probe position.
It supplies actual velocity and body/surface overlap. JumpKingPlus additionally
requires its registered handler, ordered vertical contacts and absence of overlap
with **any** one-way block. These rules remain in the reviewed adapter and are
not imposed on all materials. Pure predicates do not make unknown blocks solid.

## Method validity leases

`MethodValidity.Watch(method, reviewedOwners)` captures a shared-Harmony method
generation. Acquire after installing reviewed hooks during preparation. Existing
patches from unlisted owners refuse the lease. `IsValid` reads cached evidence
without reflection; `Reason` distinguishes disposal, initial refusal and mutation.

Patch/unpatch attempts invalidate leases before wrapper rebuilding. Failed
mutation and later unpatching do not revive old leases. Explicitly review and
acquire a new lease. `Generation` is a process observation token, not a persistent
assembly fingerprint. Final disposal removes the shared mutation hook.

Coverage includes the loaded shared Harmony 2 engine only. Independent engines,
native detours, mutable data and changes in callees need separate coverage.
Allowed owner IDs are caller assertions, not security identities or proof of
semantics. Both arithmetic and stage observation use this service. Their reviewed
native-X hook installation/removal coordinates lease renewal; unrelated owners
remain refused. There is no per-frame patch inventory or background reanalysis.

## Trace export and diagnostics

Set `Recording = true` explicitly to allocate a shared 256-event ring. Default
operation starts no file, timer, queue or worker. Enabled recorders write one
struct per stage. Disabling one does not erase another's history. Final body
observer release also releases the ring and body reference.

`Capture()` allocates an oldest-first detached `MovementTrace`; `Read()` returns
a copy and `Dropped` counts overwritten recorded events. `ToJson()` formats
schema `jkruntime.movement-trace.v1`, stage/contact records and motion-handler
coverage **at capture time**, not historical coverage for each event. Non-finite
coordinates export as null. Live bodies and arbitrary foreign objects are never
serialized. `Export(path)` writes an explicitly chosen new file and refuses
overwrite. Nothing is uploaded. The trace is insufficient for deterministic replay.

Normal diagnostics include active observers and per-stage coverage alongside
motion-handler refusals and existing journal/resource evidence. Detailed history
requires explicit trace capture; diagnostics never silently enable recording.

## Validation and costs

`InteropTests` uses an independently compiled unfamiliar provider DLL and native
resolvers. It checks single callback counts, native/additional contact evidence,
state changes, budgets, failures, duplicate ownership, retryable release,
patch/unpatch invalidation, both preparation orders, repeated lifetimes, dormant
recording, ring rollover and export without overwrite. `verify-harmony.ps1` runs
it on installed Harmony versions. MGE additionally tests installed JumpKingPlus
one-way behavior and declared unfamiliar materials.

Preparation pays reflection and patch compilation. Steady sampling uses fixed
arrays, value types, cached validity and constant-time lookup, without strings or
heap allocations. A warmed microbenchmark checks 100,000 native X passes with
arithmetic observation and recording enabled. This excludes the rest of the game
and is not a frame-time guarantee. Native candidate collection and provider
delegates retain their own costs; call budgets do not eliminate those costs.
