# Optional one-screen collision test

For an existing multi-screen map, follow [Switch your existing grid to level+](../../../docs/maps/collision.md)
and keep the whole grid. This sample is only for a separate disposable test.

Copy `level+.png` and `mapping_settings.xml` into a working one-screen map,
beside `level_settings.xml`. Keep the map's other files and put the spawn above
the floor. If it already has MME scenes, add only `MapLayout` to the existing
settings file.

The image has a black floor at Y 328–359 and a ledge at X 200–279, Y 272–279.
Empty space is fully transparent. It contains collision only; draw visible
platform art in your normal layers.

Build and open the test map, then try the ledge. For the reason behind these
cell dimensions, see [Increase collision detail](../../../docs/maps/finer-collision.md).
This folder adds collision to your map; it isn't a standalone playable level.
