# Geometry, mechanic inventory and presentation ticks

Call these APIs on the game thread. Track every registration with the owning
level's `ModuleContext`; the registry cannot guess when your player or level has
gone away.

## Native geometry

For the actual horizontal modifier pass, use [motion observation](motion-observation.md).
Its arithmetic evidence is separate from geometry, standing support and inertia.

`NativeWorldGeometry.ReadScreens()` and `ReadBlocks(screen)` validate the private
game fields and return copied arrays. Their elements are still live native
objects, not isolated simulation state. Do not mutate them through a read API.

`CopySlopeCollision(slope)` copies the actual rectangle, slope type and line
array without running constructors. Corrections already applied to the native
collision lines survive copying. An unmodified native slope remains unmodified.
Only the native slope portion is copied: arbitrary subclass behavior needs its
own reviewed simulation adapter. Navigation and simulation consumers can use the
same native screen/block access and collision-copying contract.

`ReadSlopeVertices` is shape inspection. It does not replace `IBlock.Intersects`,
block callbacks, material rules or the player's controller.

## Map topology

`MapTopology` shares side-link interpretation between camera framing, player
attachments, teleport placement and offscreen indicators. Native integer links
(including Mapping's links above screen 255) and the reviewed Expansion Blocks
MultiWarp fields use the same coordinate contract. Native links take precedence;
conflicting height bands stay ambiguous for a single preview. Unknown teleport
implementations aren't inferred from names or guessed destinations.

`Destination` reads live link targets. `Connects` recognizes a particular crossing,
including an ambiguous foreign band when the actual destination is known.
`PreviewDestination` adds a rendering hint from audited solid edge geometry. It
doesn't prove a king fits through the opening and never runs foreign collision
callbacks while drawing.

`BuildMap(screens)` builds a default spatial map of connected vertical regions.
An authored side entrance places the entire destination region beside its source,
including screens above and below the entrance. Native left/right marker slots
take priority over the game's fallback that lets a single link serve both exits.
Regions keep one placement, independent of player positions. For the base game,
the right entrance from screen 3 places screen 156 to its right, and screen 157
to the right of screen 4. Screen numbers here are the game's one-based labels;
the API uses zero-based indices.

`TopologyMap.Regions` reports each vertical range, connected group and offset.
`Connections` retains every observed side connection. A nonzero `Winding` means
that connection disagrees with the default flat placement, such as a self wrap
or a conflicting cycle. Such edges don't reposition already placed regions.
Placement prefers paths with fewer fallback exits, then fewer region crossings;
ties follow native screen and side order. Disconnected groups have no inferred
relative position. `TryPosition` and `TryChartPosition` return false between them.
`ChartPosition` keeps the original point when no relationship is known.

`Unfold(screens, anchor, maxDepth, maxImages)` returns alternate screen images with offsets
from native coordinates into the anchor's chart. A self link repeats its screen
480 pixels away; cycles can repeat indefinitely. The default budget is eight
edges and 256 images. `Truncated` reports an unfinished frontier. There is no
unique finite flat layout for every linked map. Height-band images describe
connectivity, not a navigable route through all terrain. Sealed side edges don't
create images through walls. A known solid boundary between consecutive screen
numbers separates their vertical charts. `VerticalRange` returns the contiguous
vertical range around a screen using the same observations.

`NearestImage` chooses the closest reachable target image with a default depth
of 64 edges and a 256-image limit. Raw native coordinates aren't an extra image
when a reachable image exists. If no target image fits the budget, it returns
the original point.
Use this for local contacts and explicit alternate images, not the default
placement of regions. A shallow depth suits local contacts. `TryCrossing` recognizes short movement
through a known seam and returns its coordinate shift; arbitrary long teleports
remain discontinuities. `Normalize` converts a seam-straddling body after its
center crosses. Its route-anchor overload keeps an attached passenger on the
carrier's route even outside a portal's height band.

`IsBlocked` uses native screen collision queries around the queried position,
independent of the camera's current screen. Seam-straddling rectangles also check
destination terrain. Its route-anchor overload checks a whole attachment through
one portal. This is a live game-thread physics query: foreign `Intersects`
callbacks can run. It isn't a safe rendering or isolated simulation operation.

Runtime invalidates observed geometry, spatial maps and unfolding caches before native updates;
Smooth Camera also invalidates before its existing observation pass. Call
`Invalidate()` after changing links or geometry during the same update. No
topology API changes the native king hitbox or replaces a mod's teleport behavior.

For late world overlays, `FrameComposition.ProjectWorld(point)` uses the active
presenter's logical viewport projection. Without a presenter it selects the
default spatial chart for the native viewport, then applies the native camera.
Pass canonical world coordinates, not a point already returned by a chart query:
the presenter chooses its own chart once.
Presenters supply it through the `RegisterPresenter(ready, projectWorld)` overload.
Smooth Camera projects the default map through its current pan and vertical
translation. Only a linked column actually shown by its compositor may replace
that default image, preserving visible self wraps without following distant cycles.
Inside a `ScreenPass`, native screen transforms still apply. This
keeps multiplayer players and arrows aligned with smooth framing without a
dependency on Smooth Camera's private fields.

Actors registered through `FrameComposition.RegisterWorldDraw` instead draw in
each visible `WorldPass`, using its explicit canonical translation. This keeps
their sprites below foreground and independent of the viewport overlay path.

## Explicit geometry profiles

```csharp
context.Track(RuntimeApi.Geometry.Register(
    context.ModuleId, "example.actor-contour", 1, GetPolygon));
// bool GetPolygon(IBlock block, out Vector2[] worldVertices)
```

A query names the exact profile and version. No provider means `false`, not a
silent switch to some other geometry. Two matching providers claiming the same
block are an error. Returned polygons are copied and checked for finite values.
Provider registration cannot change during a query. Throwing providers are not
silently ignored: using another shape could produce a wrong physical result.

Ball King publishes `ball-king.contour`, version 1. Its corrected bottom-left
face stays inside that contour, and existing `BallKingGeometryApi.Register`
providers retain their existing precedence. Nothing writes corrected vertices
back into native blocks. Query this profile only for blocks admitted by the ball
controller; a returned polygon is not an assertion that a material is solid.

Registry `Generation` tracks registrations and explicit `Invalidate()` calls,
not every change to a live block or third-party provider. Runtime caches no
polygons. Consumers that add caches must also account for dynamic world state,
profile/version, actor dimensions and provider-specific changes. The Ball King
bridge evaluates its third-party providers afresh on each query.

## Mechanic inventory

`RuntimeApi.Mechanics.Register(owner, id, version, effects, readState)` records an
owned mechanic. IDs are globally unique; replacement by registration order is
not allowed. `Inspect()` calls state providers only on demand and returns detached
descriptors. A failed provider produces an `Error` on its descriptor without
discarding unrelated results or inventing an inactive state.

`MechanicState` separates enabled, available and active. Its source and reason
explain the observation. For Ball King's form entry, enabled means the controller
is installed; active means the player is currently morphed, not merely that a
checkbox is selected. Runtime's diagnostic report includes this inventory.

The 1.4 `MechanicDefinition` overload adds Independent, Additive and Exclusive
composition, an effect channel, explicit incompatible IDs and optional state
participant/simulation requirement links. `Inspect()` reports conflicts between
active entries without choosing a winner or changing their state. Additive means
the owner declares combination meaningful; it is not a proof of physical parity.
Links identify separately registered contracts; they do not register providers.

Ball King, Casual, SFC, Jetpack, Rewinder, Warp, No Walk Off, Replays and Mapping
produce inventory entries. Existing controller and permission code still
makes gameplay decisions. Exclusive live roles use `GameFeatures` and `JumpSlot`;
the inventory cannot grant simulation coverage or change modified-run history.

`BlockCatalog` embeds the shared reviewed JSON catalogue, queried lazily without
executing foreign factories. `Register` reserves exact colours and records mechanic
identity, Solid/Zone/Screen scope and solidity separately. Conflicts throw; no
colour is silently remapped. Ball King's restricted-screen pixels and Mega's
Screen pixels are nonblocking metadata; Sticky and Solid materials remain terrain.
A common screen marker and XML Screens declaration are alternative encodings of
the same On/Off/Local permission; neither requires the other. Combined declarations
must agree, and neither adds the separate map authority tag. The optional catalogue
owner associates an existing Workshop owner with its module ID, not a security
permission. The catalogue is dated evidence, not knowledge of all future mods.
Mega retains its additional installed-factory collision check.

A catalogue record is metadata, not a constructed block, loaded mod state or
activation recipe. Runtime does not infer an arbitrary foreign constructor's
requirements, recreate its world resources, or turn a discovered state on
permanently. Consumers implementing a browser must distinguish known metadata,
currently observed live objects and explicitly supported construction/application.
Unknown availability must remain visible; discovery alone is not permission to
invoke every foreign factory. Build costly catalogues during preparation when
required by saved configuration, or on an explicit browsing action.

## Presentation while physics is suspended

`PresentationActivity.Begin(owner, body)` marks an actor as continuing a live
presentation transition. Its disposable lease is actor-local and may overlap
other owners. Releasing one lease cannot remove another. The API does not change
`Enabled`, advance a clock, pause input, hide a sprite or transfer control.

Warp acquires the lease when freezing its body and releases it on completion,
cancellation or restore handoff. Replays records those ticks despite the disabled
body. Actual game pause still stops the native update loop. This fixes shortened
recordings and shifted later poses; it does not add an RGB-particle track to the
replay format. Unknown disabled bodies without this declaration remain excluded.

## Regression evidence

`GeometryMechanicTests` covers profile/version isolation, conflicting providers,
copied arrays, actual native slope intersections, preservation of modified lines,
on-demand inventory reads, provider failure isolation and cleanup retry.
Ball King additionally checks its corrected contour against unchanged native
bottom slopes. Mega's controller fixture checks presentation ownership through
normal completion, cancellation and mid-transition restore. Replays checks that
the corresponding disabled-body ticks remain recordable.

These automated fixtures are not a substitute for combined in-game visual tests.

## Declared material support

Runtime 1.32 exposes `RuntimeApi.Materials` for exact-type speed/support
capabilities with links to geometry profiles and state participants. Pure support
queries are distinct from witnessed contact evidence and simulation coverage.
See [material and movement interop](interop.md) for the complete contract.

## Preparing explicit terrain edits

API 1.38 adds `RuntimeApi.Geometry.RegisterTerrainPreparation(owner, prepare)`.
Register it in `OnWorldReady` and own its disposable registration in that world
scope. It exposes preparation only, not active player behavior or a capability.
After finalizing saved geometry and before an explicit edit commits collision
arrays, Mod Inspector calls each preparer on the game thread
with a private copy of the distinct block types. A provider can prepare its
motion hooks or other module-owned resources before those types become live.

Callbacks must be idempotent, retain resource ownership in their module scope
and avoid world mutation. Throw to refuse the edit before geometry is committed;
already prepared module resources remain owned until scope cleanup. Callbacks
can't register/unregister geometry or recursively prepare another edit. Disposal
removes the callback. This doesn't replace map-load preparation and isn't a
notification for arbitrary foreign writes to native collision arrays.
