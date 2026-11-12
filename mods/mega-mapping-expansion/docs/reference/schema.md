# Format validation

[Handbook](../README.md) / [Reference and diagnostics](README.md)

MME checks your scene when the map loads. Start with the first reported error, fix that file and reload/restart. You don't need a separate compiler for ordinary mapping. Use [XML basics](xml.md) for a section/attribute lookup and [troubleshooting](troubleshooting.md) for the diagnostic steps.

Use MappingSettings version=1 for the scene entry, MappingFragment version=1 for includes, and MapLayout version=2 for layout metadata. These numbers identify different formats; they aren't the mod's release number.

## Sections and their jobs

| Section | Contains |
|---|---|
| MapLayout | Actual screens, collision source/packing, resolutions and layout links |
| Assets | Prepared image/atlas/vector resources |
| Components | Typed instances of general primitives |
| Graph | Typed value calculations and observations |
| Bindings | Graph contributions to bindable properties |
| Variables | Attempt/Save typed state |
| Effects | Prepared owned property-contribution definitions |
| Rules | Exact event reactions and actions |
| Trees | Waiting/branching sequences |
| Exports | Stable component/property/node/signal ports |
| Recipes, Instances | Authoring presets and their expansions |

The [expanded XSD](../../reference/scene-expanded.xsd) describes structure after recipe expansion. The [XML registry](../../reference/scene-parameters.xml) describes fields, bounds, units and references. Neither replaces semantic compilation. Authoring Includes/Recipes/Instances are processed before expanded scene deserialization.

## Validation stages

The source loader rejects unsafe paths, include cycles, excessive nesting, DTDs and external entities. Recipe expansion rejects missing/unknown arguments, duplicate IDs/exports, recursion and excessive expansion. Scene compilation validates component fields, typed ports, bindings, effect/action targets, tree structure and resource budgets.

Compilation rejects nonfinite/out-of-range baselines, unknown choices/references, graph cycles without delay, transform/capture cycles and incompatible types. Runtime graph faults such as division by zero become diagnostics and unavailable branches rather than trustworthy zero values. Other independent branches can continue.

Asset validation checks safe resolution, dimensions and divisible atlas cells. Preparation checks loaded native contracts, sound keys and graphics resources. Native ending structure and declared bridge variable types are checked before actor creation. A route or cutscene can still be badly staged despite passing these checks.

## Check the final artifact

Load the final playable folder in Jump King. MME validates the XML and prepares the resources there. It manages a local cache outside the map and automatically replaces outdated or corrupt entries. Invalid XML still fails with a source error; a cache cannot override it. Mod developers can also run the repository's `SceneCacheCompiler.exe --validate <map-root>` for offline checks, but mappers don't need that tool.

Handbook complete XML examples are compiled by check-docs.ps1. Native illustrative fragments are identified separately; they still need the surrounding native map and game resources. See [files](../getting-started/files.md) for a complete include example and [troubleshooting](troubleshooting.md) for a diagnostic order.
