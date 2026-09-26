# Mega Mapping authoring handbook

Mega Mapping Expansion adds map-owned visuals and presentation to Jump King.
It requires JK Runtime 1.30 or newer 1.x. It does not replace the native collision system.
You can combine descriptive vector assets, PNG props and procedural effects;
the examples in this handbook use only self-contained descriptive geometry.

## Find the right document

For integer side links, native topology and memory limits, see [Large maps](large-maps.md).

| Task | Read |
| --- | --- |
| Make a first scene; understand layers and assets | This handbook |
| Trigger states, carry lights, swap assets, save room progress | [Behavior cookbook](behaviors.md) |
| Sequence reactions, wait, branch, cancel, coordinate NPCs/endings | [Behavior trees](behavior-trees.md) |
| Extend an existing native map without an editor | [Native workflows](native-workflows.md) |
| Author native custom ending trees | [Custom endings](endings.md) |
| Reuse compound objects without custom code | [Objects and instances](objects.md) |
| Text, translations, native NPCs, intro and statistics pages | [Narrative](narrative.md) |
| Require mods or declare map-specific conflicts | [Map policies](map-policies.md) |
| Find generated defaults/schema or maintain the docs | [Schema and checks](schema.md) |
| Access scenes from another mod | [Scene API](scene-api.md) |
| Look up a setting, default, unit or limit | [Parameter reference](parameter-reference.md) |
| Build a lamp, tree, moth, watching eyes or wet rooftop | [Small recipes](recipes.md) |
| Inspect a running scene or diagnose an effect | [Preview and troubleshooting](troubleshooting.md) |
| Change the renderer itself | [Architecture](architecture.md) |

The mod's `examples/` directory contains independent scene fragments for
visuals, narrative and behaviors. They can be used with an existing playable map.

## First scene

Start with an existing playable map and enable Mega Mapping Expansion and
JK Runtime in the game. In the map's level root, create
`props/mega-mapping-expansion/scene.xml` containing this complete document:

```xml
<MegaMapping version="1">
  <Options expectedScreens="1" />
  <VectorAssets>
    <Asset id="marker" width="16" height="16" pixelSnap="true">
      <Shape type="path" data="M8 1 L15 8 L8 15 L1 8 Z" fill="#EDC97B" />
      <Shape type="rect" x="6" y="6" width="4" height="4" fill="#49364F" />
    </Asset>
  </VectorAssets>
  <Nodes>
    <Node id="first-marker" asset="marker" screen="1" x="240" y="160"
          layer="foreground" motion="bob" amplitudeY="6" duration="3" />
  </Nodes>
</MegaMapping>
```

Launch that map. A small gold diamond should bob near the centre of screen one,
in front of the King. It is decoration: the King cannot land on it. This XML
has no external asset dependencies and is not a complete playable map by itself.

If a build tool owns the level, edit its source scene and rebuild the map before
requesting debug reload. A mod build alone does not rebuild a map. The
`examples/minimal/` under this mod is another independent scene, demonstrating
shared defaults and an animated glass material.

## Coordinates, pivots and collision

The viewport is 480 by 360 game pixels. X increases rightwards and Y downwards.
Scene `screen` values are one-based. A map editor may use zero-based screen
indices and grid units: convert those deliberately when generating scene data.

A Node or Prop's `x,y` locates its pivot on the screen. `originX,originY` locates
that pivot inside the source asset or sprite-sheet frame, before scaling.
Omitted origins select the centre independently on each axis. For top-left
placement, set both to zero; for a rooted tree, put the origin at the trunk base.
Transparent padding is part of an asset's dimensions and affects its centre.

Water, fog and emitter areas use top-left `x,y`. Bushes use a centre/base
position. Puddle outlines and reflection planes use screen coordinates, not
coordinates relative to a Prop. See the reference before reusing positions
between different object types.

Visual platforms never create collision. Author the native solid or water
volume separately, and derive platform art from it. In a pseudo-3D platform,
place the walk line through the intended foot-contact area of the painted top,
not automatically along its rear rim or front fascia. Moving decorative geometry
does not move native collision. Anchors describe geometry for inspection and
rain clipping; they do not create that geometry either.

## Layering and visual depth

The normal scene order is:

| Phase | What it means for authoring |
| --- | --- |
| Native background, then mod `background` | Mod background runs before native midground; an opaque native midground can cover it. |
| Native screen draw, then mod `world` | World scenery is behind the King; useful for platform surfaces, trunks and rear props. |
| King and native foreground, then mod `foreground` | Near fog, leaves and frames can cover the King. |
| Reflection compositor | Copies the pre-UI world, draws composite water and puddles, then redraws explicit reflection occluders. |
| Advanced lighting | Multiplies the composed world by ambient and point-light illumination. |
| Native UI and final presentation | Menus are not reflected; final tint and mirroring include them. |

