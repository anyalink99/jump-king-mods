# JK Runtime

JK Runtime **1.32.0** is the shared dependency for this repository's gameplay
mods. One `JKRuntime.dll` provides module loading, Controls+, native-style UI,
input observation, gameplay coordination, state restoration and diagnostics.
Its API version is **1.32**; its stable assembly identity remains **1.0.0.0**.

## Why JK Runtime

Mods often need the same difficult infrastructure: dependency discovery, safe
restart cleanup, controller ownership, input bindings, menus and diagnostics.
Implementing each of these separately leaves players managing load order and
authors debugging interactions between several competing implementations.
JK Runtime gives participating mods shared services and explicit contracts for
when work runs, who owns a resource and how it is released.

For players, that means one Controls+ interface, consistent menus, recorded
modified-run sources and targeted fixes for reviewed third-party conflicts.
For authors, it means preparing assets before player handoff, ordering dependent
modules, coordinating movement and restoring registered state through common APIs.
These mechanisms reduce repeated integration work; they do not guarantee that
every combination of Workshop mods will work.

Read [why Runtime exists and which problems it solves](docs/why-runtime.md),
then [how it is built](docs/architecture.md). For engine versions, load order
and foreign patches, see [Harmony and other mods](docs/compatibility.md#harmony-and-other-mods).
Runtime ships no Harmony DLL and does not load or upgrade an engine for its own
hooks. It uses an already-loaded engine; it does not force Harmony 2.2.2 or make
every other Harmony version compatible with every mod.

## Players

Navigation and Back are silent; accepted actions and settings edits use the
native select cue.
Workshop uses three compact text columns with wrapped names, author/status
metadata, completion icons and the native selection arrow. Cells have no frames
or highlights, and the selected title is not duplicated below the grid.
Rows adapt to their tallest entry, with separate horizontal and vertical
separator segments, native arrow spacing and the five-pixel selection shift. Extended native menus stay within the screen and
scroll when needed. Interrupted submenus require a fresh confirmation.
Optimizations and Diagnostic mode are inside JK Runtime **Settings**.
**Mod compatibility fixes**, enabled by default in Settings, adapts Jump King
Manager's area buttons and screen selector to the currently loaded map. It uses
the map's area names and actual screen count, including unnamed screens. Changes
apply to open Manager windows immediately; disabling restores its original UI.
Screen navigation finds a clear arrival position from the loaded collision data.
See [compatibility](docs/compatibility.md#jump-king-manager-loaded-map-browser)
for supported versions and limits.
**Compact Inventory**, enabled by default in Settings, arranges owned items in
three unlabeled columns: mod items with cursed equipment, cosmetics, and
miscellaneous/trade items. Native inventory padding and one-pixel item spacing
are retained; columns fit their content, and mod-defined text colors are preserved.
Equipped items carry the native check mark. Names wrap;
each overflowing column scrolls independently to its selected item. Arrow keys,
controller directions and mouse keep the original inspect/equip/use actions.
Turning the setting off restores the native list. Unknown foreign row types fall
back to that list rather than losing actions.
See [UI validation](docs/ui-validation.md) for coverage, costs and remaining limits.

Install Workshop item **3793086563** and current versions of the mods that use
it. Keep one active Runtime copy. Old binaries referencing `UIApiPlus.dll` must
be rebuilt; do not install the old UIApi+ DLL alongside Runtime.

Controls+ edits primary/secondary bindings, including two-button chords.
Mods can expose their own **Binds** page with only their registered actions,
using the same primary/secondary slots, Clear and Default commands.
Keyboard profiles support physical left/right, middle, X1 and X2 mouse buttons;
movement and wheel scrolling are not held-button bindings. Mouse bindings have
no defaults. Open a binding slot, then press and release the desired buttons.

In menus, the first left click reveals the cursor without activating anything.
Move to select, click to activate, scroll to browse and right-click to go back.
Keyboard/controller input hides it; Escape keeps mouse mode while going back.
Native sliders/options accept clicks on their left and right halves. The cursor
ignores letterbox bars. Supported native windows use the Windows cursor plane;
menu actions still run on the game thread, with a software-cursor fallback.

Settings live beside the DLL in `JKRuntime.Settings.xml`. Legacy UIApi+ settings
are migrated once, retaining the original and a backup. Mouse chords remain in
Runtime settings rather than being exported as native controller button codes.

At results, `Flag sources` shows known contributors to the native modified-player
flag. Unknown contributors stay unknown. Runtime does not clear that flag or
change achievement eligibility.
This is registration history, not proof of feature use: an enabled mod can
register its handlers at startup even if none of its controls are used.
Witnessed native save resets preserve the sources of a carried-over counter;
removed handlers are not transferred into the next attempt's attribution.

Text-entry pages share a keyboard editor and an on-screen Latin/Cyrillic keyboard.
Typing is isolated from menu bindings; held keys stay blocked through close.

## Mod authors

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
