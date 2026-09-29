# Mega Gameplay Expansion: map tags, markers and areas

This page has everything a map needs to control Mega Gameplay Expansion with
**JK Runtime 1.36+**: exact tags, colours, paths and complete XML examples.

## Map tag and saved settings

Add the entries you need to the existing `Tags` array in `level_settings.xml`.
The snippet is only the `Tags` fragment, not a replacement settings file.

```xml
<Tags>
  <string>JKRuntime.MapControlled:mega.warp</string>
  <string>JKRuntime.MapControlled:mega.no-walk-off</string>
  <string>JKRuntime.MapControlled:mega.air-dash</string>
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

For example, on screen 2, `RGB(173,86,211)` has the same activation meaning as
`<Screens from="2" mode="on"/>` inside `<Mechanic id="mega.warp">` in the XML
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
| Warp Jump | `mega.warp` | `(173,86,211)` | `(173,87,211)` | `(173,88,211)` |
| No Walk Off | `mega.no-walk-off` | `(173,89,211)` | `(173,90,211)` | `(173,91,211)` |
| Air Dash | `mega.air-dash` | `(173,92,211)` | `(173,93,211)` | `(173,94,211)` |

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
  <Mechanic id="mega.warp">
    <Screens from="1" mode="local"/>
    <Zone id="warp-departure" screen="1" x="40" y="40" width="200" height="240"
          test="overlap"/>
    <Screens from="2" mode="on"/>
    <Screens from="3" mode="off"/>
  </Mechanic>
  <Mechanic id="mega.no-walk-off">
    <Screens from="1" to="2" mode="local"/>
    <Screens from="3" mode="off"/>
  </Mechanic>
  <Mechanic id="mega.air-dash">
    <Screens from="1" mode="off"/>
    <Screens from="2" mode="local"/>
    <Zone id="dash" screen="2" x="40" y="40" width="200" height="240"
          test="overlap"/>
    <Screens from="3" mode="on"/>
  </Mechanic>
</Mechanics>
```

## Existing Solid, Screen and Zone colours

These opaque colours remain supported alongside the common permission markers.

| Mechanic | Solid RGB | Existing Screen RGB (On) | Zone RGB |
| --- | --- | --- | --- |
| Warp Jump | `(173,47,211)` | `(173,48,211)` | `(173,49,211)` |
| No Walk Off | `(173,50,211)` | `(173,51,211)` | `(173,52,211)` |
| Air Dash | `(173,53,211)` | `(173,54,211)` | `(173,55,211)` |

Solid pixels form blocking terrain. Zone pixels form nonblocking hitbox-overlap
volumes. Existing Screen pixels are nonblocking On aliases; they cannot coexist
with a Local/Off declaration for the same mechanic and screen. All variants feed
the same effect, not separate copies.

On controlled screens use **Local + Solid/Zone** to restrict activation to that
terrain or volume. The XML example's No Walk Off screens need Solid or Zone pixels;
its Warp and Dash rectangles work without coloured local volumes. With On,
permission covers the entire screen regardless of those surfaces. With Off or
an unmarked controlled screen, the surfaces stay solid but their mechanic is denied.
There are no additional typed XML tuning parameters for these three mechanics.

## Mechanic entry and exit

- **Warp Jump:** standing on Solid arms departure; a Zone also supports entry in
  flight. Standing idle in a Zone does not teleport. Leaving while grounded removes
  local activation. A started warp can finish outside scope. Native prediction
  must find a supported landing; foreign movement/collision can refuse the warp.
- **No Walk Off:** Solid requires support by that terrain; Zone requires overlap.
  Leaving local scope removes its guard. It only guards ordinary supported walking;
  jumping, inertia, airborne motion and unsupported movement retain native handling.
- **Air Dash:** Solid grants the per-flight dash; landing on different terrain
  clears that grant. Zone/Screen scope is checked when starting. A started dash
  finishes outside scope and overlapping variants never grant extra dashes.
  Ball, Casual movement, active Jetpack thrust and Warp presentation retain their
  existing exclusions. Unload/cancellation restores unfinished owned movement state.

World exit releases local controllers and map overrides. Persistent global enables
disabled by a controlling tag remain Off. `AllowMegaGameplayExpansion` is a separate
tag for modified-run attribution of successful global use, not an activation tag.
Map-authored activation does not add the global-use mark.

## Gimmick library and other providers

While any common map-controlled mechanic is present, the Gimmick library remains
available for discovery but suspends saved/manual generic overrides. It preserves
those preferences for ordinary maps. This prevents overrides bypassing authored
permission, even when the controlled mechanic belongs to another mod.

Native wind/teleports and third-party blocks retain their original formats and
execution. They do not gain the three MGE IDs or generic activation tags just
because the library discovers them. Mega Mapping Expansion's scene is also
outside this system and keeps its ordinary persistent checkbox.

## Troubleshooting

If a mechanic stays off, check the exact tag/ID, the active screen number and
its permission first. Then check zone placement and the mechanic-specific input,
equipment or movement conditions above. Remove conflicting pixel/XML declarations.
A missing provider or malformed configuration reports the relevant mechanic ID;
markers are not a substitute for installing the mod and JK Runtime.
