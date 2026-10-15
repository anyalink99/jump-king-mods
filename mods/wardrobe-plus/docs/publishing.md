# Wardrobe+ release preparation

Build and preview scripts prepare local packages. Installation and Workshop
upload are separate steps.

## Build and verify

1. Run `scripts/check-mods.ps1 -Mod wardrobe-plus -Integration` from the repository root.
2. If appearance composition changes, run `mods/wardrobe-plus/verify-compatibility.ps1`.
3. If artwork changes, prepare the distribution PNG in the private authoring workspace.
4. Inspect the final preview and `build/wardrobe-plus/UPLOAD_TO_WORKSHOP`.

The release directory contains the single self-contained `WardrobePlus.dll`
shell, README, WORKSHOP, CHANGELOG, validation notes and `workshop-preview.png`.
The build checks the preview's PNG format, 256x256 dimensions, 8-bit palette and
size below 34 KiB, as well as native shell discovery. It rejects extra DLLs.
The implementation is embedded in the shell. Don't add `*.Module.dll`, game
assemblies, JKRuntime.dll, a second Harmony DLL, tests, reference sprites or
personal settings to the upload directory.

## Workshop editor

- Title: **Wardrobe+**.
- Description: copy the Steam BBCode in `WORKSHOP.md`.
- Content directory: `build/wardrobe-plus/UPLOAD_TO_WORKSHOP`.
- Preview: `workshop-preview.png` from that release directory.
- Required item: **JK Runtime**, Workshop ID **3793086563**, API 2.0 for packages built with the current SDK. Published 2.2.1 also supports Runtime 1.44.
- A compatible shared Harmony 2 engine must also be available. The package does
  not bundle one; verify the intended dependency setup in a clean game session.
- Skins are optional user subscriptions. Don't make GoldenBoots or an example
  collection a mandatory dependency.
- Use `CHANGELOG.md` for the version notes.

## Local installation

Close Jump King and run `mods/wardrobe-plus/install.ps1` only when installation
is intended. It delegates to the shared installer, which validates the release,
detects duplicate mod locations, backs up replaced files and rolls back failures.
It updates Runtime and installed first-party packages as one coordinated release.
It doesn't overwrite `Content/WardrobePlus` settings, presets or recipes.

## Artwork sources

The release illustration is `assets/workshop-preview-master.png`.
Its character references are the native Jump King character and GiantBoots sprite,
plus GoldenBoots from [Workshop item 3162670641](https://steamcommunity.com/sharedfiles/filedetails/?id=3162670641).
The Replays cover supplied the title-style reference. The preview doesn't bundle
skin textures for use by the mod.

The distribution image uses 256 colors without dithering, with near-black cool
background noise flattened before compression. Keep the master when editing it.
The extraction and composition helpers are private; they aren't needed to build
the public source with its finished preview.
