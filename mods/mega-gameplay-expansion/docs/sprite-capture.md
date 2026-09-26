# Custom sprite capture

Warp and Air Dash share the same appearance snapshot. Custom sprite subclasses can be captured
without rejecting an otherwise supported Warp flight.

Plain native sprites retain their texture/source/tint path and native layered
sprites are collected recursively. Other subclasses are drawn through virtual
`Sprite.Draw` into a bounded transparent target, with the actual facing. The
original screen anchor is retained and only the batch transform translates it
to the capture tile, preserving world-coordinate shaders such as Cosmic.

The capture path translates the capture by whole pixels and retains an exact integer
crop offset. The original fractional anchor still reaches virtual `Draw`.
Integer crop offsets keep fractional anchors in the same rasterization phase
and avoid a second rounding through normalized pivots. Warp and Dash share this
placement rule.

Readback is cropped to visible texels. Colour and alpha are already baked;
rendering the resulting CPU pixels does not apply tint or facing twice. Captures
are action-time snapshots, not per-frame readbacks or permanent texture caches.
The shader remains frozen during the short effect; the original sprite is
restored afterward. Arrival is captured at the predicted destination, using
the native standing or splat pose.

The render target and private SpriteBatch are disposed before returning. Only
CPU pixels and the existing device-owned white texture survive in Warp plans,
including retained rewind snapshots. The caller's batch, targets, viewport,
scissor, blend/depth/raster states, pixel textures/samplers and vertex/index
bindings are restored in a finally block, including when virtual Draw throws.
An already active batch or discard-content target is left untouched.

Capture is limited to a 128x128 canvas, 32 layers and eight composition levels.
Unsupported capture context, oversized drawings and renderer exceptions fall
back to usable texture fields; missing/invalid texture data uses a simple
18x26 silhouette. Transparent successful drawings stay transparent. A diagnostic
is emitted once per failed sprite type. This visual fallback does not relax
Warp's checks on unknown terrain or alternative movement controllers.

Integration tests compare direct GPU rendering against reconstructed captures
for native sprites and a procedural subclass with asymmetric and translucent details, both facings,
and the CosmicSprite implementation extracted from the installed Wardrobe+
package. They check GPU-state restoration, caller-target preservation, exception
and active-batch fallbacks, and the complete native Warp lifecycle with custom
sprites: transfer, assembly, controls, landing state/sound, snapshots and cancel.
No production code depends on the Wardrobe+ type name or package.

The alignment regression covers independent fractional X/Y coordinates, different
departure and arrival anchors, and prepared particle paths. Endpoints at `(180.5, 200.5)` must match direct GPU rendering pixel for pixel.
The native update-order regression also covers quarter-pixel body positions on
solid, ice and snow, checking the real position and velocity after assembly.

Run `scripts/check-mods.ps1 -Mod mega-gameplay-expansion -Integration` for native
Warp lifecycle, sprite alignment and installed Cosmic GPU reconstruction checks.
Captures and logs stay under `build/mega-gameplay-expansion/`. Test the result in
game with the intended outfit, fractional landing position and active mod set.
