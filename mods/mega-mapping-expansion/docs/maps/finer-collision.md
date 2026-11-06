# Make smaller platforms

[Map guides](README.md)

First [switch your existing grid to level+](collision.md) at its normal 60 by 45
size per screen. Once that works, you can increase the detail. The number and
order of screens stay the same.

## 1. Choose a block size

For a 13 by 13 grid:

| Pixels per screen | Whole PNG size | One collision pixel becomes |
| --- | --- | --- |
| 60 by 45 | 780 by 585 | 8 by 8 game pixels |
| 120 by 90 | 1560 by 1170 | 4 by 4 game pixels |
| 240 by 180 | 3120 by 2340 | 2 by 2 game pixels |
| 480 by 360 | 6240 by 4680 | 1 by 1 game pixel |

For this example, choose **120 by 90 per screen**. It lets you make a platform
4 pixels thick without starting with the largest image.

## 2. Enlarge the whole collision image

Resize `level+.png` from **780 by 585** to **1560 by 1170**, using
**nearest-neighbour** scaling. That doubles both dimensions. Each old pixel
becomes a 2 by 2 square of the same colour; don't use smoothing.

The grid still has 13 columns and 13 rows. Screen 1 is still at the top left,
screen 14 starts the second column, and each cell now measures 120 by 90.

## 3. Update the cell size

Keep your screen count, columns and rows. Change only `cellWidth` and
`cellHeight`. For the 40-screen example:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="atlas" screens="40" columns="13" rows="13" cellWidth="120" cellHeight="90" />
</MappingSettings>
```

### Keep old red slopes at their original size

Black rectangular platforms keep their outlines after nearest-neighbour
enlargement. Native red slopes need another step: a 2 by 2 red patch would
become four small triangles instead of one old 8 by 8 triangle.

For each screen whose old slopes you want to keep, add a `Resolution` entry
inside `MapLayout`. This example keeps screen 7 at the original scale:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="atlas" screens="40" columns="13" rows="13" cellWidth="120" cellHeight="90">
    <Resolution screen="7" width="60" height="45" />
  </MapLayout>
</MappingSettings>
```

Screen 7 still occupies 120 by 90 pixels in the PNG, but MME reads each uniform
2 by 2 group as one old pixel. Keep the whole screen on that coarse grid.
Don't add fine details there unless you remove the override and deliberately
redraw its slopes for the finer scale. Other screens use 4 by 4 game-pixel blocks.

## 4. Draw a thinner ledge and test

On a screen without a resolution override, draw a reachable black ledge just
**one image pixel high**. In this example that makes a platform 4 game pixels
thick. Add the matching visible art in your normal map layers.

Build, restart the map and jump onto the ledge. Check your preserved old slopes
too. A finer collision image doesn't automatically add finer visible artwork.

In Worldsmith Extension, set pixels per screen to `120x90` in **Build / Workshop**.
Keep the grid and authored count. If needed, put `7 60x45` under **Optional screen
resolutions**, then save and build.

For a different grid, multiply its columns and rows by your chosen cell size.
Use the same method for the 2-pixel or 1-pixel options. Another mod's blocks
must support the finer scale; see [custom blocks](../reference/collision.md#custom-block-contract).
