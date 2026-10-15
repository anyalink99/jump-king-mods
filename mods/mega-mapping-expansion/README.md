# Mega Mapping Expansion

Build reactive Jump King scenery with reusable components, typed signals and graph bindings. Animate and deform textures, project reflections and shadows, observe player actions and native map interactions, compose effects and control timed sequences.

The 1.0 construction rewrite removes the previous scene engine and formats. Planets, gaze, weather and instruments are now XML recipes made from common parts. Convert older source before loading it with this version; there is no compatibility layer.

## Install

Requires JK Runtime 2.2.0+ and the shared Harmony included in the package. Close Jump King before replacing DLLs. Install one active copy of MME and Runtime in the existing Workshop directory or Content/JKMods. Preserve settings when updating.

The menu checkbox controls scene presentation and logic. Collision/native side links remain active. Gameplay clocks stop on pause/completion. Presentation can continue after completion, but freezes during pause unless the selected view explicitly enables animateWhilePaused. Mapping inspector shows components, effective values/owners, observations, effects, trees and diagnostics. Preview/reload require -debug and a local world not controlled by another session.

## Author

Start with the [working example](examples/minimal/mapping_settings.xml) and [authoring guide](docs/getting-started/first-scene.md). Keep static scenery in the map's background and foreground layers; use MME for the parts that move or react. Put settings in mapping_settings.xml and assets at safe relative paths. Edit the XML and load the map; MME manages its disposable cache outside the map folder. No separate scene compiler is required. Read the [handbook](docs/index.md), [recipes](docs/scenes/recipes.md) and [field reference](reference/PARAMETERS_GENERATED.md).

MME also provides [pixel collision/mod slope aliases](docs/maps/collision.md), [side links](docs/maps/teleports.md), [native ending bridges](docs/integration/endings.md) and a [typed API](docs/integration/scene-api.md). Custom mod blocks keep their original behavior/discovery APIs. Worldsmith Extension is an optional authoring tool, not a runtime dependency.

Native NPCs, inventory, hidden walls, audio and victory conditions connect through [native adapters](docs/game/index.md). Start with the [native map guide](docs/game/native-map.md) to find the settings the game already provides. Use [observations](docs/observations/index.md) for movement, actions, contacts and lifecycle events; [camera shots](docs/game/camera.md) can show a change elsewhere without moving the King. Smooth travel requires Smooth Camera 0.13.0+; a screen cut also works without it.

## Build

Run mods/mega-mapping-expansion/build.ps1 or scripts/check-mods.ps1 -Mod mega-mapping-expansion. Outputs under build/mega-mapping-expansion include UPLOAD_TO_WORKSHOP, AUTHORING_KIT/minimal and _INTERNAL/SceneCacheCompiler.exe. Run verify-construction-graphics.ps1 for isolated GPU fixtures, check-docs.ps1 for handbook links/examples and verify-scene-api.ps1 for package discovery.

Compilation checks formats, types and assets. Native preparation checks game resources/contracts. These checks don't prove route completion. Read [architecture](docs/reference/architecture.md) for preparation, ownership, composition and state, and [CHANGELOG](CHANGELOG.md) for release history.

## Credits

The level+ workflow originated in Zebra's More Block Sizes. MME's pixel reader uses its own explicit version 2 layout. See [third-party notices](THIRD_PARTY_NOTICES.md) for original credits and licenses.
