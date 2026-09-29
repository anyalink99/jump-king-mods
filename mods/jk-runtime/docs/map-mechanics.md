# Map mechanic activation

JK Runtime 1.34 provides `JKRuntime.Gameplay.MapMechanics`. The mechanic still
owns its physics and content format. Runtime only resolves where the map permits
it and which parameters the map supplied.

Each supported mod has a self-contained `docs/map-authoring.md` with its exact
tags, colours, parameters and complete XML examples.

## Map authority

Add `JKRuntime.MapControlled:<mechanic-id>` to the native map's `Tags` array, for
example `<string>JKRuntime.MapControlled:ball-king.form</string>`. Each tag controls
one ID in the table below. At player startup the provider saves its global
enable preference as **Off**. Preparation alone never writes settings. Restarting
the attempt does not repeatedly write; a later visit to the map disables it again.
Leaving the map does **not** restore the previous preference. On an unrestricted
map the player can enable the mechanic manually.

On a controlled map the global enable control is locked. A screen with no explicit
permission is disabled, including when a local surface or zone is present. Tags
do not activate every screen. Pixels never implicitly add a map tag or write a
setting. Without the tag, existing global preferences and authored triggers keep
working, subject to explicit Off/Local screen rules and hard Runtime map policy.

`AllowBallKing`, `AllowJetpack`, `AllowHammer`, `AllowCasualJumping` and
`AllowSubframeCharge` and `AllowRewinder` keep their existing run-attribution meanings. They are not
activation tags. Authored activation is separate from a player's global override.

Mega Mapping Expansion is outside this activation system. Its scene uses its own
user checkbox and scene.xml configuration; it has no common activation tag, screen
markers or XML zones.

## Screen pixels or XML: choose one

A screen marker and an XML `<Screens>` declaration are alternative ways to set
the same On/Off/Local permission. Authors may use markers alone or XML alone;
XML does not require a corresponding pixel. For example, Ball King On
`RGB(173,80,211)` on screen 2 is equivalent to `<Screens from="2" mode="on"/>`
inside `<Mechanic id="ball-king.form">` in `jk-runtime/mechanics.xml`.

XML additionally supports ranges, local Zone rectangles and typed parameters.
It does not draw solid terrain. Local permission still needs a local trigger
or Zone regardless of how that screen permission is declared. If both pixel
and XML declarations exist, modes and shared parameters must agree; neither
silently overrides the other. The map authority tag is a separate choice and
is never implicitly added by either alternative.

## Screens and areas

- **On** permits the mechanic across the simulation screen. Action mechanics still
  require their ordinary input, support, charge, equipment or consumable conditions.
- **Off** denies both global and authored local activation on that screen.
- **Local** allows activation only through a mechanic's existing surface/zone
  trigger or a common XML Zone. The global preference cannot bypass Local.
- **Inherit** is available in XML only. On controlled maps it is disabled; on
  unrestricted maps it uses ordinary settings and local triggers.

A solid surface remains terrain when its mechanic is denied. A zone is nonblocking.
A screen marker creates no block. XML screen numbers are **one-based**; API screen
indices are **zero-based**. Coordinates are 480 x 360 screen-local pixels.

## Marker palette

All common screen pixels are opaque `RGB(173,G,211)` (alpha 255). The three G
values in each row are On, Off and Local, respectively. Legacy aliases below
remain supported; authoring should use one declaration per mechanic/screen.

| Mechanic ID | On G | Off G | Local G | Local source and parameters |
| --- | ---: | ---: | ---: | --- |
| `ball-king.form` | 80 | 81 | 82 | XML Zone; `sticky`, `double-jump`, `jump-profile`, `jump-height` |
| `smooth-camera.tracking` | 83 | 84 | 85 | XML Zone or existing smooth camera zone; camera profiles remain in camera.xml |
| `mega.warp` | 86 | 87 | 88 | Existing Warp surface/departure and zone; XML Zone |
| `mega.no-walk-off` | 89 | 90 | 91 | Existing No Walk Off surface/zone; XML Zone |
| `mega.air-dash` | 92 | 93 | 94 | Existing Air Dash surface/grant and zone; XML Zone |
| `more-items.jetpack` | 95 | 96 | 97 | XML Zone; temporary equipment or `equipment="owned"` |
| `more-items.hammer` | 98 | 99 | 100 | XML Zone; temporary equipment or `equipment="owned"` |
| `more-items.rewinder` | 101 | 102 | 103 | XML Zone; owned consumable required |
| `casual.controls` | 104 | 105 | 106 | XML Zone; `mode` parameter: casual or casual-plus |
| `subframe-charge.timing` | 107 | 108 | 109 | XML Zone; `quarter-step` boolean |

RGB values are reserved in the repository's block registry and checked against
loaded factories before geometry decoding. A marker for an unavailable provider
fails loading with its ID; it does not become terrain or silently disappear.

Legacy Ball markers `(160,64,255)`, `(255,96,64)` and `(64,128,255)` are now
nonblocking On declarations. They retain the restricted jump profile, disable
global Sticky and respectively inherit, deny or force Double Jump. Restricted
heights remain 75% without double jump / 35% with double jump. The Sticky material
`(255,64,160)` remains solid, including slopes; it modifies adhesion of an active
ball and does not itself grant permission to morph.

MGE keeps its existing triplets: Warp Solid/Screen/Zone G=47/48/49, No Walk Off
50/51/52, Air Dash 53/54/55, all with R=173, B=211. Existing Screen pixels import On.
Existing camera.xml screen rules import On/Off; conflicting common declarations
are rejected. Camera zones retain their own profiles and clipping, within common
screen permission. A common Local camera screen stays clipped to that screen.

