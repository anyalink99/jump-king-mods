# Advanced skin authoring

Wardrobe+ 2.0 loads schema 1 cosmetic packages. A package remains a native
Workshop Skin or Set: put `cosmetic_settings.xml` or `set_settings.xml` and all
named XNB atlases at its root, then add `wardrobe/skin.json` and its assets.
Don't put a DLL in the skin root; Worldsmith would classify it as a Mod.
Declare Wardrobe+ and any external effect libraries as Workshop required items.
The native fallback still displays when Wardrobe+ is absent.

## Build a package

Build Wardrobe+ first, then run `mods/wardrobe-plus/build-authoring.ps1`.
The resulting local `build/wardrobe-plus/_INTERNAL/authoring/SkinTool.exe` supports:

```text
SkinTool layout native-layout.json
SkinTool validate SOURCE [DEPENDENCY_SOURCE...]
SkinTool build SOURCE NEW_OUTPUT [DEPENDENCY_SOURCE...]
```

Author sources contain a PNG for each name in native XML. `build` validates
references, dependencies, atlas bounds and asset paths, copies only declared
resources and creates premultiplied Color XNB fallbacks. It refuses to overwrite
an existing output directory. Dependency folders are validated, not embedded.
Keep source artwork outside the final package. PNG/WAV files inside `wardrobe/`
are ordinary runtime resources, not XNB files.

Run `verify-skin-package.ps1 -Package OUTPUT` to exercise the installed
Worldsmith classifier and file validator without uploading. Test the result in
Wardrobe+'s Author preview as well: the runtime verifies GPU shaders and audio
decoding, which the offline author tool doesn't emulate.

The [minimal template](../examples/template/README.md) needs your fallback atlas.
`build-example.ps1` creates a complete Ashen King package using your installed
game's original King geometry. Python with Pillow is required for that example.
The authoring tool's local game/runtime DLLs must not be uploaded with a skin.

## Manifest and references

```json
{
  "schema": 1,
  "id": "my-skin",
  "name": "My Skin",
  "version": "1.0.0",
  "minimumWardrobe": "2.0.0",
  "defaultProfile": "sparks",
  "effects": [
    {"id":"takeoff", "kind":"particles", "native":"jump",
     "count":12, "color":"#FFA040FF", "endColor":"#D0401000"}
  ],
  "profiles": [{"id":"sparks", "rules":[
    {"id":"jump", "trigger":"jump", "channels":[
      {"channel":"particles", "mode":"replace", "effects":["takeoff"]}
    ]}
  ]}]
}
```

IDs are lowercase ASCII, start with a letter and contain letters, numbers,
periods, underscores or hyphens (80 characters maximum). Use stable IDs across
updates. Unknown fields, null values, future schemas, duplicate IDs, dependency
cycles, missing references and invalid numeric values produce diagnostics.
Relative asset paths stay inside `wardrobe/`; rooted paths, links and parent
escapes are rejected. Manifest size is limited to 1 MiB.

`dependencies` contains `{ "id":"shared-embers", "minimumVersion":"1.0.0" }`.
An effect reference is local `takeoff` or `shared-embers/takeoff`; external
references must name a declared dependency. UI channel choices use
`package/profile`; animation choices use `package/animation`. The default profile
comes from the body and actual worn equipment, in native composition order.
An explicit channel profile takes precedence and is evaluated once.
Outfit export includes discovered Workshop links for these dependencies.

## Rules and native feedback

Channels: `particles`, `surfaceSound`, `equipmentSound`, `shake`.
Each rule has a stable `id`, `trigger`, optional `surface` and `equipment`
(both default `*`), `minCharge`/`maxCharge` (0..1), `minSpeed`/`maxSpeed`
and `priority` (-1000..1000). Speed uses native velocity units per game tick;
land/splat use the preceding vertical impact speed.

Rules run from body through equipment to the explicit profile; within a profile
they run by increasing priority, then ordinal rule ID. Use a higher priority for
surface exceptions. Each channel entry has `scale` (0..4) and one mode:

| Mode | Result |
| --- | --- |
| `add` | Keep the earlier decision and append named effects. |
| `replace` | Suppress this native channel, clear earlier effects and emit these. |
| `keep` | Restore native behavior and clear earlier effects. No effect list. |
| `off` | Suppress native behavior and clear earlier effects. No effect list. |

