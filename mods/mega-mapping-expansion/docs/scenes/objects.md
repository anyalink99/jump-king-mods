# Rendering and composition

[Handbook](../index.md) / [Scene authoring](index.md)

Keep static scenery in the map's normal layers. Use MME for the parts that move, react, project or change. This chapter builds on [the first scene](../getting-started/first-scene.md); the [generated catalog](../../reference/PARAMETERS_GENERATED.md) supplies every exact field and limit.

## Pick the drawing component

| Component | Use it for |
|---|---|
| transform | Position, parent, screen, rotation, scale and pixel policy |
| sprite | An image or atlas animation |
| material | Shared color, opacity, UVs, blending, sampling and masks |
| mesh | A textured grid, polygon or disc with projection/deformation |
| field | A reusable sine, noise, curve or spring displacement |
| deformation | A pinned, weighted application of a field |
| emitter | A continuous stream or a bounded burst of particles |
| light | A radial/cone light with optional occluders |
| capture | Selected scene/native drawing rendered into a transparent target |
| receiver | A polygon receiving reflection, shadow or occlusion |
| stamp | Short-lived marks placed by actions |
| text | Native or bitmap text |
| sensor | Spatial/contact observations; no drawing |
| view | Screen grading, mirror, timer, intro and pause policy |

Layers are `background`, `world`, `surface`, `foreground`, `composite`, and `hud` where the component supports it. Z orders items within a layer; stable IDs settle ties. Layer choice doesn't change collision. Materials and transforms can be shared, so changing one shared opacity or parent affects all its users.

## Materials, light and UVs

Color multiplies artwork; opacity scales alpha. Blend is `alpha`, `add` or `multiply`. Additive lights and artwork can wash out the scene quickly: begin around intensity 0.05вЂ“0.1, then inspect at game scale. Radius is pixels, cone is degrees, and softness/scatter change the spread. `occluders` selects prepared contours rather than inventing collision from the light.

`uvOffset` shifts the texture in turns: 1 is a full texture width/height. `uvScale` changes repetition. The default address mode is clamp, which prevents opposite-edge wrapping. Set wrap deliberately for scrolling surfaces. A material can also use ambient/diffuse shading and `normalLight` for projected surfaces. Emission and a light component are separate controls; a bright-looking image doesn't automatically illuminate nearby objects.

## Keep pixels whole

Transform `pixelSnap=true` is the default. MME snaps final transformed geometry, including parent motion, origin, rotation and scale, to logical pixels and enforces point sampling. Graph calculations remain precise. This prevents a follower's fractional position from quietly making its edges blurry.

For intentionally smooth art, set `pixelSnap=false` on its transform and `sampling=linear` on its material. Both choices are explicit. Whole coordinates don't prevent intentional color blending from transparency, fades, crossfades or lighting. Fractional scale can still change how many whole pixels a shape occupies from frame to frame; prefer source-size artwork and whole scale factors when that matters.

Sensor transforms don't draw. An odd-width region may need a half-pixel center to preserve an exact authored rectangle; opting its transform out of snapping doesn't soften any visible art.

## Mask one image or a whole group

A material's `mask` names an asset and uses its alpha. `maskSpace=local` maps the mask with the drawing; `screen` anchors the mask in the 480 by 360 screen. A local cutout follows a moving object. A screen mask is useful for a counter window or a shaft that particles must stay inside.

Masks also work on mesh, text and receiver materials. For drawing without a direct material mask, capture the group and mask its output. Mark source drawing `captureOnly=true` to keep it in the selected capture without also drawing it normally.

This complete example captures a panel and a light, then displays the group through an alpha mask. It uses the sample panel as both art and a demonstration mask; replace the mask asset with your own cutout for a real scene.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets><Asset id="panelArt" path="mme-assets/panel.png" /></Assets>
  <Components>
    <Component id="sourcePose" kind="transform"><Value name="position" value="220,150" /></Component>
    <Component id="panel" kind="sprite" tags="maskedGroup">
      <Value name="transform" value="sourcePose" /><Value name="asset" value="panelArt" />
      <Value name="captureOnly" value="true" />
    </Component>
    <Component id="glow" kind="light" tags="maskedGroup">
      <Value name="transform" value="sourcePose" /><Value name="intensity" value="0.07" />
      <Value name="captureOnly" value="true" />
    </Component>
    <Component id="groupCapture" kind="capture">
      <Value name="selection" value="tag:maskedGroup" /><Value name="stage" value="world" />
    </Component>
    <Component id="maskedMaterial" kind="material">
      <Value name="capture" value="groupCapture" /><Value name="mask" value="panelArt" />
      <Value name="maskSpace" value="screen" />
    </Component>
    <Component id="outputPose" kind="transform" />
    <Component id="output" kind="sprite">
      <Value name="transform" value="outputPose" /><Value name="material" value="maskedMaterial" />
      <Value name="layer" value="composite" />
    </Component>
  </Components>
