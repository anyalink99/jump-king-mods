# Reusable recipes

[Handbook](../index.md) / [Scene authoring](index.md)

A recipe is an authoring preset. It expands into ordinary components, graph nodes, bindings, variables, effects and nested instances before validation. A planet or a gaze response has no dedicated runtime entity. You can copy, change and combine its XML without adding a mod.

## Use the library

The library is embedded in MME and the compiler. You don't need to include its XML files manually. Their [source](../../recipes/planet.xml) and [complete parameters](../../reference/PARAMETERS_GENERATED.md#recipes) are included for learning and modification.

| Recipe | Parts and purpose |
|---|---|
| planet | Spherical textured mesh, optional overlay, wrapping UV integrators |
| follow-offset | Softened, bounded directional offset toward an observed point |
| wind-panel | Grid mesh, field, pinned deformation and phase integration |
| precipitation | Prepared emitter; negative Y gives rising particles |
| surface-reflection | Capture, material and projected receiver |
| atlas-display | Remap/clamp an observation into atlas frames |
| atlas-digit | Decimal place extraction and clipped sliding transition |

Atlas-display and atlas-digit share the `atlas-display.xml` source file. Read argument defaults before using a recipe: atlas-display defaults to F1вЂ“F35, and component clock arguments may use different capitalization from graph clock arguments.

Here is a complete scene with a rotating surface and a small decoration that follows the King's direction:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="surface" path="mme-assets/surface.png" />
    <Asset id="mote" path="mme-assets/mote.png" />
  </Assets>
  <Components>
    <Component id="followerImage" kind="sprite">
      <Value name="transform" value="follow.transform" /><Value name="asset" value="mote" />
    </Component>
  </Components>
  <Instances>
    <Instance id="orb" recipe="planet">
      <Arg name="surface" value="surface" /><Arg name="position" value="240,180" />
      <Arg name="size" value="140,140" /><Arg name="rate" value="0.025" />
    </Instance>
    <Instance id="follow" recipe="follow-offset">
      <Arg name="origin" value="240,180" /><Arg name="extent" value="5,5" />
      <Arg name="target" value="player.center" />
    </Instance>
  </Instances>
</MappingSettings>
```

`follow.transform` is an exported component port. The follower is independent of the orb; change the artwork and origin to make an eye, marker or a small responsive accent. `nearRadius` in follow-offset softens the direction near the origin. It isn't a distance cutoff: far away, the offset approaches the configured extent. For a reaction that vanishes at a radius, calculate `clamp(1-distance/radius,0,1)` separately.

## Define your own preset

Put a Recipe in the entry's Recipes section or in a MappingFragment. Parameters have name, kind, optional default and numeric min/max. `${name}` substitutes an argument. `@id` refers to something inside this instance. Caller-supplied asset IDs retain their external meaning.

This full scene defines a small lamp preset and instantiates it twice:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Recipes>
    <Recipe id="small-lamp">
      <Parameters>
        <Param name="position" kind="Vector2" />
        <Param name="color" kind="Color" default="#88BBDD" />
        <Param name="brightness" kind="Number" default="0.06" min="0" max="0.2" />
      </Parameters>
      <Components>
        <Component id="root" kind="transform"><Value name="position" value="${position}" /></Component>
        <Component id="light" kind="light">
          <Value name="transform" value="@root" /><Value name="color" value="${color}" />
          <Value name="intensity" value="${brightness}" /><Value name="radius" value="40" />
        </Component>
      </Components>
      <Exports>
        <Export id="transform" role="component" target="@root" />
        <Export id="light" role="component" target="@light" />
      </Exports>
    </Recipe>
  </Recipes>
  <Instances>
    <Instance id="left" recipe="small-lamp"><Arg name="position" value="160,180" /></Instance>
    <Instance id="right" recipe="small-lamp">
      <Arg name="position" value="320,180" /><Arg name="color" value="#DDAA88" />
    </Instance>
  </Instances>
  <Effects>
    <Effect id="pulse" duration="0.5" fadeOut="0.5">
      <Set target="left.light/intensity" value="0.12" />
    </Effect>
  </Effects>
  <Rules><Rule id="pulse" event="jump"><Action op="effect" target="pulse" /></Rule></Rules>
</MappingSettings>
```

The expanded internals are namespaced by instance IDs. Two roots don't collide. The caller uses the exported port `left.light/intensity`; it needn't know the internal light ID. Renaming an internal component is safe if the export stays stable.

## Exports and nested composition

Export roles are component, property, node and signal. A component port is useful for material/transform references and property targets. A node port can be consumed with an input source such as `node:follow.offset`. A property port exposes a particular component/property. Signal ports preserve observation contracts. Exports are resolved and type-checked during compilation.

Inside a recipe use `@root`, `@node`, `@component/property` and `variable:@variable` for local references. Lists of graph inputs remain semicolon-separated. Nested Instances expand under their containing namespace. Missing required arguments, unknown arguments, duplicate exports, recursion and excessive expansion fail compilation before activation.

When adapting a built-in recipe, give your modified recipe a new ID. Don't include an unchanged embedded recipe under the same ID. Separate the controls a caller should use from details it shouldn't depend on: position, source, strength, rate, color, asset and clock make useful parameters; every incidental intermediate node usually doesn't.

Compile the instance in a small scene before distributing it. A reusable preset should state required assets, source type/units, clock behavior, exported ports and any native dependency. Prefer extending a recipe while existing components can express the behavior. Add a primitive only for a missing general capability.
