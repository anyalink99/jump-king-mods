# Map physics behavior and compatibility

[Handbook](../README.md) / [Reference](README.md)

Use [Choose map physics](../maps/physics.md) for setup. `Physics` is an optional
top-level section of `MappingSettings`, independent of the collision source.

## Configuration

`version="1"` is required. `profile` must be `native`, `reference` or `symmetric`.
Unknown attributes, duplicate sections and conflicting legacy declarations fail
validation. Only `reference` accepts children: one or more
`Frame screen="…" referenceScreen="…"` entries. Both addresses start at 1;
`screen` must exist, and `referenceScreen` is limited to 1–4096.

No `Physics` section means native behavior, except for explicitly authored legacy
`MapLayout/PhysicsFrame` entries. Those retain their existing implementation,
snapshot ID and saved-state version. The `reference` profile uses that same
adapter. See [reference arithmetic](physics-frames.md).

Symmetric rules apply to the whole world and remain active while decorative MME
presentation is disabled. Hot scene reload refuses changes to `Physics` or
`MapLayout`; restart the map to apply them.

## Symmetric v1 arithmetic

The player has signed 64-bit X/Y accumulators with 24 fractional bits. Each
native movement increment is rounded to the nearest 1/16777216 pixel, with ties
to even, then added as an integer. Coordinates are limited to ±4,000,000 pixels.
This covers all supported map screen addresses. The native `float` position is
a published view, not the source of the next addition.

Native X/Y movement and collision stages keep their original order. Native
gravity, velocity modifiers, bounce factors and jump charging remain in place.
For native rectangular blocks, collision uses the precise rectangle interior;
touching an edge without penetrating it isn't an overlap. During collision
backout, positive movement starts from the lower integer and negative movement
from the upper integer. The original one-pixel backout loop then runs normally.

`BodyComp.GetHitbox()` remains an integer rectangle for API compatibility. Its X
uses nearest-even rounding and Y uses floor. Native rectangular collision queries
for that current rectangle recover the precise body bounds. Other rectangle
queries retain their supplied integer bounds. Foreign consumers that infer exact
position from an integer hitbox must account for this distinction.

An external position write resets the changed axis from its new value. The other
axis keeps its fraction. Native side links still choose and commit their normal
destinations; the coordinate adapter restores the fraction discarded by native
relocation. MME moving-platform carry adds its integer displacement to the exact
state and restores it if the platform move is rolled back.

## Slopes and foreign behavior

MME does not patch `SlopeBlock.Intersects`, slope vertices, normals or the native
choice between touching slopes for this profile. Existing slope bugs and fixes
remain with their current owners. Slope mods still receive integer rectangle
queries through the game's API. The coordinate profile alone cannot guarantee
mirrored slope jumps, particularly on integer rounding boundaries.

Only game-owned rectangular block types use the precise rectangular predicate.
Foreign block implementations keep their callbacks, as do controllers which
replace native movement. Their arithmetic and contact policies require separate
compatibility checks. The inspector states this scope; it doesn't certify an
arbitrary installed patch graph. Runtime simulation providers and replay consumers
must capture the physics state as well as the published body position.

Subframe Charge's **Inputs + Physics** mode writes a corrected float position after
the native movement stage. That write is accepted as an external position update;
this profile does not make that controller's arithmetic exact or certify its
mirrored trajectories. **Charge** and **All Inputs** do not use this stepping path.

## State and lifecycle

World preparation owns the selected profile and disk-save context. Activation
attaches it to the player and registers state under
`mega.mapping.physics.symmetric.v1`, version 1. Teardown detaches the player;
returning to a native map leaves the installed hooks dormant.

Practice and shared-world snapshots carry both accumulators and their published
positions. Disk persistence captures an immutable pair with the native player
save on the game thread. The native save worker selects the matching pair for
the value it actually serializes, then writes the exact record. Publication
doesn't hold its lock during disk I/O. A bounded 256-entry queue retains recent
native save packets; no game files are copied.

Exact records use the existing MME save epoch and a separate map identity in
`MegaMapping/Construction`. A native reset invalidates old records. Restore
requires the matching native position, velocity, timestamp, direction, grounded
flag and player state. Missing or mismatched records initialize from native
coordinates. As with other auxiliary saves, an interrupted write can leave no
matching record; it must never apply a fraction from another native state.

## Verification scope

Focused tests compare mirrored rectangular trajectories across all 36 standard
charges, four subpixel starts and three body widths. They compare exact positions,
velocities and landing/knock flags through native X/Y stages, and compare room
copies at different world heights. Separate processes compare native trajectories
with dormant hooks against an untouched game process. Slope query tests verify
unchanged results, rather than asserting that slopes have been repaired.

These checks establish the listed behavior. They don't certify every custom
controller, slope mod or route through a map.
