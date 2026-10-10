# Screen teleports (side links)

[Handbook](authoring.md) · [Worldsmith](worldsmith.md) · [Large maps](large-maps.md)

Side links connect the left and right edges of screens. The King leaves one
screen and enters the target screen from the opposite edge, keeping his velocity.
They work on small maps too.

Set them in `props/mega-mapping-expansion/map.xml`, or use the **Side links**
field in Worldsmith Extension. Mega Mapping Expansion applies them to the game's
native teleport links when an attempt starts. The game handles the crossing.

## How the two sides work

Each screen has two link slots: left and right. What happens at an edge depends
on how many slots are enabled after MME applies `map.xml`:

| Enabled links on the screen | Left exit | Right exit |
| --- | --- | --- |
| None | Ordinary game boundary | Ordinary game boundary |
| Only one, in either slot | Uses that link | Uses that link |
| Both | Uses the left link | Uses the right link |

**One link serves both edges.** Writing only `side="right"` does not close the
left exit. If only one edge should be reachable, block the other with solid
collision. A painted wall won't stop the King.

Links go one way. A link from screen 2 to screen 7 doesn't add a return from 7
to 2. Add that return yourself if the route needs it.

These are screen-edge crossings, not portals that activate when the King touches
a prop. `SideLink` has no position, width or destination coordinates. Use the
map's collision to decide where the King can reach the edge.

## A complete map.xml

This example is for a map with 10 authored screens in a stock 13 by 13 compiled
collision atlas. It gives screen 2 different destinations on its two sides and
adds returns from screens 7 and 9:

```xml
<MapLayout version="1" screens="10" atlasSide="13">
  <SideLink screen="2" side="left" target="7" />
  <SideLink screen="2" side="right" target="9" />
  <SideLink screen="7" side="right" target="2" />
  <SideLink screen="9" side="left" target="2" />
</MapLayout>
```

Assuming those screens have no RGB teleport markers:

| Exit | Arrival |
| --- | --- |
| Screen 2, left | Screen 7, right edge |
| Screen 2, right | Screen 9, left edge |
| Screen 7, either edge | Screen 2, opposite edge |
| Screen 9, either edge | Screen 2, opposite edge |

To make only the intended returns reachable, put a solid wall along screen 7's
left edge and screen 9's right edge. You can instead give each of those screens
a second link if the route should continue elsewhere.

For your own map, keep its actual `screens` and `atlasSide` values. If `map.xml`
already exists, add the links to its `MapLayout` element rather than replacing
the file with this example.

| Field | Meaning |
| --- | --- |
| `version` | Use `1`. |
| `screens` | Authored screen count, from 1 to 4096. Unused atlas cells don't count. |
| `atlasSide` | Rows and columns in the square **compiled** collision atlas, from 1 to 64. It must have room for every authored screen. |
| `screen` | Screen you leave, numbered from 1. |
| `side` | Link slot to replace: `left` or `right`, in lowercase. |
| `target` | Destination screen, numbered from 1 and no greater than `screens`. |

You can't repeat the same screen/side pair or link a screen to itself. Unknown
elements and attributes are rejected. Changing `screens` or `atlasSide` doesn't
create geometry: the compiled collision atlas must already match the layout.
See [large maps](large-maps.md#native-topology) for atlas checks.

## Set links in Worldsmith Extension

Open **Build / Workshop** for the map. Check **Authored screens**, then enter
one link per line in **Side links: screen left|right target (one per line)**.
The XML example above becomes:

```text
2 left 7
2 right 9
7 right 2
9 left 2
```

Choose **Save layout**, then **Build checked copy**. The extension writes the
links to `map.xml`. Test the compiled copy in the game.

Destinations above 255 use exactly the same format. For example, `170 right 256`
links screen 170 to screen 256, provided both are within the authored count.
The one-link rule still applies. See [Worldsmith authoring](worldsmith.md) for
editor setup and expanding the collision source.

## Existing RGB teleport markers

Stock RGB markers can target screens 1–255. MME's `SideLink` targets use integers
and can reach any screen within the declared authored count.

MME replaces only the slots listed in `map.xml`. Other slots keep their native
links from the compiled map. For example, if screen 2 already has an RGB left
link to 4, adding `2 right 9` gives it two links: left to 4, right to 9.

Omitting a side doesn't disable it. Removing a `SideLink` lets the native marker
for that slot take effect again on the next attempt. There is no disabled target
value: `target="0"` and `target="-1"` are invalid. To remove an old marker link,
remove the marker from the collision source and rebuild the map.

## Load and test the map

Players need **Mega Mapping Expansion** and **JK Runtime** for links in `map.xml`.
Declare both as Workshop dependencies. Side links don't require `scene.xml`;
turning off MME's scenery checkbox leaves the routes active.

After editing links, rebuild the map if your authoring tool owns the output,
then restart the attempt or reopen the map. Scene hot reload doesn't reload
links or collision. MME reapplies the layout at each attempt and restores its
changes on unload, unless another mod has since replaced the same slots.

In the game, cross both edges of each linked screen and try each intended
return. Check that walls close unwanted exits and that the arrival area is
clear. A valid destination number doesn't guarantee a usable route.

The optional [topology check](large-maps.md#in-game-checks) exercises declared
links through the native teleporter and checks saved-position round trips. It
doesn't replace playing the route.

| Problem | Check |
| --- | --- |
| Both edges go to the same screen | There may be only one enabled link. Add a second destination or close the unwanted edge with collision. |
| An unlisted side still teleports | Look for an RGB marker in the collision source. MME preserves unlisted slots. |
| Changes don't appear after scene reload | Rebuild the map as needed and restart the attempt. |
| The layout is rejected | Check for duplicate sides, self-links, out-of-range targets and a compiled atlas that doesn't match `atlasSide`. |
| A destination above 255 doesn't work | Use `map.xml` rather than an RGB marker, and check that both required mods are loaded. |
