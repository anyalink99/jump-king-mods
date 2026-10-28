# Mega Mapping Expansion

Add moving scenery, reactions to the King, sound, dialogue changes and scripted scenes to a Jump King map. MME also supports finer collision, larger maps and custom screen connections.

**Making your first MME map?** Follow [installation and setup](docs/getting-started/README.md), then [put an image on screen](docs/getting-started/first-scene.md). The [handbook](docs/README.md) lists tasks and complete examples.

## Install

Subscribe to **Mega Mapping Expansion** and **JK Runtime** in Jump King's Steam Workshop. Wait for Steam to finish downloading, then start the game with both mods enabled. MME 1.5.0 requires **JK Runtime 2.4.0 or later**.

Smooth Camera 0.13.0 or later is optional: it adds smooth travel to camera shots. Instant cuts work without it. A map using another mod's blocks still needs that mod.

## Make a map

Keep making the ordinary Jump King map in Worldsmith or another map tool. Add `mapping_settings.xml` beside its `level_settings.xml`. MME reads that XML and the images or sounds it references when the map loads. There is no separate scene compilation step to run.

- [First image](docs/getting-started/first-scene.md): exact files, position and how to check a change.
- [Seven small lessons](docs/getting-started/lessons.md): placement, animation, area captions, jump counters, sound and saved totals.
- [Task catalogue](docs/getting-started/examples.md): ready examples and the files they need.
- [Moving platforms](docs/maps/moving-platforms.md): paint decks or configure routes, groups and graphics in XML.
- [Collision and screen connections](docs/maps/README.md): these can be used without decorative scenes.
- [All features](docs/reference/features.md): what MME supports and where to learn it.

Worldsmith Extension is an optional editor. [Loading and testing](docs/getting-started/worldsmith.md) covers it, ordinary Worldsmith and direct edits to a playable test folder. Keep static scenery in normal map layers; use MME for things that move or react.

## Inspect a problem

Open **Mapping inspector** in the game's mod settings menu while a map is loaded. [The inspector guide](docs/reference/inspector.md) shows the pages and controls. Start with [troubleshooting](docs/reference/troubleshooting.md) when a scene doesn't appear.

The MME checkbox pauses scene logic and presentation. Map collision, physics reference frames and native screen links remain active. [Pause and clocks](docs/scenes/clocks.md) explains the behavior during pause, completion and endings.

## For mod developers and maintainers

[Public API](docs/integration/scene-api.md) | [Architecture](docs/reference/architecture.md) | [Build and documentation checks](docs/reference/maintaining.md) | [Changelog](CHANGELOG.md)

Run `mods/mega-mapping-expansion/build.ps1` or `scripts/check-mods.ps1 -Mod mega-mapping-expansion`. Outputs are under `build/mega-mapping-expansion`: the mod in `UPLOAD_TO_WORKSHOP`, the portable handbook and lessons in `AUTHORING_KIT/minimal`, and developer tools in `_INTERNAL`.

## Credits

The `level+` workflow originated in Zebra's More Block Sizes. MME uses its own explicit version 2 layout. See [third-party notices](THIRD_PARTY_NOTICES.md) for credits and licenses.
