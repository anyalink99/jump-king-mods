# Make rain, rising motes and bursts

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

An emitter repeatedly creates small copies of an image. Use it for rain, sparks or motes. It uses a prepared pool; you choose its size before the scene starts.

## Emit an upward stream

This stream moves upward because velocity Y is negative. `area` is the spawn region, not the full region particles will occupy. The emitter is centered on its transform:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="mote" path="mme-assets/mote.png" />
  </Assets>
  <Components>
    <Component id="source" kind="transform">
      <Value name="position" value="240,330" />
    </Component>
    <Component id="stream" kind="emitter">
      <Value name="transform" value="source" />
      <Value name="asset" value="mote" />
      <Value name="shape" value="box" />
      <Value name="area" value="40,8" />
      <Value name="velocity" value="0,-80" />
      <Value name="spread" value="8,12" />
      <Value name="rate" value="12" />
      <Value name="lifetime" value="3" />
      <Value name="capacity" value="64" />
      <Value name="seed" value="7" />
      <Value name="clock" value="presentation" />
    </Component>
  </Components>
</MappingSettings>
```

Rate × lifetime estimates the steady live population: here 36 particles, below capacity 64. Capacity is a prepared pool, not a rate. Shapes are point, box, line and disc. Velocity, spread, acceleration and size shape the stream; a fixed seed makes it repeatable. Bind rate to charge or sensor membership to start and stop emission. Existing particles live out their lifetime.

Continuous particles reconstruct from the scene clock after restore. Bursts are transient and clear instead of replaying historical births. [Contact feedback](contact-feedback.md) shows transient stamps; use a material mask or captured group when a stream must remain inside a particular silhouette.

## Try it and change it

1. Copy `mme-assets/mote.png` from the combined sample and load the example.
2. Motes should rise from the bottom center because velocity Y is negative.
3. Set velocity to `0,80` and move the source upward to make them fall.
4. Increase `rate` to `20`, keeping capacity at least rate times lifetime for this continuous stream.

`area` is where particles are born, not their full travel area. `spread` varies velocity; `acceleration` changes it over the particle lifetime. Shapes are point, box, line and disc. A fixed seed makes the pattern repeatable. For a jump-triggered burst, use [the burst example](patterns.md#burst-particles-from-an-event). Continuous streams reconstruct after a snapshot; old bursts and stamps clear.
