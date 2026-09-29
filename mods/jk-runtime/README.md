# JK Runtime

JK Runtime **1.37.0** is the shared base for this repository's gameplay mods.
Install one `JKRuntime.dll` and the other mods can share module loading,
Controls+, native-style menus, input observation, gameplay coordination,
player appearance, cosmetic particles, state restoration and diagnostics.
Its API version is **1.37**; its stable assembly identity remains **1.0.0.0**.

## Why JK Runtime

Mods keep running into the same hard problems: finding dependencies, cleaning up
after a restart, deciding who controls the player, editing bindings, drawing
menus and diagnosing conflicts. Separate implementations make players juggle
load order and leave authors debugging several systems at once. Runtime gives
participating mods one set of services and clear rules for when work runs, who
owns a resource and when that resource is released.

Players get one Controls+ screen, consistent menus, useful modified-run details
and targeted fixes for conflicts we have actually reviewed. Authors get common
APIs for early asset preparation, module ordering, movement coordination and
state restoration. Runtime removes a lot of repeated glue, but it cannot make
every possible Workshop combination compatible.

Read [why Runtime exists and which problems it solves](docs/why-runtime.md),
then [how it is built](docs/architecture.md). For engine versions, load order
and foreign patches, see [Harmony and other mods](docs/compatibility.md#harmony-and-other-mods).
Runtime ships no Harmony DLL and does not load or upgrade an engine for its own
hooks. It uses an already-loaded engine; it does not force Harmony 2.2.2 or make
every other Harmony version compatible with every mod.

## Players

### Install Runtime

Install Workshop item **3793086563** and current versions of the mods that use
it. Keep one active Runtime copy. Old binaries referencing `UIApiPlus.dll` must
be rebuilt; do not install the old UIApi+ DLL alongside Runtime.

### Menus

Navigation and Back are silent; accepted actions and settings edits use the
native select cue.

The Workshop browser uses three compact columns. Names wrap, author and status
stay visible, and the native arrow and completion icons still work. Long native
menus stay on screen and scroll when needed. If a submenu is interrupted, it
asks for confirmation again. **Optimizations** and **Diagnostic mode** are under
JK Runtime **Settings**.

**Mod compatibility fixes**, enabled by default in Settings, adapts Jump King
Manager's area buttons and screen selector to the currently loaded map. It uses
the map's area names and actual screen count, including unnamed screens. Changes
apply to open Manager windows immediately; disabling restores its original UI.
Screen navigation finds a clear arrival position from the loaded collision data.
See [compatibility](docs/compatibility.md#jump-king-manager-loaded-map-browser)
for supported versions and limits.

**Compact Inventory**, enabled by default in Settings, arranges owned items in
three unlabeled columns: mod items with cursed equipment, cosmetics, and
miscellaneous or trade items. Names wrap and each long column scrolls to its own
selection. The original check mark and inspect, equip and use actions still work
with keyboard, controller and mouse. Turning the setting off restores the native
list. Unknown rows also fall back to that list so their actions are not lost.
See [UI validation](docs/ui-validation.md) for coverage, costs and remaining limits.

### Controls+

Controls+ edits primary/secondary bindings, including two-button chords.
Mods can expose their own **Binds** page with only their registered actions,
using the same primary/secondary slots, Clear and Default commands.
Keyboard profiles support physical left/right, middle, X1 and X2 mouse buttons;
movement and wheel scrolling are not held-button bindings. Mouse bindings have
no defaults. Open a binding slot, then press and release the desired buttons.

### Mouse controls

The first left click in a menu only reveals the cursor. After that, move to
select, click to activate, scroll to browse and right-click to go back. Keyboard
or controller input hides the cursor; Escape goes back without leaving mouse
mode. Native sliders and options accept clicks on their left and right halves.
The cursor ignores letterbox bars. Supported windows use the Windows cursor
plane, with a software cursor as a fallback. Menu actions still run on the game
thread.

### Prompted jump power in Debug Mode

Hold the right mouse button over the game and drag upward or downward to set
`P:` above the king. Release to keep the prompt. Drag again to adjust it, or
right-click without moving to turn it off when the button is released.
The number uses Jump% hold frames. One logical pixel selects a quarter frame when
Subframe Charge's Quarter-step Charge is active; otherwise four pixels select
one frame. The range follows the current surface's charge multiplier.

Tap Jump to charge and execute the selected power. Each charge starts neutral;
Left or Right held at its start or tapped during it latches that direction for
this jump. Movement before charging doesn't count. The arrow appears only during
the charge and clears on takeoff or cancellation. The prompt uses the game's
MenuFont and normal jump effects. Pausing, losing focus,
teleporting or switching controllers cancels a pending charge. The prompt is
available only in the game's `-debug` mode and is never saved.

### Settings and run flags

Settings live beside the DLL in `JKRuntime.Settings.xml`. Legacy UIApi+ settings
are migrated once, retaining the original and a backup. Mouse chords remain in
Runtime settings rather than being exported as native controller button codes.

At results, `Flag sources` lists known contributors to the native modified-player
flag. It cannot identify unknown contributors, clear the flag or change
achievement eligibility. Treat the list as registration history rather than
proof that a feature was used: an enabled mod may register handlers at startup
without any player input. If the game resets a save while carrying a counter
forward, Runtime keeps the witnessed sources. Removed handlers do not carry into
the next attempt.

Text-entry pages share a keyboard editor and an on-screen Latin/Cyrillic keyboard.
Typing is isolated from menu bindings; held keys stay blocked through close.

## Mod authors

[Cosmetic particles](docs/particles.md) provides bounded reusable systems, shared
world collision and ordered sprite submission without owning gameplay or clocks.

[Material and movement interop](docs/interop.md) supplies pure support queries,
actual contact evidence, seven movement-stage observations, method validity
leases, typed material declarations and an opt-in bounded trace. See the compiled
SDK example for preparation, activation ownership and explicit capture.

[Motion observation](docs/motion-observation.md) distinguishes supported speed
scaling from added horizontal displacement in the actual native modifier pass.
It observes existing handlers once, retains unknown coverage explicitly and
supports optional provider reports. It does not simulate arbitrary foreign code.

Start with the [developer handbook](docs/index.md) and
[first-package guide](docs/getting-started.md). Choose a service through the
[API map](docs/api-map.md), then read its ownership and failure contract.

For a new UI mod, use the SDK's `ModEntry.cs` (canonical source:
`examples/UiModExample.cs`). Use `ScopedUiPage` for the page and `UiPageStack`
for child editors. Both native-menu and modal hosts share cancellation,
release boundaries and failure cleanup. See [page authoring](docs/ui-pages.md).

For completion reports, `RunModifiers.GetEvidence()` returns a detached
`RunModifierRecord` for the active or last completed attempt. Call it on the game
thread; null means unavailable. The stable `RunKey` identifies the saved attempt
across Continue sessions. See [geometry and mechanics](docs/geometry-and-mechanics.md)
for attribution and the distinction between registration and feature use.

Prepare expensive player-independent work in [world/attempt preparation](docs/preparation.md).
Attach player behavior and publish capabilities at normal activation. Runtime
does not automatically move work hidden inside getters, constructors or a first
update into loading. The [lifecycle guide](docs/lifecycle.md) covers callback
order, dependency resolution, cancellation, failure and cleanup.

The standalone SDK contains the same handbook and complete example sources,
`JKRuntime.xml` for IDE help, and `PublicApi.md` generated from the built DLL.
Only the packaged feature DLL belongs in a feature's Workshop upload; declare
Runtime as a dependency. See [API compatibility policy](docs/api-policy.md).
Runtime orders participating modules, not arbitrary foreign DLLs/Harmony patches.
It uses the game's loaded shared Harmony engine rather than shipping another.
Known compatibility interventions and their limits are in
[compatibility](docs/compatibility.md).

## Diagnostics

Toggle **JK Runtime > Diagnostic mode** in the native main/pause mod settings for
ongoing performance capture, startup timing and callback profiling. It is off by
default. Turning it off removes its measurement hooks and exports a partial window.
Four rotating `JKRuntime.Performance.0` through `.3` text/CSV pairs stay beside the
DLL. Captures include native frame timings, allocation/GC evidence and component
costs. Profiling adds overhead; it does not measure GPU or physical input latency.

Open **JK Runtime → Runtime diagnostics** and explicitly export a text/JSON
report beside the DLL. Nothing is uploaded automatically. Startup tracing is
separate: create `JKRuntime.StartupTrace.enabled` beside Runtime before launch,
reproduce and collect `JKRuntime.Startup.txt`; remove the marker and restart to
disable it. The [diagnostic workflow](docs/diagnostic-workflow.md) explains all
capture modes, report paths, interpretation limits and temporary probes.

## Development

From the repository root:

```powershell
scripts/check-mods.ps1 -Mod jk-runtime
scripts/check-mods.ps1 -Mod jk-runtime -Integration
```

The build validates frozen ABI and focused tests, compiles examples, exports and
checks SDK documentation, then builds the standalone template in isolation.
Output is `build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll`; the author SDK is
`build/jk-runtime/SDK`. Building does not install or publish.

See [verification and release](docs/testing-and-release.md) for the acceptance
matrix, selected integration checks, installation and rollback. The coordinated
installer is `install-release.ps1`; preserve settings/backups and close the game
before replacing loaded binaries. Documentation-only changes need no DLL install.

Read [architecture](docs/architecture.md) for implementation boundaries,
[documentation maintenance](docs/maintaining-docs.md) for handbook checks, and
[CHANGELOG](CHANGELOG.md) for release history.

Map-controlled gimmicks use the shared [activation contract](docs/map-mechanics.md),
including nonblocking screen pixels, XML areas and persistent disable-on-entry tags.
Screen pixels and XML Screens declarations are alternatives for the same
On/Off/Local rule: use either one, without duplicating it. XML additionally offers
ranges, zones and typed parameters. If both describe the same screen, they must
agree; XML has no override priority. The map authority tag remains separate.
