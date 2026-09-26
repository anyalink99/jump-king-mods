# Scene parameter reference

For every serialized field/default and live-edit bounds, use the
[generated field catalog](../reference/PARAMETERS_GENERATED.md). Related guides:
[compound objects](objects.md), [texts/NPCs/intro/results](narrative.md),
[map policies](map-policies.md). These chapters include complete validated examples.

Interactive fields are documented in the
[behavior reference](behaviors.md): Effects, Regions, Rules, Flags, LightTemplates,
saveId, visible/enabled, attachments and local offsets. These fields are also
included in the generated expanded-scene XSD and the public property metadata.

[Handbook](authoring.md) · [Recipes](recipes.md) · [Troubleshooting](troubleshooting.md)

This describes the current `MegaMapping` version 1 implementation, not proposed
features. Defaults below are values actually constructed when XML omits an
attribute, before shared defaults are applied. `required` means supply a value;
an empty/zero constructor value would not make a usable declaration. A dash in
the limits column means there is no dedicated numeric range check, not that
extreme values are useful or safe. Always use finite numbers with decimal dots.
Ranges are inclusive unless explicitly marked `>`.

`px` means game pixels, `s` seconds, angles degrees. Colour values are
`#RRGGBB` or `#RRGGBBAA` (alpha last). Boolean values are `true`/`false`.
Object references use IDs, not file paths. Collections may be omitted when unused.

