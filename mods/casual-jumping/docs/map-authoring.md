# Casual Jumping: map tags, markers and areas

This page has everything a map needs to control Casual Jumping with **JK Runtime
2.7.0+**: exact tags, colours, paths and a complete XML example.

## Map tag and saved settings

Add this entry to the existing `Tags` array in `level_settings.xml`. The snippet
is only the `Tags` fragment, not a replacement settings file.

```xml
<Tags>
  <string>JKRuntime.MapControlled:casual.controls</string>
</Tags>
```

The tag keeps a mod installed just for this map from changing ordinary play after
leaving it. On this map, the author controls where the mechanic works: the global
switch is locked, unmarked screens are off, and only authored permissions enable
it. This remains true even for players who use the mechanic on other maps.

At gameplay startup, Runtime saves global enable as **Off** only if this mechanic
has never been used or explicitly enabled by the player outside maps carrying its
own tag. Leaving unlocks the switch and keeps that saved value. Once outside use
or a successful manual enable establishes a global preference, tagged maps leave
that preference alone. The history survives game restarts and is separate for each
mechanic, including items in the same mod. A different map with the same tag still
counts as map-controlled use.

Loading a DLL, checking permission, opening settings or using a preview does not
count as use. Preparation and cancelled intros do not write history or disable
settings; attempt restarts do not repeat entry writes. On upgrade, existing enabled
settings are preserved because older versions did not record usage. Unreadable
history also preserves preferences. Other settings are unchanged.

Without the tag, saved preferences and authored triggers retain their normal role;
explicit Off/Local screen rules still apply. A tag alone enables no screens.

## Choose a screen marker or XML

**Pick screen pixels or XML for each On/Off/Local rule. You do not need both.**
Paint markers when the collision image should own the rule. Use
`jk-runtime/mechanics.xml` when ranges, zones or parameters are easier to read.

For example, on screen 2, `RGB(173,104,211)` has the same activation meaning as
`<Screens from="2" mode="on"/>` inside `<Mechanic id="casual.controls">` in the XML
file. The complete file structure is shown below. The same equivalence applies
to Off and Local and to every mechanic in this guide's palette.

XML additionally supports screen ranges, rectangular Zone areas and this mod's
supported parameters. It does not replace solid terrain. Local still needs a
local trigger or Zone; declaring Local by either method does not create that area.
If both methods describe the same screen/mechanic, their modes and shared
parameters must agree: a conflict is an error, and XML does not take precedence.

The map tag is **separate from both alternatives**: it controls map authority and
conditional global disabling. Neither pixels nor XML automatically adds the tag.

## Exact screen colours

| Mechanic | ID | On RGB | Off RGB | Local RGB |
| --- | --- | --- | --- | --- |
| Casual controls | `casual.controls` | `(173,104,211)` | `(173,105,211)` | `(173,106,211)` |

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
  <Mechanic id="casual.controls">
    <Screens from="1" mode="on">
      <Parameter name="mode" value="casual"/>
    </Screens>
    <Screens from="2" mode="local"/>
    <Zone id="assisted-jump" screen="2" x="40" y="40" width="200" height="240">
      <Parameter name="mode" value="casual-plus"/>
    </Zone>
    <Screens from="3" mode="off"/>
  </Mechanic>
</Mechanics>
```

## Controls, parameters and transitions

`mode="casual"` keeps charge-and-release jumping and adds air control.
`mode="casual-plus"` uses press-to-jump and variable jump height with air control.
Write this as `<Parameter name="mode" value="casual"/>` inside Screens or Zone;
`mode` on Screens itself always means `on`, `off`, `local` or `inherit`.
Authored activation defaults to **casual-plus**, regardless of the saved mode.
There is no Casual terrain material: Local uses XML zones.

The example gives screen 1 Casual controls, the zone on screen 2 Casual+, and
screen 3 native controls. Leaving permitted scope returns control to Vanilla;
controller changes run at a safe player update boundary. The selected user mode
is retained even when map entry disables a map-only preference. Ball form can still own
movement. SFC may operate with Casual but yields while Casual+ owns jumping.

`AllowCasualJumping` is a separate map tag for modified-run attribution. It does
not enable controls or define their scope. Authored activation does not add the
mod's player-override mark; other modified-run sources are not cleared.

## Troubleshooting

If a mechanic stays off, check the exact tag/ID, the active screen number and
its permission first. Then check zone placement and the mechanic-specific input,
equipment or movement conditions above. Remove conflicting pixel/XML declarations.
A missing provider or malformed configuration reports the relevant mechanic ID;
markers are not a substitute for installing the mod and JK Runtime.
