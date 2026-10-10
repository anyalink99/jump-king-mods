# Wardrobe+

Mix skins and individual collection items into one outfit. Save looks, choose
materials and fit each sprite layer without changing its equipment effects.

## Install and open

Published version **2.2.1** needs [JK Runtime 1.44+](../jk-runtime/README.md),
including Runtime 2.0, and one shared Harmony engine already loaded by the game.
Packages rebuilt with the current SDK need Runtime 2.0+.
For a manual install, close Jump King and place the release `WardrobePlus.dll`
in `Jump King/Content/JKMods/`, with Runtime installed separately. Keep one active copy.

Open **Workshop > Wardrobe+** or choose Wardrobe+ in Mods settings. It doesn't
add a root main/pause-menu row. Left-click once to reveal the cursor, click rows,
scroll lists and right-click to go back. The old hotkey is inactive.

## Choose an outfit

Open a character or item to choose its source, skin or material. Owned items
show On, Off or Unavailable; native slot conflicts and equipment effects remain.
**From collection** follows the chosen collection and falls back to map art.
**Map appearance** and **Original game** are explicit alternatives.

Edits apply immediately. Back keeps them; there is no Apply button.
**More > Restore normal appearance** restores native artwork while keeping
saved outfits. Editing a look enables it again. Read
[outfit controls](docs/outfits.md) for the full workflow.

## Materials

Choose Gold, Glass, Magenta, Cosmic or Original texture on a character or item.
**More > Outfit material** supplies the default; individual overrides win.
These change appearance, not item effects. Materials can't separate clothing
painted into the body. Some flattened appearance consumers use a static fallback.
See [materials and compatibility](docs/outfits.md#materials).

## Position fitting

Choose an item, then **Move** to shift its poses in one-pixel steps, within ±32
pixels. Back and Done keep the fit. Pose-specific positioning can override the
all-pose offset. Ctrl+Z/Ctrl+Y and More's Undo/Redo reverse edits.
See [position fitting](docs/outfits.md#position-fitting) for scope and limits.

## Presets and files

**Outfits** saves, loads, replaces, renames and deletes looks. Presets include
equipment, sources, materials and fitting. Missing items are skipped with a
message; their references stay saved. Recipes share references and Workshop
links rather than artwork, and importing doesn't subscribe automatically.

Data lives in `Jump King/Content/WardrobePlus`, outside Workshop folders.
Atomic saves retain backups. Corrupt or newer settings open read-only.
Read [presets and storage](docs/outfits.md#presets-and-tools).

## Advanced Workshop skins

Skin packages can add animation, particles, sounds and shake through
`wardrobe/skin.json`. Existing simple skins still work. **More > Animations &
effects** controls those effects independently. Authors can start with
[skin authoring](docs/advanced-skins.md) and the
[Ashen King example](examples/ashen-king/README.md).

## Compatibility and scope

Use an updated Ball King for fitted sprite anchors. Replays uses recorded
equipment and currently installed artwork; it doesn't archive old textures.
Unknown sprite replacement mods need a compatibility check. See
[compatibility](docs/outfits.md#compatibility-and-scope) and
[validation](docs/validation.md).

## Package and installation

The release DLL embeds its implementation. Don't install test DLLs or another
Runtime from `_INTERNAL`. The coordinated `install.ps1` preserves settings and
rollback backups; close the game before using it. See
[installation and release preparation](docs/publishing.md).

## Build

Run `scripts/check-mods.ps1 -Mod wardrobe-plus` from the repository root.
The package is `build/wardrobe-plus/UPLOAD_TO_WORKSHOP/`. Read
[build and test instructions](docs/building.md) for effects and compatibility
fixtures. The [guide index](docs/index.md) links authoring and maintenance.
