# Preview and troubleshooting

[Handbook](authoring.md) · [Parameter reference](parameter-reference.md) · [Recipes](recipes.md)

## Short authoring loop

1. Edit canonical map sources, not files in a build output or installed copy.
2. Build the map with its authoring tool and compile the scene cache if used. Check the build report.
3. In a debug game using that built level, request Reload. A build alone does not reload a running scene.
4. Read the acknowledgement, inspect the affected screen and capture only when useful.
5. Before distribution, check dependencies, referenced assets and licenses. A successful build is not a playtest or route proof.

Reload validates and prepares a candidate before replacing the current scene.
If it fails, the last working scene remains active; seeing old visuals can mean
reload failed, not that the new XML settings were ignored. Reload refreshes mod
scenery, not arbitrary native geometry/audio. Rebuild and relaunch/restart the map
as appropriate for native content changes.

## Native preview controls

Preview is available only when Jump King is launched in debug mode with the
built level root: `JumpKing.exe -debug <level-root>`. This tool talks to the
running mod through map-local files; it does not capture or automate the desktop.
The correct mod and JK Runtime must already be enabled.

From the repository root, set a real built level path and tool path, then send
one reload request:

```powershell
$sceneLevel = (Resolve-Path 'C:\Maps\MyMap').Path
$scenePreview = '.\mods\mega-mapping-expansion\tools\preview.ps1'
& $scenePreview -LevelRoot $sceneLevel -Reload
```

Read its acknowledgement with `& $scenePreview -LevelRoot $sceneLevel -Status`
before sending another action. Requests share one command file; sending many
commands immediately in a script can overwrite requests the game has not read.
No status file before the first processed request is normal. Choose subsequent
controls from the table below, one request at a time.

These commands assume that particular project has been built; substitute your
own level root. In the exported minimal kit, preview.ps1 is beside this document,
so use `'.\preview.ps1'` for the tool path while still targeting a playable level.

| Control | Purpose and boundary |
| --- | --- |
| `-Status` | Read the last command acknowledgement, not a continuously fresh state snapshot. |
| `-Reload` | Load map-owned scene again; no automatic watching/rebuilding of sources. |
| `-Screen N` | Move debug player/camera to one-based screen N. |
| `-PlayerX`, `-PlayerY` | Screen-local player body position; ranges 0..462 and 0..334. |
| `-Paused $true/$false` | Freeze/resume scene simulation, not native player physics or the game menu. |
| `-Seek seconds` | Set analytic time (0..86400) and reset water/reaction history; not replay of prior interactions. |
| `-Step seconds` | Advance paused scene, at most 0.1 s. |
| `-ResumeGame` | Invoke native resume action; distinct from scene pause. |
| `-Mode scene/alpha/emission/light/reflection` | Inspect normal scene, source coverage, additive gain, light field or reflections. |
| `-Layer all/background/world/foreground` | Filter mod drawing queues; not native layers/UI, collision or a universal compositor filter. |
| `-Hidden id1,id2` | Replace hidden-ID set; `@()` clears it. Individual puddles can be hidden this way. |
| `-Select id` | Show source bounds/pivot; alpha/emission selection isolates a Prop/Node pass. Empty string clears it. |
| `-Colliders $true/$false` | Show/hide Anchor metadata and actual King hitbox. |
| `-CaptureScene` | Capture composed world before menus and debug overlays. |
| `-Capture` | Capture native 480x360 target with UI, before final tint/mirror. |
| `-Restart` | Request native debug-run restart without deleting saves. |

Selection bounds are unrotated source bounds, not a pixel-perfect deformed mesh
or collision box. Alpha/emission views bypass normal light multiplication.
Layer filtering does not isolate all late reflection/lighting work. Use the
specific diagnostic mode and hide a surface when isolating compositor effects.
CaptureScene is serviced in the active compositor path; for a bare scene without
advanced lighting or composite surfaces, use Capture if no scene capture arrives.

Command/status files and captures live under the built level's
`props/mega-mapping-expansion/`. Captures use its `preview/` directory. Commands
are one-shot across reload/restart; stale files are ignored by a new process.
Read acknowledgement and mod logs before sending another diagnostic request.
Normal rendering does not continuously read back screenshots from the GPU.

## Diagnose by symptom

