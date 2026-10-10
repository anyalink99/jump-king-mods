# What Mapping can add to a map

Start with the [first scene](authoring.md#first-scene) before combining effects.
Mapping reads map-local XML and assets. It supplies authoring and presentation;
Runtime owns shared behavior-tree execution, world state and native contacts.

## Scene features

- [Screen teleports](teleports.md): connect screen edges, set return routes
  and use destinations beyond the RGB marker limit.
- [Reusable scene behavior trees](behavior-trees.md): sequences, branching,
  parallel joins, event/flag waits, scoped effects and snapshot-safe execution.
  Use the same events for native contacts, NPC dialogue and custom ending bridges.
- [Parameterized compound objects](objects.md), expanded to ordinary components
  before play; shared art, isolated instance state and no object-specific engine.
- [Native narrative](narrative.md): strings/translations, world and HUD text,
  Old Man/Merchant bindings, conditional quotes, intro and extra statistics pages.
- Typed map counters and [Runtime map policies](map-policies.md), without changing
  native statistics, shop behavior or the player's saved mod settings.
- [Generated field/default catalog and XSD](schema.md), shared with the actual
  serializer/property registry, and independently compiled documentation examples.

- Region-triggered effects, owned property overrides, preloaded asset variants,
  player/prop attachments and carried lights with gameplay-clock expiry.
- Event rules, conditional run/save flags, snapshot restoration and a native
  inspector with numeric editing, event simulation and effect recipe export.
- Original Hidden Walls contact events, native grounded/support triggers and
  native event sound cues; scenes can be authored by hand.
- [Custom ending trees](endings.md) in `ending/custom_*.xml`, parsed by MME and
  executed by the game's native nodes for all three endings and their actors.
  MoreEndingOptions can remain installed: MME owns its prepared map roles and
  leaves other roles/maps to the existing provider.
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
480×360 frame, including menus. These layered 2.5D effects use explicit
geometry: light blockers use the King hitbox and opt-in rectangular Anchors;
composite reflections can select Prop/Node IDs and the King; rain clipping uses
solid Anchors. Geometry and reflection membership change through authored files/reload.

## Assets and screen counts

The scene compiler compiles vector assets into a hash-verified `MMGFX3` cache. Without
a cache, the runtime compiles map-local XML on load.
An existing invalid or outdated cache fails with a rebuild instruction. It is
never silently replaced by runtime source compilation. Rebuild it with the
current scene compiler, or deliberately omit it when authoring from source.

The optional map.xml records authored screen count and full integer side links
above the stock 255-target limit. expectedScreens validates geometry; it does not
create screens. See [screen teleports](teleports.md) to set up links, and
[large maps](large-maps.md) for atlas validation and resource budgets.

## Shared worlds and reload

Multiplayer Expansion 0.8.0 and Replays 2.7.0 can share declared tree cursors,
regions, effects, flags and native side links through Runtime 2.0. Local maps
keep their normal behavior. Reload is refused while a network or replay session
owns that world; stop the session before changing its state layout.
See [behavior trees](behavior-trees.md), [scene API](scene-api.md) and
[troubleshooting](troubleshooting.md) for the relevant workflows.
