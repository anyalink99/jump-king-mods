# Mega Mapping Expansion changelog

## 0.6.2 — MoreEndingOptions coexistence

- Give MME ownership of its prepared ending roles for the current map without
  removing MoreEndingOptions native patches or changing its settings.
- Skip duplicate foreign tree parsing only for those roles; release the guards
  at world exit. Verify both startup orders and repeated map/vanilla transitions
  with the installed MoreEndingOptions implementation.

## 0.6.1 — visible custom results

- Draw authored result pages through the native statistics text pass, including
  when the game's outer drawing wrapper was inlined before map preparation.
- Verify native confirmation, drawing and final-counter retention after scene
  disposal with an optimized caller compiled before adapters are installed.

## 0.6.0 — shared behavior orchestration

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

## 0.5.0 — reusable objects and native narrative

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

## 0.4.0 — native authoring infrastructure

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

## 0.3.0 — Runtime preparation

- Prepare layout, scene and GPU resources through Runtime before attempts;
  retain dormant rendering adapters within a world and release on world exit.
- Require JK Runtime 1.25 while retaining the scene API 1.0 ABI.

## 0.2.0 — interactive scenes

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
