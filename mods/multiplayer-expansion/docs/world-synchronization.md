# World synchronization

Turn on **Multiplayer settings > Host Settings > World synchronization** on the
host. It starts off and is independent of Ghosts/Solid. Every participant needs
Expansion 0.8.0+, Runtime 2.0+ and matching maps and world mods.

## What is shared

| Mechanic | Shared behavior |
| --- | --- |
| Wind | Uses the host's cycle time and each client's screen settings. Personal timers and statistics stay local. |
| Native ravens | The host advances flight and animation. Any fresh, active player can trigger a reaction. |
| Native hidden walls | Any active player can trigger their shared contact and fade state. |
| Switch Blocks | The reviewed profile shares Auto, Basic, Countdown, Group, Jump, Sand, Sequence and Threshold data. |
| MME 0.8.0 scenes | Declared behavior-tree cursors, effects, regions, flags and native side-link state. |

Native item pickup and inventory stay personal. Unknown foreign mechanics aren't
automatically synchronized. A mod author can declare state through Runtime;
they must also make simulation and persistence respect its authority rules.

## Join, pause and leave

Clients negotiate world support before applying snapshots. A mismatched state
layout is reported in the setting's description; a partial snapshot isn't applied.
The host sends snapshots at up to 20 Hz and pauses shared simulation when its
game is paused. On an interruption, guests hold the last accepted state.

Receiving world state stays in memory and is blocked from the shared mechanic's
save methods. Leaving synchronization restores a guest's prior local world;
the host keeps its own simulated state. This isn't a shared save file.
Host migration starts with the new host's world and
setting; it doesn't transfer the old host's complete simulation.

Switch contacts keep a separate entry edge for each player, with deduplicated
jump and switch-button requests. The foreign Switch Blocks DLL remains unchanged.
Very fast contacts between samples and unusual mod combinations still need
latency playtests.

## Integrate a world mechanic

Start with Runtime's [small world example](../../jk-runtime/docs/world-execution.md#try-the-small-example).
Mark shared fields, register them for the world lifetime, and gate shared updates
with Runtime authority. Keep personal preferences, rewards and inventory local.
No Multiplayer-specific provider is needed.

## Shared world state

World synchronization has separate negotiated messages under the expansion magic.
Host snapshots carry a map ID, the owner's attempt epoch and an increasing
sequence. Runtime validates the complete declared state set before mutation and
rolls back a failed application. Limits are 64 entries, 48,000 bytes per snapshot,
512 entries per collection and 4,096 values per data graph. Movement keeps its
existing protocol and authority rules. All participants need Expansion 0.8.0,
Runtime 2.0 and matching world mods for shared-world snapshots.

Use Runtime's `WorldRegistry.Shared.Attach` for declared `WorldField` objects, or
`Bind` for a declared snapshot that must restore through a semantic operation.
Neither needs an Expansion dependency or a custom wire provider. Own the binding
in the map's Runtime scope. `WorldControl` owns authority and actors;
`WorldExecution` identifies the current actor, role and phase. Keep personal
settings, rewards and inventory outside shared state. Read the
[Runtime guide](../../jk-runtime/docs/world-execution.md) for the limits and
execution requirements.

The existing public `WorldState.Register` discovery API forwards explicit byte
contracts to Runtime. It doesn't own a second registry or serializer. New code
uses Runtime directly.

The packaged SDK shell exposes native menus without loading dependencies during
native discovery. Its embedded implementation owns networking and `WorldLifecycle`
callbacks. World/attempt preparation calls Runtime's shared `NativeWorldState` and
`SwitchBlocksWorld` services; normal activation enables networking. Runtime owns
native birds, wind, hidden walls, pickup isolation and reviewed foreign hooks.
Mapping owns its authored scene and registers its declared execution state.

The host re-evaluates remote block contacts against its own loaded geometry using
the latest accepted movement sample. Per-player latch state avoids one guest
resetting another player's entry edge. Cumulative input counters survive dropped
packets without repeating held buttons. Native world simulation isn't rolled back
to the remote input time. Tests cover the installed lever behavior, state/save
isolation, map/owner/epoch/sequence rejection, and the eight data layouts; these
are not a substitute for playing every supported map mechanic under latency.

See [Runtime world execution](../../jk-runtime/docs/world-execution.md) for the
state types, lifecycle and failure rules, and [validation](validation.md) for
the installed Switch Blocks fixtures.
