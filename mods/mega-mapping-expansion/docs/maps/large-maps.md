# Add screens to your map

[Map guides](README.md)

Start with the grid you already use for `level.png` or `level+.png`. You usually
have empty cells available, so adding a screen doesn't mean resizing the image.

## 1. Check the space you already have

A 13 by 13 template has room for **169 screens**. If your map currently uses
screens 1–40, draw the next room in **cell 41**. Keep the cell size and grid
dimensions unchanged. Add that room's normal background and other map art too.

Number cells down a column, then start the next column. See the
[grid example](README.md#your-usual-levelpng-grid) if you need to locate a number.

## 2. Update the authored count

If you declared the count in `mapping_settings.xml`, change `screens="40"` to
`screens="41"`. Keep the other layout settings and any scenes.

For ordinary Worldsmith collision, the complete layout example is:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="native" screens="41" />
</MappingSettings>
```

For a 13 by 13 `level+` grid at ordinary block size, use:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="atlas" screens="41" columns="13" rows="13" cellWidth="60" cellHeight="45" />
</MappingSettings>
```

Keep your own grid and cell size if they differ. Without an explicit MME count,
keep using your editor's ordinary screen-count workflow. Declaring more screens
doesn't create their collision or art.

## 3. Build and visit the new room

Build and open the map. Test the new room and the rooms around it. Ordinary
up/down travel follows screen numbers. Drawing two cells beside each other in
the image doesn't add a sideways connection; use [screen links](teleports.md)
for that.

In Worldsmith Extension, update **Authored screens** under **Build / Workshop**.
For `level+`, keep columns, rows and pixels per screen as they were. Choose
**Save layout**, then **Build checked copy**.

## When the grid is full

You can choose a larger square from Zebra's generator, but **copy rooms by
screen number**. Increasing both grid dimensions changes where later columns
start. In a 13-row grid, screen 14 is at the top of column 2. In a 14-row grid,
screen 14 is at the bottom of column 1. Simply pasting the old image into the
top-left corner of the new square would change room numbers.

With `level+` you can instead add columns while keeping the same row count.
For example, expanding from 13 by 13 to **14 columns and 13 rows** keeps the
existing room positions and adds space for 13 more screens.

1. Expand the canvas to **840 by 585** for 60 by 45 cells. Don't stretch the rooms.
2. Keep the old image at the top left and draw the new cells in the added column.
3. Change `columns` to `14` and leave `rows="13"` and the cell size unchanged.
4. Set `screens` to the new actual count, up to `182` for this grid.
5. Build and check the new column and its links.

For finer cells, use `columns × cellWidth` by `rows × cellHeight` for the canvas
size. The format supports up to 4096 authored screens, subject to
[collision limits](../reference/collision.md#limits-and-troubleshooting).
See [layout options](../reference/map-layout.md) for vertical strips and exact fields.
