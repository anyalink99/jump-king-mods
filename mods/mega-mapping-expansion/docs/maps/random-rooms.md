# Choose a random room

[Map guides](README.md)

Make screen 1 lead to either screen 2 or screen 3. Both rooms lead back to
screen 1, so you can try the choice repeatedly.

First make sure [ordinary screen links](teleports.md) work in your map.
Choose three existing screens; the example uses **1, 2 and 3**. Keep your map's
full authored count and grid. If you need to make another room, use an empty
cell first, as shown in [Add screens](large-maps.md).

## 1. Add a choice and the returns

Save this as `screens/mega-mapping-expansion/transitions.xml`. For this test,
it replaces the links from the previous lesson:

```xml screens/mega-mapping-expansion/transitions.xml
<Transitions version="1" seed="auto">
  <Pool id="rooms" screens="2,3" mode="random" avoidLast="0" />
  <Exit screen="1" side="right" pool="rooms" />
  <SideLink screen="2" side="left" target="1" />
  <SideLink screen="3" side="left" target="1" />
</Transitions>
```

- `Pool` names the choices. Here, `rooms` contains screens `2` and `3`.
- `Exit` uses those choices when the King leaves screen 1.
- The two `SideLink` lines bring him back.

As in the two-screen lesson, block screen 1's left edge and screens 2 and 3's
right edges. With only one enabled link on a screen, both edges use it.

## 2. Build and try several crossings

Build, open the map and leave screen 1 on the right. Return and try again.
Either room can be chosen each time, so the same room can appear twice in a row.
The destination is chosen when you cross the edge.

Change `avoidLast="0"` to `avoidLast="1"` to prevent the previous room being
chosen again. **With just two rooms this makes them alternate.** Add a third
destination, such as `screens="2,3,4"`, if you want random choices without
immediate repeats; that also needs a real screen 4 and a return link for it.

## Change how the room is chosen

Change the attributes on the `Pool` line:

| I want to… | Use |
| --- | --- |
| Pick freely, allowing repeats | `mode="random" avoidLast="0"` |
| Avoid the previous room | `mode="random" avoidLast="1"` |
| Visit every room once before starting another batch | `mode="shuffle"` |
| Visit the rooms in list order | `mode="sequence" avoidLast="0"` |
| Make room 3 three times as likely as room 2 | `mode="weighted" weights="1,3" avoidLast="0"` |

Weights follow the order in `screens`. Set `avoidLast="0"` for the weighted
two-room example; preventing repeats would force alternation regardless of weights.

## Keep the same choice for an attempt

Add `refresh="attempt"` to `Pool`. MME chooses once at the start of the attempt,
and every crossing uses that choice. Loading a save keeps the saved choice.
Leave this attribute out to choose on every successful crossing.

For a switch that changes the choice, multiple links that change together,
repeatable seeds or save rules, see [all route options](../reference/transitions.md).
Worldsmith Extension's **Transitions...** window can validate the file and
**Preview 30 selections** without changing your in-game route state.

The [three-screen example folder](../../examples/transitions/three-screen/README.md)
also has files you can copy. It uses `avoidLast="1"`, so its two rooms alternate.
