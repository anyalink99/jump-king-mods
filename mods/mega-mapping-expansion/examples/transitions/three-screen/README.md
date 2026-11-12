# A small random route

Use an existing native map with three screens, open side edges and clear arrivals. Copy this folder's entry and screens folder into its root. Keep your own MapLayout instead if the map uses a different source/count.

1. Build and launch with MME and Runtime.
2. Leave screen 1 through an open side: it chooses screen 2 or 3.
3. Leave that room to return to screen 1. On the next selection, avoidLast=1 chooses the other room.

One enabled link serves both edges; close unwanted exits with collision. The fixed seed reproduces selections for the same definition and call order. This example adds routes, not collision or artwork.

[Routing guide](../../../docs/maps/teleports.md) | [Other examples](../README.md)
