# Extensible mod architecture

Updated 2026-10-27. Version 0.4.0 establishes the content/import foundation below.
The later stages are an implementation plan, not shipped capabilities.

## What exists now

| Layer | Responsibility | Current implementation |
| --- | --- | --- |
| Authored recipes | Select source assets, stable IDs and adaptations | `content/jkq-import.json` |
| Source adapter | Read Quest references, supported decisions, animation and sound metadata | `quest_source.py`, `import_quest_enemy.py`, `import_quest_art.py` |
| Offline compiler | Bake art/audio, validate references and produce one fingerprinted pack | `import-jkq.ps1`, `compile_quest_content.py` |
| Shared definitions | Enemy geometry/actions, item stats, weapon timing and presentation tokens | `ContentCatalog`, `EnemyDefinition`, `AttackDefinition`, `ItemDefinition`, `WeaponAttackDefinition` |
| Mutable simulation | Actors, health, position, action phase and consumed hits | `Battle`, `FighterState`, numbered commands and events |
| Native bridge | Input, terrain, body phases and lifecycle ownership | `ModEntry`, `GameSession`, Runtime scopes |
| Local presentation | Prepared sprites and audio; no authority or progression | `EnemyArt`, `EquipmentArt`, `QuestAudio` |
| Persistence | Personal inventory/progression and protected writes | `CharacterStore`, character schema 1 |

An enemy instance doesn't own a sprite sheet or parse a prefab. It stores a
catalogue index and mutable state; all instances of that type share the same
definition. Several definitions use one reviewed grounded-melee controller.
Attacks have their own clips, event markers and damage. Weapons likewise resolve
definitions instead of selecting damage/sounds from their saved ID.

The simulation has no rendering, input devices, file writes, Steam calls or Quest
runtime objects. Value-type positions and rectangles are its geometry inputs.
Presentation receives state/events and borrows prepared resources. Preparation
is owned by Runtime world scopes; player behavior is attached only at activation.
Character saves and recorded appearance tokens retain their existing identities.

## 1. Share action and effect infrastructure

Do this before adding many weapon or enemy mechanics:

- Consolidate enemy and weapon actions around a common timeline: windup, active
  windows, recovery, root movement and numbered effect markers. Keep different
  shape providers for the native King's held weapon and baked enemy colliders.
- Introduce typed effects for damage, knockback, stagger, guard break, healing and
  statuses. Effects carry rule parameters, source/target IDs and an application
  identity. One attack cannot reapply a consumed effect after correction/restore.
- Keep `ItemDefinition` as catalogue/slot data. Weapon and consumable definitions
  reference actions/effects; they don't contain input, menu, save or renderer code.
- Move reward tables, starting inventory and progression unlocks out of the
  remaining `CharacterProfile` rules when campaigns need different loadouts.

Acceptance: a second sword, guard-break attack or healing item is a data change;
a genuinely new effect adds one handler and tests, without edits for every item.

## 2. Separate simulation systems around real mechanics

`Battle` is still the coordinator, and the grounded controller currently lives in
it. Extract focused systems when their second use appears: locomotion, target
selection, actions/hit resolution, statuses, projectiles and encounter triggers.
Pass a small world/query context and explicit actor state; don't give each
system the entire game singleton.

Behavior profiles compose those systems. A shield guard can reuse grounded
movement with another action set. A hopping frog adds one hopping controller.
A ranged enemy adds the shared bounded projectile system. Avoid a class per
enemy name, a giant named-enemy switch, or an ECS/service framework introduced
before the actual mechanics need it.

Use stable actor/spawn IDs, integer catalogue indexes inside hot loops and
immutable definitions. Preallocate transient pools. External consumers should
eventually get read-only actor views and commands rather than mutable
`FighterState` objects; that is a deliberate SDK step after the internal rules
settle. Namespace third-party definitions and reject ID collisions at registration.

Acceptance: adding a behavior doesn't require changing saves, audio, UI and every
enemy class. Unit tests run simulation with a small terrain fixture; native
integration tests cover query/body ordering separately.

