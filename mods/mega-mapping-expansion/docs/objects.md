# Reusable map objects

[Handbook](authoring.md) · [Parameters](parameter-reference.md) · [Narrative](narrative.md)

An object is an authored assembly of ordinary components, not a new renderer or
a C# class per showcase room. A lamp can combine geometry, a light, a trigger and
rules. A tree can combine several independently animated Nodes. An observatory
can combine a spherical projection, glass, lighting and text. The map owns the
art and composition; MME owns reusable rendering, timing and interaction.

`Planet`, `Surf` and `Bush` remain documented component names: respectively a
two-layer rotating spherical projection, an analytic wave/spray field, and a
simple procedural foliage cluster. They are not Earth, the lighthouse, or the
garden scene. Detailed foliage uses authored Nodes with wind/reactive deformation;
the baked ocean tool is an optional asset producer, not a required runtime.
These specialized primitives are retained; there is no renamed duplicate format.

## First reusable assembly

Put definitions in an ordinary Include module under the scene directory. Shared
assets stay in VectorAssets/Textures outside the definitions. Instances reference
those assets; geometry is not copied per instance.

This complete scene creates two independent lamps. Entering the first lamp's
region dims only that lamp for two seconds.

```xml
<MegaMapping version="1">
  <VectorAssets>
    <Asset id="lamp-glass" width="12" height="18">
      <Shape type="rect" x="1" y="1" width="10" height="16" fill="#FFD990" />
    </Asset>
  </VectorAssets>
  <ObjectDefinitions>
    <Object id="lamp">
      <Parameters><Param name="power" type="number" default="0.8" min="0" max="4" /></Parameters>
      <Nodes><Node id="glass" asset="lamp-glass" x="0" y="0" /></Nodes>
      <Lights><Light id="bulb" x="0" y="0" radius="60" intensity="${power}" color="#FFCA80" /></Lights>
      <Effects><Effect id="dim" duration="2"><Set target="@bulb" property="intensity" value="0.15" /></Effect></Effects>
      <Regions><Region id="near" x="-16" y="-16" width="32" height="32" /></Regions>
      <Rules><Rule id="touch" event="enter:@near" effect="@dim" /></Rules>
    </Object>
  </ObjectDefinitions>
  <Objects>
    <Instance id="west" object="lamp" screen="1" x="120" y="180" />
    <Instance id="east" object="lamp" screen="1" x="360" y="180"><Arg name="power" value="0.5" /></Instance>
  </Objects>
</MegaMapping>
```

The compiled IDs are `west.glass`, `west.bulb`, `west.dim`, etc. Use those IDs
from external rules, reflection selections, inspector commands or the scene API.
Instances have no permanent runtime wrapper or per-frame object-expansion work.

## Definition contract

| Construct | Fields and behavior |
| --- | --- |
| `ObjectDefinitions/Object` | Unique case-sensitive `id`; one section of each kind |
| `Parameters/Param` | `name`, `type=number/boolean/string`; `default` required unless each instance supplies it; numeric `min/max` default to -1000000/+1000000 |
| `Objects/Instance` | Unique `id`, existing `object`; `screen=1`, `x=0`, `y=0`; at most 512 instances |
| `Arg` | `name`, nonempty `value`; unknown, duplicate, missing and out-of-range arguments are errors |
| `${name}` | Attribute substitution before validation; `${id}` is the instance ID |
| `@local` | Local component reference in `target`, `attach`, `anchor`, `enter`, `exit`, `effect`, `requiresFlag`, `setFlag`, `increment`, `counter`, `text` |
| `enter:@area`, `exit:@area`, `flag:@state` | Local event name; expands to the instance-prefixed component ID |
| `anchor:@ledge` | Local light-blocker effect target |
| `reflectionObjects="player;@shape"` | Local reflected object IDs; the `player` token stays global |

Allowed component sections are Props, Nodes, Lights, Fogs, Rains, Emitters, Texts,
Anchors, Regions, Effects, Rules, Flags, BehaviorTrees, Bushes, Waters, Puddles, ShadowSurfaces,
Planets and Surfs. Include modules, shared assets, Strings, Intro, Results and
native NPC definitions live outside objects. Recursive object definitions and
object-wide scale/rotation are not supported. Use ordinary attachment chains for
moving assemblies; an instance offset is a one-time placement, not a parent pose.

## Coordinates and ownership

Unattached spatial components receive the instance screen plus local screen minus
one. X/Y fields use their normal component defaults plus instance offsets.
Puddle/shadow outlines and planeY, explicit nonnegative water glintX and Surf
Impact points are translated too. Rain remains a full-screen field and has no X/Y.
Anchored regions use their anchor's screen/rectangle; attached Nodes/Lights keep
attachment-local coordinates. Integer-coordinate components require integral
results. The regular component validator checks final map bounds.

Paths, animation track values, and effect operands are not rewritten as spatial
coordinates. They keep their normal relative/absolute semantics; expose explicit
parameters when needed. Rule `screen` is an explicit global filter (omission means
all screens). Asset IDs, string IDs and sound keys are global. Effect `group` and
explicit `owner` become instance-local, so two copies do not cancel each other.

## Source, validation and debugging

Expansion order is Includes → Objects → Templates/Materials/LayerGroups → strict
scene validation → cache compilation. Existing authoring conveniences are one
compiler pipeline, not alternative runtime engines. The inspector shows expanded
IDs and the source module/instance. Rebuild the cache after source changes; stale
caches are rejected rather than substituted. See [architecture](architecture.md).

The authoring kit includes the independently compilable `narrative/` scene for
texts, counters and results. The complete XML above is compiled by the docs check.
