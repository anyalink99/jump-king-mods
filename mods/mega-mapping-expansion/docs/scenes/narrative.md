# Text and narrative

[Handbook](../index.md) / [Scene authoring](index.md)

For native NPC typing settings, individual voices and line events, read [NPC dialogue](../game/dialogue.md). That guide also includes a complete scene-text reveal example using `visibleCharacters`; the typography and layout options below apply to revealed text too.

Use a text component for changing labels and captions. Keep native NPC dialogue in its normal files. MME can react to dialogue-related events or control a presentation sequence, but doesn't replace the game's dialogue loader.

## Show and change a caption

This complete scene displays a caption after jumping and hides it after 1.5 seconds. It demonstrates shared variables, a binding, and a tree that can restart cleanly:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Variables><Variable id="caption" kind="Text" value="" /></Variables>
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,80" /></Component>
    <Component id="label" kind="text">
      <Value name="transform" value="pose" /><Value name="width" value="300" />
      <Value name="align" value="center" />
    </Component>
  </Components>
  <Graph><Node id="caption" op="input" source="variable:caption" /></Graph>
  <Bindings><Bind id="caption" source="caption" target="label/value" /></Bindings>
  <Trees>
    <Tree id="caption" event="jump" retrigger="restart">
      <Node op="sequence">
        <Node op="action"><Action op="set" target="caption" value="A little further." /></Node>
        <Node op="wait" seconds="1.5" />
        <Node op="action"><Action op="set" target="caption" value="" /></Node>
      </Node>
    </Tree>
  </Trees>
</MappingSettings>
```

A second jump restarts the wait. Setting a variable isn't an owned visual contribution: cancelling this tree doesn't automatically restore its text. Use a material opacity effect with tree ownership when cancellation should hide a caption immediately, or explicitly set the desired caption in the event that stops it.

## Native and bitmap fonts

`font=menu` uses the game's native font. It renders through a prepared 480 by 360 local canvas; content outside it clips. Width controls wrapping and align chooses left, center or right. Each wrapped line aligns independently. Center alignment means the line is centered around its anchor; choose the transform's Y for the intended text placement, rather than assuming it is a vertically centered bounding box.

For a bitmap font, set font to an asset ID and characters to the exact cell order. Declare its columns/rows as an atlas. Spacing adjusts the distance between cells. The glyph list must match the artwork; a missing character can't be conjured from another font.

This complete example uses the ten-digit sample atlas as a bitmap font:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets><Asset id="digits" path="mme-assets/digits.png" columns="10" frames="10" /></Assets>
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,120" /></Component>
    <Component id="number" kind="text">
      <Value name="transform" value="pose" /><Value name="font" value="digits" />
      <Value name="characters" value="0123456789" /><Value name="value" value="1207" />
      <Value name="align" value="center" /><Value name="spacing" value="1" />
    </Component>
  </Components>
</MappingSettings>
```

Both font modes accept a material, including color, opacity and masks. A graph `format` converts a typed value to Text for a binding. It doesn't provide a full string-template language. Use authored Text variables for complete captions and atlas digits when the number needs rolling transitions or tightly controlled artwork.

## Narrative sequences and actors

Build timed cues with [control trees](behavior-trees.md). Use [native ending bridges](../integration/endings.md) when a cue must occur at an actor's exact cutscene step. A follower recipe can read an ending actor's position just as it reads player.center, but must guard that actor's presence. Gaze is a composition of observed position, bounded vector math and drawing; it isn't a separate narrative entity.