| Symptom | Check first |
| --- | --- |
| Nothing appears | Enabled dependencies; correct loaded level root; scene path; parse/load error; screen number. The scene XML is not a complete native map. |
| Background missing | Opaque native midground, incorrect layer, hidden IDs, or zero opacity. Background Z cannot outrank native midground. |
| Clouds cross dark trees | Separate sky/cloud/tree assets and correct phase/Z. A colour guess or parallax coefficient is not a semantic sky mask. |
| King floats over a platform | Native collider walk line versus painted top and source pivot; graphics/Anchors do not move collision. |
| King is covered by a platform | Foreground layer, or a whole platform redrawn as reflection occluder. Separate dry front-face art from the walkable top. |
| Rocks look translucent | Source alpha, shape alpha, opacity Track and overlapping reflections. Apply alphaCutoff only to the intended solid asset, not all materials. |
| Mist jumps or loops visibly | Point sampling, mismatched loop endpoints/velocity, short repeat distance; inspect at normal speed and across the seam. Use linear sampling and gently closed motion for authored mist. |
| Tree root slides/cut end shows | Pivot, authored height, parallax mismatch and insufficient overscan beyond the viewport. Wind cannot invent missing branches. |
| PNG shrub ignores King proximity | Check its `reactRadius`, `reactStrength`, current screen and pivot. Both Props and Nodes use the spring reaction updater; wind and gaze are separate controls. |
| Moth moves but wings stay fixed | Set positive flutterHz and explicit ordered flutterBodyStart/End boundaries inside the source frame. No body region is inferred automatically. |
| Puddle has no visible King reflection | Correct screen, opacity, outline below planeY, King above plane, and an occluder not covering the result. Isolate reflection mode. |
| Reflection is too tall/flat | Adjust reflectionScaleY; Puddle perspective further compresses distant source height. Surface plane must match the walk line. |
| Rain passes through roofs | collide enabled and matching `kind="solid"` Anchors rebuilt with geometry. This is top-height clipping, not arbitrary ray collision. |
| Rings do not match rain impacts | Expected limitation: ambient rings are independent decoration. |
| Lake has no swimming behaviour | Add a native water volume; visual Water alone only animates presentation. |
| No rim | Nonzero rimOpacity and rimPixels; a visible nearby Light with nonzero rimIntensity; light rimRadius and ScreenLook scale. Remote fills intentionally do not outline the King. |
| Lamp is bright but world is flat | Glass brightness is not Light intensity; check advancedLighting, ambient floor, source radius/falloff and occlusion. |
| Light disappears in all directions under King | Expected when the King fully covers the source. Ambient/other sources should remain. |
| Other props do not cast shadows | Author rectangular Anchor blockers with blocksLight=true and advancedLighting=true; art alone is not shadow geometry. |
| Mod disappears on native Restart | Check new-run load errors and installed version in logs. Current lifecycle is designed to recreate the scene on Restart. |
| Changes appear only after relaunch | Build is not reload; wrong level path, failed reload, or native rather than mod-owned content was edited. |
| Unknown setting error | Exact XML spelling, correct element kind and direct Track children. Unsupported settings are rejected, not placeholders. |

## Performance without guesswork

Enable `Options profiling="true"` during a local check and inspect several
five-second intervals after warmup. The first interval includes lazy setup.
Logs include draw cadence, frame-interval p95/p99/maximum, queue/compositor/water CPU time,
canopy and lighting work, GC counts, managed memory and texture/mesh/target counts.
Separate native Draw and Present timings identify driver/display waiting outside
scene submission. Native Update, peak stage times and frames exceeding 1000/58 ms
are also recorded. Sampling windows reset on screen/focus changes; only
`gameActive=True` windows describe active gameplay. Test walking, jumping and light
occlusion, not only a stationary warm frame. Average FPS cannot establish a
minimum FPS guarantee. `gameActive` and `targetStep` are recorded too. A long Present
call is evidence of output waiting, not proof of a particular monitor, driver
setting or window-occlusion cause. Do not silently change the user's global VSync
or display configuration to improve a benchmark.

Queue time includes canopy submissions; compositor time includes water/lighting.
Do not add overlapping counters. Draw cadence is not GPU timing, CPU submission
time is not total GPU cost, and base texture bytes are not a full driver-memory
report. Native screenshots validate appearance, not performance.

Change one category at a time: number/area of translucent layers, wind meshes,
flex slices, emitter/rain counts, light sources or reflecting surfaces. Large
transparent textures still consume sampling/draw work; tightly bounded assets
help. Supersampling mostly affects compilation and asset quality, while active
object counts and overdraw affect runtime. A vector shape budget passing does
not guarantee a fast scene. Keep the intended art; measure before flattening or
removing layers. Turn profiling off before normal release.

## Documentation checks

From a repository checkout, build the mod first, then run:

```powershell
.\mods\mega-mapping-expansion\tools\check-docs.ps1
```

This checks local handbook links and compiles complete XML examples with the
same scene compiler used by the mod. It checks schema/asset validity, not visual
quality, route completion or native rain/physics alignment. Outputs are isolated
under the mod's build directory; it neither installs nor controls the game.
