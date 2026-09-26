# Changelog

## 2.0.0 — JK Runtime migration (unpublished)

- Hard dependency on JK Runtime 1.x, using its common generated package SDK.
- Remove old first-party reflection/optional-runtime compatibility paths.
- Use shared lifecycle, settings and gameplay/state contracts; preserve feature behavior.
- Install only as part of the coordinated runtime release; manual combined acceptance is pending.

## 1.3.3

- Resolve assignable JumpState subclasses, including Subframe Charge's passive
  and enabled wrappers, instead of native exact-type FindNode. Resolve afresh
  for jump effects after other mods replace/restore the tree.
- Keep Ball King's own jump controls available even when a custom controller
  removes JumpState entirely; omit only the unavailable optional particles.
- Verify native/replacement/restored lookup, including the actual built SFC
  assembly when available. Ball jumping continues to use its own physics.

## 1.3.2

- Build Ball King contours only from blocks whose own `IBlock.Intersects`
  contract reports `Collision_Blocking`. Non-solid teleport, checkpoint and
  other third-party trigger blocks no longer become artificial surfaces or
  prevent unmorphing.
- Preserve rolling momentum across native screen teleports and checkpoint
  teleports in both regular and Sticky modes, including an immediate landing
  on the destination surface.
- Make regular-ball slopes roll-only surfaces: neither an assisted surface
  jump nor an automatic fall bounce can launch from a slope. Sticky retains
  its intentional jump from any attached surface.
- Separate the native AABB from its contact before clearing a Sticky surface
  for takeoff. Mirrored slope jumps can no longer diverge when Jump King's
  raster collider (including its `BottomRight` triangle bug) considers one
  logical contour pose overlapping and the other one free.
- Exclude the artificial horizontal cap created when an AABB is expanded
  around a slope vertex from regular rolling ground. The same span remains
  valid when an adjacent solid block contributes a real flat top.
- Resolve a controlled flat-to-slope climb at the shared contour vertex as a
  blocked ground movement. It no longer detaches and collides again each frame,
  so one-block slopes remain stable while uphill input stays ineffective.
- When uphill input is held on a regular slope, discard any uphill component
  previously created by control before applying slope gravity. Released
  physical momentum still coasts naturally; held input can never sustain a
  climb.
- Apply the same uphill-input rule while ordinary-ball movement is temporarily
  owned by Jump King's native collision pass. Native X resolution can no
  longer project held horizontal air control into an uphill slope step at the
  unsupported low edge; falling velocity still projects downhill normally.

## 1.3.1

- Acquire new sticky surfaces from the first analytic contour crossing in
  Jump King's native X/Y movement order. Attachment no longer depends on
  post-collision integer rounding, and `BottomLeft`'s declared southwest face
  works despite the installed game's contradictory native triangle.
- Remove airborne proximity acquisition and the reattachment timer. A surface
  now has one entry path (a directed crossing) and remains authoritative once
  acquired; movement away from a face cannot immediately reacquire it.
- Keep the exact rolling contour segment between frames and hand only the
  unconsumed part of a ledge step back to native airborne physics.
- Preserve Jump King's own `CapPositionBehaviour` correction while a sticky
  contact pose is mirrored through the native collision pipeline. Teleport
  handling remains authoritative and no custom map-edge wall is introduced.
- Make every assisted jump atomically clear Jump King's grounded and knocked
  flags. A buffered landing jump can no longer play its effects and then be
  cancelled by the previous landing state or a queued floor bounce.
- Give regular-ball slopes one explicit input rule: gravity always applies,
  downhill input adds drive, uphill input adds none, and existing momentum is
  preserved in either direction.
- Normalize a native slope landing directly onto its exact configuration-space
  contour. The ball no longer drops through a transient airborne frame where
  air control could drive it uphill before the slope was reacquired.
- Treat a buffered landing jump as one ordered landing-and-jump event: play the
  native landing sound exactly once, then apply the assisted takeoff.
- Keep continuous Sticky contour transitions into a horizontal platform as
  complete native contacts, including registered landing particles, while
  suppressing only the false new-landing sound caused by the normal change.
- Validate even a zero-speed attached ball against the current contour. Save
  loads, external teleports and other position changes can no longer preserve
  a distant surface until the first movement input; native gravity resumes as
  soon as the old contact is absent.
- Preserve the last valid surface normal, tangent and rolling speed throughout
  the coyote window. A late jump can no longer normalize a cleared normal and
  send a `NaN` velocity to Jump King's camera.
- Make sticky sub-step movement atomic: an unsuccessful corner transition now
  restores the frame's starting position before native airborne motion takes
  over, preventing the same distance from being applied twice.
- Select top-surface continuation by the smallest correction from the expected
  trajectory rather than by the highest nearby support. Flat/slope seams no
  longer attract the ball upward, and an idle ball revalidates support at a
  slope endpoint.
- Replace per-frame directional contact probes with one configuration-space
  contour. Floors, walls, slopes, ordinary steps and connected block groups
  now move through the same directed-segment and intersection solver.
- Keep `BottomLeft`'s playable southwest face in that contour while isolating
  Jump King's contradictory native collision triangle in a temporary native
  contact pose. The native adapter cannot alter gameplay movement or permit a
  transition through another solid.
- Preserve one complete grounded player-state tick before applying a queued
  floor bounce on the following physics frame. Bounce landings now play the
  material landing sound, spawn native particles and count as real landings to
  disappearing and other stateful blocks.

## 1.3.0

- Replaced restricted jump-speed scaling with coherent shorter profiles: 75%
  height normally, or 35% for both jumps while Double jump mode is active.
- Split screen restrictions into configurable Double jump, disabled Double
  jump, and forced Double jump collision colours.
- Affected pause-menu settings now display `Restricted`.
- Removed the one-frame visual pull toward a platform corner when a regular
  ball rolls off a flat ledge.
- Replaced separate flat/slope handoffs with one continuous top-surface path,
  preserving momentum without hovering, snapping or duplicate landing sounds.
- Prevented directional input from driving a regular ball uphill while still
  allowing existing momentum to carry it up a slope.
- Grounded wall bounces now damp quickly while movement is held into the wall.
- Removed splat while morphed. Regular long falls bounce once and falls from
  the vanilla splat height run a two-bounce sequence.

## 1.2.0

- Added solid custom-map pixels for screen-wide reduced jump strength and
  forced sticky surfaces.
- Restricted screens use 75% surface/coyote jump strength, 35% double-jump
  strength and disable the global Sticky option.
- Sticky-surface pixels use Jump King's native neighbour topology to form
  ordinary boxes or slopes from one material colour.

## 1.1.2

- Added a six-frame coyote-time window after rolling off a surface.
- Kept the aerial jump available after a coyote jump when double jump is enabled.

## 1.1.1

- Limited custom wall restitution to the active ball hitbox.
- Made controller ordering deterministic with Jetpack and Casual Jumping.
- Kept Ball King visual ownership stable when Jetpack is enabled.

## 1.1.0

- Renamed the mod to Ball King.
- Added paired forward and reversed 8-bit transformation sounds.
- Renamed the menu options to `Sticky` and `Double jump`.
- Added the `AllowBallKing` level tag.
- Disabled Casual Jumping controls while the king is a ball.
- Reworked sticky surface following and save restoration.

## 1.0.0

- Added animated curling and unfolding.
- Added rolling across floors, slopes, walls and ceilings.
- Added surface jumps and airborne detachment.
- Added runtime composition of active clothing and reskin layers.
- Added a configurable transformation binding.
- Added airborne Jetpack compatibility.
