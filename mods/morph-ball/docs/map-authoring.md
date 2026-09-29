# Ball King: map tags, markers and areas

This page has everything a map needs to control Ball King with **JK Runtime
1.34+**: exact tags, colours, paths and a complete XML example.

## Map tag and saved settings

Add the entries you need to the existing `Tags` array in `level_settings.xml`.
The snippet is only the `Tags` fragment, not a replacement settings file.

```xml
<Tags>
  <string>JKRuntime.MapControlled:ball-king.form</string>
</Tags>
```

When the player enters the map, Runtime saves each tagged mechanic's global switch
as **Off** and locks it. Preparation and a cancelled intro do not touch settings,
and restarting the attempt does not save them again. A later visit switches them
off again. Leaving unlocks the controls but **does not turn them back on**. Other
preferences are left alone.

Without the tag, saved preferences and authored triggers retain their normal role;
explicit Off/Local screen rules still apply. A tag alone enables no screens.

## Choose a screen marker or XML

**Pick screen pixels or XML for each On/Off/Local rule. You do not need both.**
Paint markers when the collision image should own the rule. Use
`jk-runtime/mechanics.xml` when ranges, zones or parameters are easier to read.

For example, on screen 2, `RGB(173,80,211)` has the same activation meaning as
`<Screens from="2" mode="on"/>` inside `<Mechanic id="ball-king.form">` in the XML
file. The complete file structure is shown below. The same equivalence applies
to Off and Local and to every mechanic in this guide's palette.

XML additionally supports screen ranges, rectangular Zone areas and this mod's
supported parameters. It does not replace solid terrain. Local still needs a
local trigger or Zone; declaring Local by either method does not create that area.
If both methods describe the same screen/mechanic, their modes and shared
parameters must agree: a conflict is an error, and XML does not take precedence.

The map tag is **separate from both alternatives**: it controls map authority and
persistent disable-on-entry. Neither pixels nor XML automatically adds the tag.

## Exact screen colours

| Mechanic | ID | On RGB | Off RGB | Local RGB |
| --- | --- | --- | --- | --- |
| Ball form | `ball-king.form` | `(173,80,211)` | `(173,81,211)` | `(173,82,211)` |

## Screen modes and local areas

If you choose screen pixels, paint exact opaque colours (alpha 255) in the
collision image, not in background art. One marker anywhere on a screen declares its mode. The three screen markers
are nonblocking metadata: they never create platforms or collision. Paint terrain
separately. A local XML Zone is also nonblocking.

| Mode | Result |
| --- | --- |
| On | Enable throughout this screen; normal input and mechanic conditions still apply. |
| Off | Disable on this screen, including local triggers and the player's global enable. |
| Local | Enable only inside an authored XML Zone or a supported local trigger described below. |
| Inherit (XML only) | Use the saved preference/local triggers on ordinary maps; remain off on a controlled map. |

On a controlled map, an unmarked screen is off. A Zone or surface alone cannot
enable it: add a Local marker or a `<Screens mode="local">` declaration too.
On does not require a zone. Off cannot be bypassed by a zone. Markers do not
implicitly add map tags or change saved settings.

## XML placement and conflicts

Save the activation file at **`jk-runtime/mechanics.xml` relative to the map
root**. It is not under `props`. One `<Mechanics version="1">` root can contain
several `<Mechanic>` entries, each with a unique ID. Merge entries into an existing
file instead of creating a second root.

Screen numbers start at **1**. `from`/`to` ranges are inclusive; omit `to` for one
screen. Zone coordinates are local to that 480 x 360 screen. The rectangle must
fit inside it. `test="center"` (default) checks the player's hitbox center;
`test="overlap"` checks hitbox overlap. `priority` defaults to 0; the highest
priority containing zone wins. Overlapping zones with equal priority are invalid.

When both are used, pixel and XML screen declarations must agree. Conflicting
modes or parameter
values are errors, not an XML override. Prefer one source for each screen's mode.
Zone parameters override screen parameters while inside the zone. Unknown IDs,
unsupported parameters, invalid bounds and screens outside the map reject the
configuration. Use lowercase mode and parameter values exactly as shown.

These examples require at least three screens. XML alone is enough: do not add
pixels as well unless their declarations agree. Files are limited to 1 MiB,
4096 declarations and 65536 expanded screen rules; DTDs/external entities are disabled.

## Complete activation file

Save this as `jk-runtime/mechanics.xml` and add the tags above:

```xml
<Mechanics version="1">
  <Mechanic id="ball-king.form">
    <Screens from="1" mode="on">
      <Parameter name="sticky" value="false"/>
      <Parameter name="double-jump" value="off"/>
      <Parameter name="jump-profile" value="standard"/>
    </Screens>
    <Screens from="2" mode="local"/>
    <Zone id="ball-passage" screen="2" x="40" y="40" width="200" height="240"
          test="overlap">
      <Parameter name="double-jump" value="on"/>
      <Parameter name="jump-height" value="0.5"/>
    </Zone>
    <Screens from="3" mode="off"/>
  </Mechanic>
</Mechanics>
```

## Ball parameters

| Parameter | Values | If omitted |
| --- | --- | --- |
| `sticky` | `true`, `false` | Existing user/legacy Sticky rules. |
| `double-jump` | `inherit`, `on`, `off` | Existing user/legacy Double jump rules. |
| `jump-profile` | `standard`, `restricted` | Existing legacy profile, otherwise standard. |
| `jump-height` | Number from `0.05` to `2` | Profile scale; an explicit value overrides it. |

Use `<Parameter name="..." value="..."/>` within Screens or Zone. Standard has
scale 1. Restricted uses 0.75 without Double jump or 0.35 with Double jump.
Local uses XML zones; a Sticky surface alone does not enable morphing.

## Existing colours and terrain

| RGB | Meaning |
| --- | --- |
| `(160,64,255)` | Nonblocking screen On, restricted jumps, global Sticky off, Double jump inherited. |
| `(255,96,64)` | Nonblocking screen On, restricted jumps, global Sticky off, Double jump off. |
| `(64,128,255)` | Nonblocking screen On, restricted jumps, global Sticky off, Double jump on. |
| `(255,64,160)` | Solid Sticky terrain, including supported slope shapes. |

The three old screen colours now create **no collider**. They declare On plus
their listed parameters, so do not pair them with Off/Local or contradictory XML
parameters. They do not substitute for the map tag or alter saved preferences.
Sticky terrain stays solid while the mechanic is disabled. An active ball can
adhere to that material even when global Sticky is off.

## Entry, exit and attribution

On permits the Morph binding; it does not automatically transform the player.
When permission ends, the ball unfolds only when its larger hitbox fits. Under
a low ceiling it retains the small hitbox until collision-safe expansion is
possible. World exit releases the controller through its normal lifetime.

`AllowBallKing` is a separate map tag for modified-run attribution. It neither
activates Ball nor replaces the controlling tag. Authored use does not add Ball's
player-override mark; other modified-run sources remain intact.

## Troubleshooting

If a mechanic stays off, check the exact tag/ID, the active screen number and
its permission first. Then check zone placement and the mechanic-specific input,
equipment or movement conditions above. Remove conflicting pixel/XML declarations.
A missing provider or malformed configuration reports the relevant mechanic ID;
markers are not a substitute for installing the mod and JK Runtime.
