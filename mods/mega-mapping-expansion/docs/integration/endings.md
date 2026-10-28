# Coordinate an ending with scene effects

[Handbook](../README.md) / [Integration](README.md)

Use [victory conditions](../game/interactions.md#victory-conditions) to change when the ending begins. This page changes the sequence afterward: when to show an effect, move an actor, wait for a cue or finish.

Start with a copy of your complete working native/provider ending tree. Add a scene bridge at one step, then play through that ending. Keep its actor movement and finish nodes. Replacing the whole controller with a short demonstration also replaces its usual choreography.

The two-file handshake below is deliberately a minimal controller demonstration. Try it in a throwaway map with the main ending resources already present. On victory it should emit the cue, show the scene light for one Presentation second, then let EndNode finish. Test pause/resume during the cue before using the bridge in a real ending.

## Two tree formats

Construction trees live in mapping_settings.xml and use `Tree`/`Node op=...`. Native ending trees live in `ending/custom_<role>.xml` and use nodes such as SequenceOnce, MoveNode and PauseNode. Don't paste construction Nodes into a native file.

A custom native file replaces that role's tree. Keep the native actions your ending still needs: replacing a controller with only an event emitter doesn't preserve its scrolling or finish behavior. Start by copying the intended native/provider tree, then add bridges at the desired steps. Compilation validates structure and references; it doesn't prove the actors are staged well.

## Choose the final stages

MME 1.5 adds `endingPresentation`. Its `role` is `main`, `nbp` or `owl`. The independent Boolean fields `credits`, `picture` and `statistics` default to true. Set any of them false to omit that stage. For example, keep native credits and statistics while skipping the illustration:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="finalStages" kind="endingPresentation">
      <Value name="role" value="main" />
      <Value name="picture" value="false" />
    </Component>
  </Components>
</MappingSettings>
```

These choices are sampled when victory is accepted. They don't replace the role's controller or actor trees. `credits=false` omits the credit roll and its final fade/wait, while retaining the controller and normal ending cleanup. `statistics=false` skips the native statistics display and input wait. Native statistics initialization still runs to record Workshop completion. Authored `result` pages remain available; they become the first statistics pages when the native page is omitted. With no result pages, completion proceeds to the native menu flow. The required native picture asset slots must still resolve even when their page is skipped.

`endingActor` selects a native cutscene `role` (king, babe, cherub and the other roles below). Bind `visible` to hide/show its ordinary drawing. Hidden actors still run their native trees and remain available to captures. Use the existing native tree actions for movement, sprites, animation and removal; use a captured sprite for arbitrary transforms, tint, masks or deformation.

## Keep a portrait after the player disappears

A `capture` with `mode=hold` keeps an explicit selection until it is replaced, cleared or the scene resets. `onVictory=true` captures just before native victory removes the gameplay player. Select `actor:player` for his current appearance, or `actor:player.idle` for his layered idle sprite with his current facing. The capture does not retain a player entity or run another simulation.

Display it through a material's `capture` and an ordinary sprite. The saved target is a 480 by 360 screen image. `ending.arrival` supplies the final foot position projected into the viewport; using it as the output sprite's origin lets another transform position anchor the portrait by his feet. The `ending` render layer draws after native screen art and before entities, including native credit text, and only during an active ending. Choose normal layers for snapshots used during gameplay.

The `capture` and `clearCapture` actions also operate on hold captures outside victory. They require only a capture target, with no value/source. Capture actions run on the game thread during scene event handling, outside Draw. Hold captures need an explicit selection; they cannot copy an unspecified backbuffer. The `stage` restriction still applies to selected scene components. Live captures retain their existing per-frame behavior. Saved GPU pixels are transient: restart, scene reload, disabling the scene and snapshot restore clear them; gameplay/world snapshots don't serialize the pixels.

`ending.main.begin`, `ending.nbp.begin` and `ending.owl.begin` fire at accepted victory. Rules run before player teardown, with `ending.arrival` available. Use a Presentation rule to store `time.presentation` as your animation epoch. Corresponding `.finish` events fire after the native ending controller/credits cleanup; they also fire when the player skips credits. They don't mean that the picture/statistics stages have finished. [Borrowed media](scene-api.md#borrow-existing-media) can keep an already playing song and video texture through this interval without restarting them.

## Supported file roles

| Ending | Controller | Actor files |
|---|---|---|
| Main Babe | custom_main_ending.xml | custom_main_king.xml, custom_main_babe.xml, custom_main_cherub.xml |
| New Babe Plus | custom_nbp_ending.xml | custom_nbp_king.xml, custom_nbp_babe.xml, custom_nbp_hanging_babe.xml, custom_nbp_cherubs.xml, custom_nbp_crow.xml |
| Ghost of the Babe | custom_owl_ending.xml | custom_owl_king.xml, custom_owl_babe.xml, custom_owl_gargoyle.xml, custom_owl_bird.xml |

Only add files for roles you mean to replace. MME prepares and checks the whole candidate set before activation. Its scoped adapters retain native and supported foreign discovery contracts. Compatibility with a particular provider/version still needs the focused installed-game check; historical audit combinations aren't a blanket promise.

## Bridge vocabulary

| Native node | Content and result |
|---|---|
| EmitSceneEvent | Event ID as direct text; enqueue an ordinary scene event |
| SetSceneVariable | Key and Value children; write the declared variable type |
| CheckSceneVariable | Key and Value; succeed when equal |
| WaitSceneVariable | Key and Value; wait until equal |

Declare each variable in the scene first. A Boolean bridge value is true/false, an Integer is an integer, and a Vector2 uses two comma-separated numbers. Unknown variables and wrong value types fail validation. No flag aliases are accepted.

## A complete effect handshake

These two files demonstrate a minimal custom main controller: emit a cue, wait for the scene effect, then finish. They deliberately replace the normal controller sequence; use this as a handshake example and insert the same bridge into your own complete choreography. It doesn't prescribe a particular story or actor movement.

<!-- mme-example:start -->
```xml mapping_settings.xml
<MappingSettings version="1">
  <Variables>
    <Variable id="effectFinished" kind="Boolean" value="false" />
  </Variables>
  <Components>
    <Component id="pose" kind="transform">
      <Value name="position" value="240,160" />
    </Component>
    <Component id="lamp" kind="light">
      <Value name="transform" value="pose" />
      <Value name="intensity" value="0" />
      <Value name="color" value="#88DDAA" />
    </Component>
  </Components>
  <Effects>
    <Effect id="endingGlow" duration="1" fadeOut="0.6" clock="Presentation">
      <Set target="lamp/intensity" value="0.1" />
    </Effect>
  </Effects>
  <Trees>
    <Tree id="endingCue" event="ending.spark" clock="Presentation" retrigger="restart">
      <Node op="sequence">
        <Node op="action">
          <Action op="set" target="effectFinished" value="false" />
        </Node>
        <Node op="action">
          <Action op="effect" target="endingGlow" />
        </Node>
        <Node op="wait" seconds="1" />
        <Node op="action">
          <Action op="set" target="effectFinished" value="true" />
        </Node>
      </Node>
    </Tree>
  </Trees>
</MappingSettings>
```
```xml ending/custom_main_ending.xml
<SequenceOnce>
  <EmitSceneEvent>ending.spark</EmitSceneEvent>
  <WaitSceneVariable>
    <Key>effectFinished</Key>
    <Value>true</Value>
  </WaitSceneVariable>
  <EndNode />
</SequenceOnce>
```
<!-- mme-example:end -->

Presentation is essential here. Completion stops Gameplay time. Active endings still dispatch scene events and advance Presentation trees, effects, fields and integrators. Set the appropriate clock on every time-dependent part; a Presentation tree won't make a Gameplay effect advance. Sprite/emitter/stamp/field component clock values are lowercase presentation.

Pause holds event dispatch and tree progression. Views default to freezing Presentation animation too. An explicit animateWhilePaused option permits visuals, not cutscene logic. Disabling MME holds bridges that are waiting on scene values; don't design a mandatory completion handshake while allowing the required scene to remain disabled.

## Follow cutscene actors

The gameplay player is destroyed before the cutscene. Use `actor:ending.king.position` and `.present` instead of assuming player.center remains meaningful. Other roles are babe, cherub, hanging_babe, crow, gargoyle and bird. Positions are local-screen pixels; absent actors have unavailable positions.

A graph or follow-offset recipe can consume an actor position. Guard presence/availability before moving an effect to it. Captures accept `actor:ending.king` and the equivalent other role selectors. They draw a game-owned actor without updating it, which allows an overlay, reflection or shadow. MME snapshots don't recreate those actors.

## Native node reference

Native files have no node attributes or namespaces. Values are direct text or the named child elements below. Controller files allow controller-safe nodes; actor-only actions belong in actor files.

| Family | Nodes and content |
|---|---|
| Composites | RestartSequencer, Selector, SequenceOnce, Sequencer, Simultaneous, RandomSelector, RunAllAnySuccess; child nodes |
| Conditions | Evaluator with Condition/Func, each containing one node; StaticNode with Result and one node |
| Timing/signals | PauseNode seconds; BroadcastNode and ReceiverWaitNode text key |
| Movement | MoveNode: DeltaMove/X,Y and Repetitions; EndingScroll: Step/X,Y and Repetitions |
| Sprites | SetSpriteNode/GetSpriteNode native sprite name; SetSpriteEffectNode native SpriteEffects name |
| Animation | LoopingAnimNode: Step and Sprites/Sprite list; CoupleAnim: WalkOne, WalkTwo, WalkSmear, optional Speed |
| Jump helpers | JumpToTargetHeight: Velocity, TargetMove; JumpInPlace: Speed; JumpUp, FallDown, Jump, BabeJump are empty |
| Native state | CheckBBKey/SetBBKeyNode: Key, integer Value |
| Audio | PlaySFX native sound name; PlayEventSFX event key; PlayMusic empty |
| Items | GiveWearableItemNode wearable enum name; PutOnCrown and GiveCrownNBP empty |
| Ending actions | CherubsDeliver, CherubsEscape, CherubsDeliverAnim, CherubsEscapeAnim, IdleAnim, SpawnLightning, EndNode, SuicideNode are empty |
| Owl-specific | IsBirdDone is empty and belongs to owl_bird |
| Scene bridges | EmitSceneEvent, SetSceneVariable, CheckSceneVariable, WaitSceneVariable as above |

Wearable names accepted here are Crown, Shoes, CrownNBP, SnakeRing, GiantBoots, Cap, GnomeHat, Tunic, YellowShoes, CrownOwl and CapeOwl. Sprite/sound names must exist in the installed game. Cherub actions aren't available to Owl roles. Native trees are bounded to 4096 nodes and 64 levels.

Before distribution, play through completion with the intended provider set. Test pause/resume during the effect, missing actors, repeated attempts and leaving the level. `verify-ending-compatibility.ps1` checks a chosen provider against installed contracts; it can't assess your choreography.
