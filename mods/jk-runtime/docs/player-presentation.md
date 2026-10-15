# Player appearance and visual effects

Runtime provides `JKRuntime.Presentation` for shared appearance sampling, draw
contexts, frozen images, player geometry and ordered visual contributions.
All mutable operations run on the game thread.

For a frozen trail, adapt the [mod-owned example](common-effects.md#build-a-short-trail-in-your-mod).
Keep emission, timing and cleanup in your mod; the APIs below supply the shared
appearance and rendering operations.

The native bridge requires one already-loaded shared Harmony 2 engine. Runtime
does not load another engine. For example, Subframe Charge and Mega Mapping
Expansion supply a shared engine in their packages.

Call `PlayerVisuals.Prepare(scope)` during world or attempt preparation. Bind
players and register contributions at activation; own the returned registration.
The native bridge follows `PlayerEntity.SetSprite` and restores the latest source
when its last registration ends. It does not change player physics.

`PlayerAppearance.Resolve(player)` samples the body after form and attachment
contributions. `AppearanceStage.Outfit` selects an explicit outfit pose;
`Complete` includes replacements and transient effects. Form contributions are
exclusive within a sample. Contributions sort by phase, order and owner ID.
Exceptions disable the failing contribution and expose its `LastError`.

Use `AppearanceGeometry.Resolve` for fractional world placement. Collision bounds
are separate. The native anchor remains `(9, 26)`; the reviewed active
HitboxResizer draw path uses its configured half-width and height. Unknown draw
patches return approximate coverage, not a compatibility guarantee.

Frames are borrowed samples, valid only until the next update or appearance
publication. `PlayerAppearance.Draw` establishes registered provider contexts.
Draw providers must not advance clocks, dispatch events or play sounds.
`RegisterContext` supports external animation/material providers without requiring
Runtime to reference their assemblies. Publish successful changes with `Publish`;
subscribers are isolated and consumers can compare `Revision` independently.

`AppearanceCapture.Freeze` requires an idle SpriteBatch and a preservable target.
It returns an owned GPU image; disposing it releases the target. `Read` or
`AppearanceImage.ReadPixels` explicitly reads detached CPU data. Capture canvases
are 32–512 pixels per side, default 128. Pixel readback rejects drawings touching
the boundary. GPU-only images retain the full bounded canvas. Custom renderers
must fit that canvas. Capture preserves caller graphics state on failure.

Pixel colors, alpha and facing are baked. `Offset` is an integer displacement
from the floored capture anchor. Use `TopLeft` to avoid applying pivots or flips
twice. A successful transparent render remains transparent. Static fallback is
opt-in and reports approximate coverage with the original failure reason.
Texture-field fallback cannot reproduce arbitrary custom Draw implementations.

Keep effects in world coordinates and project for each camera pass. Repeated
draws must not update simulation or duplicate emission. Frozen images are
independent of source texture lifetime; CPU snapshots own no GPU target. Clear
or rebuild transient effects on state restore. Dispose images on actor/world
teardown and reacquire invalid images after graphics-device loss.

`Signal` delivers namespaced cosmetic actions with actor identity and live,
playback or restore provenance. Existing gameplay facts remain on GameplayEvents.
`CosmeticPlayback` provides an optional typed actor factory, independent playback
clocks and a bounded 256-event serialized history. Payloads are at most 4096 UTF-8
bytes. Playback suppresses recording into that live history.

Wardrobe retains its public discovery bridges for older consumers. Updated
consumers use Runtime directly and must not subscribe through both paths.

## Consumer pattern

The compiled [PresentationExample](../examples/PresentationExample.cs) captures an
owned afterimage at an action boundary and draws it in each camera pass. A host
must dispose it on actor teardown or state restoration. A capture failure skips
that optional image; it must not change whether the gameplay action is allowed.

For form artwork, sample an explicit outfit pose with `FromSprite` and pass the
pose name to the provider, for example `charge`. Exclude `ExcludeFromForm` and
`WorldEffect` layers with `Filter`. `Body` samples exclude declared world effects
before composing forms and attachments; `Complete` keeps them for live drawing.
A legacy renderer without layer metadata remains one indivisible layer.

Call `Publish` after a complete outfit change, including in-place texture updates.
Native providers that don't publish are detected through `Signature`, which
includes source rectangles, origins, tint and nested sprite/texture identity.
It cannot detect a texture's changed pixels without publication.

Mappings that need CPU silhouettes prepare one body sample per simulation tick
for dynamic renderers and reuse unchanged static samples. Ball only freezes its
animated transition while the original body is visible. Dash keeps one frozen
GPU image for an action; its echoes share that image. These are consumer policies,
not a hidden global render or readback loop.

## Compatibility boundaries

`VisualGeometry` currently describes the native sprite anchor, body center and
collision rectangle. Authored skeleton/equipment points and form rotation remain
with their render providers. Stereo supplies its course camera through
`IAppearanceProjection`; use
`PlayerAppearance.Project(frame)` to project these samples. A projection provider
must draw at the supplied screen anchor and keep projection observational. Its
cube/ship input comes from shared outfit capture; rotation stays in the renderer.

Runtime doesn't serialize arbitrary mod forms into replays. Replays keep their
existing current-art semantics and now store per-frame anchor offsets; its format
4 reader retains formats 2 and 3. Capturing an unknown `PlayerEntity.Draw` patch
or a virtual renderer with side effects still needs a reviewed adapter.
