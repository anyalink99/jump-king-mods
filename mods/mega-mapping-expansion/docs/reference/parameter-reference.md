# Find a setting and understand its value

[Handbook](../README.md) / [Reference](README.md)

Start with [field explanations](components.md) for purpose and [a worked example](../getting-started/examples.md) for the surrounding files. Use the generated tables when you need an exact spelling, default or accepted choice.

- [Component field explanations](components.md): every setting and what it changes.
- [Built-in recipe arguments](recipes.md): every argument, default, unit/meaning and export.
- [Generated Markdown](../../reference/PARAMETERS_GENERATED.md): component kinds, defaults, units, bindability, choices and recipe arguments.
- [Generated XML catalog](../../reference/scene-parameters.xml): numeric bounds and reference kinds as well.
- [Graphs](graphs.md), [actions/effects](actions.md), [events](events.md), [XML structure](xml.md): the rest of the authoring format.

## Read a field row

Type tells you the SceneValue kind. Default applies when the Value is omitted. Units distinguish pixels, seconds, turns, frames and ratios. Bindable means a graph or effect can change the setting during play. Choices are exact accepted Text values; an empty choice list can still mean an ID reference requiring validation.

Impact describes the work a change affects. Structural fields need prepared resources or references and cannot be bound. For example, sprite.frame is a bindable Number in frame units; sprite.asset names a prepared Asset and is structural. transform.position is Vector2 in pixels; transform.rotation uses degrees.

The generated data comes from the same descriptors used by MME and the inspector. Don't edit generated tables by hand. Maintainers regenerate them during [the build](maintaining.md). Map authors can load their XML directly; a separate compiler is optional developer tooling.
