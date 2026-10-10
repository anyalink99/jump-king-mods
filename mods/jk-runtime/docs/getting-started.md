# Build your first Runtime mod

Start with the SDK's working Settings page. Build it unchanged, open it in Jump
King, then replace the example with your own feature. The current SDK targets
API **2.0** and requires Runtime 2.0 or newer. Assembly identity remains **1.0.0.0**.

## Get the SDK

You need Windows, Jump King and Windows' .NET Framework C# compiler. The template
uses C# 5 and doesn't need a .NET SDK installation.

If you have this repository, run from its root:

```powershell
.\mods\jk-runtime\build.ps1
```

This repository build also needs Python 3.10+ for its documentation checks.
Copy the complete `build/jk-runtime/SDK/` folder to a short working path, such as
`C:\JKMods\FirstMod`. If you already have an exported SDK, copy that folder
instead. Building the copied template doesn't need Python or repository sources.

The folder contains `ModEntry.cs`, `build.ps1`, Runtime, the package builder,
examples and this handbook. The template compiles only `ModEntry.cs`.

## Build and install the example

In your copied SDK folder, run:

```powershell
.\build.ps1 -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\Jump King'
```

Change `GameDir` if Steam installed the game elsewhere. A successful build
creates `build/UPLOAD_TO_WORKSHOP/Example.dll`.

Close Jump King. Install Runtime separately, then put **only `Example.dll`** in
`Jump King/Content/JKMods/FirstMod/`. Don't copy the SDK, game DLLs or
`Example.Module.dll` there. Keep one active copy of each mod.

## Try it in the game

Start Jump King and find **Example Page** in the mod menus. Open **Settings**.
You should see an intensity control, a name and a Rename command. Change the
number, open Rename, enter text, then go back. The page is available from both
the main menu and the pause menu.

The example keeps these edits in memory; it doesn't save preferences. If the
mod is missing, check [troubleshooting](troubleshooting.md) before adding code.

## Make it yours

Open `ModEntry.cs`. It's the complete
[UI example](../examples/UiModExample.cs), with these parts:

- `RuntimeModule` supplies the module's stable ID and display name.
- `MainMenuItemSetting` and `PauseMenuItemSetting` add the Settings button.
- `UIApi.CreateMenuPage` embeds the page in the native menu.
- `ScopedUiPage` owns the open page; `UiPageStack` owns its child text editor.

Change the module ID and display name, then change a visible label and rebuild.
Replace the installed DLL with the game closed and check the new label. Use your
own namespaced IDs before sharing the mod, and rename the example output in
`build.ps1`. Don't compile the unchanged copy under `examples/` alongside it.

Read [UI pages](ui-pages.md) to change layout and page behavior. To save the
intensity or name, use [settings and commands](settings-and-commands.md).

## Add gameplay when you need it

The menu example needs no gameplay callback. When your feature does:

| Work | Where it belongs |
| --- | --- |
| Register definitions needed to load a map | `BeforeLevelLoad` |
| Load reusable map assets | `OnWorldReady(RuntimeScope scope)` |
| Read data that must be fresh for each attempt | `BeforeAttempt(RuntimeScope scope)` |
| Attach player behavior and publish services | `OnLevelStart(ModuleContext context)` |

Own preparation resources with `scope.Own(...)` or `scope.Defer(...)`.
Track player resources immediately with `context.Track(...)`. Read
[lifecycle](lifecycle.md) for restart and failure paths, and
[preparation](preparation.md) before loading assets or scanning metadata.

Live game APIs run on the game thread. Declare a required service before calling
`context.Require<T>()`, and resolve it during activation. Don't retain a previous
player or `ModuleContext` after unload.

## Choose the next feature

| Goal | Guide |
| --- | --- |
| Add a binding or observe a jump | [Events and input](events-and-input.md) |
| Add a movement mechanic | [Gameplay ordering](api.md#gameplay-ordering) |
| Add a sound or trail | [Small effects](common-effects.md) |
| Share a switch or timer with multiplayer and replays | [World execution](world-execution.md) |
| Change a Mapping scene from another mod | Mapping's `docs/scene-api.md` and `MegaMappingApi.dll` |

For the Mapping integration, the repository guide is outside the detached SDK;
use Mega Mapping Expansion's own handbook if working from an exported SDK.
For all Runtime services, use the [API map](api-map.md).

## Before sharing

Build through the package tool and publish the output feature DLL with any
declared feature libraries. Add JK Runtime Workshop item **3793086563** as a
dependency. Don't bundle another Runtime or lower the generated API requirement
by hand. Follow [packaging](packaging.md) and
[verification and release](testing-and-release.md) for the release checks.

Already have a native mod? The [migration walkthrough](migrating-a-mod.md) shows
how to adopt Runtime without starting from the UI example.
