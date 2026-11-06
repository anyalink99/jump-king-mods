# Color, fade, scroll and light an image

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

A material holds drawing settings that can be shared by sprites, meshes, text or receivers. Create a `material` component and put its ID in the drawing component's `material` field.

## Fade or recolor a sprite

Copy `mme-assets/panel.png` from lesson 1. This complete scene draws a blue-tinted panel at half opacity:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="panelArt" path="mme-assets/panel.png" />
  </Assets>
  <Components>
    <Component id="pose" kind="transform">
      <Value name="position" value="200,140" />
    </Component>
    <Component id="paint" kind="material">
      <Value name="color" value="#88BBFF" />
      <Value name="opacity" value="0.5" />
    </Component>
    <Component id="panel" kind="sprite">
      <Value name="transform" value="pose" />
      <Value name="asset" value="panelArt" />
      <Value name="material" value="paint" />
    </Component>
  </Components>
</MappingSettings>
```

1. Load it in your test map. Compare with lesson 1.
2. Change opacity to `1`: the image becomes fully visible.
3. Change color to `#FFFFFF`: the original colors return.

Color multiplies the image's colors. It won't paint a black pixel white. If several drawings share `paint`, changing it affects all of them. Bind `paint/opacity` to a graph for a continuous fade, or use a short effect for a pulse.

## Blending and texture channels

`blend=alpha` is normal transparent drawing. `add` adds light and can quickly wash out the image. `multiply` darkens using the source. `channel=rgba` uses the picture's colors; `channel=alpha` uses its alpha as a mask-shaped source that can be tinted. This is useful when you want a silhouette instead of the original colors.

`emission` adds an emissive contribution. It does not create a light illuminating other drawings. `ambient`, `diffuse` and `normalLight` control shading on projected surfaces. Check them first in a small [planet recipe](recipes.md) instead of adding several lights at once.

## Scroll a texture

`uvOffset` moves the sampled texture, measured in turns: `1,0` is one full width. `uvScale` changes repetition. Set `address=wrap` for repeatable scrolling; clamp holds the edge color instead. See the full [scrolling example](patterns.md#scroll-a-material).

## Add a light

A `light` needs a transform and has color, intensity, radius, cone, softness, scatter and optional `occluders`. Begin with a small intensity such as `0.05`; radius is in screen pixels. Cone is an angle in degrees. Occluders select prepared drawing contours; they don't generate game collision.

Use the [jump-flash example](behaviors.md#count-jumps-and-flash-a-light), then change one setting at a time: a larger radius spreads it farther, a narrower cone makes it directional, and softness/scatter change the spread. [Field descriptions](../reference/components.md#light) explain each setting and selection syntax.

Use [masks](masks.md) to clip drawing, and [screen views](views.md) to grade an entire screen. [Pixel policy](objects.md#keep-pixels-whole) explains point/linear sampling.
