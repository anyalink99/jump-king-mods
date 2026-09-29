# Stereo Madness

Stereo Madness is the controller bundled with its matching map. JK Runtime loads
the `.jkmod` package from the map's `jk-runtime/modules` folder, so players do
not need a second mod subscription. It requires JK Runtime 1.35 and Mega
Mapping Expansion 0.6.2. Subframe Charge is optional.

## What it controls

The controller activates only when the map contains
`props/stereo-madness/course.xml`. It turns the current outfit into a pixel cube
or ship. Cube takeoffs still fire the native jump event, each death fires the
native fall event once, and ship thrust does not add jumps. At the finish, native
physics lands the King on the ending platform. Jump King still owns victory,
saving, achievements and the results screen.

Runtime queues the handoff outside player component iteration, which lets SFC
reinstall its charge observers safely. Teardown cancels queued work, and stale
work is ignored after the player is replaced. MME supplies the intro, short
custom ending and extra statistics page. That page copies deaths and jumps before
the native victory flow resets them.

## Input and presentation

Subframe Inputs works through the native pad snapshot, including completed taps,
controller bindings and alternate bindings. No second consumer drains SFC input.
Space/Up/left mouse remain convenience shortcuts sampled at native cadence;
configure them as native alternate jump bindings for SFC buffering. Shortcuts
respect focus, pause and text capture. High refresh follows SFC's saved 240 Hz
option. Detached collision-aware prediction draws cube, camera and course between
updates, with no native position, timer, audio, event or statistics mutations.
Prediction stops at death, completion, portals and pause. Fixed gameplay remains
60 Hz with four reference physics substeps per update.

## Build and local install

Build the controller and its dependencies with
`powershell -File scripts/check-mods.ps1 -Mod stereo-madness`.
The isolated output is `build/stereo-madness-mod/UPLOAD_TO_WORKSHOP` for diagnostics;
it isn't a standalone Workshop mod. The matching map build embeds its SDK shell
as StereoMadness.jkmod alongside MegaMappingApi.dll and the shared Harmony engine.
Map sources, original art and music are distributed separately from this mod
source repository. No implementation sources come from build outputs.

The installer updates an existing Workshop map first. Without one, it copies the
bundle into Content/JKMods/StereoMadnessMap for local testing. Runtime discovers
the embedded map package in either location. An obsolete local duplicate is
moved into rollback backups when migrating to the Workshop installation.
Launch that directory through Jump King's native -debug option.
The package never registers local levels in WorkshopManager, changes map menus,
or invents Workshop IDs. A published Workshop map uses native subscription
registration. Publishing is a separate operation.

## Saves and snapshots

Body position and velocity use level coordinates, independent of the
scrolling camera. Native position-only saves restore cube/ship mode from the
course portals, following the position tool's zero-velocity semantics. Runtime
snapshots additionally keep precise velocity, rotation, portal history,
camera and death state. The installed JumpKingSaveStates input behaviour runs
once per native update while ordinary body physics is suspended. Music seeks
to the restored course time. These hooks are scoped to the active course.

## Validation limits

Installation verifies every copied file, keeps prior installations in rollback
backups and migrates the old debug junction without deleting its target. There is
one active controller copy, bundled with the map. Close Jump King before updating.
Dependencies keep their existing Workshop installations and settings.

The checks cover different layers:

- `build.ps1`: controller model and SDK package.
- `verify-package.ps1`: Worldsmith classification and embedded-package lifetime.
- `verify-native.ps1`: native GPU, input, statistics, ownership and lifecycle.
- `verify-reference.ps1`: preserved Geometry Dash traces.

The native and package checks require the matching compiled map at
`build/stereo-madness/UPLOAD_TO_WORKSHOP`; package validation also requires the
map toolchain's `scripts/check-worldsmith-package.ps1`. Reference checks require
the original browser Player.js supplied via `-ReferencePlayer`.
A complete human route has not been verified.
See the map's THIRD_PARTY_NOTICES.md for original assets and music credits.
