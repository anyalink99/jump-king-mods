# Bend a panel and make a surface ripple

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

Use deformation for hanging cloth, flexible plants or ripples. A mesh is the picture divided into vertices. A field supplies motion. A deformation applies that motion while keeping part of the mesh pinned.

## Bend artwork with fields

A mesh supplies vertices. A field supplies displacement at a point. A deformation applies that displacement with a pinned profile. They remain separate so the same field can move several meshes, and one mesh can chain several deformations through `next`.

This complete scene bends a sample panel horizontally while its lower edge stays pinned:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="panelArt" path="mme-assets/panel.png" />
  </Assets>
  <Components>
    <Component id="pose" kind="transform">
      <Value name="position" value="240,180" />
    </Component>
    <Component id="wind" kind="field">
      <Value name="function" value="sine" />
      <Value name="amplitude" value="4,0" />
      <Value name="frequency" value="0,0.008" />
    </Component>
    <Component id="bend" kind="deformation">
      <Value name="field" value="wind" />
      <Value name="pin" value="0,48" />
      <Value name="extent" value="64,96" />
      <Value name="axis" value="x" />
    </Component>
    <Component id="panel" kind="mesh">
      <Value name="transform" value="pose" />
      <Value name="asset" value="panelArt" />
      <Value name="size" value="64,96" />
      <Value name="columns" value="8" />
      <Value name="rows" value="24" />
      <Value name="deformation" value="bend" />
    </Component>
  </Components>
  <Graph>
    <Node id="rate" op="constant" value="0.3" />
    <Node id="phase" op="integrate" inputs="rate" units="turns" />
  </Graph>
  <Bindings>
    <Bind id="phase" source="phase" target="wind/phase" />
  </Bindings>
</MappingSettings>
```

Increase amplitude for more bend, reduce rate for slower motion, and move the pin to change the fixed edge. Extent and falloff control the weight away from the pin. Choose local or world space deliberately when sharing a field across differently placed meshes.

Fields support sine, deterministic noise, a curve, and a spatial spring. A spring field has length, direction, stiffness, propagation, damping, displacement limit and impulse radius. Send an `impulse` action with numeric force at a contact point to make a local disturbance travel along it. A graph spring smooths one value; a spring field stores a spatial disturbance. They solve different problems.

Mesh geometry can be grid, polygon or disc. Projection can be planar, spherical, cylindrical or perspective; projectionTilt adjusts its orientation. Columns/rows prepare tessellation and can't change during play. Use enough vertices for the desired bend, then measure; a dense grid on every small prop wastes work. The [planet recipe](../../recipes/planet.xml) shows spherical projection and UV rotation using these same parts.

## Try it and change it

1. Copy `mme-assets/panel.png` from lesson 1 and load the example.
2. The panel bends horizontally; its lower edge stays fixed.
3. Change amplitude from `4,0` to `8,0` for stronger movement.
4. Change the graph rate from `0.3` to `0.1` for slower movement.
5. Move the pin to change the fixed point.

A field can use `sine`, `noise`, `curve` or `spring`. `decay` reduces its displacement away from its local Y origin: zero leaves it unchanged. For a traveling ripple, use [the spring-field example](patterns.md#disturb-a-spring-field). A field curve uses Vector2 samples such as `0,0;1,0;0,1`; graph curves instead use position:value keys. A deformation can chain another through `next`. [Component fields](../reference/components.md) explain the field, mesh and deformation settings.
