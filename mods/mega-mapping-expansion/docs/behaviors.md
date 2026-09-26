# Regions, attachments and scene effects

Requires **JK Runtime 1.30 or newer 1.x**. These are
presentation changes: they do not create collision, change player physics or
replace native level backgrounds. Use a background-layer prop for an authored
background variant. XML remains `MegaMapping version="1"`; old scenes need no
migration. Unknown attributes and non-finite numbers are rejected.

## A carried lantern for 30 seconds

Copy this complete scene into an existing map's
`props/mega-mapping-expansion/scene.xml`. Enter the rectangle on screen one:
the panel becomes gold and a warm light follows the King. Leaving does not
cancel it. Re-entering refreshes the same lamp to 30 seconds. Expiry restores
the blue panel and removes the lamp. There is no asset loading on entry.

```xml
<MegaMapping version="1">
  <VectorAssets>
    <Asset id="blue" width="48" height="48" pixelSnap="true"><Shape type="rect" x="0" y="0" width="48" height="48" fill="#6083BA" /></Asset>
    <Asset id="gold" width="48" height="48" pixelSnap="true"><Shape type="rect" x="0" y="0" width="48" height="48" fill="#FFD27A" /></Asset>
  </VectorAssets>
  <Nodes><Node id="panel" asset="blue" screen="1" x="240" y="160" /></Nodes>
  <LightTemplates><Light id="lantern" radius="90" color="#FFD27A" intensity="1" attach="player.center" offsetY="-18" occludeKing="true" /></LightTemplates>
  <Effects>
    <Effect id="carried-lantern" duration="30" clock="gameplay" repeat="refresh" group="lantern-state">
      <Set target="panel" property="asset" value="gold" />
      <SpawnLight template="lantern" />
    </Effect>
  </Effects>
  <Regions><Region id="pickup" screen="1" x="180" y="200" width="120" height="100" enter="carried-lantern" hysteresis="2" /></Regions>
</MegaMapping>
```

The exported kit includes the same runnable scene in `behaviors/`. Merge its
sections with your scene; copying over an existing scene would replace it.
PNG props use `property="texture"` and a declared Texture ID instead of
`property="asset"`. The new asset's own dimensions, atlas timing and origin
rules apply. Use matching dimensions/pivots for a seamless visual swap.

## Region contract

| Attribute | Default | Meaning |
| --- | --- | --- |
| `id` | required | Unique, case-sensitive region ID, 1–120 characters |
| `screen` | 1 | Existing native screen, one-based |
| `x`, `y`, `width`, `height` | 0 | Rectangle inside 480×360; positive width/height |
| `test` | `hitbox` | `hitbox` overlap, `center`, `feet`, fully `contained`, or native `standing` contact |
| `anchor` | absent | Use an existing Anchor's screen and rectangle instead of repeating screen/x/y/width/height |
| `grounded` | false | Require the native BodyComp.IsOnGround state in addition to the selected test |
| `spawnInside` | `fire` | Fire on initial inside sample; `baseline` remembers membership without entry |
| `hysteresis` | 0 | Expand exit boundary by 0–32 pixels; entry boundary remains unchanged |
| `dwell` | 0 | Continuous gameplay seconds inside before entry, 0–60 |
| `enter`, `exit` | empty | Named effects; optional independently |
| `owner` | empty | Optional shared scene owner for mutually exclusive states; only with lifetime=effect |
| `lifetime` | `effect` | Use effect duration; `inside` releases this region's enter effect on exit |
| `once` | false | Consume entry once per run/snapshot timeline; exit still works |
| `requiresFlag`, `equals` | empty | Observe region only while an exact flag value matches |

Sampling occurs after the active EntityManager finishes all component updates,
including LateUpdate. Modal UI/native pause suppresses region observation.
Teleporting to an inside position counts as entry; screen changes/lost actor
count as exit on the next gameplay sample. Crossing an entire region between
two samples does **not** fire a swept-path trigger. Use a wider region when
fast travel must reliably touch it. Regions are evaluated by ordinal ID.

`standing` requires both native grounded state and blocking `IBlock` contact in
the one-pixel strip under the final body hitbox. Only the actual overlap with the
region counts, not the entire collider's bounding box. Non-blocking water does
not impersonate a platform. Native and third-party block factories work through
the same public collision query. No query runs unless a standing region exists
on the current screen. `standing` rejects nonzero hysteresis; `anchor` standing
regions require `kind="solid"`. An anchor is metadata, not a new physical block.

For the exact existing Hidden Kingdom interaction, use the native event adapter
described in [Native workflows](native-workflows.md), without duplicating hitboxes.

