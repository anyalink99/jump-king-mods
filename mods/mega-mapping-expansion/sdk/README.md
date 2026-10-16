# MME construction authoring kit

Copy mapping_settings.xml and mme-assets into an existing playable map source. Keep native spawn, Babe, collision and prop definitions. Start with the [handbook](../docs/index.md) or the [example catalogue](../docs/getting-started/examples.md).

## Install

Use MME 1.1.1 and JK Runtime 2.2.0 or later. Install one active copy of each in
the game's existing Workshop or local mod folder, then enable them in the game.
Close Jump King before replacing DLLs and keep your settings. The kit contains
map examples and the scene API; install the MME mod package separately.

## Use the kit

Edit the copied XML and load the map. MME validates it and manages its cache in the current user's local application data. You don't need a separate compiler, and the cache stays outside your map package.

The example combines a rotating surface, a flexible panel, an upward stream and rolling digits. Jump increments the counter and pulses the sphere's scale. It doesn't change collision.

The [native game guides](../docs/game/index.md) contain complete scenes for dialogue, sound zones, camera shots, inventory and victory conditions. [Observations](../docs/observations/index.md) explains how player actions and native interactions connect to the same graphs, effects and trees. The sample `mme-assets/chime.wav` is an original short test tone supplied for those lessons; replace it with a suitable sound for your map.

Read [authoring](../docs/getting-started/first-scene.md), [recipes](../docs/scenes/recipes.md), [reference](../reference/PARAMETERS_GENERATED.md) and [scene files and packaging](../docs/getting-started/files.md). Recipe source is provided for learning/customization; don't include the embedded library a second time with the same IDs.

The exported sdk folder contains MegaMappingApi.dll, API documentation and C# consumer/module examples. Use the game inspector for preview/reload. The removed formats and former preview-control protocol aren't supported.
