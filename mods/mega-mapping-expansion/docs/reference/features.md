# Capabilities and limits

[Handbook](../index.md) / [Reference and diagnostics](index.md)

This is the capability map for the current constructor. Start with [the handbook](../index.md) for a learning order, or use the links below to find a complete example.

| Family | Capabilities | Guide |
|---|---|---|
| Drawing | Sprites/atlases, materials/UVs, mesh projection, pixel policy, vector preparation | [Rendering](../scenes/objects.md), [animation](../scenes/texture-animation.md), [vectors](../scenes/vectors.md) |
| Composition | Alpha masks, capture-only groups, native/actor captures, reflection/shadow receivers, grading | [Rendering](../scenes/objects.md) |
| Motion | Typed graphs, smoothing/springs/integration, fields, pinned chained deformation, particles | [Graphs](../scenes/behaviors.md), [rendering](../scenes/objects.md) |
| Observations | Player actions/motion/charge, contacts, NPC lines, inventory, hidden walls, text and lifecycle | [Observations](../observations/index.md), [contacts](../scenes/contact-feedback.md) |
| Native adapters | NPC voice/pause, owned WAV/XNB audio and fades, scripted camera views, native win conditions | [Native game features](../game/index.md) |
| Logic | Typed variables, event rules, cooldown/once gates, owned effects, control trees | [Graphs](../scenes/behaviors.md), [trees](../scenes/behavior-trees.md) |
| Narrative | Native/bitmap text, changing captions, native cutscene bridges | [Text](../scenes/narrative.md), [endings](../integration/endings.md) |
| Geometry | Explicit layouts, four collision resolutions, mod factory contracts and slope aliases | [Collision](../maps/collision.md), [large maps](../maps/large-maps.md) |
| Navigation | Fixed integer side links, weighted pools, reroll triggers/schemes and state | [Screen links](../maps/teleports.md) |
| Reuse | Includes, typed/nested recipes and exported ports | [Files](../getting-started/files.md), [recipes](../scenes/recipes.md) |
| State | Save/Attempt lifetimes, snapshots, authoritative shared state, restore generations | [State](../scenes/state.md) |
| Tools | Automatic XML validation/local cache, generated registry/XSD, inspector preview/export/reload | [Validation](schema.md), [troubleshooting](troubleshooting.md) |
| Mod integration | Detached API values/signals/events, effect leases and discovery contracts | [Public API](../integration/scene-api.md) |

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
| Native ending tree | 4096 nodes / 64 levels |

Route pools/triggers have their own [limits](../maps/teleports.md#limits-and-validation). Component numeric bounds and per-field choices are in the [registry](../../reference/scene-parameters.xml). Excesses fail validation/preparation rather than silently allocating unbounded state.

## Practical cost

Textures keep prepared CPU/GPU data. A nominal 4096-square RGBA texture costs roughly 128 MiB under the combined eight-bytes-per-pixel accounting before other resources. Captures allocate targets, dense meshes need vertices, and particles need prepared capacity even when the current rate is low. Use shared artwork/materials, bounded pools and only the tessellation needed for visible deformation.

Pure graph branches skip unchanged inputs; stateful nodes still step on their clock. Rendering batches compatible quads and reuses unchanged mesh vertices. The world asset pool reuses unchanged content across attempts with fingerprint checks and evicts inactive entries within its budget. These optimizations don't make arbitrary scene complexity free: measure representative active scenes and preparation substages.

A mask clips drawing, not collision. A capture draws state, not another simulation. A region overlap isn't a landing. A native ending actor isn't the gameplay player. Keeping those boundaries explicit prevents accidental gameplay changes when improving presentation.
