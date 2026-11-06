# Add moving platforms

[Map guides](README.md)

MME 1.4 adds moving collision through colors or XML. Players need MME and JK
Runtime. Platforms keep working when MME's presentation checkbox is off.

## Paint a platform

Without a `moving-platforms.xml` file, these exact opaque colors are available:

| Color | Meaning |
| --- | --- |
| `#D060A0` | Horizontal platform |
| `#D061A0` | Horizontal travel range |
| `#60A0D0` | Vertical platform |
| `#61A0D0` | Vertical travel range |

Use `level+.png` at any supported resolution, or ordinary `level.png` with its
8-pixel cells. Side-connected platform pixels form one object; separate islands
move independently. Corner contact doesn't join objects. Marker pixels leave
no static collision at the starting position. Range pixels aren't solid.

Paint a range directly beside a platform along its movement axis. A connected
chain extends its travel by the chain's length. Put the range on either side,
or both. If the screen has range markers for a profile, each of that profile's
platforms needs a touching range. Without ranges, the defaults travel 96 pixels
right or 96 pixels up, then back, at 60 pixels per second. The packaged default
PNG makes the decks visible without adding a map image.

These colors were checked against the local provider catalog on 2026-10-24.
They aren't a universal reservation. Pixel collision rejects colors also claimed
by a loaded block factory or slope alias. Use XML-only objects or choose free
map-local colors when another mod needs them.

## Set speed, route and appearance

Create `screens/mega-mapping-expansion/moving-platforms.xml` beside your map's
other screen metadata. This file replaces the default palette. Add `Paint`
entries for every platform color you want to keep.

```xml screens/mega-mapping-expansion/moving-platforms.xml
<MovingPlatforms version="1">
  <Motion id="lift" axis="vertical" distance="-96" speed="40" wait="0.5" />
  <Profile id="lift" motion="lift" collision="solid" fit="tile" />
  <Paint color="#60A0D0" profile="lift" />
  <Platform id="lift-1" profile="lift" screen="1" rect="120,280,48,8" />
</MovingPlatforms>
```

This makes painted lifts and one XML-only lift. Omit `Paint` for an entirely XML
map; omit `Platform` for an entirely painted map. An XML rectangle doesn't need
a matching colored pixel. Don't leave an ordinary solid block underneath it.

Positions and sizes use game pixels. Screen numbers start at 1. Y increases
downward within a screen. Point coordinates are offsets from each platform's
authored start; the first point is always `0,0`. Negative Y moves upward and
can cross into the next numbered screen. Routes must stay within authored map
bounds. Side teleports don't transform a platform's route.

| Element | Fields and defaults |
| --- | --- |
| `Motion` | Required `id`; `speed="60"` pixels/s, `mode="pingPong"`, `phase="0"` seconds, `playing="true"`, `wait="0"` seconds |
| Obstruction | Motion `onBlocked="wait"`, `reverse` or `stop`; `blockedWait="0.2"` seconds before reversing |
| Short motion | `axis="horizontal"` or `vertical`, signed `distance="96"` |
| Waypoint motion | Two to 64 `Point` children with `x`, `y` and optional `wait`; no `axis`/`distance` |
| `Profile` | Required `id`, `motion`; `collision="solid"` or `oneWay`; `texture=""`, `fit="tile"` |
| Jump momentum | Profile `momentum="0"` (0–4 multiplier), `momentumAxes="both"`, `horizontal` or `vertical` |
| Captions | Optional `label` on Profile or Platform; Platform overrides Profile. Up to 80 characters on one line |
| Animation | Profile `columns="1"`, `rows="1"`, `frames="1"`, `fps="8"` |
| `Paint` | Required unique `color`, `profile`; optional `range` color for axis-aligned two-point motions |
| `Platform` | Required `id`, `profile`; `screen="1"`, `rect="x,y,width,height"`, optional `group` |
| Compound platform | One or more `Part rect="x,y,width,height"` children; all move together |

`pingPong` reverses at the endpoints. `loop` traverses the last-to-first segment;
it doesn't teleport. `once` stops at the final point. Waits apply at arrivals,
including the initial point. A phase advances the route before play begins.
Speed is 0.01–600 pixels/s; waits are 0–3600 seconds; phases are 0–86400 seconds.
Adjacent points must be at least one pixel apart.

Give separate platforms the same `group` to share one motion clock. Group IDs
must differ from platform IDs, and members must reference the same motion.
Different starting positions are preserved. Distinct objects aren't implicitly
grouped just because their XML rectangles touch.

## Use your own platform PNG

A platform image can be a small repeating tile or a drawing of the whole deck.
It doesn't have to be 8x8 or match the collision grid. The lab uses 16x8 tiles;
those dimensions are an example, not a required cell size.

1. Put your PNG inside the editable map, for example `props/platforms/deck.png`.
   The `texture` path starts at the map folder containing `level_settings.xml`,
   not at the XML file's folder.
2. Set the Profile's `texture` and `fit`. All platforms using that Profile use
   the same image. To give a deck its own artwork, give it a separate Profile.
3. Define its collision with a painted shape or a Platform rectangle. Image
   pixels and collision cells are independent.
4. In WS Extension, use **Validate / preview**, **Save**, then **Build checked
   copy** and **Test in-game**. Rebuild and restart the test map after editing a
   PNG; the running map keeps its loaded textures.

For a complete 64x16 drawing, save a 64x16 `props/platforms/deck.png` and put this
in `screens/mega-mapping-expansion/moving-platforms.xml`. It creates a lift on
screen 1 with matching 64x16 collision and draws the PNG once at its original
size. Add the elements to your existing file if the map already has platforms.

