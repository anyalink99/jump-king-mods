# Choose how to draw an effect

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

For a normal PNG, start with [images and coordinates](placement.md). Use the guides below as your drawing becomes more involved.

| Task | Components | Guide |
|---|---|---|
| Color, fade, repeat or scroll an image | material, sprite | [Materials and light](materials.md) |
| Hide part of an image or a whole group | material, capture | [Masks](masks.md) |
| Reflect the King or draw a projected shadow | capture, receiver | [Reflections and shadows](surfaces.md) |
| Bend a hanging panel or ripple a surface | mesh, field, deformation | [Deformation](deformation.md) |
| Rain, sparks or a short burst | emitter | [Particles](particles.md) |
| Tint a screen, hide the timer or mirror scenery | view | [Screen presentation](views.md) |
| Show changing text | text | [Text](narrative.md) |

These are visual effects. Add native/map collision separately when an object must block the King. A reflection pool does not create water physics, and an invisible sprite does not remove a platform.

## Keep pixels whole

`transform.pixelSnap=true` is the default. Final geometry lands on logical pixels and uses point sampling, including through parent motion, rotation and scale. Graph calculations retain their precision. Whole coordinates don't prevent alpha blending or lighting from changing colors.

For smooth art, explicitly set `pixelSnap=false` on its transform and `sampling=linear` on its material. Prefer source-size artwork and whole scale factors for crisp pixel art; a fractional scale can change how many whole pixels the image occupies between frames.

A sensor does not draw. A half-pixel sensor center can be useful for an exact odd-sized rectangle without softening any art.

## Existing example links

The detailed examples are now in smaller guides:

### Mask one image or a whole group

Use the complete [mask example](masks.md#mask-one-image-or-a-whole-group).

### Reflection and shadow

Use the [reflection recipe and shadow steps](surfaces.md#reflection-and-shadow).

### Bend artwork with fields

Use the [pinned-panel example](deformation.md#bend-artwork-with-fields).

### Emit an upward stream

Use the [particle-stream example](particles.md#emit-an-upward-stream).

### Screen-wide presentation

Use [views](views.md).