Within a mod drawing phase, smaller `z` values draw first. Z is not a global
depth buffer: a background object with a huge Z still cannot move in front of
the King. Use distinct Z values where order matters. Puddles have no `layer`
or `z`; they run in the later compositor, in their collection order.

`depth` is a small player-relative parallax coefficient, not geometry depth,
perspective scale, collision or sorting. `depth="1"` disables that offset.
Keep it at one for architectural layers that must remain registered. Separate
sky, clouds, distant trees, near trees and foreground into actual assets/nodes
with transparent gaps; assigning different depths cannot separate a flattened
picture. Clouds belong behind tree silhouettes and in front of the sky.

This is a layered 2.5D renderer, not a 3D scene graph. Perspective reflections,
mesh deformation and compositing provide depth cues; there is no authorable
3D camera, volume lighting, normal-map material or automatic depth extraction.

## Assets and transparency

### Descriptive geometry

An `Asset` contains ordered `Shape` elements, or references a map-local XML file
whose root is `VectorAsset`. Shapes are rectangles, ellipses, lines or paths,
with fills, gradients and strokes. Paths support M, L, C, Q and Z, in absolute
or relative form; this is not an SVG importer.

Assets are rasterized into cached textures during build or scene loading.
Runtime motion transforms or deforms those textures; it does not edit individual
path control points each frame. Split parts that need independent animation
into separate assets/nodes. Detailed C# art recipes are source generators, not
a runtime scripting API exposed by every map.

`pixelSnap` disables supersampling and antialiasing; it does not make material
opaque. `alphaCutoff` discards low-coverage pixels and makes every retained pixel
fully opaque. Use it selectively for solid stone or insects, never as a global
fix for fog, glass or water. `edgeBleed` can close small path seams but can also
thicken fine details. Asset-level `clipPath` with explicit `clipMode` provides
an include/exclude matte in asset coordinates before texture caching.

### PNG support, without a separate workflow

Register a PNG with `Textures/Texture`, then place it with `Props/Prop` using its
texture ID. Paths must be relative to the level root, remain inside it and end
in `.png`. A static image uses one column, one row and zero FPS. A sprite sheet
uses a uniform grid; frames advance left to right, then top to bottom. Make the
image dimensions divisible by the grid dimensions and leave consistent padding
around every frame. Origins refer to a single frame, not the entire sheet.

Use ordinary straight-alpha PNG exports. The loader premultiplies colour for
rendering; do not manually premultiply the source or bake a dark matte around
transparent edges. Source alpha, colour alpha and object opacity combine, so an
opacity of one cannot repair transparent pixels already present in an image.
Point sampling preserves the pixel grid; linear sampling interpolates movement
for soft materials. Wind meshes currently use point sampling regardless of the
object's sampling preference.

PNG Props share position, pivot, scale, flips, tint, opacity, Z, layers, motion,
tracks, gaze, flex, flutter, wind and silhouette-rim settings with vector Nodes.
Brightness animation changes RGB without fading coverage. Composed-frame water
and puddles can reflect both kinds of artwork. A PNG Prop can also act as an
explicit reflection occluder.

Both Props and Nodes support proximity spring reactions. Emitters accept vector
asset IDs only, and legacy selective water reflections accept vector assets/nodes
rather than PNG Props. Declaring a
shared attribute does not remove these runtime limitations. PNGs also cannot use
the vector compiler's shape-level controls, asset clipping or alpha cutoff.
Prepare transparency and independent parts in the source art tool instead.

Neither format automatically supplies collision, reflection masks, shadow-caster
geometry, material response or a skeleton. A single flattened PNG remains a
single part; divide trunk, canopy, glass, frame or pupils when they need separate
motion or roles. Use tight but sufficient padding for filtering and animation.

## Animation and interaction

`duration` is seconds per forward cycle; `pingpong` takes twice that to return.
`level-start` and `always` currently use the same scene clock. `screen-enter`
starts on entry and restarts on later entries. Positive `delay` hides an object
until its start; negative delay prewarms it. Native Restart creates a new scene
and resets those clocks.

Motion supplies a base trajectory; child Tracks add position/rotation offsets or
multiply scale, opacity and brightness. For looped tracks, match the first and
last values and consider velocity at the seam. A closed spline path smooths
direction changes but is not constant-speed motion. An orbit avoids position
key stops. `once` holds motion/track endpoints, not every subsystem: sprite-sheet
frames still loop, and flutter uses global scene time.

