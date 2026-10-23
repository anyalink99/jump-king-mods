# Clip an image or a group with a mask

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

Use a mask when only part of an image or effect should be visible. The mask uses alpha: opaque pixels show drawing; transparent pixels hide it. Start with a PNG sprite before masking a whole group.

## Mask one image or a whole group

A material's `mask` names an asset and uses its alpha. `maskSpace=local` maps the mask with the drawing; `screen` anchors the mask in the 480 by 360 screen. A local cutout follows a moving object. A screen mask is useful for a counter window or a shaft that particles must stay inside.

Masks also work on mesh, text and receiver materials. For drawing without a direct material mask, capture the group and mask its output. Mark source drawing `captureOnly=true` to keep it in the selected capture without also drawing it normally.

This complete example captures a panel and a light, then displays the group through an alpha mask. It uses the sample panel as both art and a demonstration mask; replace the mask asset with your own cutout for a real scene.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="panelArt" path="mme-assets/panel.png" />
  </Assets>
  <Components>
    <Component id="sourcePose" kind="transform">
      <Value name="position" value="220,150" />
    </Component>
    <Component id="panel" kind="sprite" tags="maskedGroup">
      <Value name="transform" value="sourcePose" />
      <Value name="asset" value="panelArt" />
      <Value name="captureOnly" value="true" />
    </Component>
    <Component id="glow" kind="light" tags="maskedGroup">
      <Value name="transform" value="sourcePose" />
      <Value name="intensity" value="0.07" />
      <Value name="captureOnly" value="true" />
    </Component>
    <Component id="groupCapture" kind="capture">
      <Value name="selection" value="tag:maskedGroup" />
      <Value name="stage" value="world" />
    </Component>
    <Component id="maskedMaterial" kind="material">
      <Value name="capture" value="groupCapture" />
      <Value name="mask" value="panelArt" />
      <Value name="maskSpace" value="screen" />
    </Component>
    <Component id="outputPose" kind="transform" />
    <Component id="output" kind="sprite">
      <Value name="transform" value="outputPose" />
      <Value name="material" value="maskedMaterial" />
      <Value name="layer" value="composite" />
    </Component>
  </Components>
</MappingSettings>
```

The capture is a screen-sized transparent target. Leave its output at `(0,0)` for a screen overlay. Moving or scaling the output moves/scales that captured screen. The output must draw after the capture stage. Don't include the output in its own capture; recursive capture graphs fail validation.

Capture selections accept component IDs, `tag:name`, `native.background`, `native.midground`, `native.foreground`, `actor:player` and [ending actor selectors](../integration/endings.md#follow-cutscene-actors). Separate multiple selectors with semicolons. Capturing an actor draws its current state; it doesn't update or clone that actor's simulation.

## Try it and change it

1. Copy the sample panel from lesson 1, then load this full example.
2. Replace the demonstration mask with your own 480 by 360 transparent PNG for a screen-space cutout. Add its own Asset ID and use that ID in `maskedMaterial/mask`.
3. Keep `outputPose` at `0,0`: the capture is already a full-screen image.
4. If you see the source twice, check its `captureOnly` setting.

Choose `maskSpace=local` for a mask that moves with an individual object. Choose `screen` for a fixed viewport window. Use [selections](../reference/selections.md) when choosing capture contents.
