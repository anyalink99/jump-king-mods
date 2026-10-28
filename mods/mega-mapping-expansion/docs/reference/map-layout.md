# MapLayout fields and screen order

[Handbook](../README.md) / [Reference](README.md)

For a first example, use [your existing grid with level+](../maps/collision.md) or
[Add screens](../maps/large-maps.md). Put `MapLayout` inside `MappingSettings`
in the map's `mapping_settings.xml`. It doesn't create backgrounds or other map art.

## Choose the source

| `source` | Collision comes from | Required fields besides `source` |
| --- | --- | --- |
| `native` | The ordinary compiled map collision | `version="2"`, `screens` |
| `atlas` | `level+`, arranged in columns and rows | `version="2"`, `screens`, `columns`, `rows`, `cellWidth`, `cellHeight` |
| `strip` | `level+`, arranged in one vertical column | Same as atlas; `columns="1"` and `rows` equal to `screens` |

`screens` is the number of playable screens, from 1 to 4096. For native collision
it must fit the compiled image. For atlas collision it must fit `columns × rows`.
Extra cells are padding and can't be link destinations. Both grid dimensions
are integers from 1 to 4096, subject to the image memory limit.

## Image size and order

An atlas uses the same column-first numbering as Zebra's `level.png` template.
You can keep its 60 by 45 cells. A 13 by 13 grid is 780 by 585, with screen 1
at the top left and screen 14 at the top of the second column. Changing to
`level+` doesn't require a new screen arrangement or a one-screen image.

The PNG is exactly `columns × cellWidth` pixels wide and `rows × cellHeight`
pixels high. A rectangular grid is also supported. For two columns and three rows:

| First column | Second column |
| --- | --- |
| 1 | 4 |
| 2 | 5 |
| 3 | 6 |

A strip uses the opposite vertical order: **screen 1 is at the bottom**.
The file is still `level+.png`. For two full-resolution screens in a strip,
create a **480 by 720** image with screen 2 above screen 1:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="strip" screens="2" columns="1" rows="2" cellWidth="480" cellHeight="360" />
</MappingSettings>
```

The game screen is always 480 by 360. Choose how many collision pixels represent it:

| `cellWidth` | `cellHeight` | Size of one collision pixel in game |
| --- | --- | --- |
| 60 | 45 | 8 by 8 |
| 120 | 90 | 4 by 4 |
| 240 | 180 | 2 by 2 |
| 480 | 360 | 1 by 1 |

These are the only supported pairs. Increasing the cell size keeps the screen
order and grid dimensions; resize the whole image with nearest-neighbour
scaling and update both cell fields. See [the detail guide](../maps/finer-collision.md)
for full-image dimensions and preserving old red slopes.

Finer collision for another mod's blocks
requires that provider to opt in; see [custom blocks](collision.md#custom-block-contract).

## Per-screen settings

`Resolution` gives a screen a coarser grid without changing its space in the
image. It is only available for atlas and strip sources. See the
[mixed-resolution example](collision.md#other-resolutions-and-strips) for image preparation.

`PhysicsFrame` preserves an imported room's original rounding frame. It works
with all three sources. See [Move a room](../maps/physics-frames.md).

Static `SideLink` entries are also accepted inside `MapLayout`, but use
`screens/mega-mapping-expansion/transitions.xml` for new routes. It keeps static
and random links in one file. Don't define the same screen/side pair twice.

## Build and size limits

Keep normal per-screen art in the map's usual files. MME reads collision on the
CPU, so a large collision image doesn't require one huge GPU texture. Visual
assets still have their normal texture limits.

Collision is limited to **512 MiB of decoded RGBA pixels**, before blocks and
other map resources. A full-resolution 13 by 13 atlas alone uses about 111.4 MiB.
See [collision limits](collision.md#limits-and-troubleshooting) for rectangle
budgets and XNB settings. Measure loading time and memory before making a large
map at full resolution.

After rebuilding, restart the map and check its last playable screen. Scene
reload doesn't reload layout, collision or routes.
