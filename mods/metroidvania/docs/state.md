# Combat state and execution

`Battle` owns the offline fight. It accepts numbered `CombatCommand` values,
stable actor IDs and a terrain callback. It has no game objects, devices, disk IO,
rendering or Steam calls. Actor 1 is the one supported player; enemy IDs increase
within the attempt. Command serials reject repeated or stale actions.

`BattleSnapshot` copies fighters, timers, attack identities, command serials,
derived stats, consumed landing identities and event counters. Snapshot schema 4
adds catalogue indexes, selected attacks, body dimensions, weapon timing and the
compiled content identity. A missing or mismatched definition is rejected before
mutation; character-save and appearance schemas stay unchanged.
Restore validates the complete snapshot before
mutating the fight. It copies input objects and clears transient events, so a
restore doesn't emit another reward or impact. This is a local state contract,
not an implemented multiplayer protocol.

`GameSession` samples the native king, advances the fight at 60 fixed ticks per
second and applies confirmed impulses at Runtime's BeforeWind body phase. Pause
and modals discard accumulated catch-up time. A frame advances at most four
ticks. Nearby AI uses live native block collision; distant AI sleeps. There are
at most 16 enemies and 24 local impact flashes. The hit path doesn't create
collision-info graphs or scan the whole map.

Enemy instances reference shared definitions with prepared collision/movement
tables. All boxes of an attack share one hit-consumed flag. The grounded controller
checks walls and support before applying source displacement. Hit/parry
reactions cancel the remaining attack. Defeat advances its collapsed visual
without emitting more XP.

Two native body-phase observers sample the speed before Y movement and the
grounded state after Y collision. Only an airborne-to-grounded transition at
`PlayerValues.MAX_FALL` queues a numbered landing. The next combat tick consumes
it once. Native teleport flags, large relocations and Runtime restore epochs
discard stale samples. Pause preserves an already confirmed pending landing.

Swing and impact events select local audio cues. A frame combines repeated cues
into a bit mask, so hitting many enemies doesn't multiply the same sound. Each
cue has one prepared voice; an already playing cue is left alone. Audio loading
and lifecycle details are in [Attack audio and falls](combat-effects.md).

`CharacterStore` owns independent progression. Gameplay rewards mark a detached
profile dirty; a quiet encounter flushes it once after one second. Menu edits,
explicit Save and attempt teardown also flush. Failed saves retain dirty state
and report the error. Runtime's SettingsFile protects unsupported, unreadable or
externally edited data and keeps the previous saved file as `.bak`.

Mode starts disabled. Disabled attempts create no battle, body behavior, input
actions, enemy collection, collision cache or prepared audio. Static UI and visual-provider
registration remain available to menus and recorded actor appearance.

The metadata catalogue is loaded once for profile validation and menus. Enemy
frame tables, atlases and sounds are prepared in loading phases; map placement
selects the types it needs. The catalogue owns item definitions and weapon action
parameters. Its public item collection is read-only; actor state stays mutable
inside simulation. Read the [architecture plan](architecture.md) before adding
another behavior or exposing a third-party content API.

Shared-world host, replica and playback roles do not advance this offline fight.
A future online adapter must define membership, authenticated commands, authority,
latency handling and reward policy. The current code does not provide network
combat, general rollback or full enemy replay.
