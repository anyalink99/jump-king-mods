# Metroidvania development plan

Updated 2026-10-27. Version 0.4.0 provides the manifest-driven content pipeline,
shared definitions and the first adapted Quest enemy. See the
[architecture plan](architecture.md) for implementation boundaries and order.
The milestones below are planned work, not available settings or compatibility
promises.

## Current foundation: Famished Crawler

The first full enemy adaptation uses Quest's `FSlaveCrawlerGlue`: its skinned
model, materials, idle/walk/lunge/poise/stagger clips, animated hurt and attack
boxes, forward attack movement and five distinct assigned sound events. The build bakes
219 poses into one atlas. Runtime uses sprites and small collision tables.

The native king can hit and parry it. Its lunge advances up to 16 pixels, stops at
walls and unsupported edges, and consumes one hit across both attack boxes.
Defeat uses its collapse/stagger clip and fades the body. The imported enemy has
200 health; its 32 base attack damage and ledge restriction are adaptations for
Jump King. Quest's generic flying-death system, navigation, drops, factions and
level scaling aren't implemented.

Authors can place `fslave-crawler` with encounter format 2. The rectangle-based
prototype and its format 1 Crawler spawns have been removed. Combat snapshots include archetype,
spawn identity, selected attack, animation clock, consumed hit and content identity.
That is a useful local contract; it doesn't enable multiplayer by itself.

## 1. Make combat and placement easy to tune

- Add a map-editor enemy palette with an animated preview, floor/support check,
  facing, patrol endpoints and a visible attack envelope. Keep these aids in the
  editor. Export the same encounter XML the mod reads.
- Show validation errors at the authored spawn, including unknown types,
  duplicate IDs, unsupported placements and overlapping patrol routes. Package
  validation should report all errors before the author launches the game.
- Add small training scenes for missed attacks, parries, narrow ledges and
  attacks during a jump. Each enemy should ship with one example encounter.
- Add source-derived stagger/poise rules before expanding the roster. Avoid
  permanent light-attack stun locks; make a committed heavy attack useful.

Acceptance: an author places and tests an enemy without editing C#; exported
maps and equivalent hand-written files behave the same. A rejected file doesn't
leave half an encounter running.

## 2. Add enemies that change the route

| Priority | Enemy | Encounter it enables |
| --- | --- | --- |
| Next | Mudfrog | A readable hop/lunge and tongue attack across a small gap. Jump over the tongue or bait the landing. |
| Then | Ratling ranged enemy | Projectiles make the player choose jump timing and safe cover. Use a bounded projectile pool and terrain sweeps. |
| Then | Shielded Red Crown enemy | Approach from another platform, parry, or use a heavy guard break instead of repeating light attacks. |
| Later | One adapted miniboss | A short arena with two attack patterns and a visible phase change, ending in a named reward or opened route. |

Import each selected prefab's actual clips, hit events, movement and assigned
sounds. Treat attacks, hit recovery and defeat as one tested content unit. Don't
add a roster entry that merely changes a sprite while reusing another enemy's attacks.
Document deliberate changes in scale, damage or navigation.

Acceptance: every attack has a readable windup, matching contact, an evade/parry
answer where intended, and a useful recovery window. Test both facings, walls,
edges, a moving player and interruption before adding the next archetype.

## 3. Give maps encounters and consequences

Extend a new encounter schema with named groups and explicit trigger/respawn
rules. Keep IDs stable when an author moves a spawn.

| Feature | Concrete author use |
| --- | --- |
| Trigger region | Wake a guard pair when the player enters a courtyard; leave the rest asleep. |
| Leash and home point | Enemies return to their own ledge instead of chasing across the entire climb. |
| Group completion | Defeating `crypt-guards` opens a gate, stops a hazard or reveals a chest. |
| Small waves | Start the second group after the first; cap living enemies across all groups. |
| Checkpoint/rest policy | Choose respawn on rest, once per attempt, or a persistent boss defeat. |
| Reward table | Give a named item, an attribute reward or a map ability once for a named encounter. |

Connect completion and trigger flags to Runtime world objects and reviewed MME
interactions. Keep map logic in the map package. Don't turn arbitrary XML into a
scripting language or execute downloaded C#.

