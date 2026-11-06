# Moving Platform Lab

Use this example in a normal Worldsmith map. Keep your map's native settings,
artwork and other assets. The supplied layout expects a 180x45 `level+.png`:
two test rooms side by side and a sealed third room for the native ending.

The maintainer tool `sdk/PlatformLab.cs` creates a complete editable map from
Worldsmith's stock Level template. Build that folder through WS Extension's
**Build checked copy**, then use **Test in-game**. No separate map generator is
required. Players need MME 1.4 or later and JK Runtime.

Room 1 contains a color-authored horizontal platform, two short lifts, an upward-passable
diagonal deck and a tall lift. Its cyan edge and upward arrows mark the one-way
deck; the solid decks have metal bodies. The ceiling lift reverses after a brief
wait when it or the King reaches the overhang. Every deck carries its own caption.
Walk right along the floor to room 2,
or left to return. The outer walls and ceilings keep the rooms separate from
the inaccessible ending. The ceiling test's overhang is the only static ledge;
the other ordinary platforms have been removed. `COLOR / HORIZONTAL` is built
from the collision image's deck and range colors rather than an XML rectangle.
Room 2 has a compound platform and a four-point shuttle. The circuit starts
moving as soon as the attempt begins. Each room has a gold **REVERSE CIRCUIT**
floor switch that changes its direction; it doesn't need a boarding event to
start. Boarding `compound` reverses that deck through a separate MME rule.
Room 2's `MOMENTUM x1` lift adds its vertical speed once when jumping: rising
launches go higher, descending launches get less upward speed. Other decks keep
the ordinary jump. Edit the Profile's `momentum` and `momentumAxes` to change it.

## Platform textures

Every moving deck has a separate editable PNG in `props/platform-lab/`.
The lab tool copies these source images; it doesn't regenerate them. Edit the
PNG, build the map and restart the attempt. The metal images initially match,
but changing one doesn't alter another platform.

| Platform | PNG |
| --- | --- |
| Color / horizontal | `color-horizontal.png` |
| Lift | `lift.png` |
| Ceiling / reverse | `ceiling-reverse.png` |
| One way | `one-way.png` |
| Compound | `compound.png` |
| Circuit / switch | `circuit.png` |
| Tall lift | `tall-lift.png` |
| Momentum x1 | `momentum.png` |

Their profiles in `screens/mega-mapping-expansion/moving-platforms.xml` point
directly to these files. All images are 16x8 tiles; `fit="tile"` repeats and clips
them to the authored collision shape. The static ceiling and room boundaries
use ordinary `screens/midground` artwork. The ceiling test's stone ledge comes
from the separate 80x8 `ceiling-static.png`, copied into that layer by the lab
tool; edit this image before regenerating the source map. Room boundaries keep
the procedural stone material.

## Scenery and checks

The lab's dark mineral stone and broken-ring ornaments use the palette and
visual language of Babes of Absurd Timings. The hanging crystals use real MME
lights with occlusion contours derived from the static collision and a moving
receiver bound to the King's body position and screen. Native backgrounds use
the game's `bg1`, `bg2` naming. All sources
are ordinary Worldsmith files, built through WS Extension.

Check standing, charging, jumping, side and underside contact. Try pause,
restart, a Runtime snapshot and Subframe Charge. Turn off MME presentation:
the physical platforms must remain active. Building checks the format, not
whether a route can be completed.