An omitted channel inherits. Use two ordered rules for `keep`, then `add`.
Surface names: `normal`, `snow`, `ice`, `water`, `sand`, or a foreign block's
full type name / registered alias. Equipment uses the game's exact `Items` name,
for example `GiantBoots`. Conditions never grant equipment or alter physics.
Replacing particles leaves native sounds alone. Surface and heavy-equipment
sounds are separate. Character shake controls don't suppress map earthquakes.
Failed snowy jumps produce no `jump` event; successful short snowy jumps expose
the native clamped charge. Foreign registered block callbacks remain available
whenever their channel keeps native behavior.

Events: `chargeStart`, `charge`, `chargeEnd`, `jump`, `rise`, `apex`, `fall`,
`land`, `splat`, `walk`, `step`, `waterEnter`, `waterExit`, `idle`, `recover`.
Idle/walk/charge/rise/fall run continuously; use cooldown or an emitter rate.
Other events are discrete. `owner/event` IDs allow another mod to raise its own
presentation events. They must be raised on the game thread.

## Effect definitions

An effect's `kind` is `particles`, `sound` or `shake`. All support `id` and
`cooldown` in seconds. Colors use `#RRGGBBAA`.

Particle fields fall into a few groups:

- source and animation: `texture` (PNG), `columns`, `rows`, `fps`;
- emission: `count`, `rate`, `duration`, `life`, `lifeSpread`, `maxAlive`;
- motion: `speed`, `speedSpread`, `angle`, `spread`, `gravity`, `drag`,
  `rotation`, `spin`;
- appearance: `size`, `endSize`, `color`, `endColor`;
- placement: `offsetX`, `offsetY`, `anchor`, `space` (`world` or `actor`) and
  `layer` (`back` or `front`); and
- blending: `blend` (`alpha` or `additive`).

Angles use degrees and -90 points up. Distances use game pixels; simulation rates
are per second. `anchor` defaults to the feet. Offsets and horizontal movement
mirror with facing. Without a texture or native source, the emitter draws a
colored pixel. A `loop` emitter ends on `stopOn`, reset or disposal. One actor can
have at most 2,048 live particles and 64 emitters.

Native particle atlases: `jump`, `water-jump`, `water-splash`, `snow`.
These reuse game artwork with the authored emitter parameters; `keep` invokes
the complete native spawning behavior.

Sound accepts `sounds` (a random choice of WAV files), or `native`, plus `volume`,
`pitch`, `pitchSpread`, `voices`, `loop`, `stopOn` and `cooldown`.
PCM WAV is recommended. Audio is bounded to 30 seconds/8 MiB per asset and 32
owned voices per actor. Volume respects game SFX mute/master and the channel
control. Stopping custom audio disposes its instances, never the game's sound.

Native sound IDs: `jump`, `land`, `splat`, `water-jump`, `water-land`,
`water-splat`, `ice-jump`, `ice-land`, `snow-jump`, `snow-land`, `snow-splat`,
`sand-land`, `heavy-land`, `heavy-splat`, `water-enter`, `water-exit`.

Shake accepts `duration`, `shakeX`, `shakeY`, `frequency` and `waveform`
(`noise`, `sine`, `alternating`). Contributions fade out and are capped at 16
pixels. Preview and replay actors never contribute camera shake.

## Animation, anchors and materials

`animations` entries have `id`, `item` (default `NULL`, the body), optional
`material`, `clips`, `attachments` and `equipmentAnchors`.
Clips select `state`: idle, walk, charge, rise, apex, fall, land, splat, recover,
lookUp. A missing state uses idle, then native fallback if idle is absent.
Live ascent switches directly to fall unless the body explicitly defines an
`apex` clip. That optional clip plays once, for at most half a second, and exits
before wrapping even if `loop` is true. The `apex` event fires in either case.
Each clip declares `texture`, `loop`, `transition` (crossfade seconds) and frames.
Since 2.0.6, optional `nativeFrames` maps each authored frame to a native regular
sprite key (0 through 12). For example, walk frames can use `[1,3,2,3]` to follow
the actual game's walk-one, smear and walk-two sprites, including replay draws.
The native sprite selects the frame without advancing the actor clock. When no
key matches (for example in a state-only author preview), timed playback applies.
These clips require zero transition and no timed frame effects; use state/event
effects for feedback. Declare `minimumWardrobe: "2.0.6"` when using this field.
Each frame has integer `x`, `y`, `width`, `height`, pixel `originX`/`originY`,
`duration`, optional `anchors` and `effects` references. Origins are pixels from
the frame top-left, unlike native Sprite's normalized center. Frame events run
in Update, including crossed frames; drawing never advances time or emits effects.

