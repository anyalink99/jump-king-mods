# Scroll textures, burst particles and ripple a surface

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

Choose one example below and try it by itself before combining it. Copy `surface.png`, `mote.png` or `panel.png` when named from the supplied sample into `mme-assets`. Paste the complete file into `mapping_settings.xml`, then [build and load](../getting-started/worldsmith.md).

The scrolling example moves the texture within a stationary sprite. The burst example emits particles on a jump. The spring example sends a traveling disturbance through a field. The nested example shows how to repeat a composition with independent IDs. [Combining examples](../getting-started/combining.md) shows the merge steps.

## Scroll a material

An integrator turns a rate into an accumulated offset. Wrapping the texture makes that offset repeat without moving the sprite's anchor:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="art" path="mme-assets/surface.png" />
  </Assets>
  <Components>
    <Component id="pose" kind="transform">
      <Value name="position" value="208,164" />
    </Component>
    <Component id="material" kind="material">
      <Value name="address" value="wrap" />
    </Component>
    <Component id="surface" kind="sprite">
      <Value name="transform" value="pose" />
      <Value name="asset" value="art" />
      <Value name="material" value="material" />
    </Component>
  </Components>
  <Graph>
    <Node id="rate" op="constant" value="0.05" />
    <Node id="turn" op="integrate" inputs="rate" units="turns" clock="Presentation" />
    <Node id="zero" op="constant" value="0" />
    <Node id="offset" op="vector" inputs="turn;zero" units="turns" />
  </Graph>
  <Bindings>
    <Bind id="scroll" source="offset" target="material/uvOffset" />
  </Bindings>
</MappingSettings>
```

Rate 0.05 moves one full texture width in twenty Presentation seconds. Negative rate reverses it. Use UV scale to repeat a pattern more often. Clamp is appropriate for a nonrepeating image; wrap is appropriate only when its edges should join. A sine node can turn the same accumulated phase into oscillation instead of continuous scrolling.

## Burst particles from an event

Keep rate zero for a burst-only emitter. A native jump event provides its captured position; the burst action uses that point instead of the emitter's fallback transform:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="mote" path="mme-assets/mote.png" />
  </Assets>
  <Components>
    <Component id="pose" kind="transform">
      <Value name="position" value="240,180" />
    </Component>
    <Component id="sparks" kind="emitter">
      <Value name="transform" value="pose" />
      <Value name="asset" value="mote" />
      <Value name="rate" value="0" />
      <Value name="capacity" value="64" />
      <Value name="lifetime" value="0.7" />
      <Value name="velocity" value="0,-35" />
      <Value name="spread" value="60,30" />
      <Value name="acceleration" value="0,45" />
      <Value name="clock" value="presentation" />
    </Component>
  </Components>
  <Rules>
    <Rule id="jumpSparks" event="jump">
      <Action op="burst" target="sparks" value="12" />
    </Rule>
  </Rules>
</MappingSettings>
```

The Number value is a particle count, truncated and bounded by the prepared capacity; without it, the native action defaults to 16. Each particle retains its birth position/velocity. Continuous streams reconstruct after restore, while these historical bursts clear. A contact event can drive the identical burst at a surface point.

## Disturb a spring field

A spring field stores displacement along a line. This scene gives a panel a ripple at activation. It uses world space so its field origin and an event point have the same coordinate system:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="art" path="mme-assets/panel.png" />
  </Assets>
  <Components>
    <Component id="pose" kind="transform">
      <Value name="position" value="240,180" />
    </Component>
    <Component id="ripple" kind="field">
      <Value name="function" value="spring" />
      <Value name="origin" value="208,180" />
      <Value name="direction" value="1,0" />
      <Value name="length" value="64" />
      <Value name="amplitude" value="0,1" />
      <Value name="segments" value="32" />
      <Value name="damping" value="4" />
      <Value name="limit" value="12" />
    </Component>
    <Component id="bend" kind="deformation">
      <Value name="field" value="ripple" />
      <Value name="space" value="world" />
      <Value name="axis" value="y" />
      <Value name="pin" value="0,48" />
      <Value name="extent" value="64,96" />
    </Component>
    <Component id="panel" kind="mesh">
      <Value name="transform" value="pose" />
      <Value name="asset" value="art" />
      <Value name="size" value="64,96" />
      <Value name="columns" value="16" />
      <Value name="rows" value="8" />
      <Value name="deformation" value="bend" />
    </Component>
  </Components>
  <Rules>
    <Rule id="disturb" event="scene.ready">
      <Action op="impulse" target="ripple" value="80" />
    </Rule>
  </Rules>
</MappingSettings>
```

scene.ready has no point payload, so the impulse uses the field origin. To react to a surface, change the event to the chosen sensor's enter/impact and retain its captured position. Stiffness restores the line, propagation couples neighbors, damping removes motion, limit bounds displacement, and impulseRadius controls the disturbed fraction of the line.

A graph spring follows one target value. A spring field carries a spatial wave. Sine/noise fields don't advance their phase just because they have a clock: bind phase to an integrator or time-based graph. Field curves are Vector2 point lists, such as `0,0;1,0;0,1`; graph curves instead use numeric `position:value` keys. Keep those formats distinct.

## Nest a reusable composition

This recipe wraps the embedded stream recipe. Its caller supplies one position; the nested parts receive a namespaced emitter export:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="mote" path="mme-assets/mote.png" />
  </Assets>
  <Recipes>
    <Recipe id="updraft">
      <Parameters>
        <Param name="position" kind="Vector2" />
      </Parameters>
      <Instances>
        <Instance id="particles" recipe="precipitation">
          <Arg name="asset" value="mote" />
          <Arg name="position" value="${position}" />
          <Arg name="area" value="32,8" />
          <Arg name="velocity" value="0,-60" />
          <Arg name="rate" value="10" />
        </Instance>
      </Instances>
      <Exports>
        <Export id="emitter" role="component" target="@particles.emitter" />
      </Exports>
    </Recipe>
  </Recipes>
  <Instances>
    <Instance id="draft" recipe="updraft">
      <Arg name="position" value="240,320" />
    </Instance>
  </Instances>
</MappingSettings>
```

The external port is `draft.emitter`, even though the emitter belongs to a nested instance. Callers needn't depend on the nested recipe's internal names. Add art, sensors or lights to this wrapper as needed; keep parameters about the behavior a caller wants to control.

These are compositions, not new runtime object types. Use [the registry](../../reference/PARAMETERS_GENERATED.md) to find compatible properties and [the inspector](../reference/troubleshooting.md) to follow each input through its output.
