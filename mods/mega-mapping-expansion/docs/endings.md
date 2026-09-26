# Custom ending behavior trees

MME reads map-authored XML and constructs Jump King's own BehaviorTree nodes.
It replaces only the authored MakeBT tree. Native ending selection,
completion/save bookkeeping, actor creation and credits remain with the game.
Native-only trees require no scene.xml or browser editor; loading these XML overrides does
require MME, not just the unmodified game.

This chapter documents the parser, validation rules and native adapters shipped
in MME. It is not a claim that an external custom-ending prototype is a finished
specification or a required dependency. The supported files and nodes are listed
below; test coverage and remaining playtest responsibilities are described under
[Checks and iteration](#checks-and-iteration).

## What is a behavior tree?

BT means **behavior tree**: a hierarchy of actions and rules controlling when
those actions run. It is not limited to character AI. Jump King also uses trees
to coordinate ending cutscenes. XML describes the tree; native game nodes execute
it as the game updates.

Each node reports one of three results:

- `Running`: the action is still in progress, such as a pause timer.
- `Success`: the action or condition succeeded; its parent decides what runs next.
- `Failure`: the action or condition failed; this is a control-flow result, not
  necessarily an error or exception.

Leaf nodes do the work: `SetSpriteNode` changes an actor's displayed sprite,
`PlaySFX` starts/resumes a sound without waiting for it to finish, and `PauseNode`
waits for the specified number of game-time seconds without freezing the game.
Composite nodes organize other nodes:

- `Sequencer` runs children in order. It waits at a `Running` child and stops on
  `Failure`; it succeeds when all children succeed. It can start again if ticked
  after completion, so it is not an implicit one-shot timeline.
- `SequenceOnce` remembers successful completion until reset, avoiding an
  unintended repeat of a completed sequence.
- `Selector` tries children in order until one returns `Running` or `Success`.
  Jump King's implementation rechecks from the first child each tick.
- `Simultaneous` ticks all children in the same update, not on separate threads.
  It returns `Running` while any child is running, then `Success` if any child
  succeeded, otherwise `Failure`. It does not mean every child must succeed.

The ending controller and each actor have separate trees. For example, the King
can change pose while another actor moves; broadcasts coordinate their timing.
Finishing an actor tree is not the same as finishing the whole ending.

## Files and ownership

To coordinate cutscenes with scene behavior trees, use the four explicit
[scene bridge nodes](behavior-trees.md#coordinate-an-ending): EmitSceneEvent,
SetSceneFlag, CheckSceneFlag and WaitSceneFlag. Those nodes require scene.xml;
their flags and types are checked across both files before activation. Native
BroadcastNode/ReceiverWaitNode remain a separate native actor broadcast bus.

Place files in the level's existing `ending/` directory. Filenames are exact.
Omit any role whose native tree should remain unchanged.

| Ending | Controller | Actors |
| --- | --- | --- |
| Main Babe | `custom_main_ending.xml` | `custom_main_king.xml`, `custom_main_babe.xml`, `custom_main_cherub.xml` |
| New Babe+ | `custom_nbp_ending.xml` | `custom_nbp_king.xml`, `custom_nbp_babe.xml`, `custom_nbp_hanging_babe.xml`, `custom_nbp_cherubs.xml`, `custom_nbp_crow.xml` |
| Ghost of the Babe | `custom_owl_ending.xml` | `custom_owl_king.xml`, `custom_owl_babe.xml`, `custom_owl_gargoyle.xml`, `custom_owl_bird.xml` |

Each file contains one BT node as its root, not a MegaMapping wrapper. The whole
tree is replaced, not appended. A controller must return Success; an actor
normally terminates with SuicideNode when no longer needed. Never put an actor
movement/animation node in a controller file: controllers have no ISpriteEntity.

Files parse during preparation, including every restart. There is no IO from
MakeBT or a tree tick. All authored target methods are checked against the
installed game; only those target methods receive adapters. Missing files install
no override. When MoreEndingOptions is installed, MME owns the roles it has
prepared for the current map. Its scoped adapter skips the matching
MoreEndingOptions tree callbacks, avoiding a second XML parse or replacement of
MME's tree. Other roles and maps keep MoreEndingOptions behavior. Its native
patches, settings and subscriptions are retained; guards release at world exit.
Both startup callback orders and repeated map/vanilla transitions are covered by
`verify-ending-compatibility.ps1` against the installed MoreEndingOptions DLL.
Callback targets and signatures are checked before use; unsupported contracts
fail explicitly. This does not claim compatibility with arbitrary ending mods.
Invalid custom data still fails preparation. No vanilla tree is used to hide a
custom-tree construction error. MME's presentation checkbox does not
rewrite an already running native cutscene.

## Small paired example

These are a deliberately short custom Main Babe controller and King actor. The
controller uses a timed finish rather than waiting for stock actor broadcasts.
They demonstrate structure, not a replacement cinematic for the showcase.

```xml custom_main_ending.xml
<Sequencer>
  <PlayMusic />
  <PauseNode>2.0</PauseNode>
</Sequencer>
```

```xml custom_main_king.xml
<Sequencer>
  <SetSpriteNode>Regular.idle</SetSpriteNode>
  <PlaySFX>Audio.Plink</PlaySFX>
  <PauseNode>1.5</PauseNode>
  <SuicideNode />
</Sequencer>
```

The King actor changes pose, plays a sound, waits 1.5 seconds and is removed.
Separately, the controller starts music, waits two seconds and signals cutscene
completion. `SuicideNode` removes an actor. A completed controller sequence returns Success
to the native ending manager. `EndNode` returns Running forever; do not put it at
the end of a controller that must reach credits. These
are small authoring examples, not ready-made cinematic choreography.

When retaining stock actors/controllers, preserve their expected broadcast keys.
Renaming or removing a signal can leave a native ReceiverWaitNode waiting forever;
syntactically valid XML does not prove cinematic completion. Keep unrelated
native level ending settings in their normal files.

## Node reference

Names and scalar keys are case-sensitive. Floats use decimal dots on every OS
locale. Required fields below are child elements, not XML attributes. A vector
contains X and Y children. Empty nodes have no parameters.

| Node | Contents |
| --- | --- |
| RestartSequencer, Selector, SequenceOnce, Sequencer, Simultaneous, RandomSelector, RunAllAnySuccess | One or more child BT nodes; native composite ordering and results |
| Evaluator | Condition and Func, each containing exactly one BT node; nested composites work |
| StaticNode | Result (`Running`, `Success`, `Failure`) and exactly one child BT node |
| PauseNode | Seconds, 0..86400 |
| EndingScroll | Step vector; Repetitions integer 0..1000000; native 30-FPS conversion |
| MoveNode | DeltaMove vector; Repetitions integer 0..1000000; native 30-FPS conversion |
| CoupleAnim | WalkOne, WalkTwo, WalkSmear sprite keys; optional Speed integer 1..1000000, default 1 |
| SetSpriteNode / GetSpriteNode | Sprite key; both set the actor sprite in MME; GetSpriteNode is not a read-only query |
| SetSpriteEffectNode | None, FlipHorizontally or FlipVertically |
| BroadcastNode / ReceiverWaitNode | Matching native broadcast string, 1..256 characters |
| PlaySFX | A native sound key, described below |
| PlayEventSFX | A native event_music key, not a filename |
| CheckBBKey / SetBBKeyNode | Key string and signed 32-bit integer Value; native actor blackboard |
| LoopingAnimNode | Step seconds (>0, up to 86400); Sprites containing 1..1024 Sprite keys |
| GiveWearableItemNode | Crown, Shoes, CrownNBP, SnakeRing, GiantBoots, Cap, GnomeHat, Tunic, YellowShoes, CrownOwl or CapeOwl |
| PlayMusic | Empty; selects this ending's normal music |
| EndNode | Empty; returns Running indefinitely; holds a tree and does not complete a cutscene |
| SuicideNode | Empty; destroys the actor |
| CherubsDeliver / CherubsEscape | Empty; native Main Babe or New Babe+ implementation; no owl equivalent |
| JumpUp / FallDown | Empty; Main Babe King behavior |
| JumpToTargetHeight | Velocity and TargetMove floats, each within +/-1000000 |
| JumpInPlace | Speed integer within +/-1000000 |
| Jump / IdleAnim | Empty; Main Babe actor behaviors |
| PutOnCrown / CherubsDeliverAnim / CherubsEscapeAnim | Empty; corresponding native Main Babe behaviors |
| BabeJump / GiveCrownNBP | Empty; native New Babe+ behavior/reward |
| IsBirdDone | Empty; only valid in owl_bird; reads its native creation-time state |
| SpawnLightning | Empty; native owl lightning behavior, also usable from other ending trees |

Movement components use finite floats within +/-1000000. These bounds prevent
invalid numeric input; they are not sensible choreography defaults. Behaviors
that use native ending-specific content still need that content and matching
actor coordination. `Animations` is not a supported MME XML node. Use the
documented sprite and animation nodes; unknown node names are rejected.

## Sprite and sound keys

Sprite categories: Regular, Babe, BabeCouple, Ending1Misc, NBPKing, NBPBabe,
OWLKing, OWLBabe, OWLGargoyle and OWLBird. Values are `Category.SpriteKey`, such
as Regular.idle or NBPBabe.BabeGroundIdle_0. Keys are checked against the installed
game's actual sprite enums, and resolved through its current player-sprite set.

PlaySFX supports these category/member combinations:

| Category | Members |
| --- | --- |
| Audio | Plink, PressStart, NewLocation, Talking, RaymanSFX, WaterSplashEnter, WaterSplashExit |
| Babe | Jump, Kiss, Mou, Pickup, Scream, Surprised |
| Menu | CursorMove, Select, MenuOpen, MenuFail, TitleHit |
| Music | TitleScreen, Opening, Ending, Ending2, Ending3 |
| Player | Jump, Land, Bump, Splat, IceJump, IceLand, SnowJump, SnowLand, SnowSplat, IronLand, IronSplat, WaterJump, WaterLand, WaterBump, WaterSplat, SandLand, EndingParasol |

PlayEventSFX uses the same native sound dictionary as [Rule sound cues](native-workflows.md#native-audio).
Missing resources are errors, not substitutions with unrelated sprites/sounds.

## Checks and iteration

Run `SceneCacheCompiler --validate LEVEL_ROOT`. This validates all present
custom-ending files even when the map has no scene.xml. The parser prohibits
DTDs/external entities, unknown fields and duplicate required fields. Limits:
1 MiB XML per file, 4096 BT nodes, nesting depth 64, scalar strings 256 characters.
Errors include filename and XML line where available.

Restart the attempt after editing ending files. Inspector scene reload does not
replace an already created native ending actor. Test each authored ending from
its entry point and confirm completion, rewards and credits; compiler validation
cannot detect every waiting-signal deadlock. Automated checks exercise all 15
native MakeBT entry points with small trees, nested decorators, locale-independent
values and adapter cleanup, not every possible choreography.

Implementation mappings are adapted under MIT; see [third-party notices](../THIRD_PARTY_NOTICES.md).

[Handbook](authoring.md) · [Native workflows](native-workflows.md)
