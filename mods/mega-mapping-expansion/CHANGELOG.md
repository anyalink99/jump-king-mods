# Mega Mapping Expansion changelog

## Unreleased

- Base map guides on Zebra's numbered 60x45-cell grid. Preserve existing screen counts, separate level+ conversion from finer collision, and explain spare cells, grid expansion and old slope scale.

- Simplify map guides into small tasks with a downloadable collision example. Move format and provider details to the reference, and use README landing pages for GitHub browsing.

- Rework the author handbook around installation, seven small lessons and map-author tasks. Add separate lesson files/assets, renderer proof images, an example-combination guide and complete field, recipe, graph, action and event explanations.
- Correct setup requirements and sensor-event spelling. Package all lessons and handbook media in the authoring kit, validate supplied scenes/routes, and add isolated lesson rendering and behavior checks.

## 1.3.0 - 2026-10-16

- Add opt-in `PhysicsFrame` entries for imported rooms. Retain the reference Y fraction through native movement, hitbox conversion and collision snapping, without changing tick cadence or block callback order.
- Carry reference-frame state in Runtime practice and shared-world snapshots. Keep unconfigured screens and foreign position writes under native control.
- Retain pre-resolution contacts when a nonblocking block teleports the player after collision resolution. Read source-screen geometry independently of the destination camera and dispatch surface sensors against the contact's screen.
- Require JK Runtime 2.2.1 for the actual pre-stage hitbox observation. Document coordinate imports and their compatibility limits.

## 1.2.0 - 2026-10-15

- Restore multi-page native intros and additional final statistics pages as construction components. Final rows retain composed values after attempt teardown and can select an ending role.
- Add conditional NPC quote bindings and scoped sprite/bubble presentation controls, five native font presets, exact language observations and reusable typed text templates.
- Recognize automatic full-charge jumps as exact native-limit launches. Clamp in-progress charge displays to the native limit and retain surface multipliers across snapshots.
- Document intro/results authoring, conditional dialogue, localization, templates and font choices with complete examples.

- Prime scene drawing on an owned offscreen target during attempt preparation, before player handoff. The pass doesn't advance clocks, emit events or draw native actors. Startup traces report `mega-mapping.graphics-prime` separately.
- Skip zero-intensity lights, read intensity once per light instead of once per vertex, and test whether a light sample is enclosed once rather than repeating it for every ray.

## 1.1.1 - 2026-10-15

- Retain the source contact of native block relocations for surface effects, even when a nonblocking teleport acts before the next observation pass.
- Clip inherited `IBlock.Intersects` implementations through their declaring type, preserving derived mod blocks such as Anywhere Teleport entrances.

- Manage scene caches automatically in local application data, outside map packages. XML edits and MME updates refresh the cache; damaged or inaccessible caches fall back to current source. Ignore old map-local caches and ship editable XML examples without a compilation step.

- Pause native event sounds used by Hidden Walls and scene sound actions with the scene. Resume continues the same voice; native hitboxes and repeat rules stay in charge of playback.
- Ignore hidden-wall observations while native or Runtime UI pause is active.
- Organize the handbook around setup, scene authoring, observations, native systems, geometry, integration and reference. Remove duplicate redirect chapters, add section navigation and a catalogue of complete examples, and keep map-specific art history with its project.

## 1.1.0

Added native NPC, inventory, hidden-wall, audio, camera and ending adapters. Added player action/motion observations, general impact events and text reveal observations. Native dialogue speed and quote selection remain in the game's files. Added task-oriented guides and complete compiler-checked examples. Requires JK Runtime 2.2.0; smooth scripted views require Smooth Camera 0.13.0.

## 1.0.1 - 2026-10-14

- Freeze Presentation animation on native pause and Runtime UI by default. A view can explicitly enable animateWhilePaused; paused logic and gameplay remain held.
- Expand the handbook into guided lessons and complete compiler-checked examples covering construction, rendering, native triggers, endings, tools and contracts.

