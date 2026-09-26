# Mega Mapping Expansion

[Technical documentation](docs/index.md).

Mega Mapping Expansion adds scenery, animation, lighting, narrative and map
policies through `props/mega-mapping-expansion/scene.xml`. Version **0.6.2**
requires [JK Runtime 1.30 or newer 1.x](../jk-runtime/README.md) when built from
this checkout. Movement mechanics such as Warp Jump belong to
[Mega Gameplay Expansion](../mega-gameplay-expansion/README.md).

Start with the [authoring handbook](docs/authoring.md), or use the
[recipes](docs/recipes.md) for complete examples.

## Enable or disable

Use the standard **Mega Mapping Expansion** checkbox in the main or pause menu.
It is enabled by default and saved in `MegaMappingExpansion.Settings.xml` beside
the mod. Unchecking immediately suspends scenery, lighting, weather, scene clocks,
event rules, intro/timer overrides and final tint/mirror effects. Rechecking
resumes the same scene, preserving flags, effects and resources.

The inspector, public scene API, snapshots, persistent data and map side links
remain available. Disabling visual effects does not change the map's routes.
Maps without `scene.xml` install only the save/reset adapter, avoiding repeated
render-hook installation during ordinary restarts. Maps with a scene retain
their full adapter and live checkbox behavior.
**Smooth Camera 0.6+** composes Mapping effects on every visible screen through
Runtime. Lighting/reflections precede camera assembly; final tint and mirroring
apply once to the world and stationary UI. Both checkboxes can remain enabled.

## Features

- [Reusable scene behavior trees](docs/behavior-trees.md): sequences, branching,
  parallel joins, event/flag waits, scoped effects and snapshot-safe execution.
  Use the same events for native contacts, NPC dialogue and custom ending bridges.
- [Parameterized compound objects](docs/objects.md), expanded to ordinary components
  before play; shared art, isolated instance state and no object-specific engine.
- [Native narrative](docs/narrative.md): strings/translations, world and HUD text,
  Old Man/Merchant bindings, conditional quotes, intro and extra statistics pages.
- Typed map counters and [Runtime map policies](docs/map-policies.md), without changing
  native statistics, shop behavior or the player's saved mod settings.
- [Generated field/default catalog and XSD](docs/schema.md), shared with the actual
  serializer/property registry, and independently compiled documentation examples.

- Region-triggered effects, owned property overrides, preloaded asset variants,
  player/prop attachments and carried lights with gameplay-clock expiry.
- Event rules, conditional run/save flags, snapshot restoration and a native
  inspector with numeric editing, event simulation and effect recipe export.
- Original Hidden Walls contact events, native grounded/support triggers and
  native event sound cues; scenes can be authored by hand.
- [Custom ending trees](docs/endings.md) in `ending/custom_*.xml`, parsed by MME and
  executed by the game's native nodes for all three endings and their actors.
  MoreEndingOptions can remain installed: MME owns its prepared map roles and
  leaves other roles/maps to the existing provider.
  This is authoring infrastructure, not a collection of finished cutscenes.
- Public `mega.mapping.scene:1:0` capability in the separate MegaMappingApi.dll.

- Vector paths, curves, gradients and reusable include/exclude mattes.
- PNG and paged sprite-sheet props alongside vector assets.
- Three native drawing phases, per-node Z order and a post-world compositor.
- Rotation, orbit, linear, bob, sway and path motion; loop, ping-pong and
  one-shot playback; level-start, screen-enter and continuous timelines.
- Property keyframes, parallax, spring reactions, wing flutter and bending bushes.
- Point and sweeping cone lights, slow pulses, visible beam scattering,
  player/prop rim lighting and player-hitbox occlusion.
- Polygon receivers for equipment-aware player shadows projected across 2.5D roofs.
- Pinned cloth meshes for foreground laundry, independent from rooted plant wind.
- Perspective water reflections of the composed world, including the King,
  with foreground occlusion and no reflected UI.
- Depth-aware particle emitters and layered moving fog.
- Depth-layered rain with optional solid-anchor clipping; concave rooftop puddles
  reflecting the pre-UI world with compressed height, rain rings and foot splashes.
- Drifting snow fields and procedural ocean meshes with overturning crests,
  material-space foam and ballistic spray.
- Custom intro text, timer visibility, whole-frame tint and mirroring.

Hiding the timer does not stop time or saving. Mirroring affects the final
480×360 frame, including menus. These are layered 2.5D effects, not a full 3D
engine: light blockers use the King hitbox and opt-in rectangular Anchors;
composite reflections can select Prop/Node IDs and the King; rain clipping uses
solid Anchors. Geometry and reflection membership change through authored files/reload.

## Documentation

- [Behavior trees](docs/behavior-trees.md): complete node/lifecycle reference,
  reusable subtrees, NPC reactions, ending handshakes and debugging.
- [Native workflows](docs/native-workflows.md): existing map folders, Hidden Kingdom
  contacts, actual platform contact, scene presets, sounds and strict errors.
- [Custom endings](docs/endings.md): exact filenames, native nodes, resource keys,
  controller/actor roles, validation and testing.
- [Behavior cookbook](docs/behaviors.md): 30-second carried lantern, temporary room
  palette, saved switch, overlap priorities, clocks and reset semantics.
- [External scene API](docs/scene-api.md): declared capability, ownership, metadata
  and a separately compiled consumer.

- [Authoring handbook](docs/authoring.md): self-contained first scene, coordinates,
  layering, PNG support, animation, reflection roles and packaging.
- [Parameter reference](docs/parameter-reference.md): current fields, actual omitted
  defaults, units, validation limits and feature-specific restrictions.
- [Small recipes](docs/recipes.md): fixed lamp, rooted tree/cloud layers, moth,
  watching eyes and a rainy rooftop. All XML examples use inline vector assets.
- [Preview and troubleshooting](docs/troubleshooting.md): native inspection,
  reload/capture controls, common failures and performance diagnosis.
- [Architecture](docs/architecture.md): implementation ownership and render passes.

## Assets and screen counts

The scene compiler compiles vector assets into a hash-verified `MMGFX3` cache. Without
a cache, the runtime compiles map-local XML on load.
An existing invalid or outdated cache fails with a rebuild instruction. It is
never silently replaced by runtime source compilation. Rebuild it with the
current scene compiler, or deliberately omit it when authoring from source.

The optional map.xml records authored screen count and full integer side links
above the stock 255-target limit. expectedScreens validates geometry; it does not
create screens. Runtime uses native TeleportLink arrays and restores them on unload.
See [large maps](docs/large-maps.md) for the topology contract and resource budgets.

## Examples

`examples/minimal/` demonstrates shared defaults and a breathing glass material
without external art dependencies. It is a scene fragment for an existing
playable map, not a complete native level. The exported minimal kit includes
the complete handbook, reference, recipes and troubleshooting guide.

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

After building, run `.\mods\mega-mapping-expansion\tools\check-docs.ps1` to
check handbook links and compile its complete XML scenes. This validates examples,
not their appearance or a map's route.
