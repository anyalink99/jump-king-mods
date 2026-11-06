# JK Runtime standalone SDK

Build the supplied Settings page, try it in Jump King, then adapt it for your mod.
The exported SDK is self-contained; you need Windows and the installed game. The
template uses Windows' .NET Framework compiler with C# 5. It doesn't need Python
or another repository mod.

## Build and try the example

1. Copy the complete exported SDK to a short path, such as `C:\JKMods\FirstMod`.
   The repository build produces it at `build/jk-runtime/SDK/`; this source
   template folder alone isn't a usable SDK.
2. Run `.\build.ps1 -GameDir '<installed Jump King folder>'` there.
3. Close Jump King and put `build/UPLOAD_TO_WORKSHOP/Example.dll` in its own
   `Content/JKMods/FirstMod/` folder. Install JK Runtime 2.0 or newer separately.
4. Start the game and open **Example Page > Settings** in the mod menus.

You should see an intensity control and Rename command. These example values
aren't saved. [Your first Runtime mod](../../docs/getting-started.md) explains the
files, what to edit and what to check in the game.

## Make your own package

Edit `ModEntry.cs`, choose your own module IDs and display name, and rename the
output in `build.ps1`. That script compiles only `ModEntry.cs`, which is the
complete scoped UI example from `examples/UiModExample.cs`. Don't compile both
copies. Other examples are separate modules; choose and adapt one as needed.

Publish your packaged feature DLL and its declared feature libraries. Don't
publish the SDK, game binaries, `Example.Module.dll` or another Runtime DLL.
Add Workshop item **3793086563** as a required dependency.

The package builder records this SDK's API version, **2.0**. Don't lower that
requirement by editing the manifest. See [packaging](../../docs/packaging.md) and
[verification and release](../../docs/testing-and-release.md).

## Find a guide

- [Handbook](../../docs/index.md): guides grouped by task
- [Lifecycle](../../docs/lifecycle.md) and [preparation](../../docs/preparation.md): resources,
  activation and restart
- [UI pages](../../docs/ui-pages.md): menus and child editors
- [World execution](../../docs/world-execution.md): state shared by multiplayer and replays
- [API map](../../docs/api-map.md): find the service for your feature
- [Compatibility](../../docs/compatibility.md): Harmony and other mods

`PublicApi.md` lists signatures from this SDK's DLL; `JKRuntime.xml` supplies IDE
help. The handbook explains when to call those APIs. `examples/interactions.xml`
contains map data for the separate world-interaction example; see
[UI world interactions](../../docs/ui-api.md#world-interactions) before using it.
