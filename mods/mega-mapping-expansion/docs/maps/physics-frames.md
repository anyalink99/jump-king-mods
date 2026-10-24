# Move a room to another screen

[Map guides](README.md)

Use this setting when you copy an existing challenge to a different screen
number and need its precise jump timing to stay the same. For ordinary new
rooms, you can leave it out.

Jump King can round the same position slightly differently on different
screens. `PhysicsFrame` tells MME to use the original screen's rounding for
that room. It needs MME 1.3.0 and JK Runtime 2.2.1 or later.

## Example: move screen 1's challenge to screen 2

1. Copy the room's collision and visible art to screen 2 of your new map.
2. Keep its spawn position within the screen, block mods and charge settings
   the same as the original challenge.
3. Add `<PhysicsFrame screen="2" referenceScreen="1" />` inside your existing
   `MapLayout` section in `mapping_settings.xml`.
4. Build and restart the map. Try the challenge on its new screen.

For a two-screen map using ordinary Worldsmith collision, the complete file is:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="native" screens="2">
    <PhysicsFrame screen="2" referenceScreen="1" />
  </MapLayout>
</MappingSettings>
```

`screen` is where the room is **now**. `referenceScreen` is where it was
**originally**. Both numbers start at 1. The original screen doesn't have to
exist in the new map.

For a `level+` map, keep its existing layout dimensions and add the same
`PhysicsFrame` line inside it. Don't replace the layout with the native example.

## Check the moved challenge

Test it at the final screen number. Moving it back to screen 1 for the test
would hide the rounding difference you're checking.

Keep a copy of the original room for comparison. Check the jumps, slope
contacts and destination reached. The setting can't compensate for a platform
or spawn moved by a pixel. A room crossing screen boundaries or using a custom
movement controller needs extra testing.

For exact movement behavior, saves and controller compatibility, see
[PhysicsFrame details](../reference/physics-frames.md).