`baseline` suppresses only the first membership observation of the run; it does
not suppress later teleports into the region. With `once`, use a save-scoped
flag if consumption must survive a new run. `inside` cannot own a stacked enter
effect. Its exit cancels only that region's effect, not another owner's layer.

## Effects and overlap

| Attribute | Default | Meaning |
| --- | --- | --- |
| `id` | required | Effect definition ID |
| `duration` | 0 | 0 = until cancelled/replaced/unloaded; otherwise 0–86400 seconds |
| `clock` | `gameplay` | `gameplay` freezes under native/modal pause; `presentation` continues when scene updates run |
| `repeat` | `refresh` | `refresh`, `replace`, `ignore`, `stack` |
| `maxStacks` | 4 | 1–32 instances of this owner/ID |
| `priority` | 0 | Higher priority applies later |
| `group` | empty | Nonempty key replaces that owner's previous group member |
| `fadeIn`, `fadeOut` | 0 | Included within duration; fade-out requires finite duration |

Refresh and ignore return a lease for the **existing instance**. Refresh resets
its age/envelope; it does not replace its original definition or attachment.
Use replace for changed definitions. Repeated leases to one instance are aliases:
disposing any alias cancels that instance. Use a unique module owner and retain
one lease per logical request. Groups are local to an owner; two regions have
different owners. For mutually exclusive room states, give the triggering
regions/rules the same `owner="room-weather"` and their effects the same
`group="weather"`. They then share `scene:room-weather` ownership and replace
the previous group member. Shared owner names are limited to 120 characters;
they cannot be used with inside-scoped regions.

Every effect is validated before any existing effect is removed. Changes apply
in priority → definition ID → owner ID → instance order. Each property starts
from the authored baseline and recomposes all current layers. Cancelling an
older effect therefore cannot overwrite a newer one with a stale saved value.

`Set` accepts `mode="set"`, `add` or `multiply`. Add/multiply are numeric only,
with finite signed operands in ±1,000,000; final values clamp to the property's
validated range. Set values must already fit that range. Fade envelopes blend
numeric values and spawned-light intensity. Booleans, colors and asset IDs
switch discretely when the envelope is nonzero; this is not a texture crossfade.
Overlapping fades compose from the lower layer's current value.

Budgets: 512 definitions, 512 regions, 512 rules, 256 flags, 128 light templates;
128 active effects and 32 spawned lights; 64 changes and 8 spawns per effect.
These are safety ceilings, not frame-rate guarantees. Each active moving light
rebuilds its attenuation field; prefer a small number of carried lights.
Durations use clamped game update deltas (at most 0.1 seconds per native update),
not wall time. Minimized/stalled applications cannot expire minutes of gameplay.

## Supported properties

Property names are case-sensitive. The public API exposes the same names,
types, choices and numeric bounds through `DescribeProperties(objectId)`.

| Target | Properties |
| --- | --- |
| Prop / Node | `visible`, `opacity`, `tint`, `x`, `y`, `offsetX`, `offsetY`, `rotation`, `scale`, `scaleX`, `scaleY`, `amplitudeX`, `amplitudeY`, `degrees`, `windStrength`, `duration`, `motion`, plus `texture` for Props / `asset` for Nodes |
| Authored Light | `enabled`, `intensity`, `radius`, `color`, `x`, `y`, `offsetX`, `offsetY`, `angle`, `rimIntensity` |
| Fog | `opacity`, `speed`, `color` |
| Rain | `opacity`, `speed`, `wind`, `color` |
| Emitter | `opacity`, `driftX`, `driftY`, `tint` |
| Water | `opacity`, `color`, `color2`, `highlight`, `reflection`, `reflectionOpacity`, `sceneReflectionOpacity`, `reflectionScaleY`, `ripple`, `interactive`, `surfaceTension`, `waveSpread`, `waveDamping`, `splashStrength`, `wakeStrength` |
| Puddle | `opacity`, `color`, `reflectionScaleY`, `ripple`, `rainRings`, `perspective` |
| Bush | `sway`, `speed`, `reactRadius`, `reactStrength`, `backColor`, `frontColor`, `highlightColor` |
| Surf | `period`, `height`, `wind`, `spray`, `break`, `chop`, `foamDetail`, `color`, `crestColor` |
| Planet | `period`, `cloudPeriod`, `cloudOpacity` |
| ShadowSurface | `opacity`, `color`, `scaleY`, `shearX` |
| `anchor:ID` (advancedLighting only) | `blocksLight`, `lightOpacity` |
| `options` | `ambientIntensity`, `ambientLight`, `tint`, `tintOpacity`, `playerRimOpacity` |
| `screen:N` (declared ScreenLook) | `ambientScale`, `ambientLight`, `ambientIntensity`, `playerRimScale` |

