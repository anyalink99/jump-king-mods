# Mega Gameplay Expansion

Warp to the landing point of a jump, stop at walking edges or dash in midair.
Each mechanic works globally or through map-authored terrain, zones and screens.

## Install and enable

Published version **0.15.2** needs [JK Runtime 1.44+](../jk-runtime/README.md),
including Runtime 2.0, and one loaded Harmony 2 engine such as the copy supplied
by Subframe Charge or Mapping. Packages rebuilt with the current SDK need Runtime 2.0+.
For a manual install, close the game and put `MegaGameplayExpansion.dll` in
`Jump King/Content/JKMods/`, with Runtime installed separately.

Open **Mods > Mega Gameplay Expansion**. All three global switches start **off**.
Enable the mechanic you want, then return to play. Maps can enable mechanics in
their own areas even when the global switch is off. Successful global use marks
the run modified unless the map permits it with `AllowMegaGameplayExpansion`.

## Warp Jump

Take off or walk from an active surface to move to the forecast landing point
through a disassembly/assembly effect. It uses the resulting native jump velocity,
including Subframe Charge correction. Hold a fresh Jump during the transition to
buffer the next jump; releasing before assembly cancels it.

Unsupported trajectories keep ordinary movement. The forecast supports known
native geometry and side links, but doesn't advance world timers, moving terrain
or arbitrary foreign hooks. Read [Warp Jump rules](docs/playing.md#warp-jump)
and [prediction ownership](docs/implementation.md).

## No Walk Off

Hold a direction to stop at a supported walking edge. Release and press again
to walk off; jumping remains free. Wind, ice/Snake Ring inertia and unsupported
motion release the guard. It doesn't catch airborne falls or stop slope sliding.
Read [No Walk Off rules](docs/playing.md#no-walk-off) and
[movement detection](docs/no-walk-off.md).

## Air Dash

Press Jump again in flight to dash. A held direction chooses left or right;
otherwise motion or facing chooses it. You get one dash per flight, refreshed
on landing. **Binds** edits a separate button or chord; Default follows Jump again.
The dash covers 80 pixels before ordinary momentum resumes. Terrain can stop it.
Read [Air Dash controls and rules](docs/playing.md#air-dash) and
[implementation](docs/air-dash.md).

## Map-controlled activation

Each mechanic has Solid, Zone and Screen variants. Use exact opaque colours and
Runtime activation rules; the common control tag can disable global enabling for
the map. A controlled map saves the global switch as Off on entry and leaves it
off afterward. The [map authoring guide](docs/map-authoring.md) has the palettes,
tags and complete XML. [Player rules](docs/playing.md) explain entry and exit behavior.

## Mod Inspector moved to Runtime

Block browsing and the former Gimmick Library are now under **JK Runtime >
ModsDebugActions > Mod Inspector**. Runtime owns observation, inspection and
experimental overrides. See [Mod Inspector](../jk-runtime/docs/mod-inspector.md).

## Development

Run `scripts/check-mods.ps1 -Mod mega-gameplay-expansion` from the repository root.
The package is `build/mega-gameplay-expansion/UPLOAD_TO_WORKSHOP/`.
Read [development and validation](docs/development.md) for test tiers, logs and
native fixtures. The [guide index](docs/index.md) links the full references.
