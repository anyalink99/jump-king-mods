# Large maps and collision layouts

[Handbook](../index.md) / [Geometry and navigation](index.md)

Screen count, collision packing and visual art are separate choices. MME can declare up to 4096 actual screens and load a finer collision grid, but a larger declared count doesn't create background art, safe arrivals or a navigable route.

## Choose native, atlas or strip

| Source | Use |
|---|---|
| native | Keep the stock collision loader; explicitly declare actual screen count |
| atlas | Use MME pixel collision packed into columns and rows |
| strip | Use MME pixel collision in one vertical strip |

For native source, compiled dimensions provide capacity. The actual screens count must fit it. For a pixel source, declare columns, rows, cellWidth and cellHeight. Atlas dimensions must equal columns Г— cellWidth by rows Г— cellHeight. Unused capacity is padding, not more screens.

This complete entry declares a native two-screen map:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="native" screens="2" />
</MappingSettings>
```

This entry declares six screens in a two-column, three-row pixel atlas. It needs a 120 by 135 level+ image before the map can run:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="atlas" screens="6" columns="2" rows="3" cellWidth="60" cellHeight="45" />
</MappingSettings>
```

Atlases number screens down a column, then move to the next column. In that example the left column contains screens 1вЂ“3 from top to bottom; the right contains 4вЂ“6. Strips put screen 1 at the bottom. Don't flip art automatically without checking which source ordering you're using.

## Pick collision resolution

| Logical collision pixels per screen | In-game size of one pixel |
|---|---|
| 60 Г— 45 | 8 Г— 8 pixels |
| 120 Г— 90 | 4 Г— 4 pixels |
| 240 Г— 180 | 2 Г— 2 pixels |
| 480 Г— 360 | 1 Г— 1 pixel |

The visual screen remains 480 by 360. Choose a finer grid only where it helps collision. Mod factories must opt into finer pixel collision; otherwise the loader rejects unsupported use rather than assuming their private block code scales safely. [Collision](collision.md#custom-block-contract) gives that author contract.

Per-screen Resolution overrides allow coarser logical grids within a finer atlas cell. Nearest-neighbor enlarge the coarse source to fill its cell; every coarse pixel group must have one color. Keep native red slope scale in mind: enlarging red pixels without the matching logical resolution turns them into many tiny slopes. The collision chapter has a complete mixed-resolution example.

## Build and navigate

Keep visual art split into native per-screen assets. CPU collision loading avoids using a GPU texture for the whole collision image, but visual textures and captures still have resource limits. A 480-resolution map with thousands of screens can have substantial CPU work and memory; measure representative screens and preparation rather than inferring performance from screen count alone.

Use integer [side links](teleports.md) for destinations above the range of native color encodings. Links don't create a clear arrival area or a reverse route. Compile layout/collision/links together, then restart the map after geometry changes. Presentation reload doesn't replace those systems.

Choose explicit authored counts over atlas capacity, check the last real screen and a padding cell, and test both sides of every unusual link. Build success validates the package format; it doesn't certify the route.
