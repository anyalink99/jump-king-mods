# Reflect the King or project a shadow

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

A capture makes an image of selected drawing. A receiver projects that image onto a polygon. This is useful for puddles, mirrors and shadows. Add water or platform collision through the native map separately.

## Reflection and shadow

A receiver projects a captured image into an ordered local polygon. Reflection projects RGBA; shadow projects alpha tinted by its material. `projection` controls the projection direction/ratio, and receiver opacity attenuates the result. Occlusion mode prepares an invisible contour that selected lights can use. These polygons are visual surfaces, independent of collision.

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

## Try it and change it

1. Copy the complete file into a working map and launch on screen 1.
2. Move the King near the bottom: the polygon should receive his reflection.
3. Change `opacity` to `0.5` to make it stronger.
4. Move the surface with `position` and change its local outline for your puddle.

For a shadow, use a capture feeding a material, set receiver `mode=shadow`, and tint that material a dark color. `occlusion` prepares an invisible contour for selected lights; it does not draw a projected image. Choose [capture selections](../reference/selections.md) deliberately; a capture draws the selected actor without running a second actor simulation.
