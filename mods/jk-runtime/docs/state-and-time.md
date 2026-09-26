# State snapshots and clocks

Use `GameState.Snapshots` to coordinate explicitly registered mod state in the
current player lifetime. It does not automatically capture Jump King's world,
inventory, persistent settings, achievements, file IO or foreign static fields.
All live state transactions and clock operations run on the game thread.

## A participant is a restoration contract

Implement `IStateParticipant` with a stable namespaced `Id`, positive schema
`Version`, `Capture`, `Validate` and `Restore`. Register it during activation and
immediately track the returned lease. A complete, compiled example is
[StateExample.cs](../examples/StateExample.cs).

`Capture` must return independent data sufficient to restore your mechanic.
`Validate` must reject incompatible data without changing gameplay. `Restore`
must also accept data captured immediately before a failed transaction, because
that is how rollback works. Include pending transitions, timers and input gates
when those affect the mechanic; restoring only position is not enough.

Participants are ordered by ordinal ID. Capture records their identities,
versions and registration generation. `IsCurrent` checks that set; it does not
prove the payload or external world is still compatible. Use `Validate` for
configuration/world constraints. Adding/removing participants or changing a
version invalidates old snapshots. Never serialize `StateSnapshot` as a durable
cross-process save format: it contains participant ownership references.

## Restore sequence

1. Reject stale participant sets or reentrant/poisoned transactions.
2. Validate every participant's requested payload.
3. Capture undo payloads for all participants.
4. Increment `RestoreEpoch` and emit `RestoreStarted`.
5. Restore in participant order; emit completion on success.
6. On failure, roll back from the failing participant backward through every
   participant already attempted. Report the original failure and rollback errors.

A failed rollback poisons later state operations. Do not swallow it and resume
as though restoration succeeded. Stale-set, validation and undo-capture failures
happen **before** `RestoreEpoch` changes. Once application begins, the epoch
changes even if application subsequently fails and rolls back. Invalidate
physical-input associations accordingly; do not rewind hardware timestamps.

`StateFields` / `FieldStateParticipant` are convenience helpers for explicitly
selected fields. Value types are copied by value, strings and `IBlock` references
are retained; an `IBlock` is not cloned. Structs can themselves contain mutable
references, which the helper does not recursively isolate. Custom mutable graphs,
geometry lifetime checks and semantic invariants need a custom participant.

## Keep clocks distinct

| Clock/evidence | Meaning | Restore policy |
| --- | --- | --- |
| Native physics tick | One game simulation update | Preserve the engine/controller's tick rules |
| `GameClock.ReadCurrent()` | Native current-attempt statistics ticks/time | Read-only unless an explicit clock lease is owned |
| `GameClock.ReadAttempt()` | Session/attempt identity | Used to distinguish attempts, not a world snapshot |
| Physical input timestamp | Monotonic observation from the input backend | Invalidate associations; never rewind |
| `GameplayEvent` timestamp | Event delivery observation | Not a physical button timestamp |
| Simulation session tick | Isolated branch time | Stored with that simulation snapshot |
| Presentation activity | An actor still presents while physics is suspended | Explicit lease; does not itself advance a clock |

`GameClock.Override(initialTicks, initialTime)` has one exclusive owner and
returns a `GameClock.Lease`. `SetFrame(frame)` sets the tick count relative to the
captured attempt snapshot plus the initial ticks; negative frames clamp to zero.
It retains the supplied initial time offset rather than deriving elapsed seconds
from the frame number. Dispose restores
the captured native statistics references and loop/achievement state. This is a
playback facility, not a general slow-motion API or permission to edit saves.
Validate the native contract before relying on private members.

Snapshots, a clock lease and `PresentationActivity` solve different problems.
Replays can display recorded poses without input resimulation; a simulator still
requires complete declared [coverage](simulation.md). A restored movement state
must not erase [run-modifier history](compatibility.md#modified-player-attribution).

## Verification

Test capture without mutation, independent payloads, complete validation before
any restore, failure partway through application, rollback of the failing owner,
failed rollback, changed participants/versions, configuration changes, and unload.
Test physical-input invalidation separately from native clock restoration.
See [testing and release](testing-and-release.md).


## Native presentation boundaries

`NativeFrameDispatch.Update(game, time)` and `.Draw(game, time)` call the installed
native MonoGame boundaries through non-inlined dispatch. They do not install hooks,
change time, choose a scheduler or grant clock ownership. A presentation scheduler
must gate Update at its call site, keep the game's native simulation delta, and
coordinate ownership with other schedulers. Do not call Update from a rendering
callback or use it to interpolate world state. Native hooks still execute on each
permitted call. Validate the exact installed Tick call sites before rewriting them.
