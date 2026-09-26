# Mega Mapping architecture

[Authoring handbook](authoring.md) · [Parameter reference](parameter-reference.md)

The runtime is a C# 5 / .NET Framework module hosted by JK Runtime, with a
separate MegaMappingApi.dll contract for external consumers.
Gameplay collision and native water belong to Jump King, not this renderer.

`src/Authoring` contains the schema, expansion, validation and vector/cache
compiler. `src/Runtime` owns prepared definitions and scene/resource lifetime.
`src/Simulation` contains reusable animation, wind, water, gaze, puddle geometry
and visibility math.
`src/Rendering` owns render plans and passes. `src/Native` is the installed-game
adapter and hook boundary; private native members never leak into render math.
`src/Diagnostics` owns profiling and the opt-in debug inspector.

A postfix on the owning EntityManager.Update drives simulation after all entity
LateUpdates; the lifetime entity owns teardown only. Hot reload replaces
only the owned scene, not the game's entity list during drawing. A failed candidate
is disposed before activation; a successful replacement disposes the previous
resources. Run-scoped generation tokens keep old teardown callbacks from stopping
a newer run. Scene instance counts are logged on disposal.

JumpGame.Update owns presentation-only advancement during pause because native pause skips the
entity manager entirely. It refreshes scene presentation/inspector values and
presentation-clock effects, while gameplay clocks and physical regions remain
held. A per-frame guard prevents a second tick if the owning manager already ran.

## Ownership and phases

Scene behavior trees compile typed authoring nodes into bounded immutable programs
before activation. Subtree call sites get independent state; event indexes include
starts, stops and waits. The existing SceneBehaviorEngine owns scheduling, flags,
effect leases and snapshots. Execution does not own a second render loop or NPC
dialogue engine. Native ending bridge leaves use the same scene event/flag service
while native composite/actor nodes stay in Jump King's BT manager.

Authored effects prepare copied operands/light prototypes once. Repeated Activate
uses those prepared recipes; external Apply validates/copies dynamic input. Scene
hosts own their native gameplay subscription, so hot reload can change event
interests without retaining an old module-level filter. A paused tick advances
presentation effects only, not queued gameplay actions or tree control flow.

Authoring XML is expanded and validated before a scene is prepared. Includes,
compound objects, templates, materials and layer groups are authoring conveniences, not mutable
runtime registries. Prepared paths, tracks, colours and ordered drawing queues
belong to one scene instance; animation and water state belong to its lifetime.

`SceneContract` exports serializer metadata and the same `SceneProperties` bounds
used by effects and the inspector; it is not a second hand-written parameter list.
`SceneTextService` resolves explicit cultures and caches measured native-font
layouts. Native narrative patches are conditional on authored actors/intro/results.
Actor wrappers restore native sprite/position after drawing; dialogue stays in the
native BT. Result pages snapshot resolved text before scene disposal and do not
hold GPU resources from the ended scene. The original stats/bookkeeping remain.

World policy belongs to JK Runtime, not a renderer toggle. It validates required
modules and cooperative restrictions before preparation. It cannot unload an
arbitrary managed DLL or reverse foreign patches and never claims otherwise.

Native hooks call background, world and foreground queues. The compositor captures
the world before native UI, applies perspective reflections, restores foreground
occluders and applies lighting. Native menus remain outside the reflection capture.
Opacity means coverage; brightness changes RGB; brightness above one uses additive
emission preserving destination alpha. Reflection occlusion is an explicit role.

Rain belongs to the per-screen drawing queues. Its optional clipping table is
prepared from solid Anchor tops, not queried from native collision each frame.
Puddles belong to the compositor after rectangular Water reflections; their
polygon scanlines are prepared once, while bounded contact-ring state belongs to
the scene. Both sample the same immutable pre-UI source, with no per-object
reflection-source mask. Ambient rain rings do not consume rain impact events.

Surf is an independent procedural cross-section, not the interactive lake spring
solver. Its Lagrangian mesh, material-space foam and historical crest spray are
layer commands. Scatter cones use another shared-vertex colored mesh. Both reuse
one owned BasicEffect and bounded per-object buffers; teardown releases them.
Surf evaluates its periodic profile once per column and applies depth decay to
the cached displacement. Foam and spray share a bounded indexed-quad batch rather
than issuing thousands of individual rotated sprites. This preserves coverage
and particle counts while reducing repeated curve evaluation and submission.
Directional light fields cache source-to-receiver directions and static King
visibility separately, so a rotating beam does not invalidate radial geometry.

Ground-shadow receivers execute immediately before native PlayerEntity.Draw.
The installed game can return a LayeredSprite with no outer texture. The native
adapter therefore exposes its public child sprites; an owned appearance cache
flattens visible native parts with their real source rectangles and pivots. Rim,
direct reflection and ground projection consume that same appearance. Equipment
changes invalidate the appearance and its silhouette; unchanged frames reuse it.
This is separate from the final-world reflection capture and creates no new
native player entity or gameplay transform.

PNG Props and vector Nodes share instance data and most drawing code. This is not
complete capability parity: both kinds support proximity reactions, but
emitters resolve vector assets and selective legacy reflections replay vectors.
Document those boundaries rather than assuming a common DTO means every field
has an effect on every object kind. See the parameter reference for retained
fields and actual omitted defaults.

## Sources and build

The mod includes independent scene examples for visuals, narrative and behaviors.
Its build compiles these into the authoring kit. Map art and native geometry
belong to the map's own build pipeline.

MMGFX3 fingerprints expanded XML, compiler version (including an embedded hash of
the actual authoring/compiler sources) and unique normalized relative
vector paths plus contents. Present stale/corrupt caches fail explicitly; absence
selects source preparation. The compiler always rebuilds from source. Cache
replacement is atomic. Installation is separate from build and records hashes.

## Checks

Default mod checks cover math, authoring, cache and native contracts. `-SceneSmoke`
adds the existing Night Garden checks against a built map. Appearance and performance
are checked in the native renderer; GDI tools are offline asset proofs only.
`tools/check-docs.ps1` also compiles handbook XML examples and checks local links.
It does not freeze source spelling, visual snapshots or incidental file counts.
