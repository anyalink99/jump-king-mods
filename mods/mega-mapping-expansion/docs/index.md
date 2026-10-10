# Mega Mapping Expansion guides

Build one scene with the [authoring handbook](authoring.md), then use the table
to find the next feature. The standalone authoring kit has the same guide layout.
Players looking for installation or the Enable switch should use the
[mod README](../README.md).

## Make and test a map

| Task | Guide |
| --- | --- |
| Add the first visible scene | [Authoring handbook](authoring.md) |
| Find a small complete example | [Recipes](recipes.md) |
| Choose an effect or understand its limits | [Feature overview](features.md) |
| Look up a field, default or unit | [Parameter reference](parameter-reference.md) |
| Edit, reload, inspect or diagnose performance | [Troubleshooting](troubleshooting.md) |
| Work in Worldsmith or use its extension | [Worldsmith authoring](worldsmith.md) |
| Connect screen edges | [Screen teleports](teleports.md) |
| Build beyond native screen-count assumptions | [Large maps](large-maps.md) |

## Add scene behavior

| Task | Guide |
| --- | --- |
| Branch, wait, loop or coordinate effects | [Behavior trees](behavior-trees.md) |
| Use temporary effects and save flags | [Behavior cookbook](behaviors.md) |
| Reuse parameterized objects | [Objects](objects.md) |
| Add text, native actors or results | [Narrative](narrative.md) |
| Replace an ending tree | [Custom endings](endings.md) |
| Fit data into native map folders and contacts | [Native workflows](native-workflows.md) |
| Declare requirements and restrictions | [Map policies](map-policies.md) |
| Animate image frames | [Texture animation](texture-animation.md) |

## Integrate or maintain the mod

[Scene API 1.0](scene-api.md) describes the capability other mods can consume.
It remains API 1.0 even though this release requires Runtime 2.0.
[Architecture](architecture.md) explains loading and rendering ownership;
[schema maintenance](schema.md) explains how generated fields and defaults stay
aligned with the parser. Don't hand-edit the generated parameter reference.
