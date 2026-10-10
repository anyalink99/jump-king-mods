# Mega Mapping Expansion

Add animated scenery, lighting, weather, text and scene behavior to custom maps.
Describe a scene in `props/mega-mapping-expansion/scene.xml` and ship it with the
map. Movement mechanics such as Warp Jump belong to
[Mega Gameplay Expansion](../mega-gameplay-expansion/README.md).

## Install and play

Version **0.8.0** needs [JK Runtime 2.0+](../jk-runtime/README.md) and the shared
Harmony included in the package. Install the mod and Runtime, then load a map
that uses Mapping. For a manual install, close the game and copy the release
package into `Jump King/Content/JKMods/`. Keep one active copy of each dependency.

## Enable or disable

Use the **Mega Mapping Expansion** checkbox in the main or pause menu. It starts
enabled and is saved in `MegaMappingExpansion.Settings.xml` beside the mod.
Turning it off immediately pauses scenery, lighting, weather, scene clocks,
event rules, intro and timer overrides, tint and mirror effects. Turning it back
on resumes the same scene with its flags, effects and resources intact.
Map entry does not change this setting. Common Runtime activation tags, screen
markers and XML zones do not control Mapping presentation.

The inspector, public scene API, snapshots, saved data and map side links remain
available while visuals are off. Routes do not change. A map without `scene.xml`
loads only the save and reset adapter; a map with a scene keeps the full adapter
so the checkbox can take effect immediately.
**Smooth Camera 0.6+** composes Mapping effects on every visible screen through
Runtime. Lighting/reflections precede camera assembly; final tint and mirroring
apply once to the world and stationary UI. Both checkboxes can remain enabled.

## Make your first scene

Start with the handbook's [complete first scene](docs/authoring.md#first-scene).
It draws a gold marker in an existing playable map using inline art, so you
don't need external textures. Check that it appears before adding animation.
Then use the [recipes](docs/recipes.md) for lamps, trees, clouds and rain, and
the [edit/reload workflow](docs/troubleshooting.md) to inspect the result.

For Worldsmith, read [editor authoring](docs/worldsmith.md). Its portable
extension handles large-map compilation and publishing; the guide also explains
the stock limits that remain.

## Features

- Connect screen edges with [side links](docs/teleports.md), including targets
  beyond the RGB marker limit.
- Build scene logic with [behavior trees](docs/behavior-trees.md), regions,
  flags and scoped effects, or reuse [compound objects](docs/objects.md).
- Add [narrative](docs/narrative.md), native actor reactions and
  [custom endings](docs/endings.md).
- Compose vector/PNG props, lighting, shadows, water, particles and weather.
  The [feature overview](docs/features.md) lists the available effects and limits.

Hiding the timer doesn't stop time or saving. Mirroring includes menus. Native
side links remain active when visuals are disabled, so the route stays intact.
Runtime 2.0 shares declared scene state with Multiplayer and Replays; unknown
foreign state isn't automatically included. Reload is blocked while a world
session owns that state. Read [shared-world behavior](docs/features.md#shared-worlds-and-reload).

## Assets and screen counts

The compiler can prepare a hash-verified `MMGFX3` cache. An invalid cache must be
rebuilt or deliberately omitted; it doesn't silently fall back to source XML.
`map.xml` can declare screen counts and side links, but doesn't create geometry.
Read [asset and screen limits](docs/features.md#assets-and-screen-counts),
[teleports](docs/teleports.md) and [large maps](docs/large-maps.md).

## Examples

`examples/minimal/` is a scene for an existing playable map, not a complete native
level. The exported authoring kit includes the handbook, reference and recipes.
Its [guide index](docs/index.md) starts with authoring tasks and separates the
mod integration and maintenance references.

## Build

From the repository root:

```powershell
.\mods\mega-mapping-expansion\build.ps1
```

Outputs:

- `build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingExpansion.dll`
- `build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/0Harmony.dll`
- `build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingApi.dll`
- `build/mega-mapping-expansion/AUTHORING_KIT/minimal/`
- `build/mega-mapping-expansion/_INTERNAL/SceneCacheCompiler.exe`

The package uses the runtime SDK. Harmony is bundled for native drawing and
text hooks; JK Runtime owns lifecycle and teardown. The build tests native
signatures, XML security, parsing, blending, cache validation and animation
math. It does not install the mod or publish to Steam.

After building, run `.\mods\mega-mapping-expansion\check-docs.ps1` to
check handbook links and compile its complete XML scenes. This validates examples,
not their appearance or a map's route.
