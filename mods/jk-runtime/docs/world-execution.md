# Shared world execution

Runtime 2.0 owns a transport-neutral world registry and execution context.
Multiplayer Expansion sends its snapshots; Replays stores them alongside player
poses. A mod can use the same declared state in both, without depending on either.

For a gate, for example, share whether it is open and how long it stays open.
The host advances that timer; a receiving client restores it. Personal display
settings and inventory stay outside that shared object. This guide explains how
to declare the state, decide who may update it and release it safely.

## Declare state

Keep shared state separate from personal settings and contact edges. Mark shared
fields with `WorldField` and own `WorldRegistry.Shared.Attach("my-mod.gate", state)`
in the world's `RuntimeScope`. IDs must describe authored objects, stay the same
on every participant and not depend on object hashes or collection order.
`WorldObjects` supplies a scoped ID lookup for objects that need one.

```csharp
public sealed class GateState
{
    [JKRuntime.World.WorldField] public bool Open;
    public int PersonalPreference;
}
```

`Attach` needs no provider, reflection callback or transport adapter. Nested
objects also declare their fields. Supported values are booleans, integers,
finite numbers, enums, short strings, one-dimensional arrays, dictionaries, sets and bounded object
graphs. Recursive graphs, delegates and arbitrary framework objects are rejected.
Snapshots preserve live object and collection references where possible. They
do not preserve aliases between different fields. Use an authored ID for links.

`Register` is the compatibility escape hatch for an existing serializer. Its
capture and preparation callbacks must be side-effect free. Preparation validates
before returning an application action. IDs and schemas must match exactly;
unknown state is rejected rather than silently dropped. The registry validates
the complete manifest, captures all undo actions, then applies it. Application
failure rolls back the failing entry and earlier entries. Failed rollback stops
further transactions and remains visible. Don't catch it and continue simulation.

Use `Bind<T>` when the declared snapshot needs to restore through an existing
semantic operation, such as a scene compositor or native topology change. Its
validation runs before any entry is mutated. The callback is not a wire provider:
Runtime owns the codec, schema, bounds and transaction. See the compiled
[world example](../examples/WorldExample.cs) for a small direct attachment.

The limits are 64 entries, 48,000 bytes per snapshot, 512 values per collection and
4,096 values per data graph. Local registration changes invalidate prepared
applications. Shared registration changes are refused while world authority is
active. Release control before unregistering resources. All calls run on the game thread.

## Try the small example

Read the complete [WorldExample.cs](../examples/WorldExample.cs). It registers
`example.world.gate`, shares `Open` and `Remaining`, and leaves
`PersonalDisplayMode` local. Its `Tick` checks `WorldExecution.CanSimulate`
before changing shared values.

The example is a state-integration module, not a visible gate or a complete map.
Your mechanic must call `Tick` from its normal update and use `Open` for its own
behavior. With no world session, it updates locally. With a host session, only
the host advances it; replicas and playback restore snapshots. Registering a
field alone doesn't stop arbitrary code from changing it.

## Own execution

`WorldControl.Begin(owner, world, attempt, role)` grants exclusive ownership.
`Host` computes shared results; `Replica` and `Playback` receive snapshots and
restore the local pre-session state on disposal. A conflicting owner is rejected.
`SetActors` publishes immutable actor samples, including cumulative input edges.
`SetTime` supplies the authoritative world clock without replacing a player's
attempt timer. Network membership, freshness and authentication belong to the
transport; the registry does not authenticate byte arrays.

`WorldExecution.Current` identifies world, attempt, tick, actor, role and phase.
Actor zero means shared work. `EnterActor` / `Enter` scopes unwind even on errors;
they must close in reverse order on the game thread. Native entity-manager updates
enter the shared context. Actor contacts and explicit input callbacks enter a
nested actor context. `WorldMechanic` marks an entity whose simulation must stop
on receiving roles. It does not automatically register every field of an entity.
Keep movement/presentation entities outside that shared simulation gate.

Receiving a snapshot is a `Restore` phase. `CanPersist` and `Emit` suppress
irreversible work there; receiving roles also suppress locally generated effects.
Personal native pickups enter a separate actor phase: a network client can keep
its own inventory, while playback cannot collect items or persist rewards.
`Emit` retains at most 256 immutable effect records with monotonic sequence IDs.
Consumers can read them with a cursor. This journal is not a reliable network
transport and does not replay arbitrary native audio or file writes automatically.

Snapshots apply on receipt, not twice every rendered frame. `Maintain` is an
explicit fallback for a known legacy boundary. Declared mechanics should use the
execution gate instead of repeated whole-world restoration.

## Existing native and foreign code

`NativeWorldState.Prepare(scope)` installs shared native bird/wind handling and
`NativeWorldInteractions` for native hidden walls and pickup isolation.
`SwitchBlocksWorld.Prepare(scope)` installs the reviewed Switch Blocks
profile. Both are reference counted and shared between playback and networking.
Foreign DLLs stay unchanged. The profile separates per-actor contact edges from
shared switch state, prevents receiving state from entering personal saves and
uses host actor samples for world interactions. This is explicit compatibility,
not an inference that all static fields describe the world.

`WorldObservation` inventories loaded entity/block handlers during preparation,
including SDK implementations behind discovery shells. Diagnostics distinguish
declared boundaries from unresolved handlers and report direct native fields,
mutable statics, external clocks/randomness, file IO, reflection and threading.
The scan is bounded and does not run each frame. It cannot prove the absence of
indirect calls, dynamically generated code, native plugins or background work.

Runtime uses the existing single Harmony 2 engine and owns only its own hooks.
It isn't a replacement CLR or a sandbox. Unmodified unknown mods still execute
normally and remain unresolved in the report. They are not advertised as fully
synchronized or deterministic. Replays remain pose playback with a world track;
they do not become deterministic input resimulation.

## Runtime and Mapping responsibilities

Runtime supplies shared mechanisms. Mapping supplies the scene content:

| Mechanism | Runtime owns | Mapping owns |
| --- | --- | --- |
| Behavior trees | Sequence, selector, parallel, repeat, invert, budgets and portable cursors | XML compilation, leaf operations and resource preparation |
| Side teleports | Native integer links, scoped ownership, validation and shared topology | Map XML and atlas layout |
| Hidden walls | Native contact boundary, aggregate host contacts and shared fade state | Authored event names and scene responses |
| Native pickup | Personal actor phase and playback isolation | Authored rules that react to pickup events |
| Scene state | Declared codec, manifest validation and atomic restoration | Flags, region dwell, effect composition and presentation resources |

Moving the entire scene renderer or XML vocabulary into Runtime would couple every
consumer to Mapping assets. Runtime supplies the common executor and world
contract; Mapping implements its authored operations through that contract.
Native pickup stays personal because sharing inventory would change ordinary
gameplay and save ownership. Shared loot would need an explicit authored policy.

`NativeSideLinks.Apply` owns only the requested native link slots and restores them
when its scope closes, preserving replacements made by another owner. It doesn't
introduce a second teleport movement controller. Mapping's world binding checks
the authored scene hash before applying cursors or effects. Receiving scenes don't
tick their rules; host regions include remote actors without advancing dwell once
per actor. Scene-layout reloads require releasing the active world session first.

## Version compatibility

API 2.0 packages require Runtime 2.0 or newer. Runtime 2.0 also accepts API 1.0–1.44
packages. Assembly identity stays `JKRuntime, Version=1.0.0.0`; existing public
members remain available. A new architecture version doesn't require rebuilding
unchanged older consumers.
