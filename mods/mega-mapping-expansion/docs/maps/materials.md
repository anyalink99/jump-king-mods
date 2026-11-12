# Block colors, behaviors and slopes

[Map guides](README.md)

Use this guide to give a block another RGB, combine native effects, change its
shape or place its behavior in a static or moving region. Collision colors select
gameplay; the map's artwork controls what players see.

For material definitions, create `screens/mega-mapping-expansion/materials.xml`
in the map. MME 1.8 needs
JK Runtime 2.9 and the mods that supply your source blocks. Materials work in
ordinary `level.png`, all four `level+` resolutions, XML areas and moving
platforms. Reload the map after editing collision; scene reload cannot replace it.

The file separates three choices: the source mod's color, a reusable combination
of source behaviors, and the color or region where that combination appears.
Source blocks keep their actual types and native handlers. MME doesn't replace
their behavior with a list of built-in restrictions.

The [complete configuration example](../../examples/materials/README.md) includes
paint, an XML area and a moving region using the same definition.

| I want to… | Start here |
| --- | --- |
| Put one source block under another RGB | [Make a color alias](#make-a-color-alias) |
| Give a block a triangular hitbox | [Make a sloped block](#make-a-sloped-block) |
| Combine block effects under one color | [Combine block behaviors](#combine-block-behaviors) |
| Resolve the order of effects | [Choose geometry and order](#choose-geometry-and-order) |
| Paint a large area without creating a block per pixel | [Compact painted regions](#compact-painted-regions) |
| Place a region, hole, polygon or mask through XML | [Place regions in XML](#place-regions-in-xml) |
| Give moving blocks a native effect | [Attach behavior to moving blocks](#attach-behavior-to-moving-blocks) |
| Toggle or inspect a material | [Control and inspect materials](#control-and-inspect-materials) |

## Make a color alias

This complete file makes `#FA79BE` behave like ordinary ice (`#00FFFF`):

```xml screens/mega-mapping-expansion/materials.xml
<Materials version="1">
  <Source id="ice" color="#00FFFF" sizing="cell" />
  <Material id="pink-ice" collision="source">
    <Use id="ice" source="ice" />
  </Material>
  <Paint color="#FA79BE" material="pink-ice" mode="cells" />
</Materials>
```

Paint the new color in your collision image. To use another mod's block, change
the source color to its exact RGB and keep that mod enabled. Each `Source` names
a native factory input; each `Material` defines behavior and geometry; each
`Paint` chooses where that material appears. Add more `Use` entries when you
want to combine effects.

## Make a sloped block

In the alias example above, add `shape="TopLeft"` to the `Material`:

```xml-fragment
<Material id="pink-ice" collision="source" shape="TopLeft">
  <Use id="ice" source="ice" />
</Material>
```

Keep the surrounding `Source` and `Paint` entries. The block retains its ice
effect, but only its triangular half has contact. A blocking source supplies
slope support and sliding. A trigger source keeps its trigger behavior without
becoming a solid platform. The same `shape` field also works on a material with
several source layers; `collision="oneWay"` requires a rectangle.

### Paint and test a slope

For a first test, keep a **60 by 45 pixel cell per screen**. Each collision pixel
then becomes an 8 by 8 block. You can use ordinary `level.png` with materials,
or [switch your existing grid to level+](collision.md). Keep the full grid and
screen count: a 13 by 13 grid still uses a 780 by 585 image.

Draw a floor in black. Above it, paint a reachable diagonal row of `#FA79BE`
pixels, stepping one pixel up and right each time. Build and open the map.
Check that the King lands on the diagonal ice surface and slides. Touching the
empty half shouldn't activate the source effect. For a mod block, also test its
original mechanic, such as the teleport destination.

At 240 by 180, each cell triangle is 2 by 2 game pixels; at full resolution it
is only 1 by 1. Choose the [collision scale](../reference/collision.md#other-resolutions-and-strips)
before drawing. Stretching the PNG without changing its layout won't create
larger triangles. Keep `mode="cells"` for this example: compacting rectangles
changes the bounds to which each triangle is applied.

### Choose the slope direction

| `shape` | Direction |
| --- | --- |
| `TopLeft` | Floor ramp rising to the right |
| `TopRight` | Floor ramp rising to the left |
| `BottomLeft` | Ceiling face pointing down and left |
| `BottomRight` | Ceiling face pointing down and right |

These names identify the game's slope faces, not the solid corners. MME keeps
native contact behavior: the two `Bottom` types have different response
directions but the same contact triangle. Check contact in-game before drawing
ceiling art; use the `Top` types for the floor-ramp example.

Some mods check their own rectangles or colors instead of native contact, so
their effects can bypass the clipped shape. At finer resolutions, cell-sized
foreign sources also need their provider's pixel-collision declaration. See the
[source-block contract](../reference/collision.md#slope-aliases-for-mod-blocks)
for compatibility requirements and tests.

### Use collision-shapes.xml for a slope alias

If you only need one source color with a triangular shape, the existing slope
alias file remains available. Use [MapLayout version 2](collision.md) and create
`screens/mega-mapping-expansion/collision-shapes.xml`:

```xml screens/mega-mapping-expansion/collision-shapes.xml
<CollisionShapes version="1">
  <Slope color="#FA79BE" source="#00FFFF" shape="TopLeft" />
</CollisionShapes>
```

Paint the alias in the layout's collision image and test it as above. Choose
either this alias or the material example for `#FA79BE`; don't claim the same
color in both files. Existing slope aliases don't need migration.

Use `materials.xml` when you also need multiple source effects, ordering,
inheritance, regions or activation controls. `collision-shapes.xml` supports
only the four native triangles. For discs, polygons and masks, use
[material areas](#place-regions-in-xml); their `shape` describes the area outline,
while a material's `shape` clips its source blocks.

## Combine block behaviors

Add a `Source` for each block, then reference them with `Use` entries in one
`Material`. The sources can come from the same mod or different mods. Assign a
paint color to the material to place their effects together.

For example, this combines Infinity Jump and Revoke Walking from Expansion
Blocks. Paint `#A050D0` wherever you want both effects:

```xml screens/mega-mapping-expansion/materials.xml
<Materials version="1">
  <Source id="infinity" color="#40FFFF" sizing="rectangle" />
  <Source id="revoke-walking" color="#EAEAF0" sizing="rectangle" />
  <Material id="air-jumps" collision="trigger">
    <Use id="jump" source="infinity" order="10" />
    <Use id="walk" source="revoke-walking" after="jump" />
  </Material>
  <Paint color="#A050D0" material="air-jumps" mode="connected" />
</Materials>
```

This uses the original Revoke Walking effect. In the installed Expansion Blocks
build checked on 2026-11-08, it prevents walking input on the ground and preserves
ice momentum. Infinity Jump also advertises ice and sand to native queries.
Combining them retains those semantics, including airborne behavior and sliding.
MME doesn't turn this pair into an unconditional velocity lock.

For a simple RGB alias, use one `Use` entry. Source colors refer directly to
installed factories, never recursively to another MME palette entry. If several
factories claim a source color, specify `factory="AssemblyName:Namespace.Factory"`
on `Source`; the full type name alone also works when it is unique. Missing or
ambiguous providers fail with the source ID and RGB in the error.

`Paint replace="true"` explicitly takes a color already owned by a factory.
Without it, that collision is rejected. Scene-consumed colors, platform markers
and slope aliases must remain distinct. This changes the map's palette, not the
provider's palette in other maps. Collision RGB doesn't recolor the artwork.

## Choose geometry and order

| Field | Meaning |
| --- | --- |
| Source `sizing="cell"` | One native factory call per painted cell. Foreign factories need the existing pixel-collision declaration for cells smaller than 8 game pixels. |
| Source `sizing="rectangle"` | Explicitly opt into rectangular geometry. The factory receives the authored rectangle; Runtime checks its geometry-copy contract. Use this for regions, compact paint and motion. |
| Material `collision="source"` | Preserve each source's collision policy; this is the default. |
| Material `collision="trigger"` | Preserve contacts and behavior without adding solid support. |
| Material `collision="solid"` or `oneWay` | Use source contacts as triggers and add separate support geometry. One-way support requires a rectangle and accepts downward contact from above. |
| Material `shape="rectangle"` | Default source shape. `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight` clip it to the corresponding native slope. |
| Material `extends="id"` | Inherit geometry, activation, priority and layers. A child `Use` with the same ID replaces that inherited layer. |
| Material `enabled="true"` | Initial activation of every instance. |
| Material `priority="0"` | Higher priorities resolve incompatible orders where materials overlap. Equal priorities use material ID order. |
| Use `order="0"`, `before`, `after` | Sort ascending order, then layer ID, subject to explicit dependencies. Lists accept commas or spaces. Missing dependencies and cycles are errors. |

MME applies the resulting handler order during native X/Y velocity, collision,
gravity and block-behavior phases, then restores the player's normal order even
if a callback throws. Unrelated handlers retain their slots. Overlapping
materials contribute compatible constraints; a conflicting lower-priority
constraint is skipped and reported in the log. A native handler still executes
once per native phase; SFC keeps its own callback cadence.

Ordering cannot make arbitrary Harmony patches commute. Nor can two parameter
variants sharing one native block type automatically become two independent
handlers: MME rejects that ambiguous combination. Mod authors must expose
distinct behavior types or implement composition in their provider. MME doesn't
silently drop one layer or invoke a stateful handler twice.

## Compact painted regions

`Paint mode="cells"` creates individual cells. `rectangles` merges exact row
runs; `connected` additionally gives side-connected rectangles one stable
instance identity. Diagonal contact doesn't join islands. Holes stay empty.
Compaction requires `sizing="rectangle"` on all sources.

Use connected regions for large gimmick areas. A filled 240×180 screen becomes
one rectangle per source layer, rather than 43,200 cell objects. Screen scopes
use `screens="1,3-5"`; omitting it applies the paint throughout the map. The
same RGB can mean different materials on disjoint screen scopes.

## Place regions in XML

This complete example puts native ice behavior in a region with a hole. It
requires no painted marker or decorative scene:

```xml screens/mega-mapping-expansion/materials.xml
<Materials version="1">
  <Source id="ice" color="#00FFFF" sizing="rectangle" />
  <Material id="ice-zone" collision="trigger">
    <Use id="ice" source="ice" />
  </Material>
  <Area id="room-zone" material="ice-zone" screen="1" rect="40,80,400,240">
    <Exclude rect="180,160,120,80" />
  </Area>
</Materials>
```

Coordinates are game pixels in a 480×360 screen, with Y increasing downward.
Add `Part rect="..."` children to union rectangles. `Exclude` subtracts rectangles.
For one bounding rectangle, `shape="disc"` makes an ellipse;
`shape="polygon" outline="0,0 100,0 80,60 0,100"` uses coordinates relative to
its top-left corner. Concave polygons use the even-odd fill rule.
`shape="mask" mask="props/masks/zone.png"` scales a PNG alpha mask into the
rectangle with nearest-neighbor sampling; alpha 128–255 is filled. Masks are
part of the material revision and must stay inside the map directory.

Area `mode="overlay"` adds the material throughout the region. `surfaces`
intersects it with existing blocking geometry; `empty` selects space without
blocking geometry. `replace` removes selected contacts before adding the new
material. Optional `select="#RRGGBB"` filters by observed source color.
Partial replacement splits copyable rectangular colliders. A cut through an
existing slope or nonrectangular provider collider is rejected; use `surfaces`
to overlay that shape. Areas are applied in document order.

## Attach behavior to moving blocks

Set `material="air-jumps"` on a [moving-platform Profile](moving-platforms.md), referencing the
material defined earlier. Moving materials require rectangular sources and
`collision="trigger"` on the material. The platform profile owns physical
support: `solid`, `oneWay`, or `trigger` for a moving hazard/zone that cannot
carry or push the player. Any custom platform paint color can select that profile.
Use the existing `texture` settings for its visible artwork.

The original block instances move with the platform. Their IDs stay stable;
factories aren't rerun during motion. Accepted motion substeps preserve crossed
trigger contacts until body processing. Rolled-back steps don't fire effects.

A provider that stores destinations or geometry in external registries needs a
relocation adapter. MME checks the current factory path without executing its
writes and rejects unsupported motion. JK Runtime's `MaterialBlocks.RegisterFactory`
accepts an `IMaterialBlockGeometry` implementation that owns copying and movement
of such provider state. A moving teleport entrance and a destination registered
elsewhere are different contracts; rectangular fields alone don't prove safety.

## Control and inspect materials

Scene actions `materialEnable`, `materialDisable`, `materialToggle` and
`materialReset` take `target` only. Target a material ID for all its instances,
an Area ID, a generated painted ID, or `platform-<platform-id>`. Reset restores
the authored `enabled` value. Enabling solid geometry through the player is
rejected before any selected instance changes.

Switches participate in native saves, save resets and Runtime world snapshots.
Snapshots reject a different material revision. Following worlds cannot issue
local material commands. The presentation checkbox doesn't disable collision.

Open **Mapping inspector → Materials and source layers** to inspect RGB sources,
layer order, geometry and instances. Debug mode permits toggling individual
instances. Use the scene's existing native-block contact sensors to observe
source types; moving materials keep stable contact identities.

Limits: 1,024 entries per section, 16 layers per material, 256 rectangles and holes
per area, 64 polygon vertices, 4,096 pixels per mask axis, 32,768 collision objects
per screen and 131,072 per map. Compaction and bounds are checked before activation.
The offline scene compiler validates the XML and assets; installed-provider
checks run during game loading.
