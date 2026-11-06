# Place objects and areas with pixels

[Map guides](README.md) | [Build a gimmick](../scenes/gimmicks.md)

Paint a color in the collision image, then describe what that color creates in
XML. A Paint template can create any scene component or recipe: a sensor, light,
sprite, emitter, camera, ending condition or a whole interactive object. You
don't need to enter each object's coordinates. This requires MME 1.6.0 or later.

## Choose the image

Use a MapLayout with `source="atlas"` or `source="strip"`. Its `image` selects
the collision file: `level+` by default, or `level`. For example:

```xml-fragment
<MapLayout version="2" screens="1" columns="1" rows="1"
           cellWidth="60" cellHeight="45" source="atlas" image="level" />
```

Edit `level.png` for that example. Compile it with your map so the game receives
`level.xnb`. With `image="level+"`, edit and compile `level+.png` instead. The
offline compiler accepts PNG when XNB is absent; if both exist, XNB wins, just
as it does in game. Rebuild after editing a PNG. Changing XML alone doesn't
recompile the image.

The layout controls screen order and scale. At 60×45, one pixel represents an
8×8 game area. At 480×360, it represents 1×1. Painting follows mixed-resolution
sampling rules too; see [collision layout](collision.md).

## Assign a color

Put Paint templates in `Paints` in `mapping_settings.xml` or an included
MappingFragment. The following fragment creates a transform and a sensor for
each rectangle painted `#AD6ED3`:

```xml-fragment
<Paints>
  <Paint id="zone" color="#AD6ED3" mode="rectangles">
    <Components>
      <Component id="${paint.id}" kind="transform">
        <Value name="position" value="${paint.position}" />
        <Value name="screen" value="${paint.screen}" />
      </Component>
      <Component id="${paint.id}.sensor" kind="sensor">
        <Value name="transform" value="${paint.id}" />
        <Value name="size" value="${paint.size}" />
        <Value name="initial" value="fire" />
      </Component>
    </Components>
  </Paint>
</Paints>
```

You can include Graph, Rules, Trees, Effects and Instances sections in the same
template. Use `${paint.id}` in every generated ID and its references so separate
areas have separate state. Prefer a recipe when several components work together;
pass `${paint.position}`, `${paint.screen}` and `${paint.size}` as recipe arguments.

| Setting | Meaning |
|---|---|
| `id` | Unique template name; 1–64 letters, digits, dots, hyphens or underscores |
| `color` | Exact opaque `#RRGGBB`; any RGB value can be assigned |
| `mode="pixels"` | One instance per sampled collision cell, including adjacent cells |
| `mode="rectangles"` | Default. Join identical horizontal runs vertically into filled rectangles |
| `consume="true"` | Default. These pixels are markers and don't create static blocks |
| `consume="false"` | Observe the color while retaining its ordinary collision behavior |

Use pixels for crystals and other small objects. Their recipe can give a single
marker a larger interaction area, such as 16×16. Use rectangles for drawn zones.
An L-shape or a region with a hole becomes several exact rectangles, each with
its own instance. It does **not** become one sensor covering the empty space.
Choose a filled rectangle if you need one enter/exit state for the entire zone.

Colors belong to the map. A consumed color overrides the native block meaning,
including black or red, so changing it can remove terrain. A color cannot also
be a moving-platform marker or collision-shape alias. Multiple scene templates
may use the same color, for example a sensor and its light. With `consume=false`,
the color still needs a native or installed block provider. Avoid anti-aliasing
and partial alpha.

## Available geometry values

| Token | Value |
|---|---|
| `${paint.id}` | Template name plus screen and top-left coordinates; stable while that origin stays unchanged |
| `${paint.screen}` | Screen number, starting at 1 |
| `${paint.position}` | Center, as `x,y` in screen-local game pixels |
| `${paint.corner}` | Top-left `x,y` in game pixels |
| `${paint.size}` | `width,height` in game pixels |
| `${paint.rect}` | Top-left rectangle `x,y,width,height` |
| `${paint.x}`, `${paint.y}`, `${paint.width}`, `${paint.height}` | Separate numeric values |

Templates expand before recipe compilation and cache lookup. Pixel edits that
change placement also invalidate the scene cache. Limits are 128 templates, 1,024 painted
areas and 65,536 expanded XML elements per document, plus the normal component, graph, audio and sensor limits.
An unmatched color creates no instances. Paint templates cannot nest or live
inside a Recipe definition; paint the recipe's Instances instead.

## Other map systems

Scene templates work with every scene component, including native adapters.
Use a generated sensor's events to control effects, sound or a transition reroll;
use its geometry values directly for components that accept a position or size.
Nonspatial settings such as audio volume still belong in XML.

In `screens/mega-mapping-expansion/transitions.xml` and `moving-platforms.xml`, a
Paint template with children can also repeat native entries directly. Use
`${paint.rect}` for a Platform's `rect`, or the separate x/y/width/height tokens
for a rectangular transition trigger. The existing childless moving-platform
`Paint color="..." profile="..."` syntax keeps its original meaning, including
connected shapes and travel ranges. See [moving platforms](moving-platforms.md)
and [transitions](teleports.md) for those entries' remaining fields.

Coordinate-based maps remain supported. Paint changes placement, not the
behavior or accepted fields of the components it creates. It doesn't rewrite
native prop files or ending choreography into a new language.

Try the [complete painted capture example](../../examples/painted-gimmicks/README.md),
which includes its editable collision PNG, XML and expected behavior.
