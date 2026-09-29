# Smooth Camera: map tags, markers and areas

This page has everything a map needs to control Smooth Camera with **JK Runtime
1.34+**: exact tags, colours, paths and a complete XML example.

## Map tag and saved settings

Add this entry to the existing `Tags` array in `level_settings.xml`. The snippet
is only the `Tags` fragment, not a replacement settings file.

```xml
<Tags>
  <string>JKRuntime.MapControlled:smooth-camera.tracking</string>
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

For example, on screen 2, `RGB(173,83,211)` has the same activation meaning as
`<Screens from="2" mode="on"/>` inside `<Mechanic id="smooth-camera.tracking">` in the XML
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
| Camera tracking | `smooth-camera.tracking` | `(173,83,211)` | `(173,84,211)` | `(173,85,211)` |

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
`test="overlap"` is also accepted, but Smooth Camera samples the king
center as a 1 x 1 point for common permission, so it doesn't switch early on
whole-hitbox overlap. `priority` defaults to 0; the highest
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
  <Mechanic id="smooth-camera.tracking">
    <Screens from="1" mode="on"/>
    <Screens from="2" mode="local"/>
    <Zone id="camera-shaft" screen="2" x="100" y="40" width="180" height="240"/>
    <Screens from="3" mode="off"/>
  </Mechanic>
</Mechanics>
```

## Camera-specific behaviour

There is no solid camera material. Local can use a common XML Zone or a `smooth`
zone from `props/smooth-camera/camera.xml`. A common Off or unmarked controlled
screen denies either zone. On forces tracking throughout the simulation screen;
the player's saved Enable can remain Off. Common Local coverage is clipped to
that screen so activation can't reveal an unpermitted neighbour.

The common file has no motion-profile parameters. Keep response speeds, focus,
windows, transitions and profile references in `props/smooth-camera/camera.xml`.
Bindings and personal profile preferences remain user settings. Intro, title,
ending and incompatible compositor states keep their existing native framing.

For a pixel-only map, add the tag and On markers on the desired screens; neither
XML file is required. For the example above, the tag plus mechanics.xml is enough.
Use camera.xml only when the map also needs profiles or its camera-specific zones.

## Camera XML and profiles

Optional file: `props/smooth-camera/camera.xml`, relative to the map root.
Camera screen `smooth`/`native` rules import On/Off into the common system.
These camera.xml screen rules are also alternatives to common On/Off pixels or
mechanics.xml Screens declarations; they don't need duplicate markers.
Use common Local permission when activation should depend on a zone.
Duplicate pixel/XML declarations must agree; camera.xml isn't an override.
Use `inherit` screens when common markers/mechanics.xml own permission and
camera.xml only supplies profiles. The following examples are separate recipes.

## Additional camera-only tags

Add these exact, case-sensitive strings to the level's normal `Info.Tags` list:

| Tag | Unmarked area | Leaving the map |
| --- | --- | --- |
| `SmoothCameraExplicitOnly` | Native camera | Resume the saved user preference |
| `SmoothCameraDisableOnEnter` | Native camera | Enable stays off until the user turns it on on another map |

The second tag wins when both are present. Its first attempt in a loaded world
saves Enable off once, with the source map recorded. Restarts don't repeatedly
save it. Returning to that map after another world is a new entry. If Enable
was already manually off, it stays manually off. Enable can't be turned on
while a strict map is active. Camera-specific smooth zones still need common
screen permission when the common controlling tag is present. A smooth screen
declaration supplies that permission by importing On.

## Screen ranges

```xml
<SmoothCamera version="1">
  <Screens from="2" to="5" mode="smooth"/>
  <Screens from="8" mode="native"/>
</SmoothCamera>
```

Screens are one-based. Omit `to` for a single screen. Ranges can't overlap.
The modes are `inherit`, `smooth` and `native`:

- `smooth` imports common On permission for that screen. It forces tracking
  regardless of Enable or the camera-only disabling tags and doesn't save Enable.
- `native` imports common Off permission and forces native screen framing,
  including inside camera-specific smooth zones.
- `inherit` follows the map tag and user preference; it can carry a profile.

After leaving a forced area, the current area's rule applies. Manual Off and
the persistent off state from a strict map therefore resume automatically.
Technical compositor incompatibility still takes priority over forced smooth
rules. Intro, title and ending lifecycle behavior remains native.

