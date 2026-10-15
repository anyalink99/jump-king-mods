# Mega Mapping handbook

MME adds reactive scenery, map layouts and connections to native Jump King
systems. Use the guide for the thing you want to build. You don't need to read
every chapter before making a scene.

**New to MME?** Start with [setup and your first task](getting-started/index.md).
**Already know the original map tools?** Read [what the game already provides](game/native-map.md).
**Looking for working code?** Browse the [example catalogue](getting-started/examples.md).
**Something broke?** Follow [troubleshooting](reference/troubleshooting.md).

## Choose a section

| Section | What you'll find |
|---|---|
| [Getting started](getting-started/index.md) | Installation, first scene, worked examples, files, compilation and Worldsmith |
| [Scene authoring](scenes/index.md) | Artwork, animation, masks, lighting, particles, deformation, reactions, text and reusable recipes |
| [Game observations](observations/index.md) | Movement, actions, charge, contacts, native interactions, pause and lifecycle events |
| [Native game systems](game/index.md) | What the game already supports; NPC dialogue, sound, items, Hidden Walls, cameras and victory |
| [Geometry and navigation](maps/index.md) | Large maps, collision resolution, slope aliases, side links and map requirements |
| [Integration](integration/index.md) | Native cutscene choreography, native file ownership and the C# scene API |
| [Reference and diagnostics](reference/index.md) | Exact fields and defaults, capabilities, limits, validation, troubleshooting and architecture |

Each folder has its own index. Chapter navigation returns to that section or
this page. There is one copy of each guide; the root doesn't contain redirects
to a second set of chapters.

## Find a common task

| I want toвЂ¦ | Start here |
|---|---|
| Place an image and react to the King | [First scene](getting-started/first-scene.md) |
| Turn a signal into a visible change | [Graphs, bindings and effects](scenes/behaviors.md) |
| Build a counter or select animation frames | [Animation and counters](scenes/texture-animation.md) |
| Clip an effect to a wall or another drawing | [Masks and composition](scenes/objects.md) |
| React to a real surface, including a modded slope | [Contact feedback](scenes/contact-feedback.md) |
| Reuse a moving planet, gaze or stream recipe | [Recipes](scenes/recipes.md) |
| Change dialogue speed or its voice | [Dialogue](game/dialogue.md) |
| Add a sound zone or control a loop | [Audio](game/audio.md) |
| Trigger something on another screen | [Remote scenes](scenes/remote-scenes.md) |
| Move the camera without moving the King | [Camera shots](game/camera.md) |
| Require an item or define a victory hitbox | [Native interactions and victory](game/interactions.md) |
| Run scene effects during an ending | [Ending trees and scene bridges](integration/endings.md) |
| Turn a mod block into a slope | [Collision](maps/collision.md) |
| Connect screen edges | [Side links](maps/teleports.md) |
| Keep counters across attempts or restore a scene | [State and saves](scenes/state.md) |
| Split, compile and package a scene | [Scene files and packaging](getting-started/files.md) |

For exact XML properties, use the [field reference](reference/parameter-reference.md)
alongside a worked guide. The [example catalogue](getting-started/examples.md)
links directly to complete scenes, including multi-file examples and their
required assets. Older scene formats need [source conversion](getting-started/files.md#convert-an-older-scene).
