# Fixes and safeguards

This reference covers Worldsmith Extension's fixes and safeguards, including
the changes listed under Unreleased in the changelog. It covers both
corrections to regular Worldsmith and safeguards in the extension's added workflows.
The [changelog](../CHANGELOG.md) records when changes shipped; the
[user guide](index.md) explains how to use them.

These fixes apply when starting the supported regular Worldsmith build through
`WorldsmithExtension.exe`. See the [supported executable](development.md#supported-worldsmith-build).
Launching Worldsmith directly doesn't apply the patches. Files already damaged by
an earlier save aren't automatically reconstructed.

## Project history and loading

| Problem | Current behavior |
| --- | --- |
| One invalid history entry can hide the whole list. | Entries are read separately, valid entries survive, and unreadable history can be recovered from a valid backup. Recovery files are preserved before the repaired list is saved. |
| Reopening a project can change its name, lose its Favorites membership or Workshop link, or duplicate a compiled package in Recent. | The extension preserves the saved identity and existing links, and matches the package to its existing entry. |
| Missing optional prop, skin, cosmetic or set settings can fail loading. | Supported missing settings and null arrays get in-memory defaults; loading alone doesn't write them to disk. Malformed files still report an error. |
| A failed project initialization can leave the editor stuck in its loading state. | Failure releases the loading state. Error reports include the affected file and underlying exception; copied reports retain full details. |
| Large project inspection can freeze the home page. | Folder inspection runs in the background and its result is reused when opening the editor. |
| A project title containing `>` can fail native history loading because Worldsmith treats the title as part of a path. | Recent and Favorites restore the saved title separately from the project directory. |

Implementation: [ProjectHistory](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/ProjectHistory.cs),
[NativeProjects](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Native/NativeProjects.cs),
[WardrobeSettings](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/WardrobeSettings.cs),
[LoadState](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/LoadState.cs).

## Scrolling backgrounds

| Problem | Current behavior |
| --- | --- |
| After a bad scrolling XML is skipped, native file and screen lists can disagree. Editing a later background then overwrites the wrong XML. | Each loaded screen keeps its exact source filename. Editing `scroll02.xml`, for example, saves that same file even if `scroll1.xml` could not load. |
| Adding a background to a screen whose XML failed to load can replace the unreadable settings with defaults. | Saving a new screen refuses to overwrite existing settings. Repair the XML and reload before editing that screen. |
| Duplicate numeric filenames, such as `scroll2.xml` and `scroll02.xml`, make the destination ambiguous. | The conflicting screen is reported and skipped; other valid screens remain available. Backup-like names are not treated as screen files. |
| Reloading can leave old list handlers attached. | The previous save handler is detached before installing the new list, so stale wrappers cannot save over newly loaded data. |
| An omitted animation frame list throws during loading. | Missing or empty frame lists are accepted without changing layer position, speed, sheet dimensions or existing frame order. An omitted layer list is also accepted. |

Skipping an invalid screen does not repair it. Its source stays on disk, and the
error names the file that needs attention.

Implementation: [ScrollingSettings](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/ScrollingSettings.cs),
[ProjectSafety](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/ProjectSafety.cs).
Checks: [screen data safety](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/tests/native/ScreenDataSafety.cs),
[scrolling loading](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/tests/native/ScrollingLoading.cs).

## Audio

| Problem | Current behavior |
| --- | --- |
| Adding audio beyond the last section inserts it before existing sections, shifting their screen assignments. | Existing sections keep their position. Any silent gap and the new one-screen section are appended. For five configured screens and a new sound on screen 10, the order is five existing screens, four silent screens, then screen 10. |
| New sections reconstruct the sound without keeping its editor audio binding. | The added ambience object keeps its binding and volume. Adding within an existing section preserves that section's coverage. Screen lookup uses one-based boundaries. |
| Static audio metadata survives project changes and accumulates on reload. | Metadata is cleared before loading. A sound named the same in two projects uses the current project's category and settings. |
| A zero-length audio section blocks screen lookup. | Zero-length sections are skipped without changing their saved data or the coverage of other sections. |
| MP3 compilation uses the wrong native compiler parameters. | The compiler adapter supplies the expected parameters and propagates failures instead of treating a failed build as successful. |

Implementation: [AudioSettings](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/AudioSettings.cs),
[NativeCompiler](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Native/NativeCompiler.cs).
Checks: [screen data safety](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/tests/native/ScreenDataSafety.cs).

## Saves, props and asset files

| Problem | Current behavior |
| --- | --- |
| Direct XML or image replacement can destroy the last good file if a write fails. | Patched save and replacement operations write through a temporary file, retain `.worldsmith-backup` copies and replace the destination only after writing succeeds. Completed XML saves appear in the status line. |
| Loading wardrobe settings can save incomplete defaults over the existing configuration. | Loading-time saves are suppressed; saving is attached after settings have loaded. |
| Missing prop definitions or textures can drop placements from the editable model. | Unresolved props remain as placeholders with their original data. Repairing the assets restores their preview. Malformed prop XML blocks affected prop saves until repaired and reopened. |
| Saving after a failed project load can write incomplete state. | Saves into the failed project's root are blocked by the load-state guard. |
| Supported delete actions permanently remove local assets or projects. | Patched asset deletions are archived under `.worldsmith-extension/deleted`; local project archival moves the project into a sibling `.worldsmith-archives` folder. The Workshop item is retained. |
| File operations can target the wrong screen or escape the intended asset folder. | Patched operations check path containment and match screen filenames exactly. Compiled-package paths use the corresponding package root. |
| Image previews can lock files or keep stale content after an external edit. | Images are loaded without retaining the source handle; the cache checks file length and modification time. |
| File-change notifications can arrive before an external editor finishes writing. | Refresh waits for the file to become readable, avoids duplicate unchanged notifications and ignores internal build/temporary paths. Waiting is bounded. |

These protections cover the operations patched by the extension, not every write
performed by third-party tools. Keep independent project backups.

Implementation: [FileSafety](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/FileSafety.cs),
[ProjectSafety](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/ProjectSafety.cs),
[SavePatches](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Patches/SavePatches.cs),
[Files](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Infrastructure/Files.cs).

## Collision, builds and previews

| Problem | Current behavior |
| --- | --- |
| Native preview regeneration can replace the chosen collision source. | An explicitly selected atlas or strip stays authoritative. Ordinary projects without that choice follow Worldsmith's newer-image rule. Preview generation and editable-copy recovery keep an explicit atlas source intact. |
| Enlarging a standard 13-row atlas to the right produces a rectangular image that the game cannot sample correctly as-is. | Checked builds repack it into a square atlas while preserving screen numbers and exact RGBA collision values. The editable PNG remains unchanged. |
| Native screen bounds and whole-map preview work limit larger projects. | Preview bounds follow the authored screen count, with collision rendering limited to the visible screen. Large-map settings are validated before compilation. |
| Full-resolution preview makes a GDI call for each pixel. | Block reads preserve screen borders, mixed resolutions and the native slope geometry. External PNG edits still appear through normal file-change handling. |
| Compiling a large version 2 level+ atlas creates large intermediate texture copies. | Collision writes directly to an uncompressed XNB one row at a time, with the same color-key and alpha behavior as native compilation. Other textures retain the native pipeline. |
| Animated thumbnails continue doing work when hidden or subscribe repeatedly. | Hidden or unloaded thumbnails stop animating, subscriptions are deduplicated, and updates are scheduled through the rendering dispatcher. |
| Repeated recursive searches and scans slow startup and compilation. | Icon lookup avoids a recursive editor scan, native member lookup is cached, and compilation reuses a file inventory. |
| Empty resource directories disappear from build or recovery output. | Builds, packages and editable copies retain required empty folders, including the NPC resource directories expected by Jump King. |
| Different entry points can disagree about whether to compile a mixed project or preserve a compiled package. | Every available image and audio source compiles, including partial-source packages. Compiled-only resources retain their bytes. Root Workshop previews don't make a package editable; nested assets named `preview.png` still compile. |
| Native background packing can copy an old XNB over a newly compiled image. | Scans and file notifications skip companion XNBs while their editable source exists. |
| Deleting a recovered image leaves its XNB active in later builds. | Patched asset deletion archives the image and its companion XNB. Both remain recoverable. |
| Resource folders named `bin` or `temp` disappear from builds or stop refreshing. | Generated names are excluded at the project root; nested asset folders are preserved by scans, copies and file-change handling. Private metadata remains excluded at every depth. |
| Collision-strip delete and rename operations target `visual_level.xnb`. | They use `bin/level.xnb`, matching the actual compiler output. |
| Recovery can imply that every compiled asset is editable. | Recovery creates a separate copy and reports unsupported assets. Only supported uncompressed Windows XNB v5 RGBA textures and SoundEffect audio are extracted to PNG/WAV. Originals remain intact. |

See [large-map limits and dependencies](index.md#large-map-support). Recovery cannot
recreate layered artwork, C# source or original audio loop metadata.

Implementation: [CollisionPatches](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Patches/CollisionPatches.cs),
[CompilerPatches](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Patches/CompilerPatches.cs),
[BuildPlan](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Building/BuildPlan.cs),
[PackedAssets](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/PackedAssets.cs),
[SpritePlayback](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/UI/SpritePlayback.cs).

## Extension testing and Workshop workflows

These are safeguards for features added by the extension, rather than claims
that regular Worldsmith had the same workflow.

| Risk or earlier behavior | Current behavior |
| --- | --- |
| Each test launch rebuilds unchanged files into a different folder. | A project keeps a stable test path. Unchanged source and verified output reuse the checked build; a failed rebuild leaves the previous copy intact. Tests launch through Steam. |
| Installing a test mod can lose settings, create an extra active copy or stop rollback after one failure. | A unique matching Workshop installation is reused; otherwise a local mod folder is used. Settings and rollback copies are preserved, ambiguous matches are rejected, and restoration continues after individual errors. |
| An upload can use files changed since validation. | Source and output hashes are checked before upload. A newly assigned item ID triggers another check before submission. Changed files require a new checked build. |
| A failed local save after Steam creates an item can lead to another new item on retry. | The session retains the assigned ID even when persisting it fails. |
| A timeout can release an upload while Steam still owns it. | The operation remains owned until its callback completes. Check Steam's result before retrying after a lost connection. |
| Workshop metadata can arrive while a build is running, or hashing can freeze the panel. | Verification runs in the background; deferred item details are applied after the build. A later refresh does not overwrite an initialized draft. |
| An existing folder link can select the wrong publication. | Updates check ownership and project type, preserve explicit folder choices and import usable Legacy links. Conflicting links require choosing the intended item. |
| Damaged extension Workshop-library XML can discard folder links. | A valid backup can restore the library; the damaged file is preserved before the next repaired save. |

Implementation: [TestBuild](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/TestBuild.cs),
[ModInstallation](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Project/ModInstallation.cs),
[PublishSession](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Workshop/PublishSession.cs),
[Publisher](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Workshop/Publisher.cs),
[WorkshopLibrary](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Workshop/WorkshopLibrary.cs).

## Launcher, interface, news and updates

| Problem or risk | Current behavior |
| --- | --- |
| Starting through another executable breaks Worldsmith's embedded font lookup. | The launcher restores the native resource assembly, and extension controls use the editor's text style. |
| UI resource selection can depend on the system language. | The editor UI uses English while the user's number and date culture is retained. |
| Extracted downloads retain Windows Internet-zone metadata and fail to load bundled assemblies. | Launcher configuration allows the bundled managed assemblies to load in that case. |
| Automatic discovery assumes a single Steam library. | The launcher and mapping worker search secondary libraries; explicit path overrides remain available. |
| News fetching blocks startup or extension posts show stale versions and incorrect ordering. | News loads asynchronously, retains offline articles, uses release-tag changelogs and the installed bundled fallback, and orders posts by corrected dates. Developer sources and author credits remain visible. |
| An extension download is incomplete, modified or contains unsafe archive paths. | Update installation checks the expected digest, size and package layout, rejects unsafe entries and installs into a separate version folder. The previous copy remains available. |

Implementation: [Engine](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Bootstrap/Engine.cs),
[InterfaceLanguage](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Bootstrap/InterfaceLanguage.cs),
[NewsFeed](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/News/NewsFeed.cs),
[Updates](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Updates/Updates.cs).

## Generated files and diagnostics

Diagnostic logs keep three generations of at most 4 MiB each. Older oversized
logs retain their newest content. Publication history keeps three recent builds;
test history keeps one retired copy and one failed staging copy. Cleanup skips
active builds, folders less than an hour old and folders containing user saves
or replays. Current test copies and recovery backups remain available.

Implementation: [GeneratedHistory](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/src/Infrastructure/GeneratedHistory.cs).

## Verification and scope

The build runs focused checks plus contracts and regression fixtures against the
installed supported Worldsmith executable. The scrolling and audio checks use
isolated project files and verify that loading does not rewrite them. Package
checks cover the distributable ZIP and its checksum.

This reference is an inventory of corrections and protections, not a claim
that every editor bug has been found. New folder, publishing, package-browser and
Mega Mapping features are described in the [guide](index.md); their safeguards are
included here where they affect correctness or data safety.