Motion changes use already declared paths/tracks; setting `motion="path"`
requires a valid authored path. Structural changes to layers, geometry, atlas
contents or topology require scene reload. Counts not listed above also require reload. Spawned lights are
owned by their effect; edit the template and reload to change their structure.
Attachment position wins over direct X/Y changes on attached lights.
Use offsetX/offsetY to move those sockets. Authored and spawn offsets are bounded
to ±4096 pixels each. ScreenLook ambientIntensity accepts -1 for inheritance or
0..1, using immediate set only; use ambientScale for animated fades or numeric
composition. Empty ScreenLook ambientLight restores color inheritance.

## Attachments

| Object kind | Attachments | Animation tracks | Effect editing | Reflection |
| --- | --- | --- | --- | --- |
| PNG Prop | Player / Prop / Node | Yes | Supported property table above | Composed world |
| Vector Node | Player / Prop / Node | Yes | Supported property table above | Composed world; legacy per-node axis-aligned approximation |
| Light | Player / Prop / Node | Pulse/sweep | Light properties; effect-owned spawning | Affects composed lighting |
| Fog / Rain / Emitter | No | Native procedural timing | Listed weather fields | Composed world |
| Bush / Water / Puddle / Surf / Planet | No | Kind-specific procedural timing | Listed material/weather fields; reload geometry | Kind-specific/composed world |
| Anchor / shadow receiver | No | No | Listed lighting/shadow fields; reload geometry | Receiver/clipping role |

Stateful flags and rules are scene-wide; they are not fields embedded into every
object kind. A single effect can coordinate tree windStrength, rain wind and fog
speed as a shared weather preset. Use one shared owner/group to switch presets.
Per-node legacy reflection is not rotation/deformation aware; prefer composed
world reflection for animated attached artwork.

Props, Nodes, Lights and LightTemplates accept `attach`, `offsetX`, `offsetY`.
Targets: `player` / `player.center`, `player.feet`, or a Prop/Node ID. Empty
attachment means authored screen coordinates. The King socket is derived from
the live native body hitbox, independent of skin/collection pixels.

Offsets are local pixels. A Prop/Node also adds its X/Y and animation position
as a local offset. Parent position, rotation and scale transform that offset;
child artwork keeps its own scale. The parent's facing reflects socket X offsets.
Lights inherit the resolved anchor screen and use their offsets instead of X/Y.
Missing/hidden parents hide attached visuals. Chains are validated, limited to
32 parents, and cycles are rejected. Attachments to props follow the same motion
pose used to draw them. A player-attached light (including through a prop chain)
ignores the player's own occluder so the light cannot extinguish inside the body.
Other lights retain authored player occlusion.

## Temporary palette while inside, and a persistent switch

This complete scene shows independent ownership: the room tint lasts only while
inside; touching the small switch sets a save-scoped flag and makes its panel
gold until native save reset. A start rule restores the appearance on loading.

```xml
<MegaMapping version="1">
  <Options saveId="example.power-room.v1" />
  <VectorAssets><Asset id="panel" width="24" height="24"><Shape type="rect" x="0" y="0" width="24" height="24" fill="#FFFFFF" /></Asset></VectorAssets>
  <Nodes><Node id="switch-panel" asset="panel" x="240" y="240" tint="#6083BA" /></Nodes>
  <Flags><Flag id="powered" value="false" scope="save" /></Flags>
  <Effects>
    <Effect id="room-palette"><Set target="options" property="tint" value="#B8CFFF" /><Set target="options" property="tintOpacity" value="0.25" /></Effect>
    <Effect id="power-on"><Set target="switch-panel" property="tint" value="#FFD27A" /></Effect>
  </Effects>
  <Regions>
    <Region id="room" x="100" y="100" width="280" height="220" enter="room-palette" lifetime="inside" />
    <Region id="switch" x="220" y="220" width="40" height="40" once="true" />
  </Regions>
  <Rules>
    <Rule id="press-switch" event="enter:switch" setFlag="powered" value="true" />
    <Rule id="apply-power" event="flag:powered" requiresFlag="powered" equals="true" effect="power-on" />
    <Rule id="restore-power" event="start" requiresFlag="powered" equals="true" effect="power-on" />
  </Rules>
</MegaMapping>
```

## Events, flags and saved progress

For multiple steps, waiting, branches and cancellation, use
[scene behavior trees](behavior-trees.md). They share these events, flags and
effects; simple Rules remain the shortest path for a single reaction. Event
dispatch is suspended during gameplay pause; queued events resume with gameplay.

