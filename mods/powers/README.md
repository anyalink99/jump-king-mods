# Powers

Warp to the landing point of your jump or dash through the air. Each power works
globally or through map-authored terrain, zones and screens.

## Install and enable

Version **0.1.0** requires [JK Runtime 2.4.2+](../jk-runtime/README.md) and a loaded
Harmony 2 engine, such as the copy supplied by Subframe Charge or Mapping.
Close the game and put `Powers.dll` in `Jump King/Content/JKMods/`, with Runtime
installed separately, or subscribe to [Powers on Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3828665574).

Open **Mods > Powers**. Both global switches start **off**. **Warp Jump** uses
the native departure velocity to forecast the first landing and move there with
a dissolve effect. Unsupported trajectories retain ordinary movement.

**Air Dash** uses a fresh Jump press in flight, or a custom button/chord set in
**Binds**. Hold a direction to choose the dash direction. One dash travels up to
80 pixels, refreshes on landing and can be stopped by terrain.

Successful global use marks the run modified unless the map permits it with
`AllowPowers`. Maps can enable powers locally even with their global switches off.
Read the [guide index](docs/index.md) for detailed rules and map authoring.

Edge protection belongs to the separate [No Walk Off](../no-walk-off/README.md)
mod. Neither mod requires the other. Old MGE settings are not imported.

## Development

Run `scripts/check-mods.ps1 -Mod powers` from the repository root. This also builds
No Walk Off for the combined regression test; the release DLL has no dependency
on it. Package: `build/powers/UPLOAD_TO_WORKSHOP/Powers.dll`.
