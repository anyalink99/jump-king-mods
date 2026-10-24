# All features and their practical limits

[Handbook](../README.md) / [Reference](README.md)

This checklist covers the MME authoring families. Follow the linked task guide for steps and examples; use [component fields](components.md), [XML](xml.md) and [events](events.md) for exact controls. [Coverage and verification](coverage.md) records how the handbook is checked.

| Feature | What you can make | Guide |
|---|---|---|
| Moving platforms | Colored or XML collision, waypoint routes, waits, groups, one-way decks and scene commands | [Moving platforms](../maps/moving-platforms.md) |
| PNG/XNB images and transforms | Placement, screen filtering, parents, rotation, scale, pixel policy, layers | [Placement](../scenes/placement.md) |
| Atlases | Automatic loop/once/pingpong, manual states, slide/crossfade transitions | [Animation](../scenes/texture-animation.md) |
| Materials and lighting | Color, opacity, blending, UV scroll/tile, alpha channels, projection shading, lights and occluders | [Materials](../scenes/materials.md) |
| Captures and masks | Alpha cutouts, local/screen masks, tagged groups, native layers and actor capture | [Masks](../scenes/masks.md), [selections](selections.md) |
| Receivers | Reflections, projected shadows and invisible light-occlusion contours | [Surfaces](../scenes/surfaces.md) |
| Mesh/field motion | Grid/polygon/disc, planar/sphere/cylinder/perspective projection, sine/noise/curve/spring fields, pinned/chained deformation | [Deformation](../scenes/deformation.md) |
| Particles and stamps | Continuous rain/motes, event bursts and temporary contact marks | [Particles](../scenes/particles.md), [contacts](../scenes/contact-feedback.md) |
| Screen presentation | Ambient/tint/exposure, visual mirror, timer visibility, simple intro and explicit pause policy | [Views](../scenes/views.md) |
| Text | Native/bitmap fonts, wrapping/alignment, reveal, changing/localized templates | [Text](../scenes/narrative.md), [languages](../scenes/localized-text.md) |
| Observations | Player pose, actions, motion, charge/quality, area/surface contacts, native interaction and lifecycle | [Observations](../observations/README.md), [events](events.md) |
| Logic | Typed graphs, bindings, variables, rules, effects, waiting/branching/repeating trees | [Reactions](../scenes/behaviors.md), [graphs](graphs.md), [trees](../scenes/behavior-trees.md) |
| Reuse | Includes, typed/nested recipes, component/property/node/signal exports, seven embedded recipes | [Recipes](../scenes/recipes.md), [arguments](recipes.md) |
| Sound | Native event keys, owned WAV/XNB voices, buses, area loops, fades and one-shot actions | [Audio](../game/audio.md) |
| NPCs | Native settings, per-NPC voice/mute/pause, line events, conditional quote and visual controls | [Dialogue](../game/dialogue.md) |
| Items and hidden walls | Count/owned/equipped/pickup signals; exact native wall identity and contact | [Interactions](../game/interactions.md) |
| Camera | Native cuts, optional smooth travel/return, priorities and fallback | [Camera](../game/camera.md) |
| Intro, victory and results | Opening pages, custom native win gates/areas, role-specific final statistics | [Results](../game/results.md), [victory](../game/interactions.md#victory-conditions) |
| Ending choreography | Native controller/actor replacement files, typed scene bridges, actor observations/captures | [Endings](../integration/endings.md) |
| Collision and layouts | Four pixel scales, native/atlas/strip, mixed resolution, larger counts and mod slope aliases | [Collision](../maps/collision.md), [large maps](../maps/large-maps.md) |
| Navigation | Integer side links, random/weighted/shuffle/sequence pools, held choices, rerolls and route schemes | [Links](../maps/teleports.md) |
| Imported room timing | Per-screen native physics reference frames | [Physics frames](../maps/physics-frames.md) |
| State and compatibility | Attempt/Save, snapshots, authority/followers, map requirements/conflicts | [Saves](../scenes/state.md), [policy](../maps/map-policies.md) |
| Tools and C# integration | Automatic validation/cache, inspector preview/export/reload, discovery/API/effect leases | [Inspector](inspector.md), [API](../integration/scene-api.md) |

## Budgets

These are ceilings, not recommended scene sizes. Several can apply to one scene at once.

| Resource | Limit |
|---|---|
| Actual screens | 4096 |
| Components / graph nodes / variables | 16384 / 4096 / 256 |
| Effect definitions / rules | 512 each |
| Active effects | 256 total; 32 per owner/definition |
| Trees | 128; depth 32; 1024 nodes per expanded tree; 8192 expanded scene nodes |
| Recipe expansion | 1024 instances; nesting 16 |
| Audio / NPC / item / hidden-wall / camera adapters | 64 of each kind per scene; camera requests also share Runtime's global 64-lease budget |
| Audio files / prepared encoded audio | 64 MiB per file / 256 MiB per world pool; decoded voices consume additional device memory |
| Sensors | 256; up to 256 contact memberships per sensor |
| Spatial spring cells / deformation chain | 4096 cells scene total / 16 deformation steps |
| Particle slots | 131072 scene total; individual emitter capacity up to 16384 |
| Stamp capacity | Up to 256 per stamp component |
| Texture dimensions | Up to 16384 per axis; vector assets up to 2048 |
| Prepared texture and CPU pixel copies | 512 MiB combined |
| World asset cache | 1 GiB ceiling |
| Expanded cache payload | 16 MiB |
| Shared scene state | 4 MiB raw / 24 KiB compressed |
| Slope aliases | 1024 |
| Intro / result pages | 16 intro pages / 8 result pages, 8 rows each, 64 rows total |
| Native ending tree | 4096 nodes / 64 levels |

Route pools/triggers have their own [limits](../reference/transitions.md#limits-and-validation). Component numeric bounds and per-field choices are in the [registry](../../reference/scene-parameters.xml). Excesses fail validation/preparation rather than silently allocating unbounded state.

## Practical cost

Textures keep prepared CPU/GPU data. A nominal 4096-square RGBA texture costs roughly 128 MiB under the combined eight-bytes-per-pixel accounting before other resources. Captures allocate targets, dense meshes need vertices, and particles need prepared capacity even when the current rate is low. Use shared artwork/materials, bounded pools and only the tessellation needed for visible deformation.

Pure graph branches skip unchanged inputs; stateful nodes still step on their clock. Rendering batches compatible quads and reuses unchanged mesh vertices. The world asset pool reuses unchanged content across attempts with fingerprint checks and evicts inactive entries within its budget. These optimizations don't make arbitrary scene complexity free: measure representative active scenes and preparation substages.

A mask clips drawing, not collision. A capture draws state, not another simulation. A region overlap isn't a landing. A native ending actor isn't the gameplay player. Keeping those boundaries explicit prevents accidental gameplay changes when improving presentation.
