# Control trees

[Handbook](../index.md) / [Scene authoring](index.md)

Use a rule for an immediate response and a tree for work that waits, branches or repeats. Trees share the graph, variable store, action vocabulary and effect library. This chapter describes construction Trees inside mapping_settings.xml. Native ending files use a different XML vocabulary, covered in [endings](../integration/endings.md).

## A complete timed sequence

This scene turns a light on after a jump, waits a second, and releases it. A new jump restarts the sequence; teleporting cancels it:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,120" /></Component>
    <Component id="lamp" kind="light">
      <Value name="transform" value="pose" /><Value name="intensity" value="0" />
    </Component>
  </Components>
  <Effects>
    <Effect id="glow" duration="0"><Set target="lamp/intensity" value="0.08" /></Effect>
  </Effects>
  <Trees>
    <Tree id="jumpGlow" event="jump" stopEvent="teleported" retrigger="restart">
      <Node op="sequence">
        <Node op="action"><Action op="effect" target="glow" /></Node>
        <Node op="wait" seconds="1" />
        <Node op="clear" />
      </Node>
    </Tree>
  </Trees>
</MappingSettings>
```

The effect is held, but owned by the action node. Clear releases all this tree's effects. Completion, cancellation and restart also release them. A tree that activates an effect and immediately finishes will therefore release it immediately. Keep a wait or condition active for as long as the tree should own the effect.

## Tree settings

A Tree has a unique ID and exactly one root Node. `event` starts it. `stopEvent` cancels it; it can't equal the start event. `retrigger=ignore` leaves an already-running sequence alone, while restart cancels and starts again. `once=true` permits one start in the attempt. A Boolean graph condition gates starting; it doesn't continuously stop the tree if the condition later becomes false.

The tree selects Gameplay or Presentation for all waits and stepping. Choose Presentation for cutscenes that run after gameplay completes. Pause still holds logic, including Presentation trees. `animateWhilePaused` permits visual clocks only. Disabling scene presentation holds both clocks and logic.

## Node vocabulary

| Node op | Fields and behavior |
|---|---|
| sequence | Run children in order; stop at failure or a running child |
| selector | Try children in order until one succeeds or remains running |
| parallel | Advance child branches together |
| repeat | One child; count=0 repeats without a fixed limit |
| invert | One child; swap success/failure |
| call | target names another tree; expand its root as a subtree |
| wait | seconds; succeed when that much selected-clock time has passed |
| waitEvent | target is an exact event ID; wait for a future event after arming |
| check | source is a Boolean graph node; fail if false/unavailable |
| waitCondition | source is a Boolean graph node; keep waiting if false/unavailable |
| action | Exactly one nested Action using the rule action vocabulary |
| clear | Release all effects owned by this tree |

Don't use waitEvent as historical memory. If an event happened before the wait armed, it isn't automatically delivered again. Use a Boolean variable plus waitCondition when the sequence must notice a condition that may already have become true.

Parallel branches may each own effects. Setting a variable is different: variable writes persist until another write/reset/restore, even when the tree is cancelled. An external sound already handed to the native audio system isn't stopped by tree cancellation.

## Coordinate two events

This complete scene starts on scene.ready, waits for a jump, then records a Boolean. It demonstrates a latch you could inspect from another graph or ending bridge:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Variables><Variable id="hasJumped" kind="Boolean" value="false" /></Variables>
  <Trees>
    <Tree id="firstJump" event="scene.ready" once="true">
      <Node op="sequence">
        <Node op="waitEvent" target="jump" />
        <Node op="action"><Action op="set" target="hasJumped" value="true" /></Node>
      </Node>
    </Tree>
  </Trees>
</MappingSettings>
```

A tree with no start event can supply a reusable call target. Calls expand before execution and reject recursion. They aren't asynchronous function calls and don't create an independent effect lifetime.

## Restore, inspect and limits

Snapshots retain cursors, waits, event latches, cause envelopes and effect ownership. Restore resumes state without replaying completed sounds or actions. The inspector reports each tree's state and faults. If a sequence stalls, inspect its current condition's availability and the exact event name before changing the wait duration.

There are at most 128 trees, depth 32, 1024 nodes per expanded tree and 8192 expanded nodes per scene. Repeat doesn't remove execution budgets. Keep unbounded repeats waiting on time or evidence rather than spinning through instant actions.
