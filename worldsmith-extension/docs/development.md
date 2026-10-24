# Development

## Build from source

Building requires Windows x64, Windows PowerShell, .NET Framework 4.8, Python 3.10+
and installed copies of Jump King and Worldsmith. Use a short checkout path.
From the repository root:

```powershell
.\scripts\check-mods.ps1 -Mod worldsmith-extension -WorldsmithDir "C:\Path\To\Jump King Workshop"
```

This builds the shared dependencies and the extension. After that, you can
rebuild only the extension with:

```powershell
.\worldsmith-extension\build.ps1 -WorldsmithDir "C:\Path\To\Jump King Workshop"
```

Both commands accept `-GameDir`. Ready-to-use output goes to
`build/worldsmith-extension/`: `PORTABLE` contains the app and `RELEASE` contains
the latest ZIP and its SHA-256 file. Candidates, tests, previous packages and dated
archives go to `build/_work/worldsmith-extension/`.

Close the portable editor before replacing it. If the app is still running, the
build stops after validation and leaves the new files in a candidate folder.
Use `-SkipPortablePromotion` to validate a candidate without replacing either
`PORTABLE` or `RELEASE`.

## Supported Worldsmith build

The launcher requires a `JKWorldsmith.exe` with this SHA-256:

```text
691B8DE520DF66EEA262EBC4AF7D5E03F8786B5AEB73A7EAF9B9FC1779FD042C
```

Patches depend on private editor members. Supporting another build requires
inspecting its contracts and rerunning the native tests. The package loads editor
and game dependencies from the installed copies; those assemblies aren't distributed.

## Code layout

All source files are under `src/`, grouped by responsibility. The engine build
collects C# files recursively; launcher and focused-test inputs are listed explicitly
in `build.ps1`.

`Authoring/LegacyCollision.cs` builds as `WorldsmithExtension.LegacyCollision.dll`.
It only decodes legacy More Block Sizes pixels into coloured rectangles, with no
editor hooks or assembly loading. The engine renders those cells through the
same native slope adapter as other previews. Keep this DLL in the portable
package; it doesn't require More Block Sizes to be installed in the editor.

| Directory | Contents |
| --- | --- |
| `Authoring/` | Standalone legacy collision decoder. |
| `Bootstrap/` | Launcher, engine startup, language setup and release metadata. |
| `Infrastructure/` | File operations, worker processes, operation ownership and background dispatch. |
| `Native/` | Adapters for editor members, projects, content compilation and Steam metadata. |
| `Patches/` | Hook registration and handlers for collision, compilation, editor UI, saves and Workshop. |
| `Project/` | Project formats and loading, resource recovery, asset safety, archiving and game testing. |
| `Building/` | Build planning, compilation, mod packaging, receipts and collision layouts. |
| `Workshop/` | Publication sessions, Steam uploads, folder links, metadata storage and Legacy imports. |
| `UI/` | Project and publication windows, Workshop actions, shared controls and sprite playback. |
| `Updates/` | Release discovery, verified downloads and the update window. |
| `News/` | Developer feeds, changelogs, caching and home-page news. |

## Workflow boundaries

`PublishSession` owns the prepared build and linked publication. Windows submit a
snapshot of form values and render session changes. Metadata received during a
build is applied when that build finishes; later catalog refreshes don't replace
an initialized draft.

`Publisher` accepts an injected verification scheduler. The editor runs source and
output hashing in the background, then resumes Steam calls on the dispatcher. An
existing-item update verifies once. Creating a new item verifies again after Steam
assigns its ID, because the project may have changed while waiting for that callback.
The assigned ID remains in the session if saving it fails.

`Operations` grants one lease for a build, publication, asset recovery, test install
or extension download. Completion releases it on success and failure. Native project
loading is tracked by `LoadState`; read-only news and update checks don't acquire a
mutation lease.

`BuildPlan` selects the same mode from every entry point. All available image and
audio sources compile, including partial-source packages opened in the package
browser. Compiled-only resources retain their bytes. Editor page availability
doesn't decide whether a source is compiled. Mods always pass through `ModPackage`, including its
managed-assembly validation. The publication form displays the selected mode.

Ordinary collision keeps Worldsmith's newer-image choice until the author saves
an explicit source. Source fingerprints include that choice, so changing only the
timestamps can't reuse a test build made from the other image. Native background
scanning and checked builds both skip XNBs whose editable sources are available.
Deleting a recovered asset archives its source and companion XNB.

Version 2 `level+` collision uses a streaming XNB writer instead of the general
texture pipeline. Validation still checks dimensions, alpha and coarse pixel
groups before replacing output. Fully transparent and magenta pixels become
transparent black, matching the native color key and premultiplication; opaque
colors keep their exact channels. Ordinary collision keeps its byte-preserving
writer, and other textures still use the native pipeline.

Collision previews read locked bitmap memory and keep the existing coordinate
mapping across screen borders. Slope detection and drawing still use the native
editor's geometry. No rendered-screen cache sits between external PNG edits and
the next preview.