## 1.0.0 - 2026-10-14

- Replace the scene engine with typed components, observations/events, incremental value graphs, bindings and owner-scoped property composition. Remove prior scene formats, specialized runtime objects, flag APIs and cache readers.
- Build planets, gaze, wind panels, weather, reflections and instruments as expandable XML recipes with typed parameters and exported ports. PNG art is the primary authoring workflow.
- Prepare assets and native contracts before player activation, reuse content across attempts, and share one state model for variables, graph/field memory, sensors, effects and control trees. Preserve third-party block/ending discovery.
- Add native ending lifecycle and actor observations/captures. Presentation-clock trees, cooldowns, fields and effects can continue with gameplay stopped.
- Apply masks to sprites, meshes, text and projected surfaces; capture-only groups can mask other drawing, including lights and particles. Keep final geometry on the pixel grid by default; fractional/linear rendering requires an explicit opt-out.
- Reconstruct continuous streams from scene clocks after restore, batch compatible quads, cache unchanged mesh vertices and prepare field-sampling slots.
- Replace the inspector, public scene capability (API 2), compiler, cache, generated reference, handbook and PNG SDK examples. Update authoring adapters to write version 2 layouts.
- Keep compiler/cache identity stable across LF and CRLF source checkouts.
- Older local maps need source migration before using this release. Project-specific directions are recorded beside their source; no runtime compatibility layer is included.


## 0.14.2 - 2026-10-14

- Fix contact decals missing slope aliases when native triangle contact returns
  an unusable intersection rectangle. Keep triangle contact testing and mask
  clipping; use the block bounds only to place the visual stamp.

## 0.14.1 - 2026-10-14

- Fix scene preparation failing when a rolling counter Prop has no XML Track
  children. Treat the absent track array as empty during validation.

## 0.14.0 - 2026-10-14

- Add contact decals for native and mod blocks, sampled before input-phase return
  teleports. Clip sparse PNG animation frames to map-authored collision masks.
- Add contact-phase Regions so a trigger crossed just before a return can still
  update counters and play its native voice clip.
- Roll integer flag digits in whole pixels inside their existing atlas windows.
  Restore and reload clear transient feedback instead of replaying it.

## 0.13.1 - 2026-10-14

- Add synchronized whole-millisecond and tenth-millisecond atlas bindings for
  the fractional charge. Two small dials can display all 170 subframe positions.

## 0.13.0 - 2026-10-14

- Publish the read-only `mega.mapping.charge:1:0` capability with charge phases,
  pause-aware live timing and exact Runtime/SFC launch evidence when available.
- Bind PNG atlas frames to whole/fractional charge or an integer flag's decimal
  digits. Keep bitmap instruments sharp without rotating or filtering textures.
- Restore local charge observations with practice snapshots. Document source
  block hitboxes, triangle clipping and collision/art ownership.

## 0.12.0 - 2026-10-13

- Advance the scene once at the native game-update boundary, independent of
  replaced entity managers. Keep the scene alive through native completion.
- Add map-authored slope aliases for native and mod blocks. Preserve the source
  block instance, type, data and blocking policy; native slopes handle sliding.

## 0.11.0 - 2026-10-12

- Add named destination pools bound to individual screen exits: random with or
  without recent-repeat exclusion, weighted random, shuffled bags and sequences.
- Select per crossing, at attempt initialization or through a reroll trigger.
  Change complete named link schemes together, with touch, screen-entry and
  scene-event triggers. Keep native edge arrival and velocity.
- Support automatic and fixed seeds with independent pool streams. Restore
  history, bag, selected schemes and trigger contacts through native saves,
  practice snapshots and Runtime shared world state.
- Validate ownership, destinations, weights and impossible histories before
  activation. Document basic and advanced setups in the screen teleports guide.
- Require JK Runtime 2.1.0 / API 2.3. Warp forecasts stop at dynamic crossings;
  they never advance the live randomizer.

