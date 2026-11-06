# Reuse a planet, follower, wind panel or reflection

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

A recipe is a reusable XML object with a few settings. Use one when you want a ready-made effect, or when you need several independent copies of your own object. MME includes seven built-in recipes; their source expands to the same components used elsewhere in this guide.

## Choose a recipe

| Recipe | What you get | Main settings to change |
|---|---|---|
| `planet` | Rotating textured sphere, with optional overlay | surface image, size, position, rate, tilt |
| `follow-offset` | A point that moves a small bounded distance toward the King or another position | origin, target, extent, response |
| `wind-panel` | A textured panel bending with a pinned field | image, size, amplitude, rate, layer |
| `precipitation` | Repeated particles | image, position, area, velocity, rate, lifetime |
| `surface-reflection` | Capture and a reflecting polygon | selection, position, outline, projection, opacity |
| `atlas-display` | Signal remapped into animation frames | atlas, source, minimum, maximum, frames |
| `atlas-digit` | One rolling decimal digit | digits atlas, source, digit place, position, roll |

The recipes are already embedded in MME. Don't Include their unchanged source files: that creates duplicate recipe IDs. Use [the argument guide](../reference/recipes.md) for every parameter, unit and exported connection.

## Use the library

The library is embedded in MME and the compiler. You don't need to include its XML files manually. Their [source](../../recipes/planet.xml) and [complete parameters](../../reference/PARAMETERS_GENERATED.md#recipes) are included for learning and modification.

Atlas-display and atlas-digit share the `atlas-display.xml` source file. Read argument defaults before using a recipe: atlas-display defaults to F1–F35, and component clock arguments may use different capitalization from graph clock arguments.

1. Copy `surface.png` and `mote.png` from the supplied sample into `mme-assets`.
2. Load the complete scene under **Use the library**. A sphere rotates at screen center and the mote offsets toward the King.
3. Change `orb/size` argument to `100,100` and `rate` to `0.05`; rebuild to compare.
4. Change the follower's `extent` to `12,8` for a larger look range.

Here is a complete scene with a rotating surface and a small decoration that follows the King's direction:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="surface" path="mme-assets/surface.png" />
    <Asset id="mote" path="mme-assets/mote.png" />
  </Assets>
  <Components>
    <Component id="followerImage" kind="sprite">
      <Value name="transform" value="follow.transform" />
      <Value name="asset" value="mote" />
    </Component>
  </Components>
  <Instances>
    <Instance id="orb" recipe="planet">
      <Arg name="surface" value="surface" />
      <Arg name="position" value="240,180" />
      <Arg name="size" value="140,140" />
      <Arg name="rate" value="0.025" />
    </Instance>
    <Instance id="follow" recipe="follow-offset">
      <Arg name="origin" value="240,180" />
      <Arg name="extent" value="5,5" />
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
        <Component id="root" kind="transform">
          <Value name="position" value="${position}" />
        </Component>
        <Component id="light" kind="light">
          <Value name="transform" value="@root" />
          <Value name="color" value="${color}" />
          <Value name="intensity" value="${brightness}" />
          <Value name="radius" value="40" />
        </Component>
      </Components>
      <Exports>
        <Export id="transform" role="component" target="@root" />
        <Export id="light" role="component" target="@light" />
      </Exports>
    </Recipe>
  </Recipes>
  <Instances>
    <Instance id="left" recipe="small-lamp">
      <Arg name="position" value="160,180" />
    </Instance>
    <Instance id="right" recipe="small-lamp">
      <Arg name="position" value="320,180" />
      <Arg name="color" value="#DDAA88" />
    </Instance>
  </Instances>
  <Effects>
    <Effect id="pulse" duration="0.5" fadeOut="0.5">
      <Set target="left.light/intensity" value="0.12" />
    </Effect>
  </Effects>
  <Rules>
    <Rule id="pulse" event="jump">
      <Action op="effect" target="pulse" />
    </Rule>
  </Rules>
</MappingSettings>
```

The expanded internals are namespaced by instance IDs. Two roots don't collide. The caller uses the exported port `left.light/intensity`; it needn't know the internal light ID. Renaming an internal component is safe if the export stays stable.

## Exports and nested composition

Export roles are component, property, node and signal. A component port is useful for material/transform references and property targets. A node port can be consumed with an input source such as `node:follow.offset`. A property port exposes a particular component/property. Signal ports preserve observation contracts. Exports are resolved and type-checked during compilation.

Inside a recipe use `@root`, `@node`, `@component/property` and `variable:@variable` for local references. Lists of graph inputs remain semicolon-separated. Nested Instances expand under their containing namespace. Missing required arguments, unknown arguments, duplicate exports, recursion and excessive expansion fail compilation before activation.

When adapting a built-in recipe, give your modified recipe a new ID. Don't include an unchanged embedded recipe under the same ID. Separate the controls a caller should use from details it shouldn't depend on: position, source, strength, rate, color, asset and clock make useful parameters; every incidental intermediate node usually doesn't.

Compile the instance in a small scene before distributing it. A reusable preset should state required assets, source type/units, clock behavior, exported ports and any native dependency. Prefer extending a recipe while existing components can express the behavior. Add a primitive only for a missing general capability.

An exported name such as `follow.transform` is a supported connection. Target it when attaching artwork or an effect. Don't depend on incidental internal component names in someone else's recipe. For your own modified recipe, choose a new recipe ID and keep exports stable for callers.
