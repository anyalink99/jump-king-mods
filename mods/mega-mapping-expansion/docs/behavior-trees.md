# Scene behavior trees

[Handbook](authoring.md) · [Effects and events](behaviors.md) · [Native endings](endings.md) · [Narrative](narrative.md)

Use a Rule for one reaction. Use a Tree when a reaction needs several steps,
waiting, alternatives, parallel work or cancellation. Both use the same scene
events, typed flags, effects, native audio and snapshots. There is no scripting
language, extra block palette or editor-specific runtime. Plain XML is sufficient.

Trees coordinate ordinary components: an ocean, NPC reaction or lamp is authored
content, not a special tree type. They do not replace native physics or the native
Old Man/Merchant dialogue trees. Native ending actor trees remain native; the
[ending bridge](#coordinate-an-ending) connects them to this same scene state.

## First sequence

Place this complete scene in `props/mega-mapping-expansion/scene.xml` inside an
existing playable map. Standing in the marked rectangle starts a two-second warm
tint. Leaving cancels it immediately. The rectangle observes native support; it
does not create a platform. Align it with a real solid block in your map.

```xml
<MegaMapping version="1">
  <Effects>
    <Effect id="warm" duration="0">
      <Set target="options" property="tint" value="#FFD49A" />
      <Set target="options" property="tintOpacity" value="0.16" />
    </Effect>
  </Effects>
  <Regions>
    <Region id="step" screen="1" x="160" y="300" width="160" height="30" test="standing" />
  </Regions>
  <BehaviorTrees>
    <Tree id="welcome" event="enter:step" stopEvent="exit:step" retrigger="restart">
      <Sequencer>
        <ActivateEffect effect="warm" />
        <PauseNode seconds="2" />
      </Sequencer>
    </Tree>
  </BehaviorTrees>
</MegaMapping>
```

Each Tree has exactly one root node. Effects activated by its nodes belong to
that execution. Success, failure, cancellation, restart or an error releases
them; it never restores an obsolete captured value over another owner's effect.
To keep a state after the tree finishes, set a flag and use ordinary Rules to
apply the persistent preset (including a `start` Rule for saved flags). Do not
expect a bare ActivateEffect root to leave a visible effect: it finishes at once.

## Entry points and lifetime

| Tree attribute | Contract |
| --- | --- |
| `id` | Required, scene-unique, case-sensitive, 1–128 characters; cannot start with `@`. |
| `event` | Optional event that starts the root. Omit for a library tree used by Subtree. `start` is queued once per attempt/reload. |
| `stopEvent` | Optional event that cancels a running root. Must differ from its start event. |
| `screen` | `0` by default: accept events from any screen. Positive values filter the **event's** one-based screen, not the camera at dispatch time. Does not pause a running tree when the King leaves that screen. |
| `once` | `false` by default. When true, consume this root on its first accepted start, including an execution later cancelled. Resets with the attempt, not saved to disk. |
| `retrigger` | `ignore` (default) keeps the current execution; `restart` cancels it and starts from the root. No duplicate concurrent execution of the same root. |
| `requiresFlag`, `equals` | Optional typed start condition. Tested on event delivery, not continuously. |

Trees accept the existing [event vocabulary](behaviors.md#events-flags-and-saved-progress):
region entry/exit, flag changes, Runtime gameplay observations, Hidden Kingdom
contacts, and `dialoguebegin:ACTOR`, `linebegin:ACTOR`, `dialogueend:ACTOR` from
bound native actors. Custom events enter through `IMappingScene.Emit`, the
inspector, EmitEvent or an ending's EmitSceneEvent. Event/flag/definition IDs
are case-sensitive. Event IDs allow 1–128 characters, matching the scene API.
Native restore notifications are not authoring events.

In one gameplay tick: sample regions, drain the event queue, then tick running
roots by ordinal tree ID. For each event, already-armed waits are notified,
stop/start subscriptions run, then ordinary Rules run by ordinal ID. Rules can
change flags before the newly started roots tick. Events emitted by tree actions
are queued for the next gameplay tick, never dispatched recursively. Rules'
start conditions and tree start conditions therefore need not observe the same
flags if a Rule changes them during that event. Use explicit separate events
when one reaction depends on another's completion.

An event with no explicit screen (Emit, tree actions, region events) uses the
sampled player screen at dispatch. Native contacts/gameplay/dialogue preserve
their originating screen. A stop event uses the same filter as a start event.

## Node reference

All node operands below are attributes. Unknown elements/attributes and stray
text are errors. In particular scene `PauseNode seconds="2"` is **not** the
native ending file's scalar `<PauseNode>2</PauseNode>` syntax.

| Node | Attributes / children | Result and behavior |
| --- | --- | --- |
| `Sequencer` | 1–64 children | Resume the running child; advance on Success; stop on Failure. Root completion is latched until another accepted event. |
| `Selector` | 1–64 children | Re-evaluate branches from the first on each tick; choose the first non-Failure. Preempting a lower branch cancels its waits/effects. A running Sequencer still resumes its cursor: conditions it already passed are not continuously checked. |
| `ParallelAll` | 1–64 children | Tick unfinished children in order. Succeed only when all succeed; fail when any fails. Completed children are latched, not executed repeatedly. Unlike native ending Simultaneous, this is an **all-success** join. |
| `Repeat` | `count` 0–1000000, one child | Repeat after Success; pass Failure through. `0` means until cancelled. Release the previous iteration's effects and always yield to the next tick, even for instant children. |
| `Invert` | One child | Swap Success/Failure; preserve Running. |
| `Subtree` | `tree` | Inline another declared root with independent execution state at every call site. Its subscription/once/screen metadata does not apply to the call. Recursive calls are rejected. |
| `PauseNode` | `seconds` 0–86400 | Start its timer when reached; Running until that much gameplay time elapses. Zero succeeds immediately. |
| `WaitEvent` | `event` | Arm when reached; succeed on the next matching delivered event. No historical/sticky events and no consumption of its own root's triggering edge. Several armed waits may observe the same event. |
| `EmitEvent` | `event` | Queue an event; succeed immediately. Does not await listeners. |
| `CheckFlag` | `flag`, `value` | Success for equality, Failure otherwise. |
| `WaitFlag` | `flag`, `value` | Success for equality, Running otherwise. Use for durable state instead of an edge that may already have happened. |
| `SetFlag` | `flag`, `value` | Set an existing typed flag. Unchanged values emit no new event. |
| `IncrementFlag` | `flag`, `amount` | Add a signed integer to an `integer` flag. Overflow faults this tree, never wraps. |
| `ActivateEffect` | `effect` | Activate an existing Effect under this node's owner. Success immediately; its lease survives until scope cancellation/finish or the effect's own expiry. |
| `ClearEffects` | None | Release all effects owned by this execution, including completed steps and parallel branches. Does not cancel the remaining control flow or another tree's effects. |
| `PlayEventSFX` | `sound` | Play a prepared native `audio.music.event_music` key; no file path or second audio mixer. Success does not mean playback has finished. |

Each ActivateEffect call site has a separate owner; effect group/repeat policies
remain owner-local. Separate tree nodes do not implicitly replace one another.
Use ClearEffects before switching an exclusive preset inside a tree. Stopping
an execution cannot undo sound already played, emitted events or flag writes.

## Reuse and parallel work

This complete scene repeats three flashes. A counter increments once, in
parallel with each flash, rather than once per waiting frame. There are no assets
or native NPC dependencies.

```xml
<MegaMapping version="1">
  <Flags><Flag id="flashes" type="integer" value="0" /></Flags>
  <Effects><Effect id="flash"><Set target="options" property="tintOpacity" value="0.2" /></Effect></Effects>
  <BehaviorTrees>
    <Tree id="flash-step">
      <Sequencer>
        <ActivateEffect effect="flash" />
        <PauseNode seconds="0.15" />
        <ClearEffects />
        <PauseNode seconds="0.35" />
      </Sequencer>
    </Tree>
    <Tree id="three-flashes" event="start">
      <Repeat count="3">
        <ParallelAll>
          <IncrementFlag flag="flashes" amount="1" />
          <Subtree tree="flash-step" />
        </ParallelAll>
      </Repeat>
    </Tree>
  </BehaviorTrees>
</MegaMapping>
```

BehaviorTrees can live in Include modules and [ObjectDefinitions](objects.md).
Object-local `tree="@step"`, `flag="@state"`, `effect="@glow"`, conditions and
`event="enter:@zone"` / `stopEvent="exit:@zone"` expand to the instance namespace.
The Tree itself gets its instance-prefixed ID. Trees are non-spatial: instance
placement does not translate them or implicitly apply a screen filter; their
regions already determine where contact occurs. Global/custom event names stay
global unless explicitly parameterized, e.g. `event="${id}:activate"`.

## React to a native NPC

Bind an existing native Old Man named `guide` to MME actor ID `guide`, then run
this scene. The map must already contain that NPC's normal native content.
Finishing the dialogue increments a counter after a short delay. Conditional
quotes, scene text and Results can read that same counter through existing
[narrative settings](narrative.md). No duplicate dialogue interpreter is created.

```xml
<MegaMapping version="1">
  <Flags><Flag id="talks" type="integer" value="0" /></Flags>
  <NativeActors><Actor id="guide" kind="oldman" name="guide" /></NativeActors>
  <BehaviorTrees>
    <Tree id="after-guide" event="dialogueend:guide">
      <Sequencer>
        <PauseNode seconds="0.5" />
        <IncrementFlag flag="talks" amount="1" />
      </Sequencer>
    </Tree>
  </BehaviorTrees>
</MegaMapping>
```

These are companion scene behaviors, **not** arbitrary replacement of an Old
Man's movement/dialogue BT or access to its private blackboard. Native actor
render properties and quotes remain configurable through NativeActors/Effects;
unsupported native actions are not silently emulated.

## Coordinate an ending

Native ending files retain their documented grammar and native actor nodes.
Four bridge leaves work in either controller or actor files:

| Ending node | Contents | Result |
| --- | --- | --- |
| `EmitSceneEvent` | Scalar event ID | Queue the same event used by scene trees/Rules; Success. |
| `SetSceneFlag` | `Key` and `Value` child elements | Write a declared scene flag; Success. |
| `CheckSceneFlag` | `Key` and `Value` | Success/Failure according to equality. |
| `WaitSceneFlag` | `Key` and `Value` | Success/Running according to equality. |

Typical handshake: reset a `ready` flag, emit `ending:cue`, wait for `ready=1`.
The scene tree listens for that cue, sequences effects, and sets ready when done.
The native controller continues to its ordinary EndNode. Prefer flags for the
acknowledgement: they cannot miss an early completion edge.

The packaged `examples/behavior-trees/` contains the complete paired scene and
`ending/custom_main_ending.xml`. It is an overlay for a playable map, not finished
ending choreography. Compile the **whole level root** so cross-file references
are checked. Bridge nodes require scene.xml and declared, correctly typed flags;
native-only ending files still need no scene.xml. A bridge pauses while MME is
disabled. Existing native ending nodes continue to follow native execution.

## Pause, reset, restore and failure

Trees and event dispatch use gameplay time. Native pause, the Runtime UI and
the MME checkbox suspend progress; queued events remain queued. Presentation
effects may still advance on their own presentation clock during native pause.
Inspector single-step explicitly advances the scene without moving the player.

A Runtime snapshot captures root status/once, every cursor, repeat counter,
wait deadline, armed/signalled event wait, effects and queued events. Restoring
does not replay already completed leaves. It restores state within the same
scene generation, not native NPC/ending BT internals or already-played audio.
An old snapshot cannot target a reloaded scene. Restart and successful reload
start fresh trees; only `scope="save"` flags persist to disk. To reconstruct a
quest/state after loading, branch on those flags from a `start` tree/Rule rather
than trying to save an executing script instruction.

Validation rejects unknown references, invalid flag types, recursive subtrees,
non-finite waits, malformed shapes and excessive expansion. Runtime errors mark
only that root Faulted, release its effects and report the node path in the
inspector. Faulted trees cannot be retriggered until reset/reload. Earlier flag
writes and sounds are not rolled back. Failure from a condition is normal BT
control flow, not an exception. Other roots keep running.

Limits: 128 definitions, 1024 expanded nodes per root, depth 32, 64 children per
composite, 8192 expanded nodes for the whole scene; 8192 node visits per tick.
The existing queue limit is 256 events and the scene effect budget remains 128
live effects / 32 spawned lights. Infinite Repeat yields once per iteration.
Logical waits can still wait forever if the map never produces the required
state: use ParallelAll/Selector deliberately and test both cancellation and
completion. There is no fallback to another script after an authoring error.

## Performance and inspection

Graphs, subtree expansion, event interests and authored effect operands are
prepared before play. Event delivery uses indexed subscriptions, including
WaitEvent; sleeping trees do not repeatedly search for native entities/resources.
No active trees means no tree traversal. Tree execution does not add render
passes. Compiled authored effects no longer serialize XML on each activation;
external dynamic Apply still validates and copies its caller-owned definition.

In **Mapping inspector → Behavior trees and waiting nodes**, inspect each root's
Idle/Running/Success/Failure/Cancelled/Faulted state and waiting node paths.
**Regions and event simulation** lists tree events as well as Rule events. In a
debug run, emit a trigger, then single-step while the inspector pauses gameplay.
**Behavior diagnostics** shows faults. The browser editor is not required.

Validate with `SceneCacheCompiler --validate LEVEL_ROOT`; the generated XSD and
field catalog include every tree/node type. The build runs execution, pause,
ownership, preemption, parallel, recursion, snapshot, native bridge, serialization
and resource-contract tests, and compiles the packaged example. These checks do
not certify a map's route, final artwork or every possible cinematic handshake.
