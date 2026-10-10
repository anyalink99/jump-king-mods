# Outfits, materials and fitting

Open **Workshop > Wardrobe+**, or Wardrobe+ in Mods settings. Changes apply
immediately; Back keeps them. Start with the [README](../README.md) to install
and select a first outfit.

## Choose a source

Open the character or an item to choose its source and appearance. From collection
follows the chosen collection; Map appearance and Original game choose those
assets explicitly. Owned equipment can be On, Off or Unavailable. Changing
appearance keeps native equipment effects and slot conflicts.

## Materials

Select **Character or an item > Material** for Gold, Glass, Magenta, Cosmic or
Original texture. **More > Outfit material** sets the default for the body and
all clothing. Individual overrides take priority; Use outfit material restores
inheritance. Selecting another skin keeps its material. Advanced collections
use the same material choice on their animated frames; Original texture restores
the collection shader, including the visor or water. Custom animation timing
and effect profiles remain independent. Materials change
immediately and also persist in saved outfits, undo and exported recipes.

Materials keep the source's shape; Glass reduces its alpha for transparency,
while Gold, Magenta and Cosmic keep the original alpha. They can't separate
clothing painted into a body atlas. They work with each native body/item slot
and compatible Workshop art. Material edits don't alter item effects.
Version 1 and 2 settings and recipes remain readable. Writes use schema 3 to
protect complete equipment snapshots from older builds.

Existing Diamond selections load as Glass and Red velvet selections as Magenta.
The serialized `Diamond` and `RedVelvet` IDs are retained for saved settings,
presets, undo, recipes and API compatibility. Magenta's palette is unchanged.
The former refraction renderer is parked as a developer-only experiment; normal
builds don't create optical maps, capture targets or refractive sprite wrappers.
See [material rendering](building.md#material-rendering) for mask ownership,
Cosmic drawing and fallback details.

## Position fitting

Select an item, then Move to start adjusting all poses immediately. Direction
buttons move one pixel, within +/-32 pixels, immediately on the character. Done
and Back keep the position. One Undo reverses a repeated movement gesture; Redo
restores it. Ctrl+Z / Ctrl+Y work outside text entry, with menu commands in More.
The footer shows the actual bindings for Move, Flip and Reset.
More options > Pose-specific positioning contains pose selection, scope and
reset tools. Pose-specific positions override the all-pose position. Positions
mirror with facing and use padded frames so pixels aren't clipped.

Adjustments belong to the resolved body, item source, exact item ID, and pose.
They don't automatically fit different anatomy or remove artwork baked into a
body texture. Item layers and their equipment conflicts remain native.

## Presets and tools

Favorite sources with the button shown beside Favorite in the appearance picker.
Button badges update when you rebind controls or switch input devices.
Outfits provides Save current look, search, Load, Replace, Rename and Delete.
Each new preset saves equipped items (including an empty outfit), collection,
individual skins, materials and position adjustments. Load restores everything
immediately. Legacy presets without equipment data keep current equipment.
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
Workshop package folders. Missing subscriptions keep their original references
and visibly fall back. Reinstalling the source restores its selection. XML writes
are atomic and keep a `.bak`; repeated fit changes coalesce, with a final flush
at gesture end or page close. Unsaved changes/errors are shown. Live preview
shares prepared textures; fitting reuses material preparation. Corrupt or newer
settings open read-only rather than being overwritten. Diagnostics can be written
from More.

Disabling Wardrobe+ restores the native selection. It keeps the mod's profiles
and doesn't rewrite subscribed skin/set configuration. The same selection policy
is used by the native inventory and Workshop toggles while enabled.

## Advanced Workshop skins

Skin and Set packages can include `wardrobe/skin.json` alongside their native
XML and XNB fallback. Wardrobe+ supplies state animations, frame events,
attachments with inertia, masked materials, particles, WAV audio and character
camera shake. Packages contain data and assets; they don't need their own DLL.

Open **More > Animations & effects** to choose animations, particles, surface
sounds, equipment sounds and shake independently. Automatic follows the outfit;
Native keeps the game's feedback. Snow, ice, water, sand, charge, impact speed
and equipped items can select different rules. Heavy-boots mechanics stay native.
Particle density, custom audio volume and character shake have separate controls.

**Author preview** simulates surfaces, heavy boots, charge and impact speed, with
state selection, event buttons and single-tick stepping. Audio is opt-in there.
Reload resources reads edited JSON, PNG, WAV and compiled effects. Invalid reloads
keep the last complete appearance; diagnostics show matching rules and errors.

[Create a skin](advanced-skins.md), or build the [Ashen King example](../examples/ashen-king/README.md).
The [Eclipse King example](../examples/eclipse-king/README.md) demonstrates a solar
visor with charge fill, reflected light and event-driven cooling across poses.
The [Vessel King example](../examples/vessel-king/README.md) contains inertial water
inside a glass silhouette, with draining and droplets on splat. Cosmetic particles
use JK Runtime's shared pool, prepared collision index and ordered drawing.
Local packages go in `Content/WardrobePlus/Skins/<package>/` and also appear in
the native Workshop Collections or Skins list with previews and selection controls. Existing simple
Workshop skins continue to work. Outfits keep missing package references.
Replays 2.3+ records discrete cosmetic events and supplies independent animation
clocks for playback and silent ghosts. Flattening consumers use the native atlas.

## Compatibility and scope

Use the updated Ball King build from this repository with fitting: its outfit
compositor respects each layer's anchor and padded frame size. Replays uses
the currently installed appearance and its recorded equipment; recipes don't
add historical texture versions to replay files. Mega Mapping Expansion's
flattened player appearance uses the same fitted positions for its effects.

The mod publishes new layers inside existing native animation wrappers. Consumers
that inspect native layers continue to see updated sprites. Advanced consumers
may discover `WardrobePlus.AppearanceEvents` in the loaded implementation:
`Revision` increments and `Changed` fires after a successful publication. An
observer exception doesn't undo other observers or the committed appearance.

Offsets are manual translations, not automatic anatomy fitting. There is no
cross-item transmog, arbitrary color picker, equipment hiding, or automatic Workshop download.
Unknown sprite replacement mods require their own compatibility check.
See [compatibility and testing](validation.md) for reproducible checks and in-game QA.
