# Combine two examples without breaking the XML

[Handbook](../README.md) / [Getting started](README.md)

Try each example separately first. Each complete `mapping_settings.xml` replaces the whole entry file. Pasting two whole documents together creates two XML roots, which won't load.

## Combine an image with a jump counter

1. Start with [lesson 5](../../examples/lessons/05-jump-counter/README.md). It already has Variables, Components, Graph, Bindings and Rules.
2. Copy `mme-assets/panel.png` from [lesson 1](../../examples/lessons/01-image/README.md) into the map's `mme-assets` folder.
3. Add one Assets section under MappingSettings. Copy lesson 1's Asset into it.
4. Copy lesson 1's two Component elements into lesson 5's existing Components section. Don't add a second Components wrapper.
5. Save, build and launch. You should see both the panel and **Jumps: 0**.

Here is the complete combined file:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="panelArt" path="mme-assets/panel.png" />
  </Assets>
  <Variables>
    <Variable id="jumps" kind="Integer" value="0" scope="Attempt" />
  </Variables>
  <Components>
    <Component id="panelPose" kind="transform">
      <Value name="position" value="240,160" />
    </Component>
    <Component id="panel" kind="sprite">
      <Value name="transform" value="panelPose" />
      <Value name="asset" value="panelArt" />
    </Component>
    <Component id="captionPose" kind="transform">
      <Value name="position" value="240,60" />
    </Component>
    <Component id="caption" kind="text">
      <Value name="transform" value="captionPose" />
    </Component>
  </Components>
  <Graph>
    <Node id="count" op="input" source="variable:jumps" />
    <Node id="pattern" op="constant" kind="Text" value="Jumps: {0}" />
    <Node id="captionText" op="template" inputs="pattern;count" />
  </Graph>
  <Bindings>
    <Bind id="showCount" source="captionText" target="caption/value" />
  </Bindings>
  <Rules>
    <Rule id="countJump" event="jump">
      <Action op="increment" target="jumps" value="1" />
    </Rule>
  </Rules>
</MappingSettings>
```

## Rename conflicting IDs

If both examples define a component called `pose`, rename one, such as `lampPose`. Change every reference to that component too: `transform`, `parent`, binding/effect targets such as `pose/scale`, and any selections or recipe arguments that name it. Renaming a file doesn't rename IDs inside it.

Graph inputs name graph-node IDs. A binding source names a graph node. An input source such as `variable:jumps` names a variable signal. Keep those connections straight when renaming. IDs are case-sensitive.

## Use fragments as the scene grows

Move related sections into a `MappingFragment` and include it from the entry. Includes still share the scene's ID space: they don't automatically rename duplicate IDs. [Files and packaging](files.md#split-a-scene-into-files) has a complete two-file example.

For several copies of the same object, use a [recipe](../scenes/recipes.md). Each instance gets its own internal names, which is easier than renaming every copied component by hand.