Jump to [Options](#options), [Texture](#texture),
[vector assets](#vectorasset-and-shape), [Node/Prop](#node-and-prop),
[Light](#light), [Bush](#bush), [Water](#water), [Fog/Emitter](#fog-and-emitter),
[Rain](#rain), [Puddle](#puddle) or [Anchor](#anchor).

## Document structure

| Parent / element | Purpose |
| --- | --- |
| `MegaMapping` | Root; `version` defaults to `1`, the only supported version. |
| `Options` | One map-wide presentation configuration. |
| `ScreenLooks/Screen` | Optional per-screen ambient/rim multipliers. |
| `Textures/Texture`, `VectorAssets/Asset` | Reusable source assets. |
| `Props/Prop`, `Nodes/Node` | PNG and vector instances respectively. |
| `Lights/Light`, `Bushes/Bush`, `Waters/Water`, `Fogs/Fog`, `Emitters/Emitter` | Procedural scene objects. |
| `Rains/Rain`, `Puddles/Puddle` | Rain fields and polygon reflections. |
| `ShadowSurfaces/Surface` | Polygon receivers for the King's projected ground shadow. |
| `Surfs/Surf` | Layered procedural ocean cross-sections, overturning crests and spray. |
| `Planets/Planet` | Rotating spherical projection of separate surface and cloud vector atlases. |
| `Anchors/Anchor` | Inspection, rain clipping, standing-region references and opt-in light blockers. |
| `Include` | `src` required, relative to containing XML; constrained to the entry scene directory subtree. |
| `Templates/Template`, `Materials/Material`, `LayerGroups/LayerGroup` | Each has required `id` and target-compatible defaults. Instances use `template`, `material`, `group`. |

All drawable objects require a map-wide unique object `id`. Texture IDs, vector
asset IDs and anchor IDs are separately unique, case-insensitively. Most objects
default to `screen="1"`; ScreenLook and Anchor require an explicit screen.
Runtime validates all screen references against authored map.xml content when present,
otherwise against the loaded native array. See [large-map topology and budgets](large-maps.md).
Map-wide budgets are 4096 drawable objects and 262144 vector shapes, not per-frame
performance guarantees. Includes permit at most 16 nested levels below entry.

## Options

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `introText` | empty | Empty/whitespace preserves stock legend; escaped `&lt;newline&gt;` inserts a newline. |
| `timer` | `inherit` | `inherit`, `hidden`; hides the native in-game overlay-items pass, not elapsed time or saving. |
| `mirror` | `none` | `none`, `horizontal`, `vertical`, `both`; final image including menus, not controls/physics. |
| `tint` | `#FFFFFF` | Final-frame multiply colour. |
| `tintOpacity` | `0` | 0..1; mixes final multiplier from white towards tint. |
| `expectedScreens` | `0` | Positive integer enables a lower-bound guard; zero disables it. Does not create screens. |
| `advancedLighting` | `false` | Enable ambient + point-light compositor. |
| `ambientLight` | `#FFFFFF` | Unlit colour floor, multiplied by intensity. |
| `ambientIntensity` | `1` | 0..1; applies in advanced lighting. |
| `playerRimColor` | `#91D8FF` | Ambient King contour colour. |
| `playerRimOpacity` | `0` | 0..1; enables local source-colored rim contribution, never an ambient outline. |
| `playerRimPixels` | `2` | 0..12 px; zero disables rim width. |
| `profiling` | `false` | Log rolling CPU/cadence/memory counters; turn off for normal publication. |

For each `ScreenLooks/Screen`: `screen` is required and unique; `ambientScale`
and `playerRimScale` each default to `1` with range 0..1. They multiply map
defaults for that screen, not every prop's brightness or the final UI.
`ambientLight` defaults to empty (inherit Options); a nonempty color overrides
the ambient hue locally. `ambientIntensity` defaults to `-1` (inherit Options),
or accepts 0..1. These overrides are applied before `ambientScale` and require
advanced lighting, like the map-wide ambient settings.

Native screen capacity follows the collision atlas dimensions. Without map.xml,
`expectedScreens` is a lower-bound check; with map.xml it must match the authored
count. Stock RGB side-teleport markers address targets 1..255; map.xml side links
use full integers. See [large maps](large-maps.md) for validation and padding rules.

## Texture

PNG usage is described in prose in the handbook; no PNG example files are needed.

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id` | required | Texture namespace ID. |
| `path` | required | Existing map-local `.png`, relative to level root. |
| `columns`, `rows` | `1`, `1` | Integers 1..64; uniform frame grid. |
| `fps` | `0` | 0..240 frames/s; zero selects first frame. |
| `frames` | `0` | Actual frame count; zero uses every grid cell across all pages. A positive count omits unused cells at the end. |

The `path` is atlas page zero. Optional `Page` children each have a required
map-local PNG `path`, in playback order (up to 15 additional pages). All pages
must have identical dimensions divisible by the shared grid. They load once and
are disposed together; page changes do not read the disk or upload a texture.
Keep pages within the target GPU's texture-size and memory limits.
See [texture animation](texture-animation.md) for atlas preparation.
Playback is discrete at `fps`; there is no temporal frame interpolation.

Frames advance row-major using object age and always wrap, including when the
object's motion loop is `once`. Frame size is the texture size divided by its
grid; author exactly divisible dimensions. Texture alpha is premultiplied on load.

## VectorAsset and Shape

| Asset attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id` | required in scene | Vector namespace ID; the scene entry supplies identity for an external source. |
| `source` | absent | Existing level-root-relative `.xml` with `VectorAsset` root; replaces inline shapes. |
| `width`, `height` | `64`, `64` | Integers 1..2048 source px. |
| `supersample` | `2` | Integer 1..4; load/build-time rasterization quality. |
| `pixelSnap` | `false` | Forces source-size, non-antialiased geometry, not opaque material. |
| `alphaCutoff` | `0` | 0..1; zero leaves coverage; positive threshold drops low alpha and makes survivors opaque. |
| `edgeBleed` | `0` | 0..2 source px; path-fill expansion to close seams. |
| `clipPath` | empty | Asset-coordinate M/L/C/Q/Z path. |
| `clipMode` | `none` | `none`, `include`, `exclude`; a nonempty path and non-none mode must be supplied together. |

External source dimensions, supersampling and shapes replace the scene entry's
values. `pixelSnap` is enabled if either declaration enables it. Entry
`alphaCutoff="0"` inherits the external cutoff; a nonzero entry overrides it.
Clipping and edge bleed should be set on the scene Asset entry; they are not
inherited from the external file by the current source loader.

`Shape` children draw in document order, back to front:

| Shape attribute | Default | Limits / meaning |
| --- | --- | --- |
| `type` | `path` | `path`, `rect`, `ellipse`, `line`. |
| `x`, `y` | `0`, `0` | Rectangle/ellipse top-left or line start, in asset px; not a transform for path data. |
| `width`, `height` | `0`, `0` | Rectangle/ellipse dimensions; supply positive sizes. Not the line endpoint. |
| `data` | empty | Path commands for path; absolute `x2,y2` endpoint for line. |
| `fill` | `#FFFFFF` | Fill colour; empty disables fill. |
| `fill2` | empty | Gradient second colour; empty gives solid fill. |
| `gradient` | `none` | `none`, `linear`, `radial`; radial uses first colour at centre, second at boundary. |
| `angle` | `0` | Linear gradient angle in degrees. |
| `stroke` | empty | Stroke colour; empty disables stroke. |
| `strokeWidth` | `0` | 0..128 source px; rounded joins/caps. |
| `opacity` | `1` | 0..1; multiplies fill/stroke colour alpha. |

M/m moves, L/l draws a line, C/c a cubic curve, Q/q a quadratic curve and Z/z
closes the path. H/V/A/S/T commands, SVG styles, filters, fonts and groups are not
supported. Shapes are static cached input, not separately tracked runtime nodes.

## Node and Prop

`Node` requires `asset` referring to VectorAssets; `Prop` requires `texture`
referring to Textures. These are the same instance schema but not perfect runtime
feature parity; note the vector-only features below. All other omitted numeric
values listed as zero really start at zero, even when serialization metadata
advertises a different default.

### Placement and appearance

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id` | required | Object ID. |
| `asset` / `texture` | required by kind | Asset ID for Node / Texture ID for Prop. |
| `screen` | `1` | One-based loaded screen. |
| `x`, `y` | `0`, `0` | Screen-local pivot position in px. |
| `originX`, `originY` | `-1`, `-1` | Asset/frame-local pivot; negative selects centre on that axis. |
| `layer` | `world` | `background`, `world`, `foreground`. |
| `z` | `0` | Ascending order within a phase, not 3D depth. |
| `depth` | `1` | Parallax coefficient; one disables. No scale/sort/collision effect. |
| `rotation` | `0` | Base angle, degrees; positive is clockwise in screen coordinates. |
| `scale`, `scaleX`, `scaleY` | `1` each | Each >0..32; axis scale multiplies overall scale. |
| `flipX`, `flipY` | `false` each | Mirror this instance's image. |
| `sampling` | `point` | `point`, `linear`; wind mesh remains point-sampled. |
| `tint` | `#FFFFFF` | Image colour multiplier. |
| `opacity` | `1` | 0..1; coverage multiplier, not brightness. |
| `rimColor` | `#9AD9E8` | Exterior contour colour. |
| `rimOpacity` | `0` | 0..1. |
| `rimPixels` | `0` | 0..12 px; specify explicitly to enable. Current constructor differs from serialization's nominal default of one. |
| `reflect` | `false` | Select vector Nodes for legacy Water only; ignored as a composed-frame filter and for PNG Props. |
| `occludesReflection` | `false` | Redraw this object after composite water/puddles; not source exclusion. |

Parallax offset is `(playerX-240)*(1-depth)*0.055` horizontally and
`(playerY-180)*(1-depth)*0.028` vertically, using screen-local King hitbox centre.
It is not automatic camera perspective. Use one for aligned sky/tree mattes.
Rim contours come from the undeformed source silhouette; wind/flex/flutter do
not rebuild a matching deformed contour. Keep this approximation in mind for
strongly bending or folding artwork.

### Timeline and motion

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `trigger` | `level-start` | `level-start`, `screen-enter`, `always`; first/last currently share the scene clock. |
| `duration` | `2` | >0..86400 s per forward cycle. |
| `delay` | `0` | s; positive hides before start, negative prewarms. |
| `loop` | `loop` | `loop`, `pingpong`, `once`; applies to motion/tracks, not all secondary clocks. |
| `motion` | `none` | `none`, `rotate`, `orbit`, `linear`, `bob`, `sway`, `path`. |
| `amplitudeX`, `amplitudeY` | `0`, `0` | px; orbit radii, linear displacement, or bob's vertical amplitude. |
| `degrees` | `0` | Rotation per rotate/orbit cycle, or sway angular amplitude. |
| `path` | empty | 2..128 semicolon-separated relative `x,y` points when motion is path. |
| `pathInterpolation` | `linear` | `linear`, `spline` (Catmull-Rom; can overshoot corners). |
| `closedPath` | `false` | Connect last path point back to first. |
| `orientToPath` | `false` | Rotate along path tangent. |
| `orientationOffset` | `0` | Degree correction for artwork's forward direction. |

Rotation/offset Tracks compose with the base motion. `bob` and `sway` use sine
waves; `linear` moves from zero to its displacement. Orbit starts at positive X
radius. A closed path includes the closing segment; do not repeat the first point
as the last unless you deliberately want a duplicate-point segment.

### Deformation and gaze

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `reactRadius` | `0` | Horizontal King-proximity radius, px; positive enables spring reaction updates for both Props and Nodes. |
| `reactStrength` | `0` | Angular reaction gain in degrees; spring response is engine-defined. |
| `flex` | `false` | Strip bending using the evaluated angle, not an articulated rig. |
| `flexSlices` | `10` | Integer 2..64; more strips cost more draws. |
| `wind` | `false` | Rooted canopy mesh; cannot combine with flex or positive flutterHz. |
| `windStrength` | `0` | 0..12 when wind enabled; model-specific bend strength. |
| `windHeight` | `0` | 8..4096 px required when wind enabled; authored tree height. |
| `cloth` | `false` | Pinned-top two-dimensional cloth mesh; mutually exclusive with wind, flex and flutter. |
| `clothStrength` | `0` | 0..12 px deformation when cloth enabled. |
| `clothHeight` | `0` | 1..512 px required when cloth enabled, measured down from the pivot. |
| `flutterHz` | `0` | 0..120 wing beats/s; zero disables. Uses scene time independently of motion duration. |
| `flutterAmount` | `0.7` | 0..0.95 wing foreshortening. |
| `flutterBodyStart`, `flutterBodyEnd` | `-1`, `-1` | Integer source-frame X boundaries; both must be explicit for flutter to draw. Start >=1 and end > start; keep within frame width with a wing on each side. Negative defaults do not infer a body. |
| `lookAtKing` | `false` | Smooth gaze offset, supported on Node and Prop. |
| `gazeX`, `gazeY` | `0`, `0` | 0..32 px horizontal/vertical offset limits. |
| `gazeResponse` | `7` | >0..60 inverse seconds; higher converges faster. |

Wind uses duration for gust timing. Avoid flex + flutter even though validation
allows that pair: flutter wins. Node proximity reaction measures horizontal
distance to the authored pivot, not overlap with a detailed plant collider.
Cloth fixes points at/above `originY`; displacement grows towards the hem.
`duration` is the complete repeating swing/fold period. The mesh is deformed
continuously, not sliced into shifted rows. Select `layer="foreground"` to
place laundry in front of the King; assigning cloth alone does not change Z.

### Track children

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `property` | `x` | `x`, `y`, `rotation`, `scale`, `scaleX`, `scaleY`, `opacity`, `brightness`. |
| `keys` | `0:0;1:0` | 2..128 `time:value` pairs; strictly increasing time, first 0, last 1. |
| `easing` | `smooth` | `linear`, `smooth` (smoothstep), `sine`, applied between each pair. |
| `cycles` | `1` | >0..1000 repeats per object forward phase. |
| `phase` | `0` | -1000..1000 cycle offset, not seconds. |

X/Y and rotation Tracks add px/degrees; scale, opacity and brightness Tracks
multiply. Brightness keys must be 0..2; combined gain is capped at two. Values
above one add RGB emission without increasing destination alpha. Scale/opacity
Track keys are not restricted like the base attributes: author sensible positive
scales and 0..1 opacity. The generic default zero-valued Track would collapse a
scale or hide opacity, so always supply keys for multiplier properties.

## Light

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id`, `screen` | required, `1` | Object identity and screen. |
| `x`, `y` | `0`, `0` | Source centre, screen px. |
| `radius` | `96` | 4..1000 px. |
| `color` | `#FFD27A` | Source colour. |
| `intensity` | `0.75` | 0..4; gain, not opacity. |
| `falloff` | `1` | 0.5..8; advanced-light attenuation exponent, larger localizes the light. |
| `rimRadius` | `0` | 0..2000 px; zero uses the smaller of the light radius and 96 px. No rim outside this distance. |
| `rimIntensity` | `1` | 0..1 local rim multiplier. Set zero for sky fills and remote ambient bounce. |
| `flicker` | `0` | Modulation strength; no dedicated range check. Start small, such as 0.02. |
| `pulsePeriod` | `0` | 0..86400 seconds; zero disables smooth periodic modulation. |
| `pulseAmount` | `0` | 0..1 attenuation at the pulse minimum; positive values require a positive period. |
| `pulsePhase` | `0` | 0..1 cycle offset. Zero starts at minimum and reaches full authored intensity halfway through the period. |
| `coneWidth` | `0` | 0..180 degrees, full angular width. Zero retains omnidirectional illumination. |
| `angle` | `0` | -360..360 degrees; 0 points right, 90 down, 180 left. |
| `sweepAngle` | `0` | 0..360 degrees of sinusoidal excursion on either side of `angle`. |
| `sweepPeriod` | `0` | 0..86400 seconds. A nonzero excursion requires a positive period. |
| `scatter` | `0` | 0..1 coverage of a visible, feathered cone; requires positive `coneWidth`. Start at 0.03–0.1. |
| `occludeKing` | `true` | Use King hitbox for source visibility. No general platform/prop casters. |
| `shadowColor` | `#000000` | Retained/validated field, currently not applied: legacy shadows use fixed black and advanced shadows attenuate light. Constructor differs from nominal serialization default. |
| `shadowOpacity` | `0.82` | 0..1; exposed-source cast-shadow strength. Full emitter coverage still extinguishes the source. |
| `layer`, `z` | `world`, `0` | Visible scattering/legacy draw order. Advanced surface illumination is composed after the world. |

Light has no Track children, texture mask or per-object receiver list. A glass
brightness Track and Light flicker are independent. Advanced illumination uses
a half-width/half-height field; rim shading is an approximation based on source
visibility, distance and direction, not surface normals. A cone gates advanced
illumination and local rim contributions. Its visible scattering mesh follows
the same direction and King occlusion, but is an authored 2D fog approximation,
not a volumetric path tracer. Place scattering behind the architecture that must
hide it. Ordinary prop/platform light casters are not inferred from their pixels.
The legacy radial halo remains radial when advanced lighting is disabled;
use advanced lighting for a consistently directional surface-light result.

Covered emitter samples cannot illuminate or scatter even with `shadowOpacity`
below one. Higher-level geometry still uses explicit layer order. A changing
cone reuses cached source-to-pixel directions and stationary King visibility;
it does not retrace every static shadow for every sweep frame.

## Bush

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id`, `screen` | required, `1` | Identity/screen. Draws in world at Z zero; no layer/Z attributes. |
| `x`, `y` | `0`, `0` | Horizontal centre and base, px. |
| `width`, `height` | `70`, `42` | Integers 8..1000 px. |
| `depth` | `0.8` | Retained schema field; current procedural Bush renderer does not apply it. |
| `sway`, `speed` | `3`, `0.7` | Idle displacement px and cycles/s. |
| `reactRadius`, `reactStrength` | `70`, `9` | Horizontal proximity radius and push displacement, px; not Node's spring model. |
| `backColor` | `#173D38` | Rear foliage. |
| `frontColor` | `#2D6A52` | Middle foliage. |
| `highlightColor` | `#5B9367` | Front highlights. |

This is a simple layered radial-patch effect, not the detailed authored tree
system. Prefer Nodes for custom silhouettes, depth layers and smooth reactions.

## Water

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id`, `screen` | required, `1` | Identity/screen. |
| `x`, `y` | `0`, `0` | Integer top-left; Y is surface plane. |
| `width`, `height` | required | Positive integers; rectangle rules below. |
| `layer`, `z` | `background`, `0` | Body draw phase/order; composed reflection occurs later. |
| `color`, `color2` | `#153B55`, `#071827` | Near/deep body colours. |
| `highlight` | `#6CB9BA` | Surface highlights. |
| `opacity` | `0.72` | 0..1 body coverage. |
| `ripple` | `2` | px, visual distortion; use nonnegative values. |
| `glintX` | `-1` | Screen-local glint centre X; negative disables. |
| `reflection` | `true` | Legacy King-reflection toggle; does not disable composite reflection. |
| `reflectionOpacity` | `0.42` | 0..1; King/complete-frame reflection coverage according to mode. |
| `reflectionScaleY` | `0.38` | >0.05..1 vertical compression. |
| `reflectionAssets` | empty | Semicolon-separated vector asset IDs for legacy scene replay, not paths or PNG IDs. |
| `reflectionObjects` | absent | Composite source filter: semicolon-separated Prop/Node IDs and/or `player`. Omit to reflect the entire world. Requires `compositeReflection=true`. |
| `sceneReflectionOpacity` | `0.28` | 0..1; legacy vector reflection coverage. |
| `compositeReflection` | `false` | Reflect pre-UI frame instead of legacy source replay. |
| `interactive` | `false` | King-coupled visual wave field; does not create native water physics. |
| `waveSegments` | `96` | Integer 12..256 surface samples. |
| `surfaceTension` | `19` | >0 restoring coefficient. |
| `waveSpread` | `72` | >=0 neighbour-coupling coefficient. |
| `waveDamping` | `2.8` | >=0 dissipation coefficient. |
| `splashStrength` | `1` | >=0 entry/exit impulse and spray gain. |
| `wakeStrength` | `0.35` | >=0 movement wake gain. |

Wave coefficients are solver tuning values, not SI material properties. Add a
native water volume separately for underwater gameplay. Composite reflection
does not honour legacy asset selection or `reflection="false"`; disable
`compositeReflection` or reduce `reflectionOpacity` to change that pass.

Water, Fog and Anchor rectangles require width/height >=1, x/y >=-2000,
x+width <=2480 and y+height <=2360. These broad limits allow overscan; they do not
make out-of-screen portions visible.

## Fog and Emitter

| Fog attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id`, `screen` | required, `1` | Identity/screen. |
| `x`, `y` | `0`, `0` | Integer top-left, px. |
| `width`, `height` | required, `54` | Integer rectangle; see Water rectangle limits. |
| `layer`, `z` | `foreground`, `0` | Drawing phase/order. |
| `color`, `opacity` | `#B7CDD0`, `0.18` | Colour and coverage (0..1). |
| `speed` | `4` | Nominal px/s; individual band speed is scaled. |
| `bands` | `5` | Integer 1..64 radial bands. |

Fog is a simple procedural band field, not a fluid simulation or automatic
depth-aware cloud mask. Detailed mist can instead be independent authored Nodes.

| Emitter attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id`, `screen` | required, `1` | Identity/screen. |
| `asset` | required | Vector asset ID only. |
| `x`, `y` | `0`, `0` | Area top-left, px. |
| `width`, `height` | required | >0..2480 and >0..2360 px respectively. |
| `layer`, `z` | `background`, `0` | Drawing phase/order. |
| `count` | `12` | Integer 1..512 particles. |
| `lifetime` | `8` | >0.1..3600 s nominal cycle, varied deterministically per particle. |
| `driftX`, `driftY` | `8`, `-5` | Total displacement over normalized lifetime, **not px/s**. |
| `wander` | `5` | px; secondary horizontal wander, reduced vertically. |
| `opacity` | `0.6` | 0..1, further modulated by life fade and twinkle. |
| `scaleMin`, `scaleMax` | `0.35`, `0.8` | Minimum >0, maximum >=minimum and <=16. |
| `depth` | `0.5` | Same small player-relative parallax formula as Node. |
| `tint` | implicit white | Omitted value is null, parsed as `#FFFFFF`. |

Particles use deterministic placement and global time, not rigid-body collision.
Emitter does not accept Node Tracks or PNG texture IDs.

## Rain

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id`, `screen` | required, `1` | Identity/screen. |
| `count` | `100` | Integer 1..512 streaks. |
| `speed` | `190` | 1..900 px/s nominal fall speed, varied per streak. |
| `wind` | `-32` | -300..300 px/s horizontal drift; negative goes left. |
| `length` | `6` | 0.5..32 px. Thickness is engine-defined. |
| `color`, `opacity` | `#7AA9BD`, `0.3` | Streak colour and coverage (0..1). |
| `layer`, `z` | `foreground`, `0` | Separate rain fields can occupy different visual phases. |
| `collide` | `false` | Clip against topmost solid Anchor height for each screen X column. |
| `snow` | `false` | Render small flakes instead of elongated streaks. `length` controls flake size, clamped visually to 3 px. |
| `drift` | `0` | 0..80 px amplitude of smooth two-frequency horizontal wandering. Works for either precipitation style. |

Rain spans the viewport with overscan. There is no source rectangle, blocker
whitelist, arbitrary collision mesh, roof material, impact callback or slanted
ray cast. `collide` consults `kind="solid"` anchors on that screen; it does not
query all native colliders. The topmost roof per X wins, so this cannot represent
rain passing through holes/under overhangs. Puddle ambient rings are independent
of individual streaks and their impacts.

Snow shares the rain field's lifetime, layers and clipping semantics, not its
suggested speeds: start at 15–36 px/s with lengths 0.6–1.8 and independently
seeded far/world/near fields. It does not accumulate snow or emit impact events.

## Planet

`Planets/Planet` requires a unique `id`, a `surfaceAsset` from `VectorAssets`,
and optionally a separate `cloudAsset`. Both are equirectangular maps: horizontal
position is longitude, vertical position runs from north to south pole. Author
periodic left/right edges to avoid seams. The sphere uses linear texture sampling;
the rest of the scene keeps its own sampling mode.

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `screen`, `layer`, `z` | `1`, `background`, `0` | Standard scene ordering; Z -10000..10000. |
| `x`, `y` | `0`, `0` | Sphere centre, screen pixels; -8192..8192. May be far outside the viewport for a large limb. |
| `radius` | `180` | 20..8192 px. |
| `period`, `cloudPeriod` | `120`, `141` | 1..86400 seconds per full 360-degree revolution. Independent cloud drift. |
| `cloudOpacity` | `0.85` | 0..1. |
| `longitude` | `0` | -360..360 degrees, initial longitude offset. |
| `viewTilt`, `axisTilt` | `0`, `0` | -90..90 viewing latitude rotation and -180..180 screen-axis rotation, degrees. |

This is a sphere-mapped mesh, not a bobbing flat planet node. Surface features
compress at the limb and travel through a full revolution; its position and
radius stay fixed. Illumination is an authored facing gradient, not astronomical
sun/terminator simulation. The two bounded meshes do not add collision.

## Surf

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id`, `screen` | required, `1` | Unique effect identity and screen. |
| `x`, `y` | `0`, `240` | Origin and undisturbed height, px; X -960..960, Y -360..720. |
| `width`, `depth` | `480`, `100` | 1..1920 px horizontal extent and 1..720 px depth of the body. |
| `height` | `48` | 0..180 px nominal crest amplitude; a fixed spatial envelope varies height gently. |
| `wavelength` | `200` | 20..1000 px between wave crests. |
| `period` | `7` | 0.5..300 seconds for one material-particle cycle. |
| `phase` | `0` | 0..1 cycle offset. |
| `variation` | `0` | 0..1 smooth, independent long-period changes to wave speed and amplitude. Zero retains the exact carrier loop. |
| `chop` | `0` | 0..12 px of short, independently moving wind waves. Deformation decays with depth and is shared by the water mesh and foam. Nonzero values deliberately break the exact carrier loop. |
| `foamDetail` | `0` | 0..1 advected foam-pocket density. Adds elongated, deforming foam walls and drainage trails while reducing opaque crest fill. Up to 330 pockets; use mainly on the nearest breaker. Zero preserves the original foam mode. |
| `bakedBody` | `false` | Suppress this Surf's runtime body/crest/spray pass when a baked Prop supplies the ocean. Explicit Impact children continue to render. |
| `break` | `1` | 0..1.6 horizontal parcel excursion relative to height. Larger values can overturn the crest. |
| `spray` | `100` | Integer 0..512 continuously renewed ballistic spray particles. |
| `wind` | `0` | -300..300 px/s spray drift. Wave travel itself follows its phase towards positive X. |
| `color`, `crestColor` | `#071D29`, `#6E9FA9` | Body and foam/material-highlight colors. |
| `layer`, `z` | `background`, `0` | Normal depth ordering; Z -10000..10000. |

Use several Surf elements with smaller, slower, lower-contrast distant swells
and one large nearby breaker. The renderer owns a bounded deforming color mesh
per effect. Material-space foam bends with the crest; spray starts at the crest's
historical position and follows a fading ballistic path. Neither is a moving
full-screen image. All mesh/effect resources belong to the scene lifetime.

Each Surf accepts up to 16 `Impact` children, explicit contact points on rocks.
`arrivalPhase` defaults to `-1` (use the parent horizontal wave phase). Set it to
`0..1` for a baked ocean whose waves travel in depth: pressure peaks when
`time / period + arrivalPhase` is an integer. Position and timing are then
independent. Tune against the baked crest at that contact; it is authored timing,
not automatic collision with a PNG animation.
Their wave-arrival pressure controls a climbing water sheet, delayed draining
foam and ballistic droplets. Contacts have `x`, `y` (-480..960, -360..720 px),
`angle` (-360..360 degrees, default -110, using screen axes), `reach` (1..200 px,
default 80), `width` (1..120 px, default 30), `particles` (0..256, default 90),
`layer` (default world) and `z` (-10000..10000, default -8). Their independent layer
allows contact foam above rock art while the ocean body remains behind it.

This is an authored ocean cross-section with contact effects, not a fluid solver:
it does not discover rock edges, conserve water volume or exert wave/player
force or buoyancy. Place the surf behind headland Nodes to establish shore
occlusion and place each contact on the visible rock outline. It does not add
native Water blocks, nor the Puddle/Water reflection pass. The geometric loop is
continuous; particle renewals fade, but their individual lifetimes need not equal
the wave period. Mesh coverage intentionally overscans by crest amplitude at both
ends, so `width` is the base extent rather than a hard rectangular clip.

## Puddle

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id`, `screen` | required, `1` | Identity/screen. |
| `outline` | required | 3..64 semicolon-separated `x,y` vertices, screen-local px; simple concave polygons supported. |
| `planeY` | `0` | 0..359 px; align to native walking surface, not front fascia. |
| `reflectionScaleY` | `0.35` | 0.08..1 vertical compression. |
| `perspective` | `0.004` | 0..0.02 inverse px; zero gives affine compression. |
| `opacity` | `0.65` | 0..1 reflected coverage. |
| `ripple` | `0.6` | 0..4 distortion amplitude in px. |
| `rainRings` | `12` | Integer 0..64 independently cycling ambient rings. |
| `color` | `#80B8CC` | Reflection/ring tint. |

Outline X is 0..480 and Y is planeY..360. It must have visible area below the
plane; rasterization uses even/odd scanlines. Shoreline remains fixed while
refraction shifts source samples. Approximate reflected distance below the plane
is `scale*h/(1+perspective*h)` for source height h above it.

There are no layer/Z, source inclusion/exclusion or player-reflection attributes.
Puddles reflect the full pre-UI frame, not a chosen Prop list, and are drawn after
composite Waters. No recursive reflections or physically traced reflected light.
King contact is tested near planeY using the native hitbox and polygon; ring
duration, radius growth, contact cooldown and spray constants are not XML settings.
This surface adds no swimming, buoyancy or new collision.

## Shadow surface

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id`, `screen` | required, `1` | Unique object identity and screen. |
| `outline` | required | 3..64 semicolon-separated `x,y` vertices; X 0..480, Y planeY..360. |
| `planeY` | `0` | 0..359 px, the actual native foot-contact plane. |
| `scaleY` | `0.45` | 0.05..3 projected vertical pixels per upright pixel. |
| `shearX` | `-0.35` | -4..4 lateral pixels per upright pixel; negative projects left. |
| `opacity` | `0.45` | 0..1 contact opacity, fading with height. |
| `color` | `#352842` | Shadow color. |
| `maxHeight` | `160` | 1..360 px maximum foot elevation above the receiver. |

These are authored affine ground projections, not ray-traced scene shadows.
For source height h above planeY, destination Y is planeY + h*scaleY and
destination X is source X + h*shearX. Positive scaleY sends a rear-lit silhouette
towards the viewer. Receivers draw after world scenery and before the King;
their outline clips the result, so their art must include a visible top surface.
They create no collision, and a Light does not automatically choose their shear.
Each eligible receiver projects independently; there is no nearest-roof or
arbitrary-object occlusion solver. Static architectural shadows belong in art.

The native player's current animation and equipment layers are combined into
one premultiplied appearance, cached between frames and refreshed when equipped
layers change. This avoids missing shadows on the native LayeredSprite and
double-darkening where clothing overlaps the body. The same appearance supports
player rim light and direct player-only water reflections.

## Anchor

| Attribute | Default | Limits / meaning |
| --- | --- | --- |
| `id` | required | Anchor namespace ID. |
| `screen` | required | One-based loaded screen. |
| `x`, `y` | `0`, `0` | Integer top-left, px. |
| `width`, `height` | required | Integer rectangle; same limits as Water. |
| `kind` | absent | Exact `solid` participates in rain clipping; `water` labels water in the inspector. |
| `blocksLight` | `false` | Opt into rectangular map-space occlusion. Requires `Options.advancedLighting=true`. |
| `lightOpacity` | `1` | Occlusion amount, 0..1. Controlled with target `anchor:ID`, independently from rain clipping. |

Other kinds are metadata. None create native collision. Generate anchors from
the real map geometry when possible and rebuild both together after edits.

Water and Puddle both accept `reflectionObjects`. Selected objects are rerendered
using their actual live pose, texture/atlas and deformation. No unselected native
background, NPC, fog or UI is copied into that source. `player` includes equipped
sprite layers. Omit the attribute for the original whole-world capture. The
list must be nonempty, unique and contain known Prop/Node IDs or `player`;
stationary objects must share the receiver's screen. Up to 128 participants per
list and 64 distinct screen/list pairs per map are supported. Identical pairs
share a prepared 480x360 target (~0.66 MiB each). Lists are structural, not a
dynamic Set property. Existing `reflect`, `reflectionAssets` and
`occludesReflection` retain their documented separate roles.
