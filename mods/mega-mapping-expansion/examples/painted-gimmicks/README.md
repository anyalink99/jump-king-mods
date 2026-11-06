# Try a colored launch marker

Use this in a disposable one-screen native test map. Copy [mapping_settings.xml](mapping_settings.xml) and the editable [level+.png](level+.png) into its root. The image includes a floor and one marker. Rebuild the collision texture, then load the map with MME 1.6.0 and JK Runtime.

Touch the marker above the floor. The King stops for half a second. Hold a direction through release to launch at 420 px/s; let go to fall. The example allows all eight directions. Diagonals have the same speed as straight directions.

Paint another opaque `#AD6ED3` pixel to create another marker. Edit `delay`, `speed`, the sensor size or the light in the recipe to change all instances. Learn how [player actions](../../docs/scenes/gimmicks.md) and [painted areas](../../docs/maps/painted-areas.md) work.

This is a complete MME scene and collision source. It needs the rest of a native test map, including a valid spawn.