</MappingSettings>
```

The capture is a screen-sized transparent target. Leave its output at `(0,0)` for a screen overlay. Moving or scaling the output moves/scales that captured screen. The output must draw after the capture stage. Don't include the output in its own capture; recursive capture graphs fail validation.

Capture selections accept component IDs, `tag:name`, `native.background`, `native.midground`, `native.foreground`, `actor:player` and [ending actor selectors](../integration/endings.md#follow-cutscene-actors). Separate multiple selectors with semicolons. Capturing an actor draws its current state; it doesn't update or clone that actor's simulation.

## Reflection and shadow

A receiver projects a captured image into an ordered local polygon. Reflection projects RGBA; shadow projects alpha tinted by its material. `projection` controls the projection direction/ratio, and receiver opacity attenuates the result. Occlusion is another receiver mode. These polygons are visual surfaces, independent of collision.

The ready-made reflection recipe connects capture, material and receiver:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Instances>
    <Instance id="pool" recipe="surface-reflection">
      <Arg name="selection" value="actor:player" />
      <Arg name="position" value="0,280" />
      <Arg name="outline" value="0,0;480,0;480,80;0,80" />
      <Arg name="projection" value="0,0.6" />
      <Arg name="opacity" value="0.25" />
    </Instance>
  </Instances>
</MappingSettings>
```

The bottom 80 pixels receive the player's reflection. Change the selection to captured decoration for another use. To build a shadow directly, use a capture as `material/capture`, set receiver mode to shadow, and choose a dark material color. There is no selection field on the receiver itself; selection belongs to the capture.

## Bend artwork with fields

A mesh supplies vertices. A field supplies displacement at a point. A deformation applies that displacement with a pinned profile. They remain separate so the same field can move several meshes, and one mesh can chain several deformations through `next`.

This complete scene bends a sample panel horizontally while its lower edge stays pinned:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets><Asset id="panelArt" path="mme-assets/panel.png" /></Assets>
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,180" /></Component>
    <Component id="wind" kind="field">
      <Value name="function" value="sine" /><Value name="amplitude" value="4,0" />
      <Value name="frequency" value="0,0.008" />
    </Component>
    <Component id="bend" kind="deformation">
      <Value name="field" value="wind" /><Value name="pin" value="0,48" />
      <Value name="extent" value="64,96" /><Value name="axis" value="x" />
    </Component>
    <Component id="panel" kind="mesh">
      <Value name="transform" value="pose" /><Value name="asset" value="panelArt" />
      <Value name="size" value="64,96" /><Value name="columns" value="8" />
      <Value name="rows" value="24" /><Value name="deformation" value="bend" />
    </Component>
  </Components>
  <Graph>
    <Node id="rate" op="constant" value="0.3" />
    <Node id="phase" op="integrate" inputs="rate" units="turns" />
  </Graph>
  <Bindings><Bind id="phase" source="phase" target="wind/phase" /></Bindings>
</MappingSettings>
```

Increase amplitude for more bend, reduce rate for slower motion, and move the pin to change the fixed edge. Extent and falloff control the weight away from the pin. Choose local or world space deliberately when sharing a field across differently placed meshes.

Fields support sine, deterministic noise, a curve, and a spatial spring. A spring field has length, direction, stiffness, propagation, damping, displacement limit and impulse radius. Send an `impulse` action with numeric force at a contact point to make a local disturbance travel along it. A graph spring smooths one value; a spring field stores a spatial disturbance. They solve different problems.

Mesh geometry can be grid, polygon or disc. Projection can be planar, spherical, cylindrical or perspective; projectionTilt adjusts its orientation. Columns/rows prepare tessellation and can't change during play. Use enough vertices for the desired bend, then measure; a dense grid on every small prop wastes work. The [planet recipe](../../recipes/planet.xml) shows spherical projection and UV rotation using these same parts.

## Emit an upward stream

This stream moves upward because velocity Y is negative. `area` is the spawn region, not the full region particles will occupy. The emitter is centered on its transform:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets><Asset id="mote" path="mme-assets/mote.png" /></Assets>
  <Components>
    <Component id="source" kind="transform"><Value name="position" value="240,330" /></Component>
    <Component id="stream" kind="emitter">
      <Value name="transform" value="source" /><Value name="asset" value="mote" />
      <Value name="shape" value="box" /><Value name="area" value="40,8" />
      <Value name="velocity" value="0,-80" /><Value name="spread" value="8,12" />
      <Value name="rate" value="12" /><Value name="lifetime" value="3" />
      <Value name="capacity" value="64" /><Value name="seed" value="7" />
      <Value name="clock" value="presentation" />
    </Component>
  </Components>
</MappingSettings>
```

Rate Г— lifetime estimates the steady live population: here 36 particles, below capacity 64. Capacity is a prepared pool, not a rate. Shapes are point, box, line and disc. Velocity, spread, acceleration and size shape the stream; a fixed seed makes it repeatable. Bind rate to charge or sensor membership to start and stop emission. Existing particles live out their lifetime.

Continuous particles reconstruct from the scene clock after restore. Bursts are transient and clear instead of replaying historical births. [Contact feedback](contact-feedback.md) shows transient stamps; use a material mask or captured group when a stream must remain inside a particular silhouette.

## Screen-wide presentation

One view may exist for each screen, plus global screen 0. A screen-specific view takes precedence over the global one. Ambient, tint/tintAmount and exposure grade the composed scene. Mirror supports none/horizontal/vertical/both. Timer is inherit/show/hide; intro supplies the scene's intro text. Use the literal marker `<newline>` in that text for a line break, escaped as `&lt;newline&gt;` inside an XML attribute. These controls don't change platform collision.

Presentation animation freezes during pause unless the selected view explicitly sets `animateWhilePaused=true`. This bindable Boolean can also be contributed by an owned effect. It permits Presentation-clock drawing, not gameplay advancement or paused event dispatch. Most maps should leave it false. For ending effects, choose Presentation clocks without enabling pause animation.
