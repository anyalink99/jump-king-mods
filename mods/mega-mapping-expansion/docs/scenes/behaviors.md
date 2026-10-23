# Make something react to the King

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

A reaction connects a game value or event to a visible result. Start with [the area-caption and jump-counter lessons](../getting-started/lessons.md#4-show-a-caption-in-an-area) if this is your first scene.

## Choose the kind of reaction

| What should happen? | Use |
|---|---|
| A caption is visible while the King is in an area | A sensor signal and binding |
| A number follows the player's charge | A graph and binding |
| A light flashes once on a jump | A rule that starts a short effect |
| A camera shows a change, waits, then returns | A control tree and effects |

A **signal** is a current value, such as `zone.touching`. An **event** is something that happened, such as `jump` or `zone.enter`. A **graph node** reads/calculates a value. A **binding** continuously sends that value to a component setting.

## Read a value

Use a graph `input` with a source such as `player.screen`, `zone.touching` or `variable:jumps`. Give the node your own ID, then use that ID as a binding's `source`.

For example, lesson 4 reads `zone.touching` into node `inside` and binds `inside` to `captionPose/visible`. It updates automatically while the scene runs. An exit rule isn't needed to undo it.

[Observations](../observations/README.md) lists game sources. [Graph operations](../reference/graphs.md) lists every calculation, input order, kind and unit. Graph `inputs` are node IDs separated by semicolons. They needn't be declared in evaluation order; MME follows dependencies.

## Count jumps and flash a light

This complete scene needs no artwork. Copy it to your test map as `mapping_settings.xml`, then [load the map](../getting-started/worldsmith.md). It displays a saved number near the top and flashes a light there when you jump.

```xml mapping_settings.xml
<MappingSettings version="1" saveId="handbook-jump-counter">
  <Variables>
    <Variable id="jumps" kind="Integer" value="0" scope="Save" />
  </Variables>
  <Components>
    <Component id="pose" kind="transform">
      <Value name="position" value="240,60" />
    </Component>
    <Component id="label" kind="text">
      <Value name="transform" value="pose" />
    </Component>
    <Component id="lamp" kind="light">
      <Value name="transform" value="pose" />
      <Value name="radius" value="48" />
      <Value name="intensity" value="0" />
    </Component>
  </Components>
  <Graph>
    <Node id="count" op="input" source="variable:jumps" />
    <Node id="caption" op="format" inputs="count" />
  </Graph>
  <Bindings>
    <Bind id="caption" source="caption" target="label/value" />
  </Bindings>
  <Effects>
    <Effect id="flash" duration="0.6" fadeOut="0.5" clock="Presentation" repeat="refresh">
      <Set target="lamp/intensity" value="0.08" />
    </Effect>
  </Effects>
  <Rules>
    <Rule id="countJump" event="jump">
      <Action op="increment" target="jumps" value="1" />
      <Action op="effect" target="flash" />
    </Rule>
  </Rules>
</MappingSettings>
```

1. Jump three times. The number should increase three times and each jump should pulse the light.
2. Set the flash duration to `1` and `fadeOut` to `0.8`. Rebuild: the pulse should last longer.
3. Change the variable to `scope="Attempt"`. A fresh attempt now starts at zero. For a saved total, keep `scope="Save"` and a stable scene `saveId`.

The `jump` rule increments `jumps` and starts `flash`. The graph converts that Integer variable into Text, then the binding updates `label/value`. The light's baseline intensity is zero; the effect temporarily contributes `0.08` and releases it afterward.

## Rules and actions

A Rule's `event` is an exact event ID. Optional `condition` names a Boolean graph node. `once=true` allows one run per attempt. `cooldown` is seconds before the next accepted run, measured on the chosen clock. A failed condition does not queue a retry for later.

Use `condition` to restrict a reaction to a screen: read `player.screen`, compare it with an Integer constant using `equal`, and put that node ID on the rule. Rules don't have a screen field of their own.

Rules for one event execute in stable ID order. Variable actions refresh dependent graph values before later rules run. A changed variable emits `variable:<id>` for later reactions; setting the same value does not. Loading state and C# API writes don't emit that action event.

[Actions and effects](../reference/actions.md) documents every action and setting, including sound, particles, stamps and custom events. [Events](../reference/events.md) lists exact event names and payloads.

## Choose a binding, effect or tree

A binding is best for a condition that remains true: while inside, while owning an item, or while charging. An effect is best for a temporary change such as a flash. A tree is useful when steps wait or branch.

Effects remember who started them. A separate exit rule cannot cancel a held effect started by an unrelated enter rule. For while-inside behavior, bind the sensor value directly. For a sequence, let the tree own and release its effects. See [effects](../reference/actions.md#effect-lifetime-and-ownership) and [trees](behavior-trees.md).

## Check the connection

Open [Mapping inspector](../reference/inspector.md). First check the input signal, then the graph/binding target under **Components and effective properties**. Check its owner if the result differs from the baseline XML: a preview or another effect can take priority.

An unavailable observation isn't zero or false. Guard optional actors or precise charge evidence with `available` and `select`. [Graph quality](../reference/graphs.md#missing-observations) explains the pattern. [Clocks](clocks.md) explains pause and completion.
