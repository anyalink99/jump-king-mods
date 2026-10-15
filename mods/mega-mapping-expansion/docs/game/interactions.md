# Items, hidden walls and victory

[Handbook](../index.md) / [Native game systems](index.md)

These adapters connect native gameplay to scene rules. They use the game's
inventory, hidden-wall test and ending controller. Keep the native files that
create those objects in the map.

## Items

An `item` component selects an exact native `Items` enum name. It exposes `count`
(Integer), `owned` (Boolean) and `equipped` (Boolean). `owned` means count is
positive; `equipped` also checks the native item option. It doesn't equip, consume
or grant an item.

`<id>.pickedUp` comes from a successful native world pickup, with its position in
the payload. `<id>.changed` reports a count change, including changes caused by
other native operations, with `previous` and `count` payloads. Loading an attempt
establishes a baseline without replaying the whole inventory as pickups.

This complete scene lights a marker while GoldRing is in the native inventory:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="ring" kind="item"><Value name="name" value="GoldRing" /></Component>
    <Component id="pose" kind="transform"><Value name="position" value="240,70" /></Component>
    <Component id="marker" kind="light"><Value name="transform" value="pose" /></Component>
  </Components>
  <Graph><Node id="owned" op="input" source="ring.owned" /></Graph>
  <Bindings><Bind id="marker" source="owned" target="marker/enabled" /></Bindings>
</MappingSettings>
```

This doesn't place a ring. Place the native item through the ordinary map
workflow. The adapter validates its name during preparation. Native save/reset
behavior remains authoritative. In a shared world, these scene observations
refer to the authoritative player's inventory, not a combined party inventory.

## Hidden walls

Existing `hiddenwallenter:<texture_name>` and `hiddenwallexit:<texture_name>`
events still observe native walls. A `hiddenWall` adapter gives a wall an authored
scene ID and a `touching` signal. It also distinguishes walls reusing one texture.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Variables><Variable id="visits" kind="Integer" value="0" /></Variables>
  <Components>
    <Component id="nearMiss" kind="hiddenWall">
      <Value name="texture" value="mme-close" />
    </Component>
  </Components>
  <Rules><Rule id="count" event="nearMiss.enter"><Action op="increment" target="visits" value="1" /></Rule></Rules>
</MappingSettings>
```

Use this scene with a native Hidden Wall whose `texture_name` is `mme-close`.
The [native-wall recipe](../scenes/contact-feedback.md#preserve-native-hidden-walls)
includes the native file. The component doesn't create the wall, change its
rectangles, replace its fade, or replay its sound.

For a repeated texture, also set `matchPosition=true` and `position` to the
wall's canonical world position. For example, native position `(20,100)` on
screen 3 becomes `(20,-620)`. This selects the wall artwork's native position;
it isn't the center of a hitbox. A repeated texture without an explicit position
is rejected during preparation. Entry/exit payloads include the native identity.

Grouped rectangles and strict edge intersection remain native. Don't approximate
a carefully placed wall with a region sensor merely to get an event. Use a sensor
when you want a different kind of overlap, hysteresis, dwell or surface selection.

## Victory conditions

An `ending` component changes a native ending's `CheckWin` result. Its `role` is
`main`, `nbp` or `owl`. There can be one adapter per role. The game still chooses
the ending, performs its completion handling and runs its native/custom tree.

The native main ending doesn't use Babe's sprite as a hitbox. It waits for the
King to be grounded on the configured ending screen, with his hitbox centre at
or beyond the ending King's start X (normally 336). Entering that area in the
air doesn't win yet. For an immediate contact finish, use `mode="replace"`,
set `position` and `size`, and set `requireGrounded` to `false`. The first strict
overlap is accepted by the native win check; no timer or later scene tick is
required. This changes the win condition, not the cutscene's own pauses.

`mode` chooses how the authored condition combines with the original result:

| Mode | Result |
|---|---|
| `replace` | Use the authored condition |
| `and` | Require both native and authored conditions |
| `or` | Accept either condition |

The authored condition starts with the bindable Boolean `condition`. It can also
require `screen` (one-based, `0` for any), `requireGrounded` (default true), and a
rectangular area described by `position` and `size`. A size of `0,0` omits the
area. Otherwise both dimensions must be at least one pixel. The area is tested against the
real current body hitbox inside the native win check, with strict integer
intersection. It isn't a decorative sprite's bounds or last frame's sensor value.
If a binding temporarily produces a zero or subpixel dimension on only one axis,
that area's condition is false; it doesn't silently remove the area requirement.

This example requires a ring and landing in a specific area on screen 2:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="ring" kind="item"><Value name="name" value="GoldRing" /></Component>
    <Component id="victory" kind="ending">
      <Value name="role" value="main" /><Value name="mode" value="replace" />
      <Value name="screen" value="2" /><Value name="position" value="300,240" />
      <Value name="size" value="80,80" /><Value name="requireGrounded" value="true" />
    </Component>
  </Components>
  <Graph><Node id="hasRing" op="input" source="ring.owned" /></Graph>
  <Bindings><Bind id="gate" source="hasRing" target="victory/condition" /></Bindings>
</MappingSettings>
```

Use a map whose native main ending is configured for screen 2, with its Babe and
ending resources present. This example changes the win area, not the native
actor start positions or ending screen. To choreograph a different scene, combine
it with [ending XML and scene bridges](../integration/endings.md).

For arbitrary polygons or compound areas, combine sensors in a Boolean graph and
bind that result to `condition`. Those sensor values have their documented sample
timing; the adapter's own rectangle is the option for a fresh native hitbox check.
For a pure story condition, leave `size=0,0` and bind a variable or expression.
For an extra requirement at the existing Babe, use `mode=and`.

No adapter means the original check runs unchanged. Disabling MME returns to the
native condition too. If the authored condition is essential to the map, declare
the requirement in [map policy](../maps/map-policies.md) and test the disabled
case. Completion uses the normal ending lifecycle; it isn't an emitted event
pretending that the game was won.

## Transitions and subsequent reactions

Use [screen links](../maps/teleports.md) for navigation changes. Those already have
fixed links, pools, schemes and event triggers. A scene can emit the configured
trigger after a pickup or dialogue line; it doesn't need a second transition
engine. A camera shot shows the result without taking ownership of collision or
the player's position.
