# Screen links and route settings

[Handbook](../README.md) / [Reference](README.md)

Start with [Connect two screens](../maps/teleports.md) or
[Choose a random room](../maps/random-rooms.md). This page covers the remaining
settings, shared route schemes, saved state and limits.

## File and static links

Save routes in `screens/mega-mapping-expansion/transitions.xml` inside the map.
The root is `<Transitions version="1">`. Optional root attributes are `seed` and
`saveId`, described below. The file is limited to 1 MiB of XML; DTDs and external
entities aren't allowed.

`SideLink` replaces one screen's left or right link:

| Attribute | Value |
| --- | --- |
| `screen` | Screen you leave, numbered from 1 |
| `side` | `left` or `right`, lowercase |
| `target` | Destination screen, numbered from 1 |

Both screen numbers must be within the map's authored count. A link can't target
its own screen. Each screen/side pair can have only one owner.

Leaving the right edge enters the destination's left edge; leaving the left
enters its right edge. Velocity is preserved. There are no destination-position
attributes. Add a separate link for a return.

**With only one enabled link on a screen, both edges use it.** With two, each
edge uses its own link. Without links, ordinary game boundaries apply. Block an
unwanted exit with collision. These rules also apply to random exits.

## Existing RGB teleport markers

Stock RGB markers can target screens 1-255. MME's `SideLink` targets use integers
and can reach any screen within the declared authored count.

MME replaces only the listed slots. Other slots keep their native
links from the compiled map. For example, if screen 2 already has an RGB left
link to 4, adding `2 right 9` gives it two links: left to 4, right to 9.

Omitting a side doesn't disable it. Removing a `SideLink` lets the native marker
for that slot take effect again on the next attempt. There is no disabled target
value: `target="0"` and `target="-1"` are invalid. To remove an old marker link,
remove the marker from the collision source and rebuild the map.

## Random destinations

Define a pool of destination screens and bind it to an exit. In this example,
leaving screen 5 chooses screen 10, 15, 20 or 25, excluding the previous result.

<!-- mme-example:start -->
```xml screens/mega-mapping-expansion/transitions.xml
<Transitions version="1" seed="auto">
  <Pool id="trials" screens="10,15,20,25" mode="random" avoidLast="1" />
  <Exit screen="5" side="right" pool="trials" />
</Transitions>
```
<!-- mme-example:end -->

Use your map's real screen numbers. The selection happens once when the King
actually crosses the edge. The King enters the chosen screen from the opposite
edge and keeps his velocity. Crossing from the right enters at the left; crossing
from the left enters at the right. The mod doesn't choose an arbitrary spawn
position inside the destination room.

The same one-link rule applies: if screen 5 has no second enabled link, both
edges use this pool. Close unwanted exits with collision or give the other slot
its own link. `SideLink` and `Exit` can coexist on different slots.

The pool's ID connects its settings to the exits that use it. To share the same
selection history, reference `trials` from several `Exit` entries. If one exit
just selected 15, the next exit using that pool can't select 15. Use separate pool
IDs for independent histories, even when their destination lists are identical.
Pools have independent random streams, so using one doesn't advance another.

Each destination must be distinct, within the authored count, and different
from every exit screen bound to the pool. Returns stay explicit: add ordinary
`SideLink` entries from trial rooms to the recovery room as needed. Falling into
recovery doesn't reset the pool. The next pass makes the next selection.

## Selection modes

| Setup on `Pool` | Behavior |
| --- | --- |
| `mode="random" avoidLast="1"` | Equal chances among destinations other than the previous one. This is the default. |
| `mode="random" avoidLast="0"` | Equal chances, including consecutive repeats. |
| `mode="weighted" weights="1,3,1,5" avoidLast="1"` | Relative chances in destination-list order. Exclude recent choices first, then use the remaining weights. |
| `mode="shuffle" avoidLast="1"` | Visit each destination once per bag. Refill after all were used; also prevent a repeat across bag boundaries. |
| `mode="sequence" avoidLast="0"` | Follow the list in order, then cycle to its beginning. |

