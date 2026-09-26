# Geometry, mechanic inventory and presentation ticks

These are game-thread APIs. Track every
registration with the owning level's `ModuleContext`; a registry entry does not
automatically know when your player or level has gone away.

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
colour is silently remapped. Ball King's solid screen pixels and Mega's non-solid
screen metadata therefore retain different meanings. The optional catalogue
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
