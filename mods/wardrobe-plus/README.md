# Wardrobe+

[Technical documentation](docs/index.md).

Mix standalone reskins and collection entries, save appearance presets, and fit
individual sprite positions. Version **1.5.1** requires JK Runtime 1.30 (or a
compatible newer 1.x) and one shared Harmony engine.
The mod uses the game's loaded Harmony; it does not bundle another copy.

Open **Workshop > Wardrobe+**, or **Wardrobe+** in Mods settings.
There is no standalone main-menu or Pause row. Legacy menu toggles are ignored.
The former wardrobe hotkey is inactive; stored legacy chords are retained.
Left-click once to reveal the cursor. Hover and click rows, scroll lists, and
right-click to go back. Fitting has clickable X/Y adjustment buttons; the name
editor and footer commands also accept clicks.

The main screen shows the collection, stable item rows and the actual worn outfit.
Items show On, Off or Unavailable. Open an item to equip/unequip it or choose its
skin. Native slot conflicts and item effects are preserved; unavailable items
cannot be granted. Filter the list to equipped items, or search an item's skins.
From collection follows the selected collection, falling back to map artwork.
More options contains explicit Map appearance and Original game choices.

Every change takes effect immediately. Back only navigates or closes the menu.
There is no Apply button or unapplied draft. Choose another skin/material without
leaving its list. More > Restore normal appearance restores native artwork while
keeping saved outfits; editing a look enables it again.
Outfits, Save outfit and More are available from the main footer.

## Materials

Select **Character or an item > Material** for Gold, Glass, Magenta, Cosmic or
Original texture. **More > Outfit material** sets the default for the body and
all clothing. Individual overrides take priority; Use outfit material restores
inheritance. Selecting another skin keeps its material. Materials change
immediately and also persist in saved outfits, undo and exported recipes.

Gold follows the bright amber, lemon and cream ramp of GoldenBoots. Magenta
uses the saturated raspberry highlights and deep shadows of the installed red
Tunic reference, retaining the source folds with subtle fabric nap.
Glass discards the source hue and retains its luminance for the shape. Its
nearly transparent interior facets and bright edges blend normally over the map and
lower character layers, with no displacement, dispersion or scene capture.
It uses ordinary sprites in the game, preview and compatible appearance consumers.

Cosmic turns the silhouette into a window onto a black star field, with a crisp
white contour, colored spiral galaxies, drifting star layers and subtle twinkling.
The source hue is discarded and only a faint trace of luminance remains. The
field is anchored to map coordinates, not the character or its facing direction.
Adjacent screen viewports share continuous coordinates and one animation time
per game draw, including camera translation. The preview uses its own screen space.
One masked shader quad draws each visible part; there is no framebuffer capture,
readback, per-pixel draw loop or per-frame texture generation.
Two shared 512x512 layers use 2 MiB of GPU texture memory, plus each appearance's
mask atlas. Native and replay sprite draws animate; texture-flattening consumers
such as Ball King and Mega Mapping use a static cosmic fallback. Custom effect,
sorted, depth/stencil or rotated-transform passes also retain that fallback.

Materials preserve the source's shape; Glass reduces its alpha for transparency,
while Gold, Magenta and Cosmic retain the original alpha. They cannot separate
clothing painted into a body atlas. They work with each native body/item slot
and compatible Workshop art. Material edits do not alter item effects.
Version 1 and 2 settings and recipes remain readable. Writes use schema 3 to
protect complete equipment snapshots from older builds.

Existing Diamond selections load as Glass and Red velvet selections as Magenta.
The serialized `Diamond` and `RedVelvet` IDs are retained for saved settings,
presets, undo, recipes and API compatibility. Magenta's palette is unchanged.
The former refraction renderer is parked as a developer-only experiment; normal
builds do not create optical maps, capture targets or refractive sprite wrappers.

## Position fitting

Select an item, then Move to start adjusting all poses immediately. Direction
buttons move one pixel, within +/-32 pixels, immediately on the character. Done
and Back keep the position. One Undo reverses a repeated movement gesture; Redo
restores it. Ctrl+Z / Ctrl+Y work outside text entry, with menu commands in More.
The footer shows the actual bindings for Move, Flip and Reset.
More options > Pose-specific positioning contains pose selection, scope and
reset tools. Pose-specific positions override the all-pose position. Positions
mirror with facing and use padded frames so pixels are not clipped.

Adjustments belong to the resolved body, item source, exact item ID, and pose.
They do not automatically fit different anatomy or remove artwork baked into a
body texture. Item layers and their equipment conflicts remain native.

## Presets and tools

Favorite sources with the button shown beside Favorite in the appearance picker.
Button badges update when you rebind controls or switch input devices.
Outfits provides Save current look, search, Load, Replace, Rename and Delete.
Each new preset saves equipped items (including an empty outfit), collection,
individual skins, materials and position adjustments. Load restores everything
immediately. Legacy presets without equipment data retain current equipment.
Unavailable items are skipped with a message, preserving the saved preset.
Replacing and deleting an existing preset require confirmation.

Names and search use JK Runtime's shared text-entry screen: type on the physical
keyboard (Latin/Cyrillic), use caret/selection, Backspace/Delete, Ctrl+A/V,
Enter to accept or Escape to cancel. A mouse/gamepad keyboard includes Latin and
Cyrillic case switching. Typing is isolated from menu controls and shortcuts.