`avoidLast` can be any integer from 0 to one less than the number of choices.
It excludes that many recent results, across bags too. With two choices and
`avoidLast="1"`, results necessarily alternate. Weights must be positive integers,
one per choice, from 1 to 1000000; they are only accepted in weighted mode.

Choose **when** to select with the separate `refresh` attribute:

| `refresh` | When a destination changes |
| --- | --- |
| `crossing` | Each successful crossing. Default for destination pools. Canceled crossings don't advance the stream. |
| `attempt` | Once at attempt initialization. Every crossing uses that choice. Loading saved state keeps its stored choice. |
| `event` | Once at initialization, then when a matching `Reroll` fires. Crossings alone don't change it. |

No-repeat history applies to selections. Repeated crossings of an attempt- or
event-refresh exit can reach the same room until another selection occurs.

## Seeds, recovery and saves

Use `seed="auto"` for a fresh seed on a new run, or an unsigned 64-bit integer,
such as `seed="12345"`, for a reproducible stream. Zero is valid. The active seed
is written to the MME log. A fixed seed with the same pool definitions and
selection order reproduces the same results. It doesn't promise the same route
when trigger timing or player choices change.

"Reroll" means make the next selection; it doesn't reset or reseed the generator.
Recovery continues the current stream. Native saves keep the seed, history,
remaining bag, current choices and trigger contacts. Loading resumes from that
state. Practice snapshots restore it too. **New Game** clears it; a fixed seed
then starts the same sequence again. Saving follows the game's save timing, so
an unsaved session lost to a crash may resume from an earlier selection.

By default, save identity comes from the authored transition definition and
screen count. Comments, formatting and attribute order don't change it. Editing
the definition starts a separate route state. For a stable explicit identity,
set `saveId="my-map-routes"` on `Transitions`. Keep that ID unique to the map.
Incompatible edits under an explicit ID fail validation. Change the ID or start
a new game to reset the route state.

Runtime shared state includes the committed choice and randomizer state.
Integrations using Runtime's shared world protocol receive the committed
selection during multiplayer and replay playback. Leaving playback restores
the local state.

Warp Jump forecasts currently stop when they reach a dynamic exit, including
one whose choice is held until an event. The forecast reports that limitation
and never consumes the live randomizer. Ordinary movement still crosses it.
Static transitions remain available to forecasts.

## Reroll triggers

A reroll changes the routes; it **doesn't teleport the King inside the room**.
All referenced pools must use `refresh="event"`.

<!-- mme-example:start -->
```xml screens/mega-mapping-expansion/transitions.xml
<Transitions version="1" seed="auto">
  <Pool id="trials" screens="10,15,20,25" refresh="event" />
  <Exit screen="5" side="right" pool="trials" />
  <Reroll id="switch" on="touch" screen="3" x="200" y="280" width="24" height="24" pools="trials" />
</Transitions>
```
<!-- mme-example:end -->

Touch rectangles use the usual 480 by 360 screen coordinates, with (0,0) at
the top left, regardless of collision resolution. Entering the rectangle fires
once. Staying on it doesn't keep rerolling; leave it to rearm. The rectangle is
invisible, so paint a switch or place a prop there if players should see it.
Spawning inside a touch rectangle counts as entering it.

Use `on="screen-enter" screen="3"` without rectangle attributes to reroll on
arrival at a room, including initial spawn. Use `on="event" event="trial.enter"`
without a screen or rectangle to listen to a [scene event](../scenes/behaviors.md).
For example, entering a region with ID `trial` emits `trial.enter`. Scene-event
rerolls need scene logic; touch and screen-entry rerolls work without a scene.
Pausing MME visuals also pauses scene events, but not the other route triggers.

`pools="starts,trials"` rerolls several pools together. If several triggers fire
in the same update or listen to the same event, each named pool selects only once.
Trigger contacts are saved, so loading while standing on a switch doesn't fire
it a second time.

## Switch complete route schemes

Use a `SchemePool` when a choice must change several links together. Each
`Scheme` is a complete set of destinations for the **same screen/side slots**.
Selection modes, weights and `avoidLast` work just like destination pools.
Schemes default to `refresh="event"`; `attempt` is also supported.

