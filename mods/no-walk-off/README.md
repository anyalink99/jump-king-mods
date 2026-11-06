# No Walk Off

Stop at the last pixel of a platform while holding a direction. Release and press
again to walk off. You can jump and turn back normally.

## Install

Version **1.0.0** requires [JK Runtime 2.4.2+](../jk-runtime/README.md) and a loaded
Harmony 2 engine, such as the copy supplied by Subframe Charge or Mapping.
Close the game before installing `NoWalkOff.dll` in `Jump King/Content/JKMods/`.
Subscribe to [No Walk Off on Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3797217682).
The existing item updates automatically.

Open **Mods > No Walk Off**. Protection starts **on** for a fresh installation.
Maps can control it through their terrain, zones and screen rules.

Wind, ice, Snake Ring inertia and unsupported motion release protection. It does
not catch airborne falls or stop slope sliding. Successful global edge stops mark
the run modified unless the map has `AllowNoWalkOff`; authored activation does not.

Warp Jump and Air Dash belong to the separate [Powers](../powers/README.md) mod.
Neither mod requires the other. Old MGE settings are not imported.

See the [guide index](docs/index.md) for movement rules, map authoring and tests.
Build with `scripts/check-mods.ps1 -Mod no-walk-off`.
Package: `build/no-walk-off/UPLOAD_TO_WORKSHOP/NoWalkOff.dll`.
