# Composed native materials

Copy the two files under `screens/mega-mapping-expansion` into a test map.
Players need Expansion Blocks, MME 1.8 and JK Runtime 2.9. These are configuration
examples, not a complete playable map or a change to an existing challenge.

The example combines Infinity Jump and Revoke Walking in three forms:

- Paint `#A050D0` in the map's collision image. Connected pixels form one region.
- `jump-room` adds an XML region with a rectangular hole on screen 1.
- `moving-zone` moves the same behavior along a horizontal route. It has no
  artwork and doesn't push or carry the player. Add a platform texture if needed.

All three use the same `air-jumps` definition and the original provider handlers.
Revoke Walking retains its native grounded-input and ice-momentum rules.
Remove the forms you don't need. Merge these files with existing settings rather
than overwriting a map's other materials or platform routes.

See the [material guide](../../docs/maps/materials.md) for geometry, ordering,
source selection, compatibility, actions and save behavior.
