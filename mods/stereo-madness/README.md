# Stereo Madness

Native controller bundled under the matching map's `jk-runtime/modules` folder.
JK Runtime loads its `.jkmod` package before map preparation; players do not need
a separate Stereo Madness mod subscription. Requires JK Runtime 1.30.4 and Mega
Mapping Expansion 0.6.2. Subframe Charge is optional.

The controller activates only on maps containing props/stereo-madness/course.xml.
The current outfit becomes a pixel cube/ship. Cube takeoffs call the native jump
event; deaths call the native fall event once. Ship thrust adds no jumps. At the
finish native physics lands the king on the ending platform; Jump King owns the
victory, save, achievement and results flow. The handoff runs through Runtime's
command queue, outside player component iteration, so SFC can safely reinstall
its charge observers. A queued handoff is ignored after disposal/player replacement
and cancelled by level teardown. MME supplies authored intro pages,
a short custom ending and an additional statistics page. Deaths and jumps on
that page are copied from the native run totals before victory resets them.

Subframe Inputs works through the native pad snapshot, including completed taps,
controller bindings and alternate bindings. No second consumer drains SFC input.
Space/Up/left mouse remain convenience shortcuts sampled at native cadence;
configure them as native alternate jump bindings for SFC buffering. Shortcuts
respect focus, pause and text capture. High refresh follows SFC's saved 240 Hz
option. Detached collision-aware prediction draws cube, camera and course between
updates, with no native position, timer, audio, event or statistics mutations.
Prediction stops at death, completion, portals and pause. Fixed gameplay remains
60 Hz with four reference physics substeps per update.

Build the controller and its dependencies with
`powershell -File scripts/check-mods.ps1 -Mod stereo-madness`.
The isolated output is `build/stereo-madness-mod/UPLOAD_TO_WORKSHOP` for diagnostics;
it is not a standalone Workshop mod. The matching map build embeds its SDK shell
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

Body position and velocity are expressed in level coordinates, independent of the
scrolling camera. Native position-only saves restore cube/ship mode from the
course portals, following the position tool's zero-velocity semantics. Runtime
snapshots additionally preserve precise velocity, rotation, portal history,
camera and death state. The installed JumpKingSaveStates input behaviour runs
once per native update while ordinary body physics is suspended. Music seeks
to the restored course time. These hooks are scoped to the active course.

Installation verifies every copied file, preserves prior installations in rollback
backups and migrates the old debug junction without deleting its target. There is
one active controller copy, bundled with the map. Close Jump King before updating.
Dependencies retain their existing Workshop installations and settings.

Checks: build.ps1 (model and SDK package), verify-package.ps1 (installed Worldsmith
classification and embedded-package lifetime), verify-native.ps1 (native GPU, input,
statistics, ownership and lifecycle), verify-reference.ps1 (preserved GD traces).
The native and package checks require the matching compiled map at
`build/stereo-madness/UPLOAD_TO_WORKSHOP`; package validation also requires the
map toolchain's `scripts/check-worldsmith-package.ps1`. Reference checks require
the original browser Player.js supplied via `-ReferencePlayer`.
A complete human route has not been verified.
See the map's THIRD_PARTY_NOTICES.md for original assets and music credits.
