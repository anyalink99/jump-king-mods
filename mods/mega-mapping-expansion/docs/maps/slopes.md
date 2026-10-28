# Make a sloped block

[Map guides](README.md)

Give a block a triangular shape while keeping its usual effect. For example,
you can make a triangular ice block. The solid half works as a slope; the other
half is empty.

Use [your existing grid with level+](collision.md), at 60 by 45 pixels per cell.
Keep its full grid and screen count. This example uses ordinary ice, so it
doesn't need another block mod.

## 1. Name a colour for the shaped ice

Create `screens/mega-mapping-expansion/collision-shapes.xml` in your map:

```xml screens/mega-mapping-expansion/collision-shapes.xml
<CollisionShapes version="1">
  <Slope color="#FA79BE" source="#00FFFF" shape="TopLeft" />
</CollisionShapes>
```

`source="#00FFFF"` means ice. `color="#FA79BE"` is the new colour you'll paint
for this particular shape. Choose a colour that isn't already used by another
block. `shape="TopLeft"` makes a floor slope that rises to the right. The names
come from the game's slope types; they name the face, not the solid corner.

## 2. Paint and test a slope

Choose a reachable spot in one screen's **60 by 45 cell**. Each pixel in that
cell becomes an 8 by 8 block in the game. A 13 by 13 grid still uses a 780 by 585
image; don't crop it down to the cell you're editing.

Draw a floor in black. Above it, paint a reachable diagonal row of `#FA79BE`
pixels, stepping one pixel up and right each time. Build and open the map.
Check that the King can land on the diagonal ice surface and slide. Touching
the empty half of a cell shouldn't activate it.

At full resolution each triangle would be only one pixel across. Choose the
collision scale before drawing the slope; stretching the PNG without changing
the layout won't create larger triangles.

## Choose the slope direction

| `shape` | Direction |
| --- | --- |
| `TopLeft` | Floor ramp rising to the right |
| `TopRight` | Floor ramp rising to the left |
| `BottomLeft` | Ceiling face pointing down and left |
| `BottomRight` | Ceiling face pointing down and right; see the native contact caveat below |

MME keeps the game's native slope behavior. The two `Bottom` types have different
response directions but the same native contact triangle; check contact in-game
before drawing ceiling art. Use the `Top` types for the floor-ramp example.
The file doesn't support custom circles or polygons.

## Use a block from another mod

Replace `source` with that block's exact collision colour and keep its mod
enabled. Try both halves of the block and its original effect, such as a
teleport. A trigger block keeps its trigger behavior; shaping it doesn't turn
it into a solid platform.

Some block mods use their own contact checks and need compatibility work. If
the block is rejected at a finer collision size, try the ordinary 60 by 45
scale and check with its author. The [block reference](../reference/collision.md#slope-aliases-for-mod-blocks)
describes supported providers and the C# contract.
