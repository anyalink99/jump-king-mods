# Subframe Charge: map tags, markers and areas

This page has everything a map needs to control Subframe Charge with **JK Runtime
1.34+**: exact tags, colours, paths and a complete XML example.

## Map tag and saved settings

Add this entry to the existing `Tags` array in `level_settings.xml`. The snippet
is only the `Tags` fragment, not a replacement settings file.

```xml
<Tags>
  <string>JKRuntime.MapControlled:subframe-charge.timing</string>
</Tags>
```

When the player enters the map, Runtime saves the mechanic's global switch as
**Off** and locks it. Preparation and a cancelled intro do not touch the setting,
and restarting the attempt does not save it again. A later visit switches it off
again. Leaving unlocks the control but **does not turn it back on**. Other
preferences are left alone.

Without the tag, saved preferences and authored triggers keep their normal role;
explicit Off/Local screen rules still apply. A tag alone enables no screens.

## Choose a screen marker or XML

**Pick screen pixels or XML for each On/Off/Local rule. You do not need both.**
Paint markers when the collision image should own the rule. Use
`jk-runtime/mechanics.xml` when ranges, zones or parameters are easier to read.

For example, on screen 2, `RGB(173,107,211)` has the same activation meaning as
`<Screens from="2" mode="on"/>` inside `<Mechanic id="subframe-charge.timing">` in the XML
file. The complete file structure is shown below. The same equivalence applies
to Off and Local and to every mechanic in this guide's palette.

XML additionally supports screen ranges, rectangular Zone areas and this mod's
supported parameters. It doesn't replace solid terrain. Local still needs a
local trigger or Zone; declaring Local by either method doesn't create that area.
If both methods describe the same screen/mechanic, their modes and shared
parameters must agree: a conflict is an error, and XML doesn't take precedence.

The map tag is **separate from both alternatives**: it controls map authority and
persistent disable-on-entry. Neither pixels nor XML automatically adds the tag.

## Exact screen colours

| Mechanic | ID | On RGB | Off RGB | Local RGB |
| --- | --- | --- | --- | --- |
| Charge correction | `subframe-charge.timing` | `(173,107,211)` | `(173,108,211)` | `(173,109,211)` |

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

On a controlled map, an unmarked screen is off. A Zone or surface alone can't
enable it: add a Local marker or a `<Screens mode="local">` declaration too.
On doesn't require a zone. Off can't be bypassed by a zone. Markers don't
implicitly add map tags or change saved settings.

## XML placement and conflicts

Save the activation file at **`jk-runtime/mechanics.xml` relative to the map
root**. It isn't under `props`. One `<Mechanics version="1">` root can contain
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

These examples require at least three screens. XML alone is enough: don't add
pixels as well unless their declarations agree. Files are limited to 1 MiB,
4096 declarations and 65536 expanded screen rules; DTDs/external entities are disabled.

## Complete activation file

Save this as `jk-runtime/mechanics.xml` and add the tags above:

```xml
<Mechanics version="1">
  <Mechanic id="subframe-charge.timing">
    <Screens from="1" mode="on">
      <Parameter name="quarter-step" value="false"/>
    </Screens>
    <Screens from="2" mode="local"/>
    <Zone id="fine-charge" screen="2" x="40" y="40" width="200" height="240">
      <Parameter name="quarter-step" value="true"/>
    </Zone>
    <Screens from="3" mode="off"/>
  </Mechanic>
</Mechanics>
```

## Charge mode and independent settings

Authored On/Zone defaults to **ordinary correction**, independently of the
player's substep preference. Add parameters inside Screens or Zone:

```xml
<Parameter name="substep-ms" value="1"/>
```

`substep-ms` enables substeps and sets a finite physical hold interval from
**0.1 through 17 ms**, inclusive. Use `8.5` for half steps, `4.25` for quarters,
`2.125` for eighths or `1` for milliseconds. Other intervals are supported for
maps. Use a decimal point. Input hardware and scheduling still limit measurement
accuracy; a small authored interval isn't a hardware guarantee.

`substep-charge="true"` enables substeps with the authored default of 4.25 ms
when no interval is supplied. `substep-charge="false"` keeps whole steps even
when `substep-ms` is present. Write these as Parameter elements, for example:

```xml
<Parameter name="substep-charge" value="false"/>
```

The older `quarter-step` boolean remains supported. `true` always means 4.25 ms,
regardless of the player's selection. New `substep-charge` and `substep-ms`
parameters take precedence over that legacy parameter. Screen parameters are
inherited by zones; a zone can override the interval or turn substeps off.
The complete example above retains ordinary correction on screen 1, quarter
steps inside the screen 2 zone, and native charge outside that zone and on screen 3.

Local uses XML zones; SFC has no solid charge material. Scope changes safely
recompose the native charge policy. Input support and normal charge eligibility
still apply. SFC yields while Ball or Casual+ owns a different jump controller.

The map tag controls **charge correction only**. Show SFC, bindings, Subframe
Inputs, 240 Hz and Runtime Optimizations remain independent user preferences.
Disabling correction isn't a request to disable measurement or presentation.
The substep enable and selection are retained when global correction is saved Off.

`AllowSubframeCharge` is a separate map tag: it allows ordinary player-enabled
correction without SFC's modified-run mark, but doesn't enable correction.
Player-enabled substeps still add that mark. Explicit map-authored
substeps use authored attribution instead. No option clears another
source's existing modified-run history.

## Troubleshooting

If a mechanic stays off, check the exact tag/ID, the active screen number and
its permission first. Then check zone placement and the mechanic-specific input,
equipment or movement conditions above. Remove conflicting pixel/XML declarations.
A missing provider or malformed configuration reports the relevant mechanic ID;
markers aren't a substitute for installing the mod and JK Runtime.
