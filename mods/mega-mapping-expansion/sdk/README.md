# MME authoring kit

Use this kit to add MME features to a working Jump King map. Start with [installation and a test map](../docs/getting-started/README.md), then [your first image](../docs/getting-started/first-scene.md). The [handbook](../docs/README.md) lists all tasks.

## Install the runtime mods

Subscribe to **Mega Mapping Expansion** and **JK Runtime** in Jump King's Steam Workshop. This kit describes MME 1.3.0 with Runtime 2.2.1+. It contains the map examples, assets and handbook used below.

## Try a lesson

1. Make a separate copy of a native map that already loads and allows a jump on screen 1.
2. Open [lesson 1](../examples/lessons/01-image/README.md). Copy its `mapping_settings.xml` and `mme-assets` directory beside the map's `level_settings.xml`.
3. [Build and launch the map](../docs/getting-started/worldsmith.md) with MME and Runtime enabled. Look for the panel at `(240,160)` on screen 1.
4. Continue through [the seven lessons](../docs/getting-started/lessons.md), one replacement entry at a time. Each lesson carries its required assets.

The `mapping_settings.xml` at the kit root is a combined demonstration with a rotating surface, flexible panel, particles and rolling digits. Use it after the small lessons. `chime.wav` is an original short test tone supplied for the lessons; replace it with suitable audio in a real map.

## Make your own scene

Edit XML and load the map. MME validates it and manages a disposable local cache outside the map. You don't compile or upload a cache. [Combining examples](../docs/getting-started/combining.md), [files/packaging](../docs/getting-started/files.md) and [the example catalogue](../docs/getting-started/examples.md) explain the next steps.

Built-in recipes are already embedded; the `recipes` files are source for learning/modifying, not files to Include a second time under unchanged IDs. The exported `sdk` folder contains `MegaMappingApi.dll` and C# examples for mod developers. [The API guide](../docs/integration/scene-api.md) explains that separate workflow.
