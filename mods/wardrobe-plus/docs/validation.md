# Wardrobe+ compatibility and testing

The native contract fixtures target Jump King assembly version 1.0.0.0, SHA-256
`476F2033B8B614EC97B04311799B2B78239397A2FE8018C77BB45A1F946FFC88`.
Other game versions require their own compatibility checks.

## Reproduce the checks

From the repository root:

```powershell
.\scripts\check-mods.ps1 -Mod wardrobe-plus
.\scripts\check-mods.ps1 -Mod wardrobe-plus -Integration
.\mods\wardrobe-plus\verify-compatibility.ps1
```

| Area | Coverage |
| --- | --- |
| Selection | Item/source precedence, partial and missing collections, returning subscriptions and locked randomization |
| Persistence | XML round trips, atomic replacement, backups, failed writes, corrupt/newer files and independent outfit snapshots and schema 3 equipment |
| Native integration | Workshop refresh, inventory selectors, menu registration, disable/re-enable and appearance rollback |
| UI and fitting | Pointer and controller navigation, native text, footer layout, both facing directions and native movement/ending frames |
| Other mods | Ball King composition, Replays cache refresh and Mega Mapping's flattened player appearance |
| Materials | Global/item inheritance, legacy IDs, presets/undo/recipes, premultiplied transparency, undistorted background blending, absence of Glass scene capture, native pose silhouettes, fitting/preview parity and material picker |
| Package | Preview format, shell discovery, embedded implementation and dependency exclusions |

The GPU fixtures use isolated graphics devices and copied native assets. Captures
stay under `build/wardrobe-plus/_INTERNAL/tests/`. Failure-injection cases exercise
rollback. Equipment checks use an isolated native inventory/settings cache and
fixture directory; user saves and subscribed skin configurations are untouched.

## In-game checks

Automated tests do not establish physical controller feel, compatibility with
every third-party sprite replacement, an end-to-end ending sequence or long
sessions with arbitrary Workshop art. Those need interactive playtesting.

The material GPU fixture retains `materials.png`, `material-timing.txt`,
`installed-material-references.png` and `crystal-scene-*.png`. Palette references
are GoldenBoots (3162670641) and the red Tunic (3162938650); source assets stay in
their Workshop folders and are not packaged. Normal builds test Glass against
ordinary alpha compositing over changing patterned backgrounds, and verify zero
refraction captures and no equipment-triggered rebaking. Fitting, native poses,
preview and third-party appearance consumers use the same transparent atlases.

The parked experiment can be tested with `build.ps1 -Graphics -ExperimentalRefraction`.
Its retained live crystal tests verify displaced
RGB samples, source hue removal, moving backgrounds, holes, mirroring, viewport,
scissor and transform restoration on the installed game's discard target.
`crystal-live-timing.txt` measures CPU submission, not isolated GPU execution.
In that experimental build, native and replay drawing share live refraction. Ball King and Mega Mapping's
flattened consumers are checked against the fitted static fallback, which retains
the live outfit's lower-layer context. Unsupported custom render passes also
use the fallback. There is no gameplay framebuffer readback.

## Cosmic checks and performance

Cosmic GPU checks compare overlapping moving masks, facing directions, moving
camera origins and two stacked screen viewports against a single continuous
view. They verify white edges, unchanged exterior pixels, animation at rest,
all native pose silhouettes, fitted clothing, clipping, extra texture/sampler
restoration and one quad with no refraction capture. Settings, presets, undo and
recipe round trips include Cosmic. `cosmic-*.png` retains a native-size and a
larger preview over the installed map art.
The compatibility run installs Smooth Camera's real transform postfix after
Cosmic has already drawn, verifying that a previously JIT-compiled renderer
still follows camera hooks. Replay rendering is compared live; Ball King and
Mega Mapping's actual compositors are compared against the static fallback.

Set `WARDROBE_COSMIC_BENCHMARK=1` for the GPU-completed comparison against the
static version. It alternates mode order over four rounds, discards 50 warm-up
frames per round, and measures 600 frames per mode with an identical one-pixel
GPU fence. Results go to `cosmic-performance.csv`; these are isolated fixture
frames, not full-game FPS or proof of compatibility with every camera mod.
`WARDROBE_REACH=1` exercises the Reach device profile.

## Live editing and menu lifetime

Fixtures cover immediate appearance publication, grouped fitting Undo/Redo,
equipment/slot conflicts, empty versus legacy presets, missing inventory,
recipe snapshots, map equipment restoration and rapid queued actions. Failed
persistence remains retryable. Borrowed previews and shared materials retain
correct ownership through repeated refitting.

The Workshop regression opens the real embedded page, triggers Equip/Unequip
and advances deferred native callbacks. Equipment, material and preference
changes must preserve the active page, including repeated menu synchronization.

Shared Runtime text-entry checks cover Unicode input, focus, selection, deletion,
length limits, nested capture and release of held keys. Wardrobe captures include
keyboard, controller and Cyrillic entry. See [UI pages](../../jk-runtime/docs/ui-pages.md)
for the shared contract. An in-game typing session and physical controller checks
remain part of interactive validation.

The optional refraction benchmark uses `WARDROBE_BENCHMARK=1` with
`build.ps1 -Graphics -ExperimentalRefraction`. Compare warmed samples from the same
device and scene; the retained experiment is not the normal Glass renderer.
