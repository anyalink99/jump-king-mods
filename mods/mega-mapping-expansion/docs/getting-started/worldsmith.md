# Worldsmith workflow

[Handbook](../index.md) / [Getting started](index.md)

You can author MME with ordinary Worldsmith, Worldsmith Extension, another map builder, or a text editor. MME and JK Runtime are runtime dependencies. Worldsmith Extension is an optional authoring tool.

## With ordinary Worldsmith

1. Start from a normal playable map source and keep its native definitions.
2. Add `mapping_settings.xml`, fragments and referenced artwork at safe relative paths.
3. Build the native map with Worldsmith, preserving the MME entry and extra files. Check that the playable folder contains all referenced XML and resources.
4. Load that folder in Jump King with the required mods. MME reads and validates the scene automatically; inspect diagnostics if it doesn't load.
5. After edits, rebuild or copy the changed files into the playable folder and restart the map.

Worldsmith turns textures into uncompressed XNBs. Keep the authored image path in Assets; MME finds a matching compiled file when the image is absent. Include custom fragment/vector XML in the final package. You don't compile an MME cache: the mod manages it outside the map.

## With Worldsmith Extension

Extension provides layout/collision controls and scene authoring helpers. Current MME uses MapLayout version=2 and construction MappingSettings version=1. An older generator that emits removed formats needs source conversion; a valid-looking old preview isn't evidence that MME 1.0 can read it.

Use explicit screen count, source packing and cell resolution. For mixed resolution, preserve uniform pixel groups when enlarging coarse collision art. Side links can be edited in Extension or in transitions.xml. Consult [collision](../maps/collision.md) and [screen links](../maps/teleports.md) before setting them: a single enabled native link serves both sides of the screen.

## With another map builder

Choose the intended map source explicitly when your builder manages several maps. Let its renderer generate construction XML and copy the final settings, fragments and resources into the playable map. MME reads those files directly when loading the map.

Keep editable source separate from generated handoffs. Build validation covers formats/assets, not route completion. The playable package must include everything it needs independently of the builder.

## Edit and reload in game

Run with -debug for Mapping inspector previews and presentation reload. Inspect effective properties, observation quality and owned effects. Export saves an effect fragment for you to review/include; it isn't a two-way Worldsmith synchronization system. Restart after layout, collision, side-link or native prop changes. Shared-state ownership can block reload.

Before distributing, test the built assets, pause/resume, restarts and completion using the actual mod dependency set. Use the mod versions required by the map. See [troubleshooting](../reference/troubleshooting.md) when an editor preview and game output disagree.

## Side links and runtime dependencies

Side links require MME and JK Runtime in the game. Extension is optional. A map using another mod's block factory still requires that mod; drawing an MME slope alias doesn't replace its behavior. See [screen links](../maps/teleports.md) and [custom blocks](../maps/collision.md#custom-block-contract).