## 0.10.0 - 2026-10-12

- Load optional mapping settings and screen, transition, prop, particle and GUI
  files from the map's familiar folders. Resolve IDs across files and reject
  conflicting legacy/new entry points. Keep legacy scenes, includes and layouts.
- Add `SceneCacheCompiler --migrate` with validation, rollback and original-file
  backups. Convert the handbook and authoring-kit examples to the new layout.
- Read scene textures compiled by ordinary Worldsmith, preserving premultiplied
  alpha and the existing decoded resource budget.
- Hash the combined scene for cache validation. Keep saved IDs and the public
  scene API unchanged. Require JK Runtime 2.0.3 / API 2.2.

## 0.9.0 - 2026-10-12

- Add opt-in `MapLayout version="2"`: one `level+.png` atlas or bottom-up strip,
  60x45 through 480x360 cells, rectangular grids and exact screen counts.
- Allow individual screens to keep a coarser grid in the same image, with
  pixel-group validation and world-aligned sampling across resolution changes.
- Read compiled collision on the CPU and create native blocks with preserved
  materials, neighbour sampling and integer side links. Keep collision active
  when Mapping visuals are disabled; require JK Runtime 2.0.2 / API 2.2.
- Keep legacy More Block Sizes loading intact and skip its size/texture override
  only on version 2 maps. More Block Sizes remains optional for the new format.
- Add explicit custom-factory opt-in, resource limits, format validation and
  a step-by-step authoring guide. Credit More Block Sizes for level+.
- Rebuild `scene.mmgfx` with the updated compiler when upgrading maps that ship
  a scene cache; compiler identity validation remains strict.

## 0.8.0 - Runtime world execution

- Require JK Runtime 2.0 and use its behavior-tree control flow, hidden-wall
  contacts and native side-link lifetime.
- Bind declared scene execution state for shared-world networking and replay:
  running trees, waits, region edges/dwell, effects, flags and queued events.
- Evaluate host regions against any active actor. Receiving scenes restore host
  state without independently advancing actions or publishing personal saves.
- Validate authored scene identity and prepared effects before applying state;
  preserve existing light resources across repeated snapshots.
- Refuse scene-layout reloads while an active world session owns the bindings.

## 0.7.3 - bounded diagnostic logs

- Keep diagnostic logs to three 4 MiB generations. Oversized older logs retain
  their newest bytes instead of continuing to grow.

## 0.7.2 - shared player presentation

- Use Runtime body samples for player shadows, rims and reflections, including animated outfits and forms. Retain integer crop placement and reuse static samples.
- Requires JK Runtime 1.35 or newer 1.x.

## 0.7.1 - user-controlled scene activation

- Restore the ordinary persistent scene checkbox on every map.
- Remove shared map-controlled activation, screen markers and XML zones for Mapping presentation.
- Preserve scene state, public discovery APIs and side-link topology.

## 0.7.0 Р Р†Р вЂљРІР‚Сњ map-controlled activation

- Use JK Runtime 1.34 authority, shared screen markers and XML zones for scene presentation.
- Persist presentation Off on controlled map entry and lock the global switch while the map owns activation.
- Keep scene resources, persistent state, public API and side-link topology available while presentation is disabled.

## 0.6.2 Р Р†Р вЂљРІР‚Сњ MoreEndingOptions coexistence

- Give MME ownership of its prepared ending roles for the current map without
  removing MoreEndingOptions native patches or changing its settings.
- Skip duplicate foreign tree parsing only for those roles; release the guards
  at world exit. Verify both startup orders and repeated map/vanilla transitions
  with the installed MoreEndingOptions implementation.

## 0.6.1 Р Р†Р вЂљРІР‚Сњ visible custom results

- Draw authored result pages through the native statistics text pass, including
  when the game's outer drawing wrapper was inlined before map preparation.
- Verify native confirmation, drawing and final-counter retention after scene
  disposal with an optimized caller compiled before adapters are installed.

