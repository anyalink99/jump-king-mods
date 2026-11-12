# Connect two screens

[Map guides](README.md)

Make leaving the right edge of screen 1 take the King to screen 2. Then add a
return. The King enters from the opposite edge and keeps his velocity.

Choose **two existing screens** in your map. The example uses screens 1 and 2.
Leave room to arrive at the left edge of screen 2 and the right edge of screen 1.
Keep the rest of your map and its normal collision grid.

## 1. Check the screen count

Keep the map's actual screen count. For example, if your ordinary Worldsmith
map has 40 screens, its `mapping_settings.xml` can declare:

```xml mapping_settings.xml
<MappingSettings version="1">
  <MapLayout version="2" source="native" screens="40" />
</MappingSettings>
```

Use your own count in place of `40`. If `MapLayout` is already present, keep
it, including any `level+` dimensions. Don't change `screens` to `2` just because
this example connects two rooms. You don't need to switch to `level+` for links.

## 2. Add the links

In your map folder, create `screens/mega-mapping-expansion`. Inside it, create
`transitions.xml` and paste:

```xml screens/mega-mapping-expansion/transitions.xml
<Transitions version="1">
  <SideLink screen="1" side="right" target="2" />
  <SideLink screen="2" side="left" target="1" />
</Transitions>
```

Read the first line as: **from screen 1, use the right link to go to screen 2**.
The second line adds the way back. A return isn't added automatically.

## 3. Close the unused edges

**Jump King uses a single link for both edges when a screen has only one link.**
So, in this example, either edge of screen 1 would lead to screen 2.

Put a solid wall along screen 1's **left** edge and screen 2's **right** edge.
Use collision for the walls; background art won't stop the King. Leave the
intended exits open.

## 4. Build and cross both ways

Build the map and open the built copy. Leave screen 1 on the right: you should
arrive at screen 2 on the left. Leave screen 2 on the left to return.
Check that the unused edges are blocked and the arrival platforms are safe.

### Using Worldsmith Extension

In **Build / Workshop**, set **Authored screens** to your real count. Under
**Side links**, enter one line per link:

```text
1 right 2
2 left 1
```

Choose **Save layout**, then **Build checked copy**. This writes the same files
as the XML steps above.

## Give each edge a different destination

For a map with at least three screens, these two lines make screen 1 lead to
screen 2 on the left and screen 3 on the right:

```xml-fragment
<Transitions version="1">
  <SideLink screen="1" side="left" target="2" />
  <SideLink screen="1" side="right" target="3" />
</Transitions>
```

Add the return links you need. Each screen/side pair can appear only once, and
a link can't target its own screen. Screen numbers above 255 work the same way.

If an unexpected exit still works, check for an old teleport marker in the
collision image: MME keeps links you didn't replace. Remove unwanted markers
and rebuild. Leaving a link out of the XML doesn't disable an old marker.

Next: [choose a random room](random-rooms.md). For every route setting, saved
state and limits, see [the route reference](../reference/transitions.md).