Anchors contain `id`, `x`, `y`, `rotation` relative to the character origin.
Attachments contain `id`, `texture`, atlas rectangle, pixel origin, `anchor` or
`parent`, `offsetX`/`offsetY`, `rotation`, `layer`, `inertia`, `stiffness`, `damping`.
Parent cycles are rejected. Inertia follows actor velocity and runs separately
for live, preview and replay actors.
To align an existing native wearable with custom poses, add an equipment anchor:
`{"item":"Cap","anchor":"head","nativeX":0,"nativeY":-28}`.
Its difference from the frame's head anchor offsets that item's native sprite;
items with their own custom animation use their own clips instead.

Material entries have `id`, `kind`, `color`, `strength`, optional `mask`,
`texture`, `palette`, `scrollX`/`scrollY`, `pulse`/`pulseSpeed`.
Kinds: original, tint, palette, glow, scroll, gold, glass, magenta, cosmic, shader.
Mask red controls affected pixels in atlas UV coordinates. Palette needs at least
two colors. Original texture uses the authored material. Since 2.0.8, a non-original outfit
or item material overrides the surface of both authored animation frames and the
native fallback. Item overrides take priority over the outfit default. Animation
pose timing, attachments and effect profiles remain independent. Generated
material frames are prepared once and owned by the appearance; rendering doesn't
read back or generate textures. A custom shader is compiled MonoGame
3.7.1 DirectX `.mgfxo`, not executable package code. It must expose
`MatrixTransform`; expose `SpriteTexture` for the current atlas. Optional parameters:
`Time`, `Charge`, `Tint`, `Mask`, `Pattern`. The bundled `assets/Advanced.fx` is a
working reference. Unsupported foreign shader/sorted/depth passes use plain
authored textures. A flattened sprite consumer receives the native fallback.

Since 2.0.5, optional float parameters also include `IsCharging` (0 or 1),
`StateTime`, `JumpAge`, `LandingAge` (seconds, -1 before the event) and
`JumpCharge` (charge captured at takeoff). Event ages survive pose changes and
freeze with the actor clock. Restore resets them; each preview/replay actor owns
its own history. Use `minimumWardrobe: "2.0.5"` for shaders requiring them.
The Eclipse King example uses these parameters to cool light across the apex.

Since 2.0.7, exposing `LiquidLevel` opts a shader into cosmetic liquid simulation.
Optional floats `LiquidTilt`, `LiquidWave` and `Facing` supply world-space slope,
damped impact waves and the current draw's horizontal direction (1 or -1).
Each actor owns a bounded spring integrator driven by velocity changes and jump,
land and splat events. Splat drains the default 0.72 fill fraction in 0.3 seconds;
it remains empty while flattened and recovers at 0.24 per second afterwards.
Restore clears motion history. Particle effects can set `liquidScale: true` to
scale emitted counts by the remaining fraction; without a liquid actor, counts
remain unchanged. These are cosmetic effects and don't modify world physics.
The Vessel King example includes per-pose area masks and a premultiplied glass
shader. Declare `minimumWardrobe: "2.0.7"` when using these parameters or fields.

### Water collisions and native pixel ownership

Since 2.0.8, particle effects may set `collision: "water"` with `space: "world"`.
Droplets resolve subpixel motion against blocking geometry, disperse on impact,
slide with damping and fall off edges. They disappear at their configured `life`
plus `lifeSpread`; use about four seconds for spills. `liquidScale` scales the
burst to the vessel's remaining contents. The pool caps colliding droplets at 256
per actor; standalone previews use gravity without world collision. Live actors
and replay actors query the current level, including off-camera screens, without
retaining world blocks across updates or changing gameplay.