Wind uses a rooted triangulated canopy with five analytic branch groups, not an
arbitrary bone rig. Flex is a simpler strip bend. Flutter folds wing regions
around a stable body. Wind cannot share one object with flex or flutter; do not
combine flex and flutter either, as flutter takes rendering precedence. Gaze
offsets a separate pupil smoothly towards the King. Procedural Bush is a simple
legacy foliage effect; use authored wind Nodes for detailed trees and vegetation.

## Light and reflection roles

Treat a lamp as separate fixed housing, animated luminous glass and a `Light`.
A brightness Track animates the glass, not the Light's intensity. `flicker`
modulates illumination independently; there is no shared light-track binding.
Use advanced lighting with an appropriate ambient floor and falloff for a dark
scene. Rims need both nonzero opacity and width; specify `rimPixels` explicitly.

Point-light occlusion uses the King's rectangular hitbox and opt-in rectangular
Anchors (`blocksLight="true"`). Full source
coverage removes that source's light and local rim contribution, not ambient
light or other sources. Platform/branch artwork does not automatically cast
shadows; author appropriate blocker geometry instead.
Rims approximate exterior silhouette illumination; they are not material-aware
normal mapping. Glowing glass remains authored art, not a physically simulated
emissive surface.

Choose a reflection mode deliberately:

| Surface | Sources and purpose |
| --- | --- |
| Legacy Water | Separately replayed King and selected vector artwork; approximate, not the full animated frame. |
| Composite Water | Complete pre-UI frame, vertically compressed into a rectangular lake; optional interactive waves. |
| Puddle | Complete pre-UI frame inside an authored polygon, with compression and perspective. Shallow visual surface over native ground. |

Composite Water and Puddle accept `reflectionObjects="player;object-id;another-id"`
to rerender only those participants. Omit it for the complete pre-UI world.
`reflect` and `reflectionAssets` belong to legacy Water and do not filter
the composed frame. `occludesReflection` redraws an object over the reflected
result; it is not a source-exclusion flag. Use it for dry opaque banks/faces,
carefully: redrawing a platform top over the King can hide the King, and redrawing
translucent art can increase its apparent opacity. Split those roles into parts.

Water interaction animates a height field and spray when the King crosses the
surface. To change movement in a lake, place a native water volume at the same
height. Puddle contact rings are visual, and ambient rain rings are independent
analytic decoration, not events from simulated rain impacts.

## Modules and shared defaults

An entry scene can use `Include` with `src="modules/forest.xml"`. Each included
file has a `MegaMapping` root and ordinary collections. Include paths resolve
from the including file and must remain under the entry scene directory.
Texture paths and external vector `source` paths still resolve from the level
root, even inside modules. Keep Options in the entry scene only.

Templates, Materials and LayerGroups define named defaults. Instances reference
them with `template`, `material` and `group`. Precedence, highest first, is:
instance, template, material, group, constructor default. Defaults cannot inherit
other defaults. Direct `Track` children are supported; there is no `Tracks`
wrapper. If the instance supplies any Tracks, its entire Track collection wins.
Defaults must contain attributes valid for their eventual target type.

Names and attributes are case-sensitive XML; use the documented spelling and
lowercase enum values. IDs/references are case-insensitive. Object IDs are shared
across drawable kinds; texture, vector-asset and anchor IDs each have their own
namespace. Give instances stable descriptive IDs without leading/trailing spaces.
Unknown settings, duplicate IDs, DTDs, cyclic includes and excessive nesting fail
validation instead of silently doing nothing.

## Build, cache and package

Keep editable scene modules and vector sources with the map. `scene.mmgfx` is an
optional MMGFX3 cache of vector rasterization, not editable source or a substitute
for missing sources. Its fingerprint covers expanded scene semantics, vector
paths/contents and compiler identity. A present stale/invalid cache is an error;
normal frame rendering does not rehash or parse XML. PNGs remain separate files.

In a repository checkout, build this mod and its tools with:

```powershell
.\scripts\check-mods.ps1 -Mod mega-mapping-expansion
```

For a standalone existing level, the built compiler accepts two positional
arguments: the level root and the output cache path. It validates scene data and
compiles assets, but cannot prove the loaded game's screen count or playability.
A map build pipeline can invoke this compiler after preparing the level assets.

Keep the map package self-contained: entry XML, included modules, external vector
sources, referenced textures and native map assets. Document JK Runtime and Mega
Mapping Expansion as dependencies; map art generation tools are not runtime
dependencies. Do not distribute private logs, preview command files or local
backups as map content. Confirm asset/audio licenses separately.

The mod build exports `AUTHORING_KIT/minimal` with this documentation and a
standalone scene; it is not a complete map. Building does not install, upload or
launch anything. See the troubleshooting
guide for the separate rebuild/reload/inspect cycle.
