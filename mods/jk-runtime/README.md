# JK Runtime

JK Runtime is the shared base for these Jump King mods. It supplies Controls+,
menus, input handling and services that let mods share player behavior, world
state and rendering. Runtime 2.0.0 includes the former UIApi+ and Controls+.

## Install

Subscribe to [JK Runtime on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3793086563)
and the mods you want to use. Let Steam finish downloading, then restart Jump
King. Keep one active Runtime copy. Don't install the old `UIApiPlus.dll` beside
it; mods built for that DLL need an update.

Runtime 2.0.1 loads API 1.0–1.44, 2.0 and 2.1 packages.
New packages built with the current SDK need Runtime 2.0.1 or newer. See
[version compatibility](docs/api-policy.md) for details.

## Players

Open **Mods > JK Runtime** in the main or pause menu. Useful starting points:

- **Controls+** edits keyboard, mouse and controller bindings, including two-button
  chords. Individual mods can offer a smaller Binds page for their own actions.
- **Pinned settings** puts chosen settings below Resume and can expose toggle
  shortcuts in Controls+.
- **Compact Inventory** puts items in three columns. Turning it off restores the
  native list. The setting starts on.
- **Mod compatibility fixes** starts on and enables the reviewed Jump King Manager
  and BoGMod3 fixes. It doesn't make every combination of mods compatible.
- **ModsDebugActions > Mod Inspector** shows loaded blocks, materials and mod
  state. Its overrides are experimental.
- **Diagnostic mode** records frame and callback timings while you reproduce a
  problem. It starts off; turn it off after collecting the capture.

In menus, the first left click reveals the cursor. Move to select, click to
activate, scroll to browse and right-click to go back. Keyboard or controller
input hides the cursor. Bindings and settings are saved beside the Runtime DLL
in `JKRuntime.Settings.xml`.

Read [Using JK Runtime](docs/players.md) for menu behavior, Debug jump prompts,
settings and run flags. For a problem, start with
[collecting diagnostics](docs/diagnostic-workflow.md) or
[troubleshooting](docs/troubleshooting.md).

## Mod authors

Start with [your first Runtime mod](docs/getting-started.md). Build the supplied
example, open its Settings page in the game, then adapt it. The
[developer handbook](docs/index.md) gives the reading order and groups services
by the work you want to do.

Runtime handles loading, dependency order and resource cleanup for participating
mods. Your mod supplies its feature and registers the resources it owns. Prepare
map assets before play; attach player behavior when the module activates.
Read [lifecycle](docs/lifecycle.md) and [preparation](docs/preparation.md) before
adding gameplay or expensive startup work.

| What you want to add | Start here |
| --- | --- |
| Settings, a menu page or bindings | [UI pages](docs/ui-pages.md), [settings](docs/settings-and-commands.md), [input](docs/events-and-input.md) |
| A player mechanic or controller | [Gameplay ordering](docs/api.md#gameplay-ordering), [geometry and materials](docs/geometry-and-mechanics.md) |
| World state for multiplayer and replays | [Shared world execution](docs/world-execution.md) |
| Appearance, sound or particles | [Player presentation](docs/player-presentation.md), [small effects](docs/common-effects.md), [particles](docs/particles.md) |
| Camera or scene rendering | [Frame composition](docs/runtime-coordination.md#scene-and-camera-rendering) |
| Map-controlled features | [Map mechanics](docs/map-mechanics.md) |
| Integration with an existing mod | [Migration walkthrough](docs/migrating-a-mod.md), [interop](docs/interop.md) |

Use the [API map](docs/api-map.md) to find individual services. The standalone
SDK contains the handbook, complete examples, IDE XML help and a generated
`PublicApi.md` signature reference. Publish your packaged feature DLL and declare
Runtime as a dependency; don't bundle a second Runtime.

Runtime orders its SDK modules. Other mods keep their own callbacks and patches.
Runtime uses an already-loaded Harmony engine and doesn't ship one. Read
[compatibility](docs/compatibility.md) before relying on a foreign mod's private
members, and [why Runtime exists](docs/why-runtime.md) for examples of cooperation.

## Build and maintain Runtime

From the repository root, with Jump King installed:

```powershell
scripts/check-mods.ps1 -Mod jk-runtime
scripts/check-mods.ps1 -Mod jk-runtime -Integration
```

The build checks the API and examples, then exports the mod to
`build/jk-runtime/UPLOAD_TO_WORKSHOP/` and the author SDK to
`build/jk-runtime/SDK/`. It doesn't install or publish.

See [verification and release](docs/testing-and-release.md) for checks,
installation and rollback; [architecture](docs/architecture.md) for implementation
boundaries; and [documentation maintenance](docs/maintaining-docs.md) for handbook
updates. Release history is in [CHANGELOG](CHANGELOG.md).