Use explicit campaign/map IDs and a versioned save scope for persistent bosses,
chests and doors. Store reward receipts by campaign, encounter ID and reward
revision. A relocated spawn mustn't duplicate a reward; a revised encounter must
have an intentional migration/reset rule. Training victories remain a separate
repeatable sandbox.

Acceptance: loading, resting and restarting reproduce the authored policy;
completion effects and unique rewards happen once. Updating a map preserves old
progress or reports a migration choice without silently overwriting it.

## 4. Add co-op through Runtime's existing world contract

Keep Metroidvania dependent on Runtime. Multiplayer Expansion already transports
Runtime world snapshots; another combat transport or mandatory Expansion
dependency shouldn't be needed for the first integration.

1. Replace the fixed player-1 assumption with stable participating actor IDs.
   Give enemies an explicit target ID and choose among fresh, eligible actor
   samples. A stale/disconnected actor can't trigger attacks or receive rewards.
2. Simulate shared AI, projectiles, hits, health and encounter completion only in
   Local/Host execution. Register a bounded binary state through
   `WorldRegistry.Shared`; Replica and Playback restore and present it. Merely
   registering fields doesn't transfer execution ownership.
3. Send numbered attack/guard commands bound to the sender's session identity.
   The host validates action timing, equipped stats, facing and range. Clients
   can't award damage, invent enemies or submit their own XP result.
4. Reuse the existing world stream's maximum 20 Hz cadence and shared 48,000-byte
   snapshot ceiling. Budget combat against other registered mechanics. Encode
   IDs, positions, health, action/clip tick and consumed attack identities; never
   send sprite frames, bones, particles or audio samples.
5. Let viewers interpolate positions and advance visual clip time locally between
   accepted samples. Number attack/hit/defeat effects separately; deduplicate them
   on retransmission, correction and late join. Restoring a snapshot mustn't play
   every old hit sound.
6. Keep character files personal. Apply approved encounter rewards once through
   participant-specific receipts. A guest's received world snapshot must never
   overwrite their inventory or another map's save.

Match map identity, encounter schema, mod rules and content hashes before joining
combat. Host pause freezes shared simulation; a guest's character menu doesn't
pause the host. Follow Expansion's existing hold-on-interruption and local-world
restoration behavior. Its current host migration starts a new host world; the
first combat adapter should restart from a checkpoint after host loss rather than
claim seamless migration.

Acceptance: two players can hit/parry the same enemy without duplicate damage or
XP. Cover late join, pause, disconnect, rejoin, mismatched content, packet loss,
reordered requests and a state restore during the lunge. Measure bytes and CPU
with 1, 4 and 8 players before increasing enemy limits.

## 5. Replays, PvP and equipment that matter

- Record the same authoritative commands, periodic combat snapshots and numbered
  effects for replay. Seeking rebuilds enemies, health, doors and clip phases;
  playback never writes progression. Sound and particles remain local effects.
- Add opt-in duels before general PvP. Keep friendly fire off in co-op. Offer
  equalized duel equipment and an explicit reward policy so repeated duels don't
  become a progression farm. Validate hits on the host before adding latency
  compensation or prediction.
- Add equipment with a clear encounter use: a guard-break weapon, resistance to a
  named hazard, or boots that reduce fall damage. Map-granted movement abilities
  need explicit campaign rules and authored routes; keep the native king and its
  regular controls as the base.
- Add a small miniboss reward room, a locked shortcut and a rest checkpoint as
  the first complete map example. This tests the whole loop rather than adding
  disconnected menus or stat variants.

## Cost and presentation rules for every milestone

Prepare meshes, atlases and audio before player handoff; retain immutable data
across attempts. No mesh skinning or animation decoding in Update/Draw. Use
bounded enemy/effect/projectile pools, sleep distant groups, and check terrain
only for actors that move or attack. Render only nearby sprites.

Keep the initial limit of 16 living enemies until a representative map and co-op
test justify changing it. Report simulation, native queries, drawing, audio and
network costs separately; a cheap-terrain fixture isn't an in-game benchmark.

Communicate enemy intent through poses, movement and sound. Keep permanent HUD
content limited to useful combat information. Put authoring details in the
editor/docs and optional explanations in an explicitly opened page.
