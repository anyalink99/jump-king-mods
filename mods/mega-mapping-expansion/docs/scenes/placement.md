# Place, resize and group images

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

Start with [lesson 1](../getting-started/first-scene.md). An Asset names the image file, a transform holds its placement, and a sprite draws it. Reuse that structure for signs, decorations, overlays and animated props.

## Position and screen

Open the transform connected to your sprite. Set `position` to `X,Y` in a 480 by 360 screen. Set `screen` to the one-based screen number. `screen=0` repeats the same local drawing on every visible screen; it does not turn the whole map into a single huge canvas.

For example, change `panelPose/position` from `240,160` to `180,100`, save and [reload or rebuild](../getting-started/worldsmith.md). The sprite moves 60 pixels left and up.

## Choose an anchor

| Object | Where the transform position is placed |
|---|---|
| Sprite | Image top left by default; `origin` moves the anchor within the image |
| Grid/disc mesh | Center of the geometry |
| Box/disc sensor | Center of the area |
| Text | Horizontal anchor selected by `align`; text height is laid out around its anchor |
| Receiver or polygon | Authored local outline relative to its transform |

For a 64 by 32 sprite, `origin=32,16` puts its center at the transform position. Rotation and scale then happen around that point. Check the actual dimensions of your image before using those numbers.

## Resize and rotate

Add `scale` to the transform: `1,1` is original size, `2,2` doubles it, and `-1,1` mirrors it horizontally. `rotation` uses degrees. With a top-left origin, a rotated sprite swings around its corner; use a centered origin if you want it to rotate in place.

The default `pixelSnap=true` rounds final geometry to game pixels and uses point sampling. Calculations can still use fractions. For intentionally smooth drawing, set `pixelSnap=false` and choose `sampling=linear` on its material. See [pixel policy](objects.md#keep-pixels-whole).

## Draw in front or behind

Set the drawing component's `layer`. The available scene layers are `background`, `world`, `surface`, `foreground`, `composite` and, on sprite/text, `hud`. Within one layer, higher `z` draws later; IDs settle ties. Check [the component reference](../reference/components.md) for layers accepted by each kind.

| Layer | Place in the native drawing sequence |
|---|---|
| background | Before native midground drawing |
| world | After the native screen drawing pass |
| surface | Before native foreground drawing, after world capture |
| foreground | After native foreground drawing |
| composite | After foreground capture, for final scene overlays |
| hud | After the native in-game overlay items |

Native scenery participates at its game render stages. Start with `foreground` when your test image is hidden by ordinary map art, then choose the intended layer deliberately. Moving an image between layers changes drawing order, not collision.

## Move several images together

Create a transform for the group. Give each child's transform a `parent` pointing to that group ID. Child positions become offsets from the parent. Here two panels move with one anchor:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="panelArt" path="mme-assets/panel.png" />
  </Assets>
  <Components>
    <Component id="group" kind="transform">
      <Value name="position" value="240,180" />
    </Component>
    <Component id="leftPose" kind="transform">
      <Value name="parent" value="group" />
      <Value name="position" value="-80,0" />
    </Component>
    <Component id="rightPose" kind="transform">
      <Value name="parent" value="group" />
      <Value name="position" value="40,0" />
    </Component>
    <Component id="left" kind="sprite">
      <Value name="transform" value="leftPose" />
      <Value name="asset" value="panelArt" />
    </Component>
    <Component id="right" kind="sprite">
      <Value name="transform" value="rightPose" />
      <Value name="asset" value="panelArt" />
    </Component>
  </Components>
</MappingSettings>
```

Copy `mme-assets/panel.png` from lesson 1. Launch on screen 1: two panels should appear. Change `group/position`, then its rotation, and rebuild. Both children should move together. Parents must be transforms, with no cycles; a chain is limited to 64 levels.

For several copies of a more involved object, use [recipes](recipes.md). For a value that moves a transform during play, use [a binding](behaviors.md).
