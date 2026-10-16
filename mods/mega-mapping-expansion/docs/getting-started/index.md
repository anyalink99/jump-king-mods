# Start using MME

[Handbook](../index.md)

Start with a Jump King map that already loads. MME adds scene behavior and map
features to that map; it doesn't replace the game's map format.

## Before you start

The current release is MME 1.1.1 and requires JK Runtime 2.2.0 or later. Install
one active copy of each, using the [installation instructions](../../README.md#install).
Smooth Camera 0.13.0 or later is needed only for smooth camera shots. Screen cuts
work without it. Blocks supplied by other mods still require those mods.

You can author every feature with files and a text editor. Ordinary Worldsmith
can build the native map. [Worldsmith Extension](worldsmith.md) is an optional
editor. Keep your editable source separate from the
folder you upload. Edit the XML and load the map; MME manages its cache outside
the map folder. You don't need to compile the scene.

## Choose your starting point

### I want to add moving or reactive scenery

1. Follow [your first scene](first-scene.md). It places an image, explains
   coordinates and anchors, then adds a reaction to the King.
2. Read [signals, graphs and effects](../scenes/behaviors.md). This is how a game
   observation becomes a change in the scene.
3. Pick a [worked example](examples.md) close to your idea. Each example says
   which assets and native map files it needs.
4. Read [scene files and packaging](files.md) before packaging the result.

### I already know the original map tools

Start with [what the game already provides](../game/native-map.md). It covers
native props, NPC lines and typing speed, audio, Hidden Walls and endings. Keep
using those formats. Add an MME adapter when you need to connect a native event
to artwork, sound, a camera shot or a different victory condition.

The [native game section](../game/index.md) leads to those tasks. You don't need
to learn meshes, shaders or control trees to change a dialogue voice.

### I only need layout, collision or screen links

Go to [geometry and navigation](../maps/index.md). You can use pixel collision,
sloped versions of mod blocks and screen links without building a decorative
scene. The guides show the required files and explain which mechanics remain
owned by the original block provider.

## How to use the examples

A complete example is a small scene, not a snippet to append to an existing XML
document. Try it in a working map first. When combining examples, merge their
sections and give components, graph nodes and variables unique IDs. Use
[fragments](files.md#split-a-scene-into-files) to split a larger scene across files.

Tables and snippets in the reference explain individual fields. Use the
[example catalogue](examples.md) when you need something you can run. If a scene
doesn't load or behave as expected, follow [troubleshooting](../reference/troubleshooting.md)
from the first reported error.
