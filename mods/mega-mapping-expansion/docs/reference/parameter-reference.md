# Component and recipe reference

[Handbook](../index.md) / [Reference and diagnostics](index.md)

The [generated Markdown catalog](../../reference/PARAMETERS_GENERATED.md) lists every component field and embedded recipe argument. The [XML catalog](../../reference/scene-parameters.xml) adds exact numeric bounds and reference kinds. They come from the same descriptors used by compiler and inspector, so they are the field reference for this build.

## Read a field row

Type is the required SceneValue kind. Default is used when a Value is omitted. Units distinguish pixels, seconds, turns, frames and ratios. Bindable tells you whether a graph/effect can change the property during play. Choices are exact accepted Text values. Empty Choices means the field isn't an enum; it may still be an ID/reference with its own validation.

Impact describes what a change affects: pose, drawing membership/order, geometry, material or structural preparation. Structural fields can't be bound. A referenced asset or component ID is structural even though its XML value looks like ordinary text.

For example, sprite.frame is a bindable Number measured in frame units. sprite.asset is an asset reference that requires preparation. Transform.position is Vector2 in pixels; rotation is degrees. Material.sampling accepts point or linear but can't switch dynamically. View.animateWhilePaused is a bindable Boolean with false baseline.

## Find a working example

- [Authoring](../getting-started/first-scene.md) introduces transform, sprite, light and sensor.
- [Rendering](../scenes/objects.md) covers every drawing family, materials, capture, masks, deformation and particles.
- [Graphs and actions](../scenes/behaviors.md) explains operation input order, types, effects and rules.
- [Animation](../scenes/texture-animation.md), [charge](../observations/charge.md) and [text](../scenes/narrative.md) build data-driven displays.
- [Recipes](../scenes/recipes.md), [trees](../scenes/behavior-trees.md) and [endings](../integration/endings.md) show composition and sequence control.
- [Vectors](../scenes/vectors.md) documents the optional VectorAsset rasterization format.

The generated XSD describes expanded scene structure, not all recipe authoring shorthand or semantic constraints. A valid XSD document can still have an invalid graph or resource reference. Always run the compiler.

Regenerate the reference using `SceneCacheCompiler.exe --reference path/to/reference`. Don't edit generated tables separately from descriptors. The build also generates the expanded XSD. Check the runtime/compiler version when a field appears in a downloaded guide but is rejected locally.
