# Move a room without changing its native jump timing

[Handbook](../index.md) / [Geometry and navigation](index.md)

Use `PhysicsFrame` when importing a timing-sensitive room from another screen.
MME 1.3.0 and JK Runtime 2.2.1 are required. Ordinary screens keep native behavior;
this setting applies only to the screens you name.

For example, two challenges originally built on screen 1 now occupy screens 2
and 5. Put their original screen number in `referenceScreen`:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="atlas" screens="6"
             columns="1" rows="6" cellWidth="60" cellHeight="45">
    <PhysicsFrame screen="2" referenceScreen="1" />
    <PhysicsFrame screen="5" referenceScreen="1" />
  </MapLayout>
</MappingSettings>
```

This example needs a 60 by 270 collision atlas, with screens arranged down the
column. Add the two `PhysicsFrame` entries to your existing `MapLayout`; keep
its source, dimensions, resolutions and side links. The entries also work with
`source="native"` and `source="strip"`. Restart the game after changing them,
as you would after changing collision geometry.

Both attributes use one-based screen numbers. `screen` must exist in this map.
`referenceScreen` is the room's original screen number, from 1 to 4096; that
screen doesn't have to exist in the new map. Each screen may have one entry.
No separate script, scene component or authoring compiler is needed.

## Why an identical room can behave differently

Jump King stores screen 1 at world Y 0 through 359, screen 2 at -360 through -1,
and so on. Native hitboxes truncate floating-point coordinates toward zero.
Local Y 107.9 becomes 107 on screen 1. The same point on screen 2 is world
Y -252.1, which becomes -252: local 108. That one-pixel difference can make a
slope collide a tick earlier and change the rebound velocity.

Changing the cast alone isn't enough. Adding velocity to a larger negative
`float` loses different fractional bits. A long sequence of additions can then
cross an integer boundary on a different tick.

The adapter retains the Y coordinate in the reference frame, performs the
native velocity addition there, and uses that coordinate for native hitbox
conversion and Y collision snapping. It publishes the translated world Y to
the body. Collision blocks, teleport destinations, camera addresses, Hidden
Walls and scene selectors keep their real world coordinates.

## What stays under the game's control

The adapter doesn't create another update loop or change tick duration. Native
movement stages, gravity, horizontal movement, slope response and block callback
order still run once in their normal order. Configure Subframe Charge separately
if the imported challenge needs a particular charge interval.

The adapter remains active when MME's visual checkbox is off. It belongs to map
physics, like collision shapes. Runtime's mechanic inventory reports it as
`mega-mapping.physics-frame` with movement and collision effects.

The retained fraction belongs to the current player and attempt. Direct position
changes, including block teleports, start a new reference coordinate at the
destination. Runtime practice snapshots and shared-world snapshots carry the
fraction too; restoring them doesn't replay movement. Ordinary native disk saves
still store the native world position, so resuming a save made midair can lose
the fraction that native save data cannot represent. Starting or returning to an
integer spawn does not have that ambiguity.

## Check the import, not just the setting

Keep collision pixels, slope orientation, spawn position, block providers and
charge settings consistent with the original. `PhysicsFrame` doesn't compensate
for a spawn moved one pixel sideways or a missing material mod.

The supported path is native body Y integration, native hitboxes and native Y
collision resolution. Foreign controllers that replace those stages, or do
their own coordinate arithmetic directly on `BodyComp.Position`, need their own
compatibility review. The adapter does not make every controller translation
invariant. Rooms that cross vertical screen boundaries also need a check of the
neighbor's reference frame; use this first for self-contained imported rooms.

Compare native trajectories at the final screen addresses. Cutting a room out
and testing it back on screen 1 hides exactly the problem this setting solves.
Match collision ticks, velocities and the destination reached, not just an image
of the route. These checks establish the tested trajectories, not every route
through a map.
