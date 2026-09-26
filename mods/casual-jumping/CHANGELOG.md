# Changelog

## 2.0.0 — JK Runtime migration (unpublished)

- Hard dependency on JK Runtime 1.x, using its common generated package SDK.
- Remove old first-party reflection/optional-runtime compatibility paths.
- Use shared lifecycle, settings and gameplay/state contracts; preserve feature behavior.
- Install only as part of the coordinated runtime release; manual combined acceptance is pending.

## 1.3.4

- Integrate directly with the renamed Subframe Charge API.

## 1.3.3

- Coordinate jump-node replacement with Subframe Charge when changing
  Vanilla, Casual and Casual+ at runtime.
- Make the integration independent of DLL load order and keep Subframe Charge
  optional.

## 1.3.2

- Added a six-frame Casual+ jump grace period after leaving a platform.

## 1.3.1

- Made Jetpack and Ball King detection independent of DLL load order.
- Fixed controller ordering when the three gameplay mods are combined.

## 1.3.0

- Moved Jetpack into an independent compatible mod.
- Returned Casual Jumping to the Vanilla, Casual and Casual+ control settings.

## 1.2.0

- Made Jetpack mode register area entry as soon as the king appears on its
  screen, without waiting for a landing.
- Kept Jetpack flight sprites synchronized with vertical direction and removed
  the persistent bounce sprite from Jetpack mode.
- Added an optional world-space jetpack fire trail with inherited momentum,
  wind response, indexed level collision and a stepped flame-to-ember colour
  ramp.

## 1.1.0

- Added Jetpack as a fourth control selection alongside Vanilla, Casual and
  Casual+.
- Added smooth, momentum-preserving mid-air thrust with a limited top speed.
- Added a pose-aware jetpack sprite with animated ignition, sustained flame and
  shutdown frames behind the king.
- Added an embedded looping 8-bit jetpack sound.
- Added jetpack volume and visibility settings to the main and pause menus.
- Added the independent `AllowJetpack` level tag.
- Added a browser-based pose editor for maintaining jetpack placement.
- Packaged pose data, graphics and audio inside `CasualJumping.dll`.

## 1.0.1

- Fixed Casual+ jumping from quicksand.
- Kept held landing jumps buffered until landing.
- Preserved vanilla tailwind and headwind over Casual air control.
- Kept Casual+ jump height active through slope contacts.
- Added the vanilla pre-game held-jump buffer to Casual+.
- Added the `AllowCasualJumping` level tag for approved maps.

## 1.0.0

- Added Vanilla, Casual and Casual+ control modes.
- Added variable jump height and air steering.
- Added a held landing jump buffer.
- Added slope-safe directional control.
- Added single-bounce wall sliding.
- Preserved the original maximum jump height and horizontal range.
