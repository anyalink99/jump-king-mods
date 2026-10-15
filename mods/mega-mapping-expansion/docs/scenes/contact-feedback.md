# Sensors and contact feedback

[Handbook](../index.md) / [Scene authoring](index.md)

Use a region sensor for вЂњthe King's body overlaps this area.вЂќ Use a surface sensor for вЂњthe movement/collision system observed contact with this block.вЂќ Use native hidden-wall events when preserving a hidden wall's exact behavior. These are different contracts; replacing one with another can change a map's triggers.

## Regions, shapes and membership

Sensors use centered transforms. A box of size `8,8` at `300,228` spans `(296,224)` to `(304,232)`. For an odd-width box, keep the half-pixel center with `pixelSnap=false` on its nonvisual transform. Disc uses size as its two diameters. Polygon takes ordered local `outline` points. The transform's parent, rotation, scale and screen also apply.

Region tests include shape boundaries. Native hidden walls use strict integer `Rectangle.Intersects`, which excludes a merely shared edge. Don't use a general region as a drop-in replacement for that native test. A region's position signal is the actor's center, **not** an estimated point on its boundary. Its normal is unavailable.

Membership emits `<id>.enter` and `<id>.exit`; `touching` remains true while a qualifying member is present. `initial=baseline` records the first sample without an entry event; `initial=fire` allows entry on that first sample. `dwell` delays entry until contact has lasted the given seconds. `hysteresis` expands the exit boundary for an existing region membership to reduce flicker. `actor` defaults to player; `*` accepts observed actors. Enabled controls sampling.

## Choose the phase before tuning the box

`phase=contact` observes completed movement evidence at the early player-update phase, before return-block behavior can overwrite the struck position. This is useful for fracture stamps on a return surface. `phase=scene` observes the current player pose in the scene update. It can already be the destination after a warp.

An area overlap doesn't mean the King landed on its upper face. A nonblocking teleport can activate from a side overlap. For a landing-only visual, require downward velocity, a previous feet position above the top, and current contact feet crossing it. Keep that evidence from the contact phase; reading the relocated player's feet can instead describe the exit. Test successful falls, sideways grazing and upward flight separately.

The event is queued with its actor, screen and typed payload. For stamps, use the event's captured position. Don't replace it later with `player.center` unless you deliberately want the current position after relocation.

## Select actual surfaces

For a warp light, listen to the actual warp surface, optionally selected by its
exact ID, and add the desired approach-direction condition. A box region alone
can enter when the player's edge only touches the block; the provider may require
strict overlap. The rectangle shown in the scene editor isn't proof of a native
warp contact.

A surface sensor has `shape=surface` and a selection. Tokens are separated by semicolons and match any selected surface:

| Selection | Match |
|---|---|
| `type:Full.Namespace.BlockType` | Original concrete block type |
| `tag:providerTag` | A tag supplied by a contact provider |
| `Full.Type@x,y,w,h` | One exact prepared surface ID |
| Empty selection | All observed surfaces |

Use exact IDs from inspection rather than guessing them. Slope aliases retain their source block identity. Only actual triangle contact counts; the empty half doesn't become a rectangular trigger. Position may be Estimated and normal Sampled: the sensor exposes evidence quality rather than promising a perfect geometric manifold.

`minimumSpeed` filters surface contacts in pixels per second. Each contact can emit enter/exit. `.impact` requires blocking resolver impact evidence. **Nonblocking return and warp blocks don't acquire an impact just because the King overlaps them.** React to their enter event when appropriate.

## Place a transient mark at contact

This complete scene selects JK+ warp blocks and stamps the sample mote at the captured point. It requires JK+ and a map containing its warp blocks. Replace the mote with a mark atlas for a real impact animation.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets><Asset id="markArt" path="mme-assets/mote.png" /></Assets>
  <Components>
    <Component id="pose" kind="transform"><Value name="screen" value="1" /></Component>
    <Component id="returns" kind="sensor">
      <Value name="transform" value="pose" /><Value name="shape" value="surface" />
      <Value name="selection" value="type:JumpKingPlus.Blocks.WarpBlock" />
      <Value name="phase" value="contact" />
    </Component>
    <Component id="marks" kind="stamp">
      <Value name="asset" value="markArt" /><Value name="duration" value="0.7" />
      <Value name="capacity" value="8" /><Value name="clock" value="presentation" />
      <Value name="layer" value="foreground" />
    </Component>
  </Components>
  <Rules>
    <Rule id="markReturn" event="returns.enter" cooldown="0.04">
      <Action op="stamp" target="marks" />
    </Rule>
  </Rules>
