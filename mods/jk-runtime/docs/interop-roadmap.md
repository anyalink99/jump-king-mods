# Foreign-mod interoperability API status

The six proposed facilities ship in Runtime 1.32. See the [interop guide](interop.md)
for contracts, ownership, costs and limitations, and the compiled
[InteropExample.cs](../examples/InteropExample.cs).

| Facility | Shipped API | Boundary |
| --- | --- | --- |
| Actual contact evidence | `MovementSample.Contact` | Actual accepted queries, not a unique supporting block or hypothetical support. |
| Pure support queries | `SupportQuery`, `SupportBudget`, `MaterialRegistry.QuerySupport` | Explicit provider contract; no general predicate analyzer or collision callback replay. |
| Movement-stage deltas | `MovementObservation`, `MovementObserver.Read` | Seven native intervals, with explicit completion and coverage. |
| Method validity | `MethodValidity.Watch` | Shared Harmony mutation generation, not independent detours or callee semantics. |
| Material capabilities | `RuntimeApi.Materials`, `MaterialCapabilities` | Exact-type declarations and links, not inferred simulation coverage. |
| Bounded trace export | `MovementObserver.Recording`, `Capture`, `MovementTrace.Export` | Opt-in 256-event ring, not deterministic replay. |

MGE consumes declared support for unfamiliar materials. Its reviewed JumpKingPlus
adapter preserves ordered one-way and initial-overlap rules and uses the shared
top-face predicate. Other foreign one-way implementations require a declaration
or reviewed adapter; a witnessed landing cannot replace that contract.

General IL predicate analysis and blind cloning of foreign state remain outside
these APIs. New adapters need native/foreign fixtures, conservative refusal,
scoped teardown and allocation evidence.