Rules match `event`, optionally `screen`, `requiresFlag`/`equals`; perform
`effect`, `setFlag`/`value`, and/or `sound`; and support `once` and gameplay `cooldown`
(0–86400 seconds). Events are queued, never recursively dispatched. Rules run
by ordinal ID; conditions see earlier changes from that dispatch. Unchanged
flag writes emit no event. Failures disable the offending observer and appear
in diagnostics. A cycle exceeding 256 events per tick disables rules until reload.

Built-ins: `start`, `enter:<region>`, `exit:<region>`, `flag:<flag>` plus Runtime
`chargestarted`, `chargeended`, `jump`, `landed`, `supportlost`, `teleported`,
`screenchanged`, `pausechanged`. Native event availability follows Runtime's
observations. Restore events are intentionally not replayed as author events.
Native events retain their originating screen in the queue; moving before rule
dispatch does not change their screen filter. `sound` is a key from the game's
loaded `audio.music.event_music`, not a path. Missing cues reject scene resource
preparation. Playback uses the native sound object and its music/SFX preferences;
it does not create a second mixer. See the native workflow chapter for packaging.
`hiddenwallenter:TEXTURE_NAME` and `hiddenwallexit:TEXTURE_NAME` observe the
original Hidden Walls contact test and install no adapter when unused.
Custom mods and the debug inspector can call `Emit` with another event ID.

Flag and rule IDs have a 120-character limit, leaving room for generated owner/event
prefixes. Flags contain strings up to 256 characters. `scope="run"` is the default.
`scope="save"` requires a stable, map-unique `Options.saveId`. Rename a flag
with `from="old-id"` to migrate an old key; an existing new key wins. Changing
saveId starts a different map identity. Effect timers themselves are not disk
persisted: save a flag and reconstruct the state with a start rule.

Save files live under the native resolved save directory in
`MegaMapping/<SHA256(saveId)>.xml`, with atomic replacement and a retained backup.
The native save worker receives copied data from the game thread. A native
DeleteSaves call advances the context epoch; old sidecars remain recoverable but
cannot reactivate. Changing the native directory rejects stale writes. A third
party that redirects saving only inside its own file writer, without changing
native PREFIX/SAVE_FOLDER, needs a separate adapter; it is not automatically
recognized as an independent slot. Do not enable saved flags for such a slot
integration until that adapter has been verified.

## Restore and reload

Runtime snapshot participant `mega.mapping.scene-state` version 1 captures
effects, clocks, region membership/dwell/once, rules/cooldowns, tree cursors/waits/once, flags and queued
events. Restoring invalidates old public leases and restores the current scene's
semantic state. It does not duplicate entry. A snapshot from another scene/reload
is rejected. Camera/body restoration remains Runtime's responsibility. Transient
water/cloth/reaction histories are rebuilt; pixel-exact visual replay is not promised.

Reload validates/prepares a separate scene before switching. Run effects and
region state restart on successful reload; saved flags reload from their sidecar.
Failed preparation keeps the current scene. Old effect handles become inactive.

## Inspector and tooling

Open **Mods → Mega Mapping Expansion → Mapping inspector**. It lists effective
object values and their winning owner, active effects/remaining time, definitions,
regions, flags, tree status/waiting nodes and errors. Launch with `-debug` to edit numeric values with arrows,
wheel or pointer drag, choose asset/color values, activate/cancel effects, simulate
declared rule/tree events and export an effect recipe. Inspector overrides have their
own owner and high priority; Clear removes only those overrides. Closing keeps
the preview; export writes `preview/inspector-effect.xml`, never scene.xml.
Region detail shows a scaled screen rectangle and sampled player hitbox; world
outlines can be toggled separately. Advance scene effects by one second to test
expiry without advancing player physics. Emit enter event invokes authored event
rules; it does not teleport the actor or impersonate physical region membership.

Use `SceneCacheCompiler --validate <level-root>` for validation without writing
a cache. `--schema <output.xsd>` exports the DTO schema for an **expanded** scene.
Includes, templates and materials are authoring conveniences removed before that
schema applies. XSD describes shape/types; semantic ranges and cross references
remain the validator's responsibility. Error messages retain object/field context
and source file/line for recognized IDs from included modules.

See [Scene API](scene-api.md) for external mods and [Parameter reference](parameter-reference.md)
for rendering fields. Camera effects, swept regions, texture crossfades
and a visual behavior graph are separate extensions; the runtime does not
interpret arbitrary scripts or expressions in XML.
