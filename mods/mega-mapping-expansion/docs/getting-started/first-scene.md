# Your first reactive scene

[Handbook](../index.md) / [Getting started](index.md)

Start with a map that already loads in Jump King. Keep its normal collision, spawn, Babe, screen art and native settings. A scene file adds presentation; it doesn't supply the rest of the level.

Install MME and its required JK Runtime version from the [setup guide](index.md#before-you-start), with one active copy of each. Copy the `mme-assets` directory from [the minimal example](../../examples/minimal/mapping_settings.xml) into your map source. These lessons use that artwork so you can get a visible result before making your own assets.

## First scene

Create `mapping_settings.xml` at the playable map root, beside `level_settings.xml`:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets><Asset id="panelArt" path="mme-assets/panel.png" /></Assets>
  <Components>
    <Component id="panelPose" kind="transform">
      <Value name="position" value="240,160" />
      <Value name="screen" value="1" />
    </Component>
    <Component id="panel" kind="sprite">
      <Value name="transform" value="panelPose" />
      <Value name="asset" value="panelArt" />
    </Component>
  </Components>
</MappingSettings>
```

Build with your usual map tool and load it with both mods enabled. On screen 1, the image's top-left corner should appear at `(240,160)`. Change that position to `180,100` and rebuild. This confirms you're editing the source your build actually uses.

There are three IDs here. `panelArt` names a resource, `panelPose` holds a transform, and `panel` draws the resource at that transform. References use IDs, not paths. This separation lets several drawings share a material or move under one parent transform.

If nothing appears, inspect the first error in Mapping inspector. Moving a sprite won't fix an asset that failed to load. [Troubleshooting](../reference/troubleshooting.md) gives a short diagnostic path.

## Coordinates and anchors

Coordinates are local to a 480 by 360 screen. X grows rightward; Y grows downward. A sprite's `origin` is the point in its image placed at the transform position. The default `0,0` anchors the top-left corner. For a 64 by 32 image, `origin=32,16` anchors the center. A mesh's geometry is centered on its transform instead.

Screen numbers begin at 1. A transform with `screen=0` participates on every visible screen using the same local coordinates. Use that for repeated decoration or a HUD. To restrict a rule to a screen, test `player.screen` in a graph condition; rules don't have a screen field.

Transforms also accept rotation in degrees, a two-axis scale ratio, and a `parent` transform ID. Child positions are relative to their parent. Place the parent anchor where the group should rotate or grow, then place children around it. Materials hold color, opacity and sampling independently of the transform.

Leave `pixelSnap=true` for pixel art. Calculations retain precision; final drawing rounds geometry to logical pixels. Intentionally smooth drawing requires an explicit opt-out. See [pixel policy](../scenes/objects.md#keep-pixels-whole).

## Make an area react

Replace the first file with this complete scene. It needs no artwork. A dim blue light becomes stronger while the King's body overlaps the central area:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="zonePose" kind="transform">
      <Value name="position" value="240,180" />
      <Value name="screen" value="1" />
    </Component>
    <Component id="zone" kind="sensor">
      <Value name="transform" value="zonePose" />
      <Value name="size" value="120,80" />
      <Value name="phase" value="scene" />
    </Component>
    <Component id="lamp" kind="light">
      <Value name="transform" value="zonePose" />
      <Value name="radius" value="70" />
      <Value name="color" value="#88BBDD" />
      <Value name="intensity" value="0.02" />
    </Component>
  </Components>
  <Graph>
    <Node id="inside" op="input" source="zone.touching" />
    <Node id="dim" op="constant" value="0.02" />
    <Node id="bright" op="constant" value="0.09" />
    <Node id="selected" op="select" inputs="inside;bright;dim" />
    <Node id="response" op="constant" value="12" />
    <Node id="brightness" op="smooth" inputs="selected;response" clock="Presentation" />
  </Graph>
  <Bindings><Bind id="brightness" source="brightness" target="lamp/intensity" /></Bindings>
</MappingSettings>
```

The sensor doesn't draw or change collision. Its transform marks the center: this box spans X 180РІР‚вЂњ300 and Y 140РІР‚вЂњ220. `phase=scene` samples the current pose in the scene update. Contact-phase sensors capture movement evidence earlier, before return behavior can relocate the player.

Read the graph from top to bottom. `inside` is Boolean. `select` chooses a Number. `smooth` approaches that Number over time. The binding sends the result to `lamp/intensity`. The same sensor could select a frame, bend a mesh or reveal text; nothing about it belongs specifically to a light.

Try a smaller sensor, another color, then a slower response such as `4`. You have changed when the reaction happens, what it looks like, and how quickly it changes using separate controls.

## Choose a binding, effect or tree

A binding expresses РІР‚Сљwhile insideРІР‚Сњ or РІР‚Сљfollow this value.РІР‚Сњ An effect expresses РІР‚Сљflash for half a second.РІР‚Сњ A rule connects an event to immediate actions. A control tree handles a sequence that waits or branches. Continue with [signals, graphs and effects](../scenes/behaviors.md) for a saved jump counter and feedback example.

## Inspect an edit

Open **Mapping inspector** from the game menu. Inspect `zone.touching`, then `lamp/intensity` and its contributing owners. If touching changes but intensity doesn't, follow the graph and binding. If touching never changes, check the sensor's center, size, screen and phase.

In a `-debug` run you can preview bindable values, activate authored effects and reload presentation. **Clear preview** removes only inspector contributions. Export writes a fragment to include deliberately; it doesn't rewrite your map source. Failed reload preparation leaves the active scene intact. Layout/collision changes need a restart, and reload is blocked while another session owns world state.

Save your XML and restart the map, or use the presentation reload described in [scene files and packaging](files.md). MME refreshes its local cache automatically. Once the first scene behaves predictably, split it into fragments or make [a reusable recipe](../scenes/recipes.md).
