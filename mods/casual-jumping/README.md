# Casual Jumping

Version **2.2.0** requires [JK Runtime 1.34 or newer 1.x](../jk-runtime/README.md).

Casual Jumping adds air control and a press-to-jump mode without changing the
game's maximum jump. It works in the base game, DLC and Workshop maps.

[Technical guides](docs/index.md) · [Map setup](docs/map-authoring.md)

## Control modes

- `Vanilla` restores the original Jump King controls.
- `Casual` keeps the original charge-and-release jump and adds directional
  control in the air.
- `Casual+` starts a jump when the button is pressed, supports variable jump
  height, adds directional control in the air and accepts a held jump during
  the intro.

Casual and Casual+ keep the original maximum height and horizontal range. Wind
still works. On slopes, uphill input is ignored while the King slides, but
downhill input still works. Holding toward a wall gives one normal bounce, then
a wall slide until the wall ends or the player moves away.

Casual+ also buffers a held jump until landing, preserves upward momentum after
an early release, allows a new jump to cancel the fallen pose and keeps a
six-frame jump grace period after walking off a platform.

## Settings

Choose a mode from the main or pause menu:

```text
Controls: Vanilla
Controls: Casual
Controls: Casual+
```

The default mode is `Casual+`. Assisted controls mark the run as modified and
disable achievements. Selecting `Vanilla` removes the custom player behaviour.

Level authors can add `AllowCasualJumping` to permit Casual and Casual+ without
marking the run as modified.

Jetpack now comes with More Items. Ball King and Jetpack are optional, but JK
Runtime is required. Their integration uses Runtime's form and thrust roles, so
it does not depend on DLL load order or private reflection.

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

## Map-controlled activation

Add this tag to `Tags` in `level_settings.xml`:

- `JKRuntime.MapControlled:casual.controls`

| Mechanic | ID | On RGB | Off RGB | Local RGB |
| --- | --- | --- | --- | --- |
| Casual controls | `casual.controls` | `(173,104,211)` | `(173,105,211)` | `(173,106,211)` |

These colours are opaque, nonblocking metadata. On enables the mechanic, Off
blocks it, and Local allows only local triggers or XML zones. Once the map has
the control tag, unmarked screens are off and players cannot enable the mechanic
manually. Entering the map also saves the global setting as Off. It stays off
after leaving until the player turns it back on.

**Screen markers and XML Screens declarations are alternatives for the same
On/Off/Local rule: use either one, without duplicating it in the other.** XML also
supports ranges, zones and supported parameters. If both are used for the same
screen/mechanic, they must agree; XML does not override a marker. The map tag
remains separate and is not added automatically by either method.

Put optional screen and zone rules in `jk-runtime/mechanics.xml` at the map root.
Screen numbers start at 1. Zone coordinates use 480 x 360 screen-local pixels.
If a screen has both a pixel and an XML rule, they must say the same thing.

The [map authoring guide](docs/map-authoring.md) has the full XML and all Casual
Jumping-specific rules.