```xml-example
<MovingPlatforms version="1">
  <Motion id="deck-rise" axis="vertical" distance="-96" speed="40" />
  <Profile id="deck-art" motion="deck-rise"
           texture="props/platforms/deck.png" fit="native" />
  <Platform id="deck-1" profile="deck-art" screen="1" rect="120,280,64,16" />
</MovingPlatforms>
```

Choose how the image fills the authored rectangle:

| `fit` | Result | Example |
| --- | --- | --- |
| `tile` (default) | Repeat at the image's original size; clip the last tile to the rectangle | A 16x8 PNG fills a 64x16 deck with four columns and two rows |
| `stretch` | Resize one image to the rectangle's width and height | A 64x16 PNG fills a 96x16 deck, making its artwork wider |
| `native` | Draw once at the rectangle's origin, without resizing or clipping to its bounds | A 64x16 PNG matches a 64x16 deck; larger artwork can extend beyond its collision |

Transparent pixels only hide artwork. They don't cut holes in collision, and
opaque pixels outside a rectangle don't become solid. A painted platform uses
the collision image's grid; XML `rect` and `Part` sizes use whole game pixels
and don't have to be multiples of eight. Use several `Part` rectangles for a
compound collision shape. Rotation and scale of physical collision aren't
supported.

Each Part draws the Profile's image separately from that Part's origin. A single
PNG isn't spread across the combined outline of a compound platform. For pieces
with different images, use separate Profiles and Platforms with the same motion
and `group`; the group keeps them moving together.

For animation, set `columns`, `rows`, `frames` and `fps` on the Profile. The image
must divide evenly into that grid. For example, a 64x8 strip with `columns="4"`,
`rows="1"`, `frames="4"` and `fps="8"` has four 16x8 frames. The selected frame
uses the same `fit` rules as a still image.

PNG, BMP and supported Color XNB images are accepted. Blank `texture` loads
`assets/platforms/default.png` from the installed MME folder. Use a map-owned PNG
for custom artwork so authors don't have to edit the installed mod.
`texture="none"` hides the platform's artwork while keeping its collision.

## Control a platform from a scene

Rules and control trees accept `platformStart`, `platformStop`, `platformReverse`
and `platformReset`. Their `target` is a platform or group ID. Reverse preserves
the current position. Reset returns to the authored phase and playing setting;
it can move geometry immediately, so use it where the start is clear.

```xml-fragment
<Rule id="start-lift" event="scene.ready">
  <Action op="platformStart" target="lift-1" />
</Rule>
```

Events are `platform:ID.arrived`, `platform:ID.board` and `platform:ID.leave`.
Arrival payload `point` is a zero-based waypoint index; boarding/leaving use
`-1`. Painted IDs appear in Mapping inspector as
`paint-rrggbb-SCREEN-ISLAND`. Prefer explicit IDs for long-lived scene rules.
Rules pause with scene presentation; physical movement uses gameplay time.

Movement rolls back to the last safe step when a deck or its rider would enter
blocking geometry. `onBlocked="wait"` holds the clock and retries later.
`onBlocked="reverse"` returns along the current route after `blockedWait`;
`onBlocked="stop"` waits for a start command. These responses apply to a whole
synchronized group, including a blocked second rider. No crushing damage is added.
`oneWay` permits upward passage from below.

Set Profile `momentum="1"` to add the deck's current velocity to a jump once.
`0` keeps the ordinary jump; `0.5` transfers half the velocity and `2` doubles it.
`momentumAxes` selects which components are added. Velocity uses the authored
pixels/second speed, converted to native velocity units; it doesn't fluctuate
with rounded one-pixel movement. Waiting, stopped and blocked decks supply zero.
Walking off a deck doesn't add an impulse. Momentum follows the jump already
chosen by the installed movement provider; it doesn't replace charging.

Set `label="CEILING / REVERSE"` on a Platform for a caption that follows its deck.
A Profile label also covers painted platforms using that profile. Labels use the
native small font and are optional in other maps.

Pause and completion hold movement. Restarts, native saves and Runtime snapshots
restore route progress, waiting and playing state. World synchronization sends
authoritative platform state; followers don't advance another local clock.
Replays 2.7.3+ and Run Verifier 1.4.1+ include this file in map revisions.

## Edit in WS Extension

Open an editable map and choose **Moving platforms...** in project details.
Select a motion, profile, paint rule, platform or waypoint to edit its fields.
Use **Add lift**, **Add color**, **Add part** and **Add waypoint** to extend it.
The XML tab preserves direct edits when you validate and save.

Choose a screen and play or scrub the 120-second preview. It overlays the
platform shape and route on the collision image. Click a deck to select its
definition. The packaged default PNG and custom PNGs appear in the preview;
image grids animate, while compiled XNB textures use a placeholder. See
[Use your own platform PNG](#use-your-own-platform-png) for paths, whole-deck
artwork and repeating tiles. The preview doesn't simulate the King or blocking
obstacles.

**Validate / preview** and **Save** check dimensions, references, colors, texture
grids and bounds. Saving is atomic and retains the previous XML as
`.worldsmith-backup`. **Build checked copy** validates the same format. Restart
the test map after geometry or route edits; scene hot reload doesn't replace
physical platforms.

Up to 128 platforms, 128 observed player bodies and 4096 compacted rectangles
are supported. One world clock moves all observed bodies; a blocked rider holds
the whole synchronized group. Overloaded arrival-event queues report a
diagnostic and suppress excess events rather than interrupting collision. Inspect
**Mapping inspector > Moving platforms** for generated IDs and effective state.
Test tight contacts, jumps, screen crossings and your chosen movement mods in
the game. A valid build doesn't prove route completion or arbitrary mod support.

The [two-room lab](../../examples/moving-platforms/README.md) covers lifts,
diagonal movement, color ranges, a compound deck, scene control and a ceiling.
