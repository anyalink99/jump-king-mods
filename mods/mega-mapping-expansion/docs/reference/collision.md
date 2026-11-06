# Collision colours, resolutions and custom blocks

[Handbook](../README.md) / [Reference](README.md)

To reuse your collision image, follow [Switch your existing grid to level+](../maps/collision.md).
For smaller blocks, follow [Increase collision detail](../maps/finer-collision.md).
This page covers exact colours, mixed resolutions, slope aliases, block-provider
requirements and file limits. [MapLayout fields](map-layout.md) describe image size and screen order.

## Colours and materials

Use the game's exact collision colours:

| Colour | Meaning |
| --- | --- |
| `#000000` | Solid |
| `#FF0000` | Native slope cell |
| `#00FFFF` | Ice |
| `#FFFF00` | Snow |
| `#FF6A00` | Sand |
| `#FFFFFF` | No-wind area |
| `#00AAAA` | Water |
| `#B6FF00` | Quark |
| `#808080` | Fake block marker; affects slope orientation |
| Fully transparent or `#FF00FF` | Empty |

Native green wind codes and blue side-teleport codes keep their usual meanings.
Registered block factories take precedence over these marker codes, as in the
game. Unknown opaque colours cause an error with their screen and coordinates.
Partial alpha is rejected. The numbered borders in Zebra's drawing template
aren't a separate metadata channel; hide that guide layer when exporting
`level+`. Keep intentional native fake-block markers where your slopes need them.
Worldsmith turns opaque magenta into transparency;
MME and Worldsmith Extension use that same empty-space rule.

Red is a triangle at the **screen's logical scale**, with orientation inferred
from solid neighbours as in the game. When enlarging old slope artwork, keep a
`60x45` resolution override for that screen. Without that override, an 8 by 8
patch of red on a full-resolution screen becomes many small slope cells. For
new pixel outlines, paint stair steps in black, or author the smaller red slope
cells and their neighbours deliberately.

## Other resolutions and strips

Choose the size of each cell in the image:

| Pixels per screen | Size of one collision pixel in game |
| --- | --- |
| 60 by 45 | 8 by 8 |
| 120 by 90 | 4 by 4 |
| 240 by 180 | 2 by 2 |
| 480 by 360 | 1 by 1 |

Use the matching `cellWidth` and `cellHeight`. An atlas image must be exactly
`columns * cellWidth` by `rows * cellHeight`. By default, each screen uses that
resolution. You can give individual screens a coarser logical grid without
changing the image's packing:

<!-- mme-example:start -->
`mapping_settings.xml`:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" screens="169" columns="13" rows="13" cellWidth="480" cellHeight="360" source="atlas">
    <Resolution screen="1" width="60" height="45" />
    <Resolution screen="2" width="120" height="90" />
  </MapLayout>
</MappingSettings>
```
<!-- mme-example:end -->

Screen 1 still occupies 480 by 360 pixels in the PNG. Enlarge its original
60 by 45 artwork **8 times with nearest-neighbour scaling** and place it in that
cell. Each 8 by 8 pixel group must have one colour; the build rejects groups
with conflicting pixels. Its blocks and red slopes retain their 8 by 8 size.
Screen 2 uses 4 by 4 groups; other screens use individual pixels. This lets old
and detailed screens share one PNG. An override can't be finer than the image
cell. In Extension, enter `1 60x45` or `2 120x90` in **Optional screen resolutions**.

For a single vertical strip, set `source="strip"`, `columns="1"` and
set `rows` to the same number as `screens`. Its width is `cellWidth` and its height is
`screens * cellHeight`. **Screen 1 is at the bottom**, matching the traditional
`visual_level.png` workflow. The file is still named `level+.png`.

## Slope aliases for mod blocks

MME can clip a native or mod block to any of the four native
slope shapes. This belongs to Mapping's collision loader and remains active
when Mapping's visual checkbox is off. Keep the source block's mod installed.

Use MapLayout version 2 and create
`screens/mega-mapping-expansion/collision-shapes.xml`:

```xml-fragment
<CollisionShapes version="1">
  <Slope color="#FA79BE" source="#28264B" shape="TopLeft" />
  <Slope color="#FA7ABE" source="#28264B" shape="TopRight" />
  <Slope color="#FA7BBE" source="#28264B" shape="BottomLeft" />
  <Slope color="#FA7CBE" source="#28264B" shape="BottomRight" />
</CollisionShapes>
```

Paint the alias `color` in `level+.png`. `source` is the original opaque RGB of
the block you want to shape. Both use `#RRGGBB`. Alias colors must be unique,
different from their source, and unclaimed by other loaded block factories.
Aliases cannot reference other aliases. Up to 1024 aliases are supported.

The example uses JK+'s WarpBlock at RGB `(40,38,75)`, which targets local
position `(320,304)` and preserves velocity. The four alias colors are map-local
choices, not a palette owned by MME. For another block,
choose free alias colors and its exact source RGB, including encoded destination
data. Source factories still receive the original color, and their own texture
queries see original source colors during construction.

Mapping keeps the factory-produced object and its concrete type. Its existing
body behavior, destination and material data remain available. Only triangle
contact counts; the empty half doesn't activate the block. A blocking source
also gets native slope collision and sliding. A nonblocking trigger gets no
physical support. Changes in the source block's blocking policy still apply.
Ordinary unshaped instances keep their original collision.

