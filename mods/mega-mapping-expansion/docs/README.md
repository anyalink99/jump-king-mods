# Make maps with Mega Mapping Expansion

MME adds images, animation, reactions and extra map features to a normal Jump King map. You write its settings in XML; the game reads them when the map loads.

**Start here:** [install MME and prepare a test map](getting-started/README.md), then [put your first image on screen](getting-started/first-scene.md). The [seven lessons](getting-started/lessons.md) build up one idea at a time.

## Find the thing you want to make

| I want to... | Guide |
|---|---|
| Find where to put files and how to launch my edit | [Files](getting-started/files.md), [loading and testing](getting-started/worldsmith.md) |
| Place, rotate, resize or group images | [Images and coordinates](scenes/placement.md) |
| Animate a prop or show a number with rolling digits | [Animation and counters](scenes/texture-animation.md) |
| React to a jump, an area or a particular block | [Reactions](scenes/behaviors.md), [sensors](scenes/contact-feedback.md) |
| Add light, masks, reflections, moving cloth or particles | [Drawing and effects](scenes/README.md) |
| Display a message or translate it | [Text](scenes/narrative.md), [languages and templates](scenes/localized-text.md) |
| Play sound, change NPC speech or react to an item | [Native game systems](game/README.md) |
| Show a distant change with the camera | [Remote scenes](scenes/remote-scenes.md), [camera shots](game/camera.md) |
| Add an opening sequence, win condition or final results | [Intro/results](game/results.md), [victory](game/interactions.md#victory-conditions), [ending choreography](integration/endings.md) |
| Draw finer collision, make slopes or add screens | [Geometry](maps/README.md) |
| Connect screens or randomize routes | [Side links](maps/teleports.md) |
| Save a counter or sequence state | [Saves and snapshots](scenes/state.md) |
| Reuse a ready-made planet, gaze, wind or reflection | [Recipes](scenes/recipes.md) |
| Fix an error or understand an inspector value | [Troubleshooting](reference/troubleshooting.md), [inspector](reference/inspector.md) |

## Browse the handbook

- [Getting started](getting-started/README.md): installation, first result, lessons, example files and editors.
- [Scene authoring](scenes/README.md): images, animation, reactions, effects, sequences and reusable recipes.
- [Game observations](observations/README.md): what you can read about the King and native interactions.
- [Native game systems](game/README.md): sound, NPCs, inventory, hidden walls, cameras and victory.
- [Geometry and navigation](maps/README.md): collision, layouts, links and map requirements.
- [Integration](integration/README.md): native ending files and the C# API.
- [Reference](reference/README.md): all fields, XML structure, events, limits and diagnostics.

Use the [example catalogue](getting-started/examples.md) for something to copy. Use [capabilities](reference/features.md) for a full feature checklist and [component fields](reference/components.md) to understand a setting. The [generated tables](../reference/PARAMETERS_GENERATED.md) give exact types and defaults.
