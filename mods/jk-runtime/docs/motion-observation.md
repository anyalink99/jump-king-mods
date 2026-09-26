# Observe horizontal movement

API 1.31 provides `motion-observation-v1` in `JKRuntime.Gameplay`.
It observes the native `UpdateXPositionFromVelocityBehaviour` pass without
replaying a handler, getter, collision query or state update. No provider names,
block colours or speed coefficients are used for arithmetic classification.

## Ownership and preparation

Own `MotionObservation.Prepare(types)` or `PrepareLoaded()` in `OnWorldReady`,
`BeforeAttempt`, or an explicit paused configuration action. Preparation reads
managed method bodies and installs shared hooks using the loaded Harmony 2
engine. It does not load an engine or construct block handlers. `PrepareLoaded`
examines loaded assemblies referencing JumpKing; prefer explicit types when known.
Do not prepare from Update, Draw or a player constructor.

At activation, own `scope.Observe(body)` in the attempt. Multiple observers of a
body share one capture. Each enabled observer retains the prepared service, so
disposing a preparation scope cannot invalidate a live observer. Release attempt
observers before the world scope; the last lease removes only Runtime's patches.
Cleanup failures remain retryable. All ownership operations and reads are on the
game thread. See the compiled [example](../examples/MotionObservationExample.cs).

Set `observer.Enabled = false` when unused. A body with no enabled consumers does
not record a pass; `Read()` retains its previous sample. Compare `Sequence` to
reject stale samples after disabled updates, aborted pipelines or restores.
Read after the native X movement pass, normally at `BodyPhase.AfterXCollision`.
The sample's `Step` is the pre-collision requested displacement; wall resolution
can change the body's actual displacement afterward.

MGE prepares only when No Walk Off is globally enabled or authored, or when an
explicit library action introduces its blocks. The prepared world lease survives
attempt restarts; body observers and edge latches do not. Initial handler discovery
does not occur on the first moving frame. Late-loaded DLLs need explicit preparation
or a new world; unprepared callbacks run normally with unknown coverage.

## What the result means

| Kind | Meaning for the observed modifier pass |
| --- | --- |
| `ControlledOnly` | Supported local arithmetic leaves zero input at zero and does not reverse the observed nonzero input direction. |
| `ExternalMotion` | A supported handler introduces a nonzero neutral result. Later cancellation does not erase this evidence. |
| `Unknown` | A handler, method shape, patch graph, report or completed displacement cannot be covered. |

These labels describe this pass, not the provenance of all player motion. A
consumer must separately check its control model, inherited velocity, wind,
direct position changes, post-material friction, collision and support. No Walk
Off does that and yields on both external and unknown motion. The service never
clamps movement, changes run flags or replaces a collision rule.

`Source` and `Reason` identify a relevant contributing/refused handler when
available. `scope.Inspect()` allocates a detached handler inventory for explicit
diagnostics; do not call it each frame. `IncludesDeclaredReport` distinguishes
passes containing provider-supplied evidence from entirely automatic observation.
The normal Runtime diagnostics export includes `motionObservation` with handler
coverage/refusals and native-pass validity; exporting does not activate observation.

## Arithmetic coverage

A bounded forward data-flow analysis rejects backward branches, exception
regions, unsupported opcodes, speed-dependent control flow, speed escaping into
fields/calls, by-ref calls, nonlinear speed products and speed-dependent divisors.
Limits are 512 instructions, 64 original locals, 64 evaluation-stack entries and
1024 supplied handler types per preparation call. Float locals, forward branches
independent of the explicit speed argument, addition, subtraction, multiplication,
division by independent values and negation are supported. Unsupported methods
keep their original instructions and remain callable.

Accepted methods retain their original arithmetic, calls, writes and return value.
Additional float locals carry the same arithmetic with the current neutral input.
Independent values, including property results, are copied from the real evaluation
stack after their one real evaluation. The original callback and getter are never
called with a test input. The exact managed identity body needs no instrumentation.

Neutral input starts at zero for the native pass and flows through handlers in
their actual order: `(v + 0.5) * 0.5` has neutral output `0.25`. Independent
state/branch decisions are held at their observed values. This is a local arithmetic
projection relative to the explicit speed parameter, not a second simulation of
the world with neutral controls. It cannot infer intent hidden inside an opaque
helper or reconstruct an alternative history of mutable world state.

Numeric operations retain their order; no reassociation into an approximate
linear formula is performed. Nonfinite values, missing reports, changed returns,
reversed direction and unexpected final X displacement yield unknown evidence.
The CLR's original calculation is preserved; added float locals are evidence,
not replacements for the gameplay values. Exceptions still propagate normally;
the native finalizer clears the active capture even when a handler throws.

## Explicit provider reports

A cooperating handler can read `GetNeutralInput(this)` and call
`Report(this, actualResult, neutralResult)` during its single real invocation.
Reports are accepted only for the currently observed handler instance. Outside
that invocation, neutral input is zero and reports do nothing. Capture world
values once and apply your own arithmetic to both numeric inputs; do not call
stateful getters twice. The example demonstrates this contract.

The report's actual result must match the returned result. This detects inconsistent
reports, but cannot prove that a provider's declared neutral calculation is honest
or complete. Such passes set `IncludesDeclaredReport`. Existing foreign patches
on a prepared method still prevent automatic trust, including explicit reports
from a handler whose prepared patch graph has become stale.

## Patches and cost

Preparation refuses automatic certification of handlers with foreign Harmony
patches. Runtime watches the shared engine's `PatchFunctions.UpdateWrapper`:
a later patch/unpatch invalidates the affected handler, and a change to the native
movement method invalidates that pass. Removal does not silently restore stale
coverage; release all leases and prepare again. There is no per-frame patch
inventory scan. Untracked native detours and independent Harmony engines are not
covered. The engine adapter requires the reviewed Harmony 2 mutation entry point;
if absent, preparation fails rather than weakening invalidation.

Hot-path work is a weak-table body lookup, dictionary lookups for actual handler
types, float operations, reference checks and struct publication. It has no
reflection invocation, file IO, LINQ, per-frame collection construction or string
formatting. Installed instrumentation has a small residual cost while consumers
are disabled; disposing all leases removes it. Preparation/JIT cost is measured
separately and must stay outside gameplay handoff.

`MotionObservationTests` prints preparation time and observed/disabled microsecond
costs and checks steady-state allocations. These are local microbenchmarks, not
end-to-end frame-time guarantees. The focused MGE suite checks unknown-provider
speed changes, water, inertia/wind/carry, both edges and release/repress. Integration
also checks installed JumpKingPlus, Expansion Blocks and ConveyorBlockMod when
available, plus the installed Harmony versions through Runtime verification.

Related future facilities are proposals in the [interop roadmap](interop-roadmap.md),
not part of this API's guarantees.
