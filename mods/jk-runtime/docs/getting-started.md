# Choose a Runtime integration

Current SDK: **1.32**, assembly identity **1.0.0.0**. New packages built with
this SDK require Runtime API 1.32. Existing packages retain their own minimum.
The SDK build exports JKRuntime.dll, XML documentation, package tools and this
documentation tree. Start from the exported README/build.ps1/ModEntry.cs template.

For an existing native mod, use the [migration walkthrough](migrating-a-mod.md)
and buildable LessAutoEquipping example instead of starting with a blank module.

The [handbook index](index.md) is the complete reading path. For a first package:

1. In this repository, build `mods/jk-runtime/build.ps1`. Copy the resulting
   `build/jk-runtime/SDK` folder elsewhere, or use an already exported SDK.
2. Edit `ModEntry.cs` (the complete scoped UI example) and replace its IDs with stable
   namespaced IDs. Read [lifecycle](lifecycle.md) before attaching player behavior.
3. Run `build.ps1 -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\Jump King'`
   in that copied SDK. It uses Windows' .NET Framework compiler with C# 5 syntax.
4. Install only `build/UPLOAD_TO_WORKSHOP/Example.dll` in the feature's existing
   Workshop folder, or its own local `Content/JKMods` folder. Install a matching/newer
   Runtime separately. Close the game first; preserve settings and avoid duplicates.
5. Check Runtime diagnostics for activation/dependency errors, then run the
   relevant [acceptance cases](testing-and-release.md). A build alone is not a playtest.

| Goal | Entry point | Lifetime and guide |
| --- | --- | --- |
| Prepare resources before handoff | `OnWorldReady`, `BeforeAttempt`, `RuntimeScope` | [Preparation and lifetime guide](preparation.md) |
| Add a menu page | `UIApi.CreateMenuPage`, `ScopedUiPage` | Native embedded page; [page guide](ui-pages.md) |
| Open a child editor | `UiPageStack.Push` | Top-only input/drawing and owned child cleanup; [page guide](ui-pages.md#nested-pages) |
| Add Workshop navigation | `RegisterMainMenuItem`, `UiMainMenuPlacement.Workshop` | Registration scope; [UI API](ui-api.md) |
| Observe jump/land/pause | `GameplayEvents.Subscribe` | Track subscription in ModuleContext; [events/input](events-and-input.md) |
| Suspend a component | `ComponentSuspension.Acquire` | Dispose lease; never write Enabled to release another owner |
| Add a mechanic | `RuntimeApi.Mechanics`, body pipeline | Explicit effects/authority; [geometry/mechanics](geometry-and-mechanics.md) |
| Capture/restore mod state | `GameState.Snapshots.Register` | Versioned participant, validate before restore; [API](api.md) |
| Diagnose integration | Module status, RuntimeJournal | Bounded output; [diagnostics](diagnostic-workflow.md) |
| Change a Mapping scene | `mega.mapping.scene:1:0` | Requires Mega Mapping Expansion 0.2 and MegaMappingApi.dll; use that mod's `docs/scene-api.md` |

Native gameplay, module graph, UI and state operations run on the game thread.
Prepare player-independent assets/data in `OnWorldReady` / `BeforeAttempt`, owning
them in the supplied `RuntimeScope`. Acquire player-bound resources in
`OnLevelStart` and immediately `context.Track(lease)`.
Declare `Requires` before `context.Require<T>()`; resolve during the level
lifetime, not from a static initializer. Do not keep ModuleContext after unload.
Partial module activation releases tracked resources in reverse order.

Use the smallest supported contract. Watching an event does not require a body
patch. Displaying an embedded page does not require a custom modal. Authoring a
Mapping region or a 30-second light does not require a new gameplay mod.
Choose native bindings through `UiInputHints`; internal UiAction names are not
user-facing key labels.

Use stable namespaced IDs for registrations and owners. Disposing an older
UiRegistrationScope cannot unregister a newer same-owner replacement.
ComponentSuspension coordinates cooperating owners, preserves initially disabled
components, rejects reentrant transitions and retains retryable failed restores.
It cannot coordinate foreign mods that directly overwrite Component.Enabled.

Build through the SDK package tool. It creates a native discovery shell that
does not require Runtime during ModLoader.GetTypes. Keep implementation inside
the package, declare dependency references, and avoid a second Runtime/Harmony
installation. See [compatibility policy](api-policy.md) for the guarantees and
limits of frozen ABI checks and old consumer execution.