</MappingSettings>
```

No explicit point is needed: the action takes the contact event's position. A stamp's optional mask is screen-space prepared alpha; derive it from the painted terrain silhouette to keep cracks inside both rectangular and sloped surfaces. It can also clip any other transient mark. Duration bounds the lifetime; capacity bounds simultaneous marks. Restore clears stamps without replaying old impacts.

The same event can activate a light, increment a variable, emit another event, burst particles or impulse a spring field. The contact contract doesn't depend on cracks. Keep the event's position/normal/velocity evidence if another consumer needs it.

## Preserve native hidden walls

MME already observes the game's hidden-wall transitions. Keep the native wall in `props/hidden_walls/hidden_wallN.xml`. Its `texture_name` becomes part of the event ID: `hiddenwallenter:<texture_name>` and `hiddenwallexit:<texture_name>`. Multiple rectangles in one RaymanData are a single native membership. Sound settings, PlayEveryTime re-entry, integer hitbox rounding and strict edges remain owned by the game.

Use distinct texture names when two different walls need different reactions. Two walls sharing a name publish the same event ID. To hide old artwork, supply a transparent texture under each distinct name; don't remove the wall and approximate it with a region. Keep its original sfx_info if its native voice should still play. Adding a second MME sound action would duplicate it.

For a new wall, create `props/hidden_walls/hidden_wall1.xml` with the native definition below. Supply `props/hidden_walls/textures/closeZone.png` in your Worldsmith source and compile it to the normal native texture. A transparent image makes this a trigger without visible artwork. Position places the artwork; explicit hitboxes place the trigger independently. `use_screen_coords=true` interprets those rectangles in the named screen's coordinates.

```xml-fragment
<RaymanCollection>
  <walls>
    <RaymanData>
      <texture_name>closeZone</texture_name>
      <use_screen_coords>true</use_screen_coords>
      <Position><X>296</X><Y>120</Y></Position>
      <hitboxes>
        <Rectangle><X>296</X><Y>120</Y><Width>8</Width><Height>8</Height></Rectangle>
      </hitboxes>
      <sfx_info><play_mode>PlayEveryTime</play_mode></sfx_info>
    </RaymanData>
  </walls>
</RaymanCollection>
```

This is native XML, not a MappingFragment. With no named enable clip, the native wall uses its default Rayman sound; an existing wall can keep its `sfx_name_enable`/`sfx_name_disable` event-music keys. Put additional Rectangle children inside the same hitboxes to make a union with one entry/exit state. Put another RaymanData in walls for an independent trigger.

This complete construction file counts native entries for a wall named `closeZone`. The native wall and its texture must already exist in the map; preparation checks that name.

```xml mapping_settings.xml
<MappingSettings version="1" saveId="handbook-native-wall">
  <Variables><Variable id="entries" kind="Integer" value="0" scope="Save" /></Variables>
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,60" /></Component>
    <Component id="label" kind="text"><Value name="transform" value="pose" /></Component>
  </Components>
  <Graph>
    <Node id="entries" op="input" source="variable:entries" />
    <Node id="caption" op="format" inputs="entries" />
  </Graph>
  <Bindings><Bind id="caption" source="caption" target="label/value" /></Bindings>
  <Rules>
    <Rule id="count" event="hiddenwallenter:closeZone">
      <Action op="increment" target="entries" value="1" />
    </Rule>
  </Rules>
</MappingSettings>
```

For вЂњonce per jump,вЂќ declare an Attempt Boolean, arm it on jump, condition only the counting rule on it, and consume it after counting. That policy must not accidentally gate the native sound. For a faithful remake, compare trigger entry on the successful path as well as failed paths; an edge-only contact is a useful regression case.

## Diagnose a missing or extra reaction

Inspect touching and position quality first. Confirm the screen and phase. For surfaces, verify type/ID selection and whether the block actually blocks. For native walls, verify texture_name, grouped rectangles and native audio ownership. Then check the rule condition and cooldown. A visual mask clips drawing; it doesn't shrink the trigger.
