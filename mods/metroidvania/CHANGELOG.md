# Changelog

## 0.4.0 — 2026-10-27

- Add one import manifest for enemies, equipment, balance, appearance IDs and
  shared audio. Resolve enemy clips, weighted attacks, health, collider references
  and sound GUIDs from Quest's source configuration.
- Replace the named crawler implementation with shared catalogue definitions,
  indexed actor state and one reusable grounded-melee controller. Support several
  attacks and variable collider sets per definition.
- Load item stats, weapon action timing, sound roles, healing and art mappings
  from the compiled catalogue. Keep saved item IDs and appearance tokens stable.
- Compile a complete pack before promotion, preserve prior files on failure and
  reject stale manifests/tools, missing resources and altered compiled assets.
- Save enemy atlases with the offline PNG encoder and fully decode staged PNGs
  before promotion. Reject truncated image streams before they reach the game.
- Ship maintained import/build helpers in `build-support` and keep content
  fingerprints stable across Git line endings and UTF-8 BOMs.
- Prepare only requested enemy atlases/audio alongside the default training type.
  Add source discovery, importer checks, multi-archetype/action restoration tests
  and an infrastructure plan for maps, effects, saves, co-op and replays.

## 0.3.0 — 2026-10-26

- Add the Famished Crawler from Quest: baked model/materials, five source clips,
  animated hurt/attack boxes, terrain-limited lunge movement and four assigned
  voice/attack/movement/pain events. Training now spawns this enemy.
- Add typed map spawns in encounter format 2. Remove the rectangle-based crawler,
  its AI and its old format 1 spawns. Reject unknown XML fields and duplicate IDs
  before activating the encounter.
- Include enemy identity, animation time, consumed attacks and the content hash
  in local combat snapshots. Preserve one hit and reward across restoration.
- Add reproducible enemy baking, native GPU/audio checks and an authoring guide.
  Document the next steps for encounters, equipment, co-op, replays and PvP.

## 0.2.0 — 2026-10-26

- Add Quest sounds for light/heavy weapon swings, unarmed attacks, hits, blocks
  and parries. Prepare 11 reusable voices during world loading and respect the
  native volume and pause controls.
- Add heavy-landing damage: 8% of maximum health, rounded up. Ordinary jumps,
  teleports and state restores don't produce fall damage. Track each landing once.
- Add a reproducible offline audio importer and checks for cue timing, native
  playback, bounded voice reuse and fall-state safety.

## 0.1.0 — 2026-10-26

- Add an opt-in offline mode with separate character, inventory, equipment and
  camp screens using JK Runtime.
- Add light/heavy attacks, a timed directional guard, crawler encounters,
  experience, four attribute axes and a small equipment catalogue.
- Keep the combat model separate from input, rendering and persistence, with
  numbered commands, stable actor IDs and restorable snapshots.
- Add bounded map-authored spawns, training encounters, independent profile
  saves and compact Runtime equipment visuals.