`assets/king-mask.json` contains explicit native frame scanlines, not runtime
colour recognition. `rows` selects visible King pixels, `occluded` marks foreign
pixels over the reconstructed body, and `foreign` marks separate objects and
accessories. Both protection classes keep source colour and alpha. Only
`occluded` contributes to hidden body geometry for contours and fluid volume.
Regular frames permit custom body silhouettes; joint ending poses use the authored
native anatomy. A substantially redrawn third-party ending needs a matching mask.
`tools/preview_king_mask.py` exports the two-colour mask, reconstructed body atlas
and an HTML review with enlarged per-frame source/mask/geometry comparisons.
`assets/item-masks.json` supplies separate coverage for every native equipment
slot. The same material pipeline resolves collection/item sources first, then
selects ownership by slot, pose and dimensions before applying fitting offsets.
The body mask is never reused for boots, crowns, capes or other items. Ordinary
poses keep arbitrary item silhouettes; ending masks describe native anatomy
and palette variants, not substantially redesigned third-party silhouettes.

`tools/build_item_masks.py --king GAME/Content/king --layout native-layout.json
--output candidate.json` prepares native item coverage for review. NPC-only
groups stay protected. Hidden pixels require an exact translated regular pose
match and agreement across candidates; missing evidence remains unfilled.
Review candidates before replacing the source asset. `tools/preview_item_masks.py`
with `--king`, `--layout` and `--output` exports full item mask atlases and
per-frame comparisons. Green marks visible ownership, red protected other
artwork and blue protected overlaps used only to complete the item contour.

`assets/embedded-masks.json` identifies reward pixels carried in the **base**
atlas rather than a separate item layer. These regions take the owning item's
material independently of the body's material and current equipment: first-ending
crown delivery, NBP Babe's original crown and handoff, and the owl's carried cape.
The NBP replacement crown belongs to Babe and stays protected, including its
delivery. The original scene geometry and resolved base artwork are retained;
this is material routing, not automatic retargeting of an arbitrary replacement
item texture to an unprovided cinematic pose. Normal item layers continue to use
their resolved collection or individual texture. Reward/equipment events remain
native. `tools/preview_embedded_masks.py` combines the source masks with the
graphics test's original/Glass exports for a review of every affected scene pose.

The body reskin example generators share the body mask; body-only sets don't replace equipment
sources or grant crowns/capes.

## Ownership and integration

Asset preparation runs before player activation in OnWorldReady for native
startup loads, or during explicit appearance/preview changes. The supplied world
scope keeps prepared resources. Actors reference-count their resource generation;
restarts, reloads and disposable previews can't release another actor's textures.
PNG dimensions are capped at 8192 and selected texture memory at 128 MiB.
The startup substage is `wardrobe-plus.presentation-assets`.

`WardrobePlus.Advanced.PresentationEvents.Happened` observes defensive event
copies. `RegisterSurface(Type,string)` returns an owned disposable alias;
`Raise("owner/event")` adds a cosmetic event without modifying gameplay.
Native `RegisterJumpSound`, landing sound and particle callback APIs remain intact.

Runtime 1.35 provides typed cosmetic actors, appearance generations, scoped draw
contexts and namespaced action delivery. The BCL/reflection `PresentationBridge`
version 1 remains available for older consumers and forwards to the same history.
Replays 2.4 writes format 4 with sparse events and draw anchors; it also reads
formats 2 and 3. Ghosts
are silent. Seek reconstructs particles silently over at most the last 30 seconds;
long-running effects originating earlier may be absent until their next event.
Old recordings animate with the current outfit but have no historical cosmetic
events to replay. Recordings don't embed Workshop assets or freeze package versions.

## Acceptance

Run focused checks and GPU tests against the installed game. Set
`WARDROBE_SKIN_TEST` to a built package before `build.ps1 -Graphics` to render
the Ashen King states and exercise its PNG/WAV/XNB loading. Inspect the generated
image, then test native snow, water and heavy boots in play. The code tests also
exercise native snowy jump eligibility and foreign sound/particle callbacks.
Use `verify-compatibility.ps1` for Ball King, Replays, Mapping and Smooth Camera.
Keep settings outside Workshop package folders. No build command uploads to Steam.
