# Read and write MME XML

[Handbook](../README.md) / [Reference](README.md)

Use a text editor and save as UTF-8. Start from [a complete lesson](../../examples/lessons/README.md), then change one setting at a time. The final file has exactly one MappingSettings root.

## Roots, sections and includes

`<MappingSettings version="1">` is the entry. Optional `saveId` identifies saved variables. Its sections are MapLayout, Assets, Components, Graph, Bindings, Variables, Effects, Rules, Trees, Exports, Recipes and Instances. Keep one wrapper for each section; put multiple entries inside it. [Combining examples](../getting-started/combining.md) shows a complete merge.

`<Include src="fragments/light.xml" />` includes a `<MappingFragment version="1">` file. Its path is relative to the map root, even when another fragment includes it. IDs share one merged scene. Paths must stay inside the map. Include cycles, duplicate sections in one document, unknown fields and duplicate IDs are rejected.

MapLayout uses version 2; Transitions and CollisionShapes use version 1 in their own files. Native ending files use their native node root, and Runtime policy uses MapPolicy. These numbers belong to different formats, not the mod release number.

## Assets

| Asset attribute | Meaning |
|---|---|
| id | Name used by sprite/mesh/material/font references |
| path | Safe map-relative image, compiled counterpart or vector XML path |
| kind | png by default, or vector for the limited VectorAsset format |
| columns, rows | Atlas grid; each defaults to 1; allowed 1..256 |
| frames | Used cells, or 0 for all grid cells; cannot exceed grid capacity |
| fps | Automatic animation speed, 0..240; 0 leaves the starting frame |

Image dimensions must divide evenly into the grid. [Animation](../scenes/texture-animation.md) explains frame order, playback and manual selection. [Vectors](../scenes/vectors.md) documents VectorAsset/Shape options. Sounds are declared with an audio component's path, not an image Asset.

## Values and names

| Kind | Example | Used for |
|---|---|---|
| Number | 0.25 | Continuous values |
| Integer | 3 | Whole counts and screens |
| Boolean | true | Conditions and switches |
| Vector2 | 240,180 | X/Y positions or two-axis values |
| Color | #88BBDD | Color multipliers |
| Text | Hello | Captions, IDs and choices |

Use a dot for decimals, finite numbers and the exact case in the examples. Number and Integer are distinct. Use `&amp;` for a literal ampersand, `&lt;` for a less-than character and `&quot;` for quotes inside a double-quoted attribute. Text components accept `&#10;` for a newline; the simple native view/intro string uses escaped `<newline>` markers.

Colors also accept `#RRGGBBAA` with alpha, or four comma-separated channels from 0 to 1. A Vector2 always has two comma-separated numbers. Keep file paths and IDs as ordinary Text rather than wrapping them in another layer of quotes inside the XML attribute.

A Component uses `id`, `kind`, optional `tags`, and Value children with `name` and `value`. Look up every [component field](components.md). Component/property addresses use a slash, as in `lamp/intensity`. Signals use their declared names, such as `zone.touching`; variables use `variable:jumps`. Graph `inputs` and selections use semicolons, while vector literals use a comma.

## Logic and reuse

- [Graphs and bindings](graphs.md): every Node/Bind attribute, operation, type and input order.
- [Rules, actions and effects](actions.md): every Rule/Action/Effect/Set attribute and lifetime.
- [Trees](../scenes/behavior-trees.md): Tree settings and every Node operation.
- [Recipes](../scenes/recipes.md): Recipe/Parameters/Param, Instance/Arg, local @ references and Exports.
- [Built-in recipe arguments](recipes.md): defaults, purpose and exported ports.
- [Selections](selections.md): drawing, surface and actor selector vocabularies.

The expanded XSD is for tools after recipe expansion. It doesn't describe every authoring shorthand or check resources and graph types. MME validates the real scene during load. [Validation](schema.md) explains the stages.
