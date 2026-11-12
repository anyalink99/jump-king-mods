# Choose map physics

[Map guides](README.md)

MME 1.7.0 adds a map-owned `Physics` section to `mapping_settings.xml`.
Leave it out to keep the game's original physics. Changing a profile requires
a map restart and can change jump timings.

| Profile | Use it for |
| --- | --- |
| `native` | Original game coordinates and collision behavior; also the default |
| `reference` | Preserve an imported room's original screen arithmetic |
| `symmetric` | Remove coordinate bias and screen-height-dependent precision; use exact bounds for native rectangular contacts |

## Symmetric coordinates

Add this section directly inside `MappingSettings`, beside `MapLayout` if your
map has one. You don't need to change collision resolution or use `level+`.

<!-- mme-example:start -->
```xml mapping_settings.xml
<MappingSettings version="1">
  <Physics version="1" profile="symmetric" />
</MappingSettings>
```
<!-- mme-example:end -->

The profile applies to the whole map. It keeps a precise fractional position
on both axes, treats native rectangular contacts consistently in both horizontal
directions and retains that fraction across native side links. Moving-platform
carry and rollback preserve the same state. Gravity, jump charge and update
frequency stay under their existing owners.

**This profile does not fix slopes.** Slope geometry, intersections and normals
remain owned by the game and any installed slope mod. A map containing slopes
or a custom movement controller is not automatically guaranteed to have mirrored
trajectories. See [behavior and compatibility](../reference/physics.md).

In the game's **Mapping inspector**, open **Map physics** to see the profile and
its compatibility scope. The MME presentation checkbox doesn't disable physics.

## Preserve an imported challenge

Use `reference` when exact old timings matter. This example places a challenge
from screen 1 on screen 5 while retaining screen 1's Y arithmetic:

<!-- mme-example:start -->
```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="native" screens="5" />
  <Physics version="1" profile="reference">
    <Frame screen="5" referenceScreen="1" />
  </Physics>
</MappingSettings>
```
<!-- mme-example:end -->

Unlisted screens use native behavior. Screen numbers start at 1; the reference
screen doesn't need to exist in the new map. Existing `PhysicsFrame` entries
inside `MapLayout` still work and do not need migration. Don't combine them
with a `Physics` section.

Keep the collision, spawn, block dependencies and charge settings of the original
challenge. Follow [the imported-room checks](physics-frames.md#check-the-moved-challenge)
at its final screen address.

## Saves and profile changes

Symmetric coordinates participate in Runtime practice and shared-world snapshots.
Disk saves keep their extra fraction alongside the matching native player save.
A stale record cannot replace a different native position or timestamp.

When starting from an older save without an exact record, the profile initializes
from its native position. Switching profiles changes physics; test from a known
spawn before using old timing notes. Physics version 1 pins these rules.