Native member resolution includes parameter types and caches the resolved member.
Add new host contracts to the corresponding adapter and cover them in native tests.
Feature hooks handle editor-specific arguments and delegate work to these components.

Workshop library reads validate XML and can use the last valid backup. A later
save keeps the damaged file separately before writing repaired data. Catalog
notifications isolate view failures from persistence failures. Legacy folder discovery
is cached briefly and invalidated by an explicit Workshop refresh.

## Tests

The build runs collision round trips, XNB pixel comparisons, failed-save cases,
source/output receipt checks, resource-directory preservation, package recovery and
an offline Steam publishing state machine. Tests are grouped by behavior; the suite
runner reports assertion counts. Workflow tests cover deferred metadata, retrying an
assigned item after persistence failure, operation ownership, common build selection
and rollback with multiple failures.

Native tests load the installed editor without opening its window. They exercise
large-map navigation, preview rendering, unresolved props, hidden-wall coordinates,
image replacement, path containment, animation subscriptions, page layout and
loading-time wardrobe saves. Test mode also checks that compiled packages keep their
compiled assets. They also check the project adapter, mixed-source recompilation
and background execution through the WPF dispatcher.

Compatibility fixtures build the installed Level, Skin, Set and Tileset templates
(with the Tileset settings normally written by native project creation). They
compare ordinary collision selection with the original initialization method,
compile changed pixels in partial-source maps, check nested resource names through
the native watcher and verify collision-strip output paths. Secondary Steam-library
discovery uses isolated VDF fixtures. These checks don't open users' maps or start
an interactive game session.

Pixel compiler fixtures compare complete RGBA payloads with the installed native
compiler for RGBA and indexed PNGs. Preview fixtures compare block reads with a
scalar reference for every resolution, atlas/strip layouts, mixed-resolution
neighbors, alpha and reversed bitmap stride. Native tests retain the slope
geometry comparisons and large-atlas source-preservation checks.

Recent and Favorites load saved titles without generating folder paths from them.
Invalid entries don't hide the other projects. If a history file is unreadable,
the loader tries its `.worldsmith-backup` and native `.temp` backups. Recovery
shows a warning and keeps the original files unchanged until a save is requested;
that save preserves them as separate `.recovery-*` copies first. Native tests
cover these cases against isolated history files.

For the Windows download integration test, add `-DownloadedPackageChecks` to the
extension build command. This marks a copied package as an Internet download and
checks the launcher configuration against error `0x80131515`. It can trigger Windows
security scan notifications, so it runs separately from routine builds.
The package uses the application-scoped
[loadFromRemoteSources](https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/loadfromremotesources-element)
setting to load downloaded assemblies.

Use the editor for final checks of project loading, game launch and Steam uploads.
Navigation timings are recorded in `extension.log` after the dispatcher becomes idle.
Project inspection is timed separately from native model loading. Format inspection
runs off the dispatcher, and opening the editor reuses that result. Error dialogs
show the causes of parallel load failures; Copy includes the full exception stack.

Missing prop and wardrobe settings use empty or native defaults in memory. Loading
doesn't create those files. Malformed XML still fails with its path, and failed
project loads remain protected against saves.

## Updating the extension

The updater reads the public GitHub
[releases API](https://docs.github.com/en/rest/releases/releases#list-releases) and
selects tags beginning with `worldsmith-extension-v`. Versions use
`major.minor.patch` or `major.minor.patch-preview.number`; releases for other mods
are ignored.

Preview 21 and older require one manual ZIP upgrade to the current release
because their package allowlist predates the separate platform PNG.

Archives must match the release asset's SHA-256 digest. The build generates
`package-manifest.xml` with format version 1, the release version and each file's
relative path, size and SHA-256. New resource directories and dependencies can be
added to later packages without changing the updater's file allowlist. Required
application files still have to be present.

Extraction checks the manifest against the complete archive. It rejects paths
outside the destination, Windows device names, links, duplicate entries, file/directory
conflicts, unlisted files and size or hash mismatches. The archive is limited to
64 MiB, expanded data to 256 MiB and the manifest to 4096 files. Files are checked
again before activation. Failed downloads remove their incomplete stage; a
successful preparation removes the duplicate ZIP. Version folders live under
`%LOCALAPPDATA%/WorldsmithExtension/updates/packages`; `current.xml` records the
selected version. Failed startup restores the previous selection.

The launcher follows a newer selected version during normal startup. Workers and
contract checks use their own bundle. `--ignore-updates` starts a specific local
copy. Restart waits for the previous editor process to exit before activation.

Offline tests cover version ordering, release channels, archive validation,
changed prepared files, launcher selection, rollback and downgrade prevention.

## Publishing a release

Build the complete ZIP from a validated clean export and upload it with its SHA-256
file. Verify the published assets and checksum, then remove previous Worldsmith
Extension releases and their tags from GitHub. Leave other components' releases
and tags alone. The changelog retains the history of changes.

The updater downloads the full package, so users can skip intermediate versions.
Updater discovery, download and upgrade checks are separate from publication;
run them only when explicitly requested.
