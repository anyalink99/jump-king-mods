# Keep counters and understand restarts

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

Choose **Attempt** for a value that should reset with each attempt. Choose **Save** for a total that belongs to the native save. Try [lessons 5 and 7](../getting-started/lessons.md#5-count-jumps-in-this-attempt) to compare them with a visible counter.

A practice snapshot is different from a fresh attempt: restoring it resumes the captured count and sequence state. It does not replay all the jumps or sounds that happened before it. The details below help when your map uses saved story choices, timed effects or shared play.

## Variables

Declare a variable with ID, kind, initial value and scope. Supported scene variable scopes are Attempt and Save. The public enum also contains Transient for other resource descriptions; it isn't an accepted persistent scene-variable scope.

Attempt is appropriate for whether this jump has already counted, a sequence latch, or a temporary choice. Save is appropriate for totals that survive a new attempt. Save variables require a stable scene saveId. They are stored under `MegaMapping/Construction/<saveId hash>.xml` in the game's native save context.

Don't change saveId just because artwork or the map title changed. Do change it deliberately when a new scene must have independent data. Changing a variable's kind isn't a safe automatic migration of its old value. Keep conversion in an explicit tool that preserves existing new data and respects native reset epochs.

The saved jump-count [example](behaviors.md#count-jumps-and-flash-a-light) is a complete starting point. For once-per-jump category counts, keep the category total in Save and its armed Boolean in Attempt; only arm it from actual jump events.

## What a snapshot retains

| State | Restore behavior |
|---|---|
| Variables and observations | Restore a coherent typed batch |
| Graph delay/integrator/spring memory | Resume from restored memory |
| Field memory | Resume spatial simulation |
| Sensor membership/dwell | Restore entry state rather than creating a new entry for every overlap |
| Rule once/cooldown gates | Preserve consumed gates and elapsed clock state |
| Effects and trees | Restore envelopes, ownership, cursors, waits and event latches |
| Clocks and queued events | Restore scene timing and pending work |
| Continuous emitters and automatic atlas animation | Reconstruct from clocks/definitions |
| Burst particles and stamps | Clear; don't replay historical transient marks |
| Native audio already played | No historical replay or scene-owned rewind |
| Inspector preview contributions | Local tooling state, excluded from shared portable state |
| Prepared textures/meshes/capture targets | Resources, not serialized gameplay state |

Restoring is different from restarting. Restart creates a new attempt and reloads Save variables. Restore resumes a compatible attempt state. Native cutscene actors remain game-owned: observing/capturing them doesn't make MME responsible for recreating their entities.

## Ownership and generation

Effects belong to the rule, tree action or API caller that activated them. Removing one owner recomposes current remaining contributions. Old effect leases become invalid after restore. Successful scene replacement also changes the signal hub; consumers must rebind.

World/attempt/activation resources are prepared and released through Runtime scopes. A failed reload candidate doesn't replace a working scene. Successful replacement releases the previous activation/resources. Collision/layout changes require a restart rather than trying to reinterpret state against different geometry.

## Shared state

An authoritative world executes scene logic. Followers consume its state and don't independently trigger effects, emit scene events, alter variables or write personal saves. Read-only inspection remains available. Shared packets validate compiler/scene identity, decoded artwork, types, counts, finite values and size budgets before mutation.

The identity includes expanded definitions and decoded texture pixels, not just filenames. Two maps with the same entry name but different art aren't assumed compatible. MME refreshes its local cache automatically. Shared sessions still need matching scene definitions, assets and MME implementations; exchanging cache files is unnecessary.

Reload is blocked while another session owns world state. Receiving authoritative state doesn't overwrite personal Save variables on disk. Native reset invalidates old save owners so an earlier attempt cannot write its old totals back afterward.

See [limits](../reference/features.md) before designing large shared scenes. A successful local scene isn't proof that its state fits the compressed network budget. Inspect representative populated state, not an empty startup packet.