## XML

Optional file: **`jk-runtime/mechanics.xml`**, relative to the map root.

```xml
<Mechanics version="1">
  <Mechanic id="ball-king.form">
    <Screens from="1" to="2" mode="on">
      <Parameter name="sticky" value="false" />
      <Parameter name="double-jump" value="off" />
      <Parameter name="jump-profile" value="standard" />
    </Screens>
    <Screens from="3" mode="off" />
  </Mechanic>
  <Mechanic id="more-items.jetpack">
    <Screens from="2" mode="local" />
    <Zone id="flight" screen="2" x="40" y="40" width="200" height="240"
          test="overlap" priority="1" equipment="loan-equipped" />
  </Mechanic>
</Mechanics>
```

Screen and pixel declarations merge only when modes and overlapping parameter
values agree. Contradictory declarations fail with the mechanic and screen;
XML is not a silent override. Zone parameters override screen parameters while
inside that zone. Highest priority wins. Overlapping zones with equal priority,
unknown IDs/parameters/attributes, invalid bounds and out-of-map screens are errors.
Zones use `test="center"` by default, or `overlap`. A Zone alone does not enable
an unmarked screen on a controlled map. XML limits: 1 MiB, 4096 declarations,
65536 expanded screen rules; external entities/DTDs are prohibited.

Ball parameters: `sticky=true|false`, `double-jump=inherit|on|off`,
`jump-profile=standard|restricted`, `jump-height=0.05..2` (explicit scale overrides
the profile). Omitted tuning preserves the provider's existing defaults.
Casual defaults to casual-plus when authored. Subframe authored activation defaults
to ordinary subframe correction; `quarter-step=true` adds quarter-step charging.
Bindings, visual preferences, measurements and refresh settings remain user options.

## Items and transitions

Jetpack and Hammer remain More Items inventory items. Map On/Zone grants a temporary
`loan-equipped` source by default; it never buys, grants, consumes or saves equipment.
`equipment="owned"` requires actual owned count and saved equipped choice. Leaving
permission removes the loan and restores effective ordinary equipment rules.
A loan cannot be unequipped through the durable inventory toggle. Rewinder permission
never supplies charges or bypasses its inventory, snapshot and restore checks.

Runtime's owned `MapMechanics.Watch` reconciles item/controller changes outside native
player component iteration. With the optional Harmony engine it runs before the
player tick; the Runtime command entity also reconciles between entity updates.
Effect execution checks permission directly, including before deferred teardown. Resources and bindings release through the normal
module scope. Ball exits at a collision-safe expansion; while under a low ceiling
it retains its small hitbox until expansion is possible. Hammer waits for a foreign
movement owner to release its lease. Started Warp/Dash actions retain their existing
completion/cancellation contract; a denied screen cannot initiate another action.

The MGE Gimmicks browser stays available for discovery, but saved user overrides
and manual override writes are suspended while any map-controlled mechanic is
present. This prevents the generic reflection/geometry tools from bypassing map
permission. The original override preferences are preserved for unrestricted maps.

## Scope of the contract

Mega Mapping Expansion retains its ordinary saved presentation checkbox and has
no common activation provider. `props/mega-mapping-expansion/scene.xml` describes
the scene; `props/mega-mapping-expansion/map.xml` describes native teleport links.
Disabling presentation does not sever routes.

Native wind/one-way wind and teleport pixels keep the game's loader and execution
semantics. JK+ low gravity/warp, Expansion Blocks MultiWarp, CustomWindSwitch and
VerticalWind keep their original formats and code. They are not falsely advertised
as controllable providers: using their names in mechanics.xml or a controlled tag
fails unless a provider explicitly registers that ID. Generic discovery does not
authorize arbitrary foreign field writes or unpatching. Runtime hard assembly
conflict policies remain available when a map requires incompatible mods absent.

Stereo Madness is a map-local exclusive course controller with no global enable
preference: course.xml and the course's native handoff remain authoritative.
Wardrobe appearance services and Screen Solver are not movement gimmicks; no
physics marker is introduced for them. Retired standalone Hammer/UIApi+ paths do
not create duplicate providers. Replays, Overlay+ and Run Verifier are outside
this contract.

## Provider integration

Register the stable ID, validated parameter schema and a callback that persists
only global enable Off in BeforeLevelLoad. Read/compile rules in Runtime preparation;
do not mutate the player or save there. At activation, install passive observers
when `NeedsController(id, savedEnabled)` is true, and call `Current`/`Resolve` at
the effect's native boundary. `HasRules` includes zone-only maps. `CanConfigure`
gates menus; `RequireUserEnable` protects the underlying setting/API.

Use `RuntimeApi.Mechanics.RegisterControlled` for an inspectable provider which
actually enforces this permission. Registration alone does not suppress physics.
Use `MapMechanics.Watch(owner, readState, apply)` only for changes that require
controller recomposition; track its returned lease in the activation context.
The command entity provides a safe fallback when the optional tick hook is unavailable. Never modify a component
collection from a body callback or draw callback. `ImportScreen` adapts already
parsed legacy metadata. `DeclareScreen` collects decoded pixels for world reuse.

Runtime reports malformed authoring before player activation. The diagnostic export
includes `mapMechanics` with authority, screen mode, source and denial reason. Inspect the provider's
existing mechanic diagnostics for availability and actual activity; permission does
not promise simulation support, route completion or run verification.
