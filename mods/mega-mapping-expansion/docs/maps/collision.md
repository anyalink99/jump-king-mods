# Switch your existing grid to level+

[Map guides](README.md)

Keep the screen grid you already drew. Switching to `level+` changes which
image MME reads for collision; it doesn't require starting with a single screen
or moving your rooms around.

If you only want scenes, sound or screen links, keep `level.png` and skip this
conversion. Use `level+` for MME's collision options, such as finer platforms
or shaped mod blocks.

This example uses a **13 by 13 grid with 40 authored screens**. Use your own
grid dimensions and screen count when they differ.

## 1. Copy the collision image

Make a copy of `level.png` named `level+.png` beside `level_settings.xml`.
For this grid it stays **780 by 585 pixels**, with the same 60 by 45 cells and
the same screen numbers. Don't resize it yet.

Clean up any template guides in the copy:

- Remove coloured borders and screen numbers that were only drawing guides.
- Use fully transparent pixels or opaque magenta (`#FF00FF`) for empty space.
- Keep real collision colours, including intentional grey fake-block markers
  used beside slopes. Grey `#808080` has a native meaning; it isn't a colour to
  erase automatically across a finished map.
- Use exact opaque colours for blocks. Turn off antialiasing and avoid partly
  transparent pixels.

The template's grey background, dark green/blue borders and labels aren't a
ready-to-play `level+` image. Keep a guide layer in your editor, then hide it
when exporting collision. See [the colour table](../reference/collision.md#colours-and-materials)
if you're unsure which pixels are gameplay markers.

## 2. Add the layout

Save this as `mapping_settings.xml` in the same folder:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="atlas" screens="40" columns="13" rows="13" cellWidth="60" cellHeight="45" />
</MappingSettings>
```

Here is what to change for your own map:

| Setting | Put this here |
| --- | --- |
| `screens` | Your actual screen count, not the number of spare cells |
| `columns` and `rows` | The number of columns and rows in the PNG |
| `cellWidth` and `cellHeight` | `60` and `45` to keep ordinary 8 by 8 blocks |

`source="atlas"` means a grid numbered down each column, just like the template.
Keep both version numbers as shown.

If `mapping_settings.xml` already has scenes, keep them and add `MapLayout`
inside its `MappingSettings` section. Replace an existing `MapLayout` rather
than adding a second one. [XML basics](../reference/xml.md) shows the structure.

## 3. Build and compare

Build the map, open the built copy in Jump King and try a familiar platform on
screen 1. Check another column too, such as screen 14 in the example grid.
The same platforms should still be in the same rooms at the same size.
**Renaming the image hasn't made the collision finer.**

Keep the ordinary Worldsmith files in the project. For this layout MME reads
`level+` for collision, so maintain your collision edits there. Your normal map
art still decides what the platforms look like.

### Using Worldsmith Extension

In **Build / Workshop**, choose **level+ atlas** under **Authoritative collision
image**. For this example, set authored screens to `40`, columns and rows to
`13`, and pixels per screen to `60x45`. Choose **Save layout**, then **Build checked
copy** or **Test in-game**. Saving the layout doesn't resize your image.

### If it doesn't work

| What happens | What to check |
| --- | --- |
| MME reports an unknown colour | Check the reported pixel for leftover template borders, numbers or an incorrect block colour. |
| MME reports invalid alpha | Export exact opaque block colours and fully transparent empty space; disable antialiasing. |
| Rooms appear under different numbers | Check `rows` matches the image grid. Numbering runs down columns. |
| Old collision still appears | Check the built copy contains the new XML and compiled `level+.xnb`, and both required mods are loaded. |

Next: [make smaller platforms](finer-collision.md), [shape a mod block](slopes.md),
or [add screens](large-maps.md).

For a separate disposable test, the [one-screen sample](../../examples/maps/one-screen/README.md)
is also available. You don't need it to convert an existing map.