This example chooses a starting room, then routes both possible starting rooms
to the same trial for that scheme. It can represent differently shaped rooms
with left/right starts, without moving the King to a new position inside a room.

<!-- mme-example:start -->
```xml screens/mega-mapping-expansion/transitions.xml
<Transitions version="1" seed="auto">
  <SchemePool id="route" mode="shuffle" avoidLast="1">
    <Scheme id="start-a">
      <SideLink screen="5" side="right" target="10" />
      <SideLink screen="10" side="right" target="20" />
      <SideLink screen="15" side="left" target="20" />
    </Scheme>
    <Scheme id="start-b">
      <SideLink screen="5" side="right" target="15" />
      <SideLink screen="10" side="right" target="25" />
      <SideLink screen="15" side="left" target="25" />
    </Scheme>
  </SchemePool>
  <Reroll id="new-attempt" on="screen-enter" screen="3" pools="route" />
  <SideLink screen="20" side="right" target="3" />
  <SideLink screen="25" side="right" target="3" />
</Transitions>
```
<!-- mme-example:end -->

Every scheme must list exactly the same owned slots. Include entry links and
any returns that must change as a pair. Unlisted links keep their native markers
or independent static settings. A slot can't belong to two schemes, a scheme
and a destination pool, or a dynamic exit and a static `SideLink`.

## Edit and preview in Worldsmith Extension

Open **Transitions...** from the level's project actions. Edit the single XML
file, or use **Add random pool example**, then set its exit and destination
numbers. **Validate** checks the complete definition against the map's authored
screen count. **Save** keeps the previous file as `.worldsmith-backup`.

Select a pool and choose **Preview 30 selections**. With an automatic XML seed,
the preview uses the number in **Preview seed**; a fixed XML seed takes priority.
The preview has its own state. It shows successive *selections*, so a held
attempt/event choice doesn't imply a reroll on each in-game crossing. For schemes,
it shows every link in the selected scheme.

Layout saves, builds and recovered working copies preserve dynamic definitions.
The older **Side links** field still edits static links. It doesn't replace pools
or reroll triggers. Build a checked copy after editing and play the actual route.

## Limits and validation

A file supports 32 pools, 64 choices per pool, 256 total choices, 64 reroll
triggers and 512 owned exit slots including static links. IDs are case-sensitive
and use 1-64 letters, digits, dots, underscores or hyphens. Pool lists and trigger
pool lists accept commas or whitespace. Screens still range from 1 to 4096,
within the actual authored count. Unused atlas padding isn't a destination.

Validation rejects duplicate IDs, unknown fields, duplicate destinations, missing
pools, self-links, overlapping owners, invalid weights, impossible histories and
schemes with different owned slots. MME checks these definitions when the map
loads; inspect the first reported error before testing routes. A valid definition doesn't prove
that collision leaves a safe entry or that every route can be completed.

## Load and test the map

Players need **Mega Mapping Expansion** and **JK Runtime** for authored side links.
Declare both as Workshop dependencies. Side links don't require scene resources;
turning off MME's scenery checkbox leaves the routes active.

After editing links, rebuild the map if your authoring tool owns the output,
then restart the attempt or reopen the map. Scene hot reload doesn't reload
links or collision. MME reapplies the layout at each attempt and restores its
changes on unload, unless another mod has since replaced the same slots.

In the game, cross both edges of each linked screen and try each intended
return. Check that walls close unwanted exits and that the arrival area is
clear. A valid destination number doesn't guarantee a usable route.

| Problem | Check |
| --- | --- |
| Both edges go to the same screen | There may be only one enabled link. Add a second destination or close the unwanted edge with collision. |
| An unlisted side still teleports | Look for an RGB marker in the collision source. MME preserves unlisted slots. |
| Changes don't appear after scene reload | Rebuild the map as needed and restart the attempt. |
| The layout is rejected | Check for duplicate sides, self-links, out-of-range targets and a compiled collision capacity smaller than the authored screen count. |
| A destination above 255 doesn't work | Use `transitions.xml` rather than an RGB marker, and check that both required mods are loaded. |