Rules are selected from the king's native screen and hitbox center on simulation
updates. Additional presentation frames and rendered neighboring screens don't
activate another area's rule or advance screen events.

## Rectangular zones

```xml
<SmoothCamera version="1">
  <Screens from="3" mode="inherit"/>
  <Zone id="shaft-entry" screen="3" x="100" y="40"
        width="180" height="240" priority="10" mode="smooth"/>
</SmoothCamera>
```

For this zone on a common controlled map, give screen 3 Local permission in
mechanics.xml or with RGB(173,85,211). Don't use a camera.xml native screen:
it imports Off and denies the smooth zone. The earlier complete example uses
screen 3 Off, so replace that declaration when trying this separate example.
On a camera-only strict map without the common tag, inherit leaves the surrounding
area native.

Coordinates are screen-local 480 x 360 game pixels. Entry uses the king's hitbox
center. Top/left edges are included, bottom/right edges excluded. IDs must be
unique; overlapping zones on a screen must have different priorities. The highest
priority containing zone wins. Its `inherit` mode inherits the screen rule, and
an omitted profile inherits that screen's profile. Common permission is resolved
after these camera-specific rules. A native zone
can't subtract from a common On screen, and a smooth zone can't override a
common Off screen. For selective activation use common Local permission and
smooth zones. Use camera profiles for framing changes within permitted scope.

Zones are activation rectangles, not viewport crops. A smooth zone inside an
otherwise native screen is limited to that screen: it can't expose an unmarked
neighbor. A screen with native subzones is excluded from neighboring smooth
coverage and horizontal portal previews. Use full-screen ranges for connected
scrolling shafts and zone profiles for framing changes inside those ranges.

## Coverage and transitions

The camera stays inside contiguous full-screen smooth coverage. An isolated
smooth screen can't scroll vertically beyond itself. It still overrides the
global enable state and may use manual views or valid horizontal connections.
Side portals require smooth coverage at both endpoints. Their visible vertical
neighbors are restricted as well; no native area is exposed by a side atlas.
Closed edges keep the existing collision-opening checks.

`transition="snap"` is the default at rule changes. `transition="smooth"`
animates entry into a smooth area from the previous safe framing, clamped to the
new coverage. Exit into native framing is immediate so forbidden areas never
flash during a transition. Continuous valid side-portal crossings keep
their coordinate rebase and departure view, including between authored ranges.

## Recommended motion profiles

```xml
<SmoothCamera version="1">
  <Profile id="shaft">
    <Settings>
      <Horizontal>false</Horizontal>
      <Vertical mode="Window" focus="0.6" window="100"
                negativeResponse="12" positiveResponse="18"
                fastAssist="true" lookAhead="20" lookDelay="0.2"
                recenter="false" recenterDelay="0.8"/>
      <HorizontalAxis mode="JumpKing" focus="0.5"
                      negativeBand="24" positiveBand="24"/>
      <LookMargin>48</LookMargin>
      <HoldMilliseconds>250</HoldMilliseconds>
    </Settings>
  </Profile>
  <Screens from="2" to="5" mode="smooth" profile="shaft" transition="smooth"/>
</SmoothCamera>
```

Profile IDs are case-sensitive and unique. References must exist. Profile
parameters use the same validation and defaults as user settings. Axis mode
names are `JumpKing`, `Direct`, `Window`, `Screen`; focus uses fractions (0.5),
not percentages (50). Omitted profile fields use Classic defaults. Bounds and
units are in [settings](settings.md).

Motion-profile precedence: personal profile for this map, then author profile
when Map recommendations is on, then global settings. Camera forcing and
coverage rules are independent of this preference. Forced camera use therefore
doesn't imply that the map overrides the player's preferred response speed.

## Validation and lifetime

The file is read during `BeforeAttempt` and refreshed on restart. File parsing,
reference validation and metadata reads never run in Draw. State is owned by
the loaded world and cleared on exit; prepared hooks remain dormant in intros.
Forced maps keep the hooks needed to detect their areas even when Enable is
off. High-refresh requests exist only while smooth presentation is active.

Unknown attributes/elements, unsupported versions, invalid ranges/rectangles,
ambiguous zones, invalid numeric values, duplicate IDs and missing profiles
reject the file as a whole. The camera uses native framing and exposes the
error under Map rules and in the log. The original file remains untouched.
External XML entities are disabled and files are limited to 1 MiB of characters.
These rules don't change physics, teleports, run saves or native screen events;
the disabling tags do persist the Enable preference as described above.