## 0.6.0 Р Р†Р вЂљРІР‚Сњ shared behavior orchestration

- Add typed, reusable scene trees with sequence/selector/parallel/decorator nodes,
  waits, flags, native sound and scoped effects; inline bounded nonrecursive subtrees.
- Share region, native gameplay, Hidden Kingdom and NPC dialogue events with Rules;
  connect native ending trees through explicit scene event/flag bridge nodes.
- Snapshot cursors, timers, event waits and once state; cancel effects on preemption,
  completion and faults; suspend queued gameplay actions during pause.
- Prepare authored effects once instead of serializing XML on every activation.
  Own gameplay subscriptions by scene so hot reload updates tree/rule interests.
- Include trees in reusable Objects, generated XSD/field catalogs and the inspector.
  Ship a node/lifecycle handbook and a compiled scene/ending handshake example.

## 0.5.0 Р Р†Р вЂљРІР‚Сњ reusable objects and native narrative

- Compile parameterized Object definitions into independent ordinary components,
  with instance-local IDs, events, flags and effect ownership.
- Add exact-culture strings, world/screen text, native intro pages and integer
  counters; retain native statistics and append map-owned result pages.
- Bind existing Old Man and Merchant entities for visual effects, attachments and
  selected reflections; add conditional Old Man quotes and native dialogue events.
- Require Runtime 1.29 for preparation before native intro construction and
  world-scoped module/mechanic policy. Do not unpatch foreign mods.
- Generate the complete XML field/default/live-edit catalog from the model and
  shared property registry; ship the XSD, narrative fixture and task-oriented docs.
- Keep source syntax in the one authoring pipeline, with strict validation and no
  alternate legacy runtime. Existing component IDs and graphics remain intact.

## 0.4.0 Р Р†Р вЂљРІР‚Сњ native authoring infrastructure

- Observe original Hidden Walls contact events; add native standing tests and
  Anchor references without replacing collision or inventing a new block palette.
- Retain originating screen metadata on native events; add native event sound cues.
- Extend the existing effect/property infrastructure to water, puddles, surf,
  planets, bushes, shadow receivers and rectangular light blockers.
- Rerender explicitly selected Prop/Node/player reflection participants using
  their live pose, deformation and equipment-aware sprite composition.
- Support all 15 custom-ending actor/controller files documented in ENDINGS.md,
  using MME's XML parser and native BT nodes; validate before binding. External
  prototype documentation is not the specification for this implementation.
- Partition ordinary prop/water/puddle work by screen, avoid unchanged light
  membership rebuilds and skip uniform-ambient copy/light-map passes.
- Require successful preparation; report invalid custom files/caches explicitly
  instead of substituting another execution path. Cache upgrades require rebuild.
- Document manual native workflows and ending contracts; compile documentation
  examples and test native ending adapters, rendering passes and teardown.

## 0.3.0 Р Р†Р вЂљРІР‚Сњ Runtime preparation

- Prepare layout, scene and GPU resources through Runtime before attempts;
  retain dormant rendering adapters within a world and release on world exit.
- Require JK Runtime 1.25 while retaining the scene API 1.0 ABI.

## 0.2.0 Р Р†Р вЂљРІР‚Сњ interactive scenes

- Add typed scene API, region/event rules, conditional flags, owned effects,
  repeat policies, fades, gameplay/presentation clocks and preloaded variants.
- Attach lights and props to the King or other props; update moving-light caches
  in place and exclude the carrying actor from its own light occlusion.
- Restore semantic scene state with Runtime snapshots; save declared flags at
  native save boundaries with context identity, migration and reset epochs.
- Add native inspector, property metadata, numeric drag editing, region views,
  world outlines, timer stepping, event simulation and recipe export.
- Reject non-finite input, retain authoring origins and ship compiled examples,
  an external API consumer, validation-only tooling and expanded XML schema.
- Require JK Runtime 1.12; preserve version-1 XML and native collision behavior.