The source factory must create a reference-type `IBlock` whose contact method
can be patched. Metadata-only pixels and factories returning null cannot be
shaped. Nonrectangular source contacts are clipped, not expanded. Higher
resolution still requires the source factory's pixel-collision declaration.
Mapping never registers the alias as a different foreign block type.

This preserves mechanics that consume the block's `IBlock.Intersects` results.
A mod that tests `GetRect()` directly, scans colors independently or implements
its own contact loop can bypass the clipped shape. Check that provider before
promising equivalent behavior. Changing shape isn't a replacement behavior API.

The installed-provider check `verify-warp-shapes.ps1` exercises JK+'s real factory
and warp handler with all four shapes, empty halves, two screens, preserved
velocity and native sound/mute handling. It doesn't certify unrelated providers.

### Custom hitboxes and source behavior

Three things are authored separately: the artwork, the block's source
collision and Mapping's shape mask. Drawing a triangle doesn't create one.
The alias keeps only contact shared by the original block and its selected
native triangle. It can't fill an empty part of the source hitbox or enlarge
the source's returned contact.

The current XML mask vocabulary is exactly `TopLeft`, `TopRight`, `BottomLeft`
and `BottomRight`, using Jump King's `SlopeType` names. These are explicit
orientations; they don't use neighboring red cells to guess a slope direction.
The names describe the slope face, not the solid corner: `TopLeft` is a floor
ramp rising to the right, and `TopRight` rises to the left. MME preserves native
contact behavior. The game's two `Bottom` types have different response normals
but the same contact triangle, so don't assume their collision halves are mirrors.
The cell's logical rectangle comes from MapLayout and any screen resolution
override. There is no arbitrary polygon or circle syntax in this file.

For another custom hitbox, implement that shape in the source mod's
`IBlock.Intersects` method and register its `IBlockFactory` normally. Respect
the rectangle supplied by the loader. A positive query must return the actual
intersection and its normal blocking policy; a bounding rectangle alone can't
represent a hollow or disconnected shape accurately. Mapping can then clip
that existing contact with a native triangle if the map needs it.

On aliased blocking blocks, Mapping retains the original concrete instance for
typed body behaviors and adds a native slope helper for physical support and
sliding. Contact with the original instance is nonblocking after clipping, so
it doesn't also create a rectangular floor. The helper reads the source's
original blocking policy before supplying support. If the source is a trigger,
both its identity and trigger behavior remain available without a solid slope.

Test both halves of the cell with the King, including the diagonal boundary.
Also check the source mechanic: teleport destination, damage, surface effects,
conditional blocking and any code that tests a concrete block type. Use the
same declared resolution for collision and collision-derived platform art.

## More Block Sizes and custom blocks

MME's pixel loader activates only for an explicit version 2 pixel layout. Native source=native uses the stock loader. Foreign More Block Sizes maps keep that mod's own collision contract; MME doesn't convert sizes.png or provide its removed map.xml reader.

With source=atlas or strip, MME reads level+.xnb on the CPU. More Block Sizes' size loader is bypassed for that explicit format. Both mods can coexist, but their formats remain separate.

## Custom block contract

Custom factories work at 60 by 45 through their existing registration. At finer
resolutions, the factory's author must explicitly opt in through
`MegaMappingApi.dll` before native screen loading:

```csharp
MegaMappingExpansion.Api.PixelCollisionFactories.Register(typeof(MyBlockFactory));
```

Register the factory with `LevelManager` normally as well. Opting in promises
that the factory respects the supplied rectangle, uses sampler coordinates
at the declared resolution and doesn't assume an 8-pixel block or 60 by 45 grid.
Factories receive one source cell at a time and aren't merged by Mapping.
Neighbour reads through `LevelTexture.GetColor` and `LevelManager.IsBlockEdge`
use the new layout. Static `LevelTexture.GetCoord` retains its native contract;
use the sampler instance instead. Don't opt in an unmodified foreign factory
on its author's behalf.

## Limits and troubleshooting

- At most **4096 authored screens** and **512 MiB of decoded collision pixels**.
  A 13 by 13 full-resolution atlas uses about 111.4 MiB; 20 by 13 uses about
  171.4 MiB. Native blocks, editor previews and other assets use additional memory.
- At most **32768 generated rectangles per screen** and **131072 per map**.
  Adjacent cells of the same native rectangular material are merged. Slopes and
  custom blocks stay separate. Avoid checkerboards of isolated pixels.
- Compile an **uncompressed Windows XNB v5**, `SurfaceFormat.Color`, one mip.
  These are ordinary Worldsmith's default output settings. Compressed, mipmapped,
  truncated or mismatched collision files are rejected with a rebuild message.
- GPU texture dimension limits don't apply to Mapping's collision reader.
  They still apply to backgrounds, props and other visual assets.
- Restart or reload the map after changing collision or layout. Scene hot reload
  doesn't replace native geometry. Keep a backup when converting an old map;
  `sizes.png` is never automatically baked into the new image.
- Use Replays 2.7.1+ and Run Verifier 1.3.3+ when recording or verifying these maps;
  their revision fingerprints include alternate collision and layout metadata.
- Runtime's offline map library reports version 2 collision as unindexed. The
  loaded-world inspector and native geometry queries use the actual blocks.

Build checks verify files and formats. Playtest movement and modded materials
separately; a successful build doesn't prove route completion.
