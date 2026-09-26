# Simulation API 1

Available in Runtime 1.2.0 through `RuntimeApi.Simulation` and advertised by
`RuntimeApi.Supports("simulation-sessions-v1")`. Assembly identity remains 1.0.0.0.

This is an opt-in simulation kernel, not a complete Jump King simulator. Runtime
1.4 also supplies the native ballistic adapter used by Warp and a first-divergence
conformance harness. Retained Screen Solver sources test full native controls and
reviewed Workshop adapters, but the Solver mod is not part of this installation.
Do not register a mechanic as supported merely because its state can be restored.

## No background work

The registry is allocated only on first access. Registering a provider stores
metadata and delegates; it does not invoke Capture or Advance. There are no
simulation entities, game-loop callbacks, timers, workers or geometry scanners.
`CheckCoverage` examines metadata only. `Open` captures once and `Step` performs
exactly one requested simulated tick. An installed but unused Screen Solver must
not call either method. Provider registration must remain lightweight too.

Sessions dispose explicitly. Provider registration/unregistration invalidates
existing sessions; Runtime level teardown invalidates an already-created registry
without creating one. SearchJob owns its session and closes it on cancellation,
success, budget exhaustion or error. No real player components are modified.

## Coverage and ordering

`SimulationSeed` contains a copied world-data blob, initial pose, tick, actual
tick duration, game fingerprint and exact-version mechanic requirements. The
consumer is responsible for a complete, trustworthy inventory of active effects.
An empty or incomplete inventory is not evidence of native-only gameplay.

`SimulationProvider` declares exact mechanic versions, an evidence identifier,
simulation phases and provider dependencies. There must be exactly one provider
for every requirement. Missing coverage, version mismatches, conflicts, dependency
cycles and duplicate ownership reject a session before Capture runs. Dependencies
are hard requirements and participate in stable ordinal topological ordering.
Ordering is phase-first, then dependency order within each phase.

An evidence string is provenance, not certification. A native adapter must verify
the executable fingerprint itself before accepting a seed. Registration does not
inspect Harmony patches or infer compatibility with undeclared effects.

## Branch state

Capture returns serialized provider state. Each Advance receives a short-lived
`SimulationTick`: read-only input, clock and seed, a value-type shared pose,
copy-on-read/write own state and copy-on-read peer state. Providers cannot replace
another provider's serialized state. Cross-provider communication uses declared
ordering, peer reads and the shared pose.

`SetLocal` / `GetLocal<T>` share newly constructed branch objects between phases
of one Step. Scratch objects are not snapshot state and are discarded afterward.
Never put a live game object or cross-step mutable cache there. Persistent data
must remain in the serialized state used to reconstruct the next tick.

Keep all controller timers, direction history, world timers, random state and
pending effects in serialized state. Never retain mutable simulation state in a
provider instance or closure. Provider callbacks are trusted code: this API is
not an OS/security sandbox and cannot prevent a malicious callback from accessing
static game objects or doing I/O. Only audited pure adapters may be invoked.

Every Step copies its input snapshot. Branches cannot alter the parent's blobs;
source and result poses are values. Callback contexts expire immediately after
return, including exceptional returns. Snapshots have full byte-identity keys
including tick and provider state, not rounded coordinates or wind direction.

State/world blobs have size limits and events are bounded per tick. An invalid
pose, provider exception or oversized state poisons and closes the session.
Cancellation discards the unfinished branch without changing its parent. A token
is checked between phases; a callback must be bounded because arbitrary managed
code cannot be forcibly interrupted safely. All APIs are owner-thread only.

`RuntimeApi.Simulation` is accessed on the game thread, so its registry and
sessions belong there. A separately constructed `SimulationRegistry` belongs to
the thread that constructed it; its sessions/registration leases must stay there
too. This permits isolated hosts, not moving a shared Runtime session to a worker.
Capture live inputs on their permitted thread before constructing isolated seed
data. Native ballistic/shadow APIs still depend on native contracts and must not
touch game objects from a simulation worker.

Snapshots from another session are rejected. Live-state changes require explicit
invalidation by the consumer; the kernel does not poll the game for changes.

## Native wind utility

`NativeWind.Velocity` is a pure port of the installed game's clock conversion and
wind waveform, including float operations, TimeSpan rounding, intensity and fixed
direction. It does not apply wind to a body or model the separate wind-activation
latch, Snow, NoWind regions or camera entry rules. Current-attempt stats returned
by native AchievementManager have no legacy-time offset; pass zero for that path.

The focused tests compare its output exactly with `WindManager.CurrentVelocityRaw`
in a headless installed-game fixture over 17,184 combinations. They also test
dormant callback counts, branch isolation, expired contexts, registry conflicts,
order, cancellation, session ownership and failure cleanup.

## Native ballistic adapter

`NativeFlightSimulation` owns the former Warp native-body shadow implementation.
`CreateShadow` constructs separate native behaviors and redirects collision reads
to an `INativeFlightWorld`. It strips audio, particles and native side-transfer
side effects; the world contract must reject unsupported flight conditions.
`AdvanceShadow` accepts only an adapter-created shadow and the audited 1/60 step.
`TryPredict` retains the 7200-tick ceiling and explicit refusal reason. `Commit`
is an intentional live mutation, called only after a caller accepts a forecast.

Mega supplies the reviewed world contract: static native materials and its own
blocks, native wind and native side-exit teleports. Real world time
does not advance by predicted ticks. This adapter models airborne native body
movement, not charge input, Ball King contours, active Jetpack or arbitrary
foreign controllers. Runtime 1.5 adds optional `INativeFlightWind`: supply the
captured clock's force for the requested relative tick and current screen.
The adapter preserves the body's wind latch and native Snow/NoWind gating.
`AdvanceShadowFromX` resumes a new branch captured at BeforeXMovement without
replaying wind, velocity cache or water-state cache. It must not be used on a
pre-tick capture or on a branch that has already advanced. Older wind-free
`INativeFlightWorld` implementations remain valid.

Runtime 1.6 adds `INativeFlightTeleports.HandleTeleport(BehaviourContext)`,
called at the native teleport stage after X movement and capping. Implementers
must change only the isolated body/context and their own screen index, preserving
native tick order. They own link validation and loop/work budgets. Worlds that
do not implement it still refuse activated side-exit teleports. Query
`RuntimeApi.Supports("native-flight-teleports-v1")` for the addition. This does
not enable background simulation or change live teleport behavior.

`SimulationConformance.Compare` takes matching initial state readers, the same
input sequence and explicit reference/candidate step functions. It stops on the
first differing tick and reports differing fields, input and evidence label.
The harness itself never captures or advances the live game. `ReadBody` supplies
native pose/support fields; add controller, material and world fields for a
broader claim. `CompareFields` also works inside an existing test loop.

Mega compares every ballistic tick, not merely the final landing, against the
installed native BodyComp over 2160 impulse/fall cases. Runtime tests inject a
known fourth-tick fault and verify first-difference reporting. Neither this test
nor a geometry provider certifies unsupported mechanic combinations.