## 3. Give maps a stable encounter contract

Evolve the current typed spawn file into named groups, activation regions, leash
bounds, waves and completion flags. Add editor palette/preview/placement checks
against the same definitions and schemas the runtime uses. Export data; don't
generate map-specific C#.

Use Runtime world objects and reviewed map interactions for doors, shortcuts,
hazards and chests. A map owns its encounter policy and campaign IDs. Combat owns
hit rules. Presentation owns sparks/sounds. A door callback mustn't award XP by
calling into a character menu.

Separate personal character saves from map/campaign progress. Boss/chest rewards
need versioned receipts keyed by campaign, encounter ID and reward revision.
Add explicit migrations for renamed/removed definitions and changed encounters;
never silently replace missing items or overwrite an unknown save. Keep backup
and failed-write behavior. Add inventory paging before broadening the owned-item
count beyond the current small catalogue.

Acceptance: moving a spawn preserves its identity; restarting/resting follows the
authored respawn policy; a unique reward can't be granted twice by reload.

## 4. Build co-op and replay adapters around the same state

Replace the fixed player-1 assumption with authenticated participating actor IDs.
The host chooses targets and validates numbered commands, equipment and timing.
Only Local/Host execution advances AI, contacts and shared encounter outcomes.
Replica/Playback restore authoritative state and produce local presentation.

Declare bounded binary state through Runtime's world registry and reuse
Multiplayer Expansion's existing world transport. Include rules/content identity,
attempt epoch, actor/spawn IDs, action/attack phase and consumed-hit identities.
Transmit state and logical events; each viewer owns animation interpolation,
particles and prepared sounds. Keep saves, native body objects, sprite frames,
PCM data and bone matrices out of packets.

Use sequence IDs for effects and reward receipts. Late join, pause, packet
reordering, seek and state correction mustn't replay damage, sounds or XP.
Replays use the same state contract with persistence disabled. Keep inventory
personal and guest world restoration separate from campaign progression.

First co-op acceptance: two players fight one enemy and share a group-completion
door, with no duplicate hits or rewards under loss/rejoin. Respect the current
shared 48,000-byte world snapshot limit and maximum 20 Hz stream; measure before
adding another channel or prediction. Host migration/checkpoint recovery is a
separate feature, not an implication of snapshot support.

## 5. Extend content through reviewed adapters and APIs

Add a small versioned content API once built-in definitions and migrations are
stable. Other mods should register validated definitions and prepared resources
through scopes/capabilities. Worldsmith needs a catalogue view and placement
metadata, not direct access to internal simulation classes.

Keep source adapters separate from the portable pack. Another importer or a
hand-authored enemy should produce the same definitions without pretending to be
Quest. New adapters declare supported bindings/mechanics and output diagnostics.
They mustn't execute source game assemblies or arbitrary downloaded scripts.

Later PvP is another authority/team/reward policy over the common action rules.
Start with opt-in duels and equalized equipment. Don't make the enemy catalogue,
item system or native Jump King renderer depend on PvP/networking.

## Cost and dependency rules

Runtime remains the required platform. More Items and Multiplayer Expansion are
optional integrations; content definitions don't reference either transport or
inventory implementation. Keep UI and source tools out of the combat kernel.

Prepare source-derived data, textures and PCM in named loading stages. Load only
declared map/training dependencies and reuse them across attempts. Never discover
types, parse a catalogue or lazily decode an asset from the first attack/Draw.
Retain explicit actor, marker, audio, projectile and memory budgets. Optimize
native spatial queries after representative-map measurements, with invalidation
for changing terrain; avoid another parallel physics simulation.

Track simulation allocations, native query time, GPU memory/draw calls, audio
voices and network bytes separately. The current prepared 16-enemy fixture checks
zero allocations per simulation tick; it doesn't measure full game cost. More
content mustn't bring hidden per-actor decoders, timers, tasks or listeners.