More options on a saved outfit contains Duplicate, Favorite, Export and map
assignment. More on the main screen contains Undo/Redo, preview controls,
randomization, recipes and diagnostics. The favorites randomizer respects locks.
Map preferences restore the full preset on entry. Recipes contain references
and Workshop links, not artwork; put them in `Content/WardrobePlus/Recipes`.
Import restores the recipe immediately without automatically subscribing to mods.

Settings, presets and recipes live in `Jump King/Content/WardrobePlus`, outside
Workshop package folders. Missing subscriptions retain their original references
and visibly fall back. Reinstalling the source restores its selection. XML writes
are atomic and retain a `.bak`; repeated fit changes coalesce, with a final flush
at gesture end or page close. Unsaved changes/errors are shown. Live preview
shares prepared textures; fitting reuses material preparation. Corrupt or newer
settings open read-only rather than being overwritten. Diagnostics can be written
from More.

Disabling Wardrobe+ restores the native selection. It preserves the mod's profiles
and does not rewrite subscribed skin/set configuration. The same selection policy
is used by the native inventory and Workshop toggles while enabled.

## Package and installation

The release package contains `WardrobePlus.dll`, this README, the Workshop
description, changelog, validation notes and the validated Workshop preview.
See [publishing instructions](docs/publishing.md) and [Steam description](WORKSHOP.md).
For a local installation, place only the release DLL in
`Jump King/Content/JKMods/` alongside the installed JK Runtime and shared Harmony,
then restart the game. Do not copy the `_INTERNAL` test/build directory. The build
scripts never perform this installation automatically. Alternatively, run
`install.ps1` to use the coordinated installer with backups and rollback; this
also updates Runtime and installed first-party packages.

Run `build-preview.ps1` to reproduce the release 256x256 indexed PNG from
`assets/workshop-preview-master.png` (requires Python and Pillow). It retains
256 colors and removes only near-black cool background noise before compression.
`workshop-preview-256.png` is the validated source for release packaging.
The build checks the preview and copies it as `workshop-preview.png` into the
release directory. The large original is retained separately.

## Build

Run `scripts/check-mods.ps1 -Mod wardrobe-plus` from the repository root.
The release shell is `build/wardrobe-plus/UPLOAD_TO_WORKSHOP/WardrobePlus.dll`.
Builds do not install or upload anything. No game assemblies, Runtime DLL or
separate implementation DLL should be included in the Workshop package.

Normal builds compile and embed `assets/Cosmic.fx` and the two Cosmic PNG layers.
The deterministic art generator is `tools/build-cosmic.py` (Python and Pillow);
it produces seamless toroidal galaxy and star layers, never generated per frame.
To exercise the parked refraction code,
run `mods/wardrobe-plus/build.ps1 -Graphics -ExperimentalRefraction`. This defines
`WARDROBE_REFRACTION`, restores the old behavior for the Glass slot, and runs its
retained GPU fixtures. This experimental build replaces the local build output;
rebuild without the switch before installing or publishing a normal package.
There is no in-game refraction setting.

`build-effect.ps1` uses the compatible MonoGame 3.7.1 effect compiler. Only
experimental builds additionally compile and embed `assets/Crystal.fx`.
On first use it downloads the pinned official SDK archive, verifies its SHA-256,
and extracts it into the internal build cache with 7-Zip; it does not install
the SDK. Alternatively, pass `-EffectCompiler` to `build.ps1` with an existing
3.7.1 `2MGFX.exe`. The SDK and compiler are build dependencies only.

For native GPU/preview tests, use
`scripts/check-mods.ps1 -Mod wardrobe-plus -Integration`
or `mods/wardrobe-plus/build.ps1 -Graphics`. Graphics tests use the
installed game assemblies and atlases in an isolated fixture; they never launch
the game or write its saves. Images are retained under
`build/wardrobe-plus/_INTERNAL/tests/<run-id>`.

Run `mods/wardrobe-plus/verify-compatibility.ps1` to freshly build Ball King,
Replays, Mega Mapping Expansion and Smooth Camera, then exercise their real
appearance consumers and camera hook against fitted sprites. Direct `build.ps1 -Compatibility` uses
existing consumer builds and is intended for iteration after that initial run.
Every build also checks native shell discovery before Runtime loading and
idempotent module registration after Runtime loading.

## Compatibility and scope

Use the updated Ball King build from this repository with fitting: its outfit
compositor respects each layer's anchor and padded frame size. Replays uses
the currently installed appearance and its recorded equipment; recipes do not
add historical texture versions to replay files. Mega Mapping Expansion's
flattened player appearance uses the same fitted positions for its effects.

The mod publishes new layers inside existing native animation wrappers. Consumers
that inspect native layers continue to see updated sprites. Advanced consumers
may discover `WardrobePlus.AppearanceEvents` in the loaded implementation:
`Revision` increments and `Changed` fires after a successful publication. An
observer exception does not undo other observers or the committed appearance.

Offsets are manual translations, not automatic anatomy fitting. There is no
cross-item transmog, arbitrary color picker, equipment hiding, or automatic Workshop download.
Unknown sprite replacement mods require their own compatibility check.
See [compatibility and testing](docs/validation.md) for reproducible checks and in-game QA.
