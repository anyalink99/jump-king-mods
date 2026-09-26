# JK Runtime standalone SDK

This README describes the exported `build/jk-runtime/SDK` folder. Copy that whole
folder anywhere outside the repository, edit `ModEntry.cs`, choose your own
module/mechanic IDs and run `build.ps1 -GameDir <installed-game>` on Windows.
It uses the .NET Framework compiler supplied with Windows. No other repository
mod or map project is needed. The default build compiles only `ModEntry.cs`.
That file is the canonical `examples/UiModExample.cs`: a scoped Settings page in
both native menus, with numeric editing and owned child text entry. Edit this
file directly; do not compile its unchanged copy from examples alongside it.

Install only `build/UPLOAD_TO_WORKSHOP/Example.dll`, with matching or newer JK
Runtime 1.x. Do not publish the SDK, game binaries, `Example.Module.dll` or a
second Runtime DLL inside your mod. Add Workshop item **3793086563** as a required
dependency. Rename the example package/output when making your own release.

In the exported folder, `docs/why-runtime.md` explains which integration problems
the shared services solve; `docs/architecture.md` explains the layers and ownership.
Read `docs/compatibility.md` for Harmony, foreign mods and load-order diagnostics.
For implementation, start with `docs/index.md`, `docs/getting-started.md` and
`docs/lifecycle.md`. Read `docs/preparation.md` before loading assets or scanning
metadata. `examples/PreparationExample.cs` shows owned world/attempt data and
lightweight activation; `examples/StateExample.cs` shows isolated state payloads.
The other examples cover modules, UI pages and map interactions. Examples are
separate modules: select/adapt one instead of compiling them all into one package.
`examples/interactions.xml` supplies the map data needed by the UI integration
example; see `docs/ui-api.md` for placement/schema.

`docs/api.md` and `docs/ui-api.md` describe the service contracts. `PublicApi.md` is generated
from this SDK's DLL and routes every exported type to its guide. `JKRuntime.xml`
supplies IDE documentation where public APIs have XML comments. Signature and
link checks cannot replace reading the semantic contract and testing in game.

PackageBuilder targets the SDK's API minor version. Do not lower
that requirement by hand without testing against the older Runtime. Preserve
stable IDs and user data when upgrading. Follow `docs/testing-and-release.md`
for validation, payload selection and installation/rollback.
