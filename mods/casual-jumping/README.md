# Casual Jumping

Requires [JK Runtime 1.30 or newer 1.x](../jk-runtime/README.md).

Casual Jumping adds three optional control schemes to Jump King. It works with
the base game, DLC and Workshop maps and does not depend on a custom level.

## Control modes

- `Vanilla` restores the original Jump King controls.
- `Casual` keeps the original charge-and-release jump and adds directional
  control in the air.
- `Casual+` starts a jump when the button is pressed, supports variable jump
  height, adds directional control in the air and accepts a held jump during
  the intro.

Casual and Casual+ keep the game's original maximum jump height and horizontal
range. All assisted modes preserve wind and follow the native collision
response on slopes: uphill air input is ignored while sliding, while downhill
input remains available. Holding into a wall gives one normal bounce and then
settles into a wall slide until the wall ends or the player moves away.

Casual+ also buffers a held jump until landing, preserves upward momentum after
an early release, allows a new jump to cancel the fallen pose and keeps a
six-frame jump grace period after walking off a platform.

## Settings

The current mode can be changed from either the main menu or the pause menu:

```text
Controls: Vanilla
Controls: Casual
Controls: Casual+
```

The default mode is `Casual+`. Assisted controls mark the run as modified and
disable achievements. Selecting `Vanilla` removes the custom player behaviour.

Level authors can add `AllowCasualJumping` to permit Casual and Casual+ without
marking the run as modified.

Jetpack is part of More Items. Ball King and Jetpack integration use runtime
form/thrust roles and ordered body phases, not optional assembly reflection.
Those feature mods remain optional; JK Runtime itself is mandatory.

Subframe Charge remains active in `Vanilla` and `Casual`. In `Casual+`,
Casual Jumping owns the complete jump implementation and Subframe Charge
automatically yields its native jump-node replacement. The integration is
optional and independent of DLL load order.

## Installation

Place `CasualJumping.dll` in:

```text
Jump King/Content/JKMods/
```

Restart the game after installing or updating the mod.

## Building

Run either script from this directory:

```powershell
.\build.ps1
.\install.ps1
```

The release DLL is written to:

```text
build/casual-jumping/UPLOAD_TO_WORKSHOP/CasualJumping.dll
```

The project targets .NET Framework 4.5 and the Jump King Workshop API.
JK Runtime is its only required mod dependency.
