# Worldsmith Extension guide

[Download and installation](../README.md#install) · [Fixes](fixes.md) · [Changelog](../CHANGELOG.md)

## Open a project

Choose **Open project...** on the home page and select the project folder.
You can also drop a folder into the window or select one from Recent or Favorites.

Maps with editable image sources open in the editor. A folder with only compiled
assets opens in the package browser. Mixed projects open in the editor when the
needed page images are present. The project summary tells you which format was
found and what can be edited.

To open a prepared mod, select the release folder containing its DLL and resources.
To compile a C# mod, open a folder with one root `.csproj`. This requires a compatible
.NET SDK and the project's dependencies. For projects with their own build scripts,
open the prepared release folder.

## Test in game

Close Jump King, then choose **Test in-game**. The button is available in both the
editor and the package browser. Source projects are built into a separate test
folder. Mixed projects compile every available image and audio source and keep
compiled-only resources. This also applies in the package browser when some editor
pages don't have editable images. Packages without sources keep their compiled
assets. The same build rules apply when publishing through project details or Workshop.

Each project keeps the same test folder across launches. If neither the project nor
the built files changed, testing reuses the checked build. A failed rebuild leaves
the previous copy intact.

Generated build history is bounded: publication builds keep three recent copies,
while test preparation keeps one retired build and one failed staging directory.
Cleanup runs during later builds and leaves work less than an hour old alone.
Open publication panels and active builds hold a lock that prevents cleanup.
The current test build for each project stays available. Folders containing saves,
replays or a `.keep` file are preserved, as are project and installation backups.

`%LOCALAPPDATA%/WorldsmithExtension/extension.log` keeps three generations of up
to 4 MiB each. On the next log write, oversized logs from older versions are
reduced to their newest 4 MiB before rotation.

If a build fails, the error dialog includes the worker's reported cause and exit
code. **Copy to clipboard** also includes recent compiler output and the worker
command. The same output is recorded in `extension.log`; share the report or log
when asking for help. A screenshot of the general failure message alone doesn't
identify the failed asset or missing dependency.

Empty resource folders don't have to be present in a source map. Checked builds
and package copies create the game's resource directories in the output and leave
the source alone. Root `bin`, `obj`, `temp`, `saves` and `savesperma` folders stay out
of the package; resource subfolders with those names are kept. Private `.git`, `.vs`
and `.worldsmith-extension` folders stay excluded at every depth. Linked project
paths and linked source assets aren't supported; use a normal local working copy.

For mods, testing updates an existing Workshop installation when exactly one
matching copy exists. Otherwise the mod gets its own folder under
`Content/JKMods`. Settings stay in place, and replaced files are backed up under
`%LOCALAPPDATA%/WorldsmithExtension/test-backups`. If several installed copies
match, remove the duplicates first. Install the mod's dependencies as usual.

## Publish to Steam Workshop

The extension publishes maps, skins, sets and mods from the regular Worldsmith
release. Legacy isn't needed for uploads or updates.

### Publish a new item

Choose **Publish new...** in the Workshop section and select a project or release
folder. You can also open **Build / Workshop** from an already open project.
Map and skin names come from their settings; mod packages can use the heading in
`WORKSHOP.md` or `README.md`. Review the title, description, tags and preview.
New items default to Private.

Choose **Build checked copy**, then **Publish new item**. The panel shows whether
the folder will compile its sources or keep a compiled package. Editing the source or build output
requires another build before uploading. The editor checks both the project and
the prepared files in the background before submitting to Steam.

### Update an existing item

Open the appropriate Workshop category and select your item. Its linked folder
appears below the list. **Open project** opens it for editing or package browsing;
**Open folder** opens it in Explorer. **Update...** opens the publishing panel with
the item's title, description, tags and visibility already filled in. Build the
checked copy, review the fields and choose **Update item**. Leaving the preview
field empty keeps the existing Workshop image.

Use **Choose folder...** when a project has moved or you want to publish from a
new build folder. Worldsmith remembers this choice. **Open installed copy** is
available for subscribed items installed by Steam; it opens that downloaded
package. The pencil beside an item edits its Workshop details without a file build.

Existing folder links from Legacy's `cached_saved_folders.set` are read from the
editor folder and Steam's Worldsmith installation folders. Recent projects and
previous extension uploads are also recognized. A linked `UPLOAD_TO_WORKSHOP`
folder opens with its Workshop name. If a folder has conflicting links, choose
the intended item in Workshop and select the folder again.

For an open folder without a link, **Choose existing item...** selects one of your
publications by name. The selected item must match the folder's type. Publication
IDs are saved with the folder link and in `.worldsmith-extension/workshop.xml`;
a failed upload can be retried against the same item.

Wait for Steam's result before starting another upload. After a lost connection,
check the Workshop item before retrying. Steam may require you to accept its
Workshop agreement before an item becomes visible.

## Recover editable assets

In the package browser, choose **Create editable working copy...** and select a
parent folder. The extension creates a new project folder, keeps the compiled
files and extracts supported images and sounds. The original package is unchanged.

Recovery supports uncompressed Windows XNB v5 RGBA textures and SoundEffect audio.
These become PNG and WAV files. Existing source files are kept. A report in the
new folder lists anything that could not be recovered.

Compressed textures, effects and other unsupported XNB readers remain compiled.
Original layered artwork, C# source and audio loop metadata can't be recovered.
Rebuilding recovered audio may change its encoding or loops. When a map has no
layout metadata, check its screen count: the collision atlas may include unused cells.

## Saving and backups

The bottom status line names the file after a completed save. XML and image
replacements keep the previous file as `.worldsmith-backup`.

Deleted assets go into a `.worldsmith-extension/deleted` folder within their owning
folder. **Archive local project** moves the whole project to a sibling
`.worldsmith-archives` folder. The status line shows the location. Open that folder
to recover the project; its Workshop item is kept.

Props with missing settings or textures remain in the map and appear as placeholders.
Repair their assets to restore the preview. Malformed prop XML blocks prop saves
until repaired and reopened. Loading wardrobe settings doesn't save defaults over
the existing configuration.

## News

The home page shows the latest post from Worldsmith, Jump King and Worldsmith
Extension. Select a post to read it. Extension posts contain the release changelog;
previously loaded articles remain available offline.

## Extension updates

The home page shows the installed version and **Extension updates**. The extension
checks GitHub at startup. Open this panel to check manually or turn automatic checks
off. Preview versions receive newer previews and stable releases; stable versions
receive stable releases.

Choose **Download update**, then **Restart in updated version** when finished editing.
The download is verified and installed in a separate folder. Your launcher uses the
selected newer version on later launches. The previous copy remains available.

Moving from Preview 15 or older to Preview 16 needs one manual download of the
complete ZIP. Extract it into a new folder and start its `WorldsmithExtension.exe`.
Older updaters don't accept the added `WorldsmithExtension.LegacyCollision.dll`;
keep that file beside the engine in the new package. To return to an earlier
copy, run its `WorldsmithExtension.exe --ignore-updates`.

## Large-map support

For [Mega Mapping Expansion](https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/README.md)
maps, **Build / Workshop** includes screen count, collision layout and side-link
settings. The bundled compiler also builds XML scenes and their assets.
See the [mapping guide](https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/docs/getting-started/worldsmith.md)
for formats, limits and required game mods.

### Add screens beyond 13 by 13

This section describes the original `level.png` / `visual_level.png` workflow.
For full-resolution collision or large maps compiled with ordinary Worldsmith,
use [level+](#pixel-collision-with-level).

Without a saved collision-source choice, the extension follows regular Worldsmith:
the newer image wins when both exist, and equal timestamps prefer `visual_level.png`.
**Save layout** makes the chosen atlas or strip authoritative, regardless of later
timestamp changes. Version 2 uses the source declared in `mapping_settings.xml`.

The two existing collision sources grow in different directions. There is no
special expansion mode or resize command in Worldsmith; edit the PNG's **canvas
size** in an image editor, keeping its pixels at their original scale.

- **atlas** uses `level.png`. For the standard 13-row template, keep the height at
  **585 pixels** and add **60-pixel columns on the right**. Keep old pixels
  anchored at the left. Every column adds 13 screen slots.
- **strip** uses `visual_level.png`. Keep the width at **60 pixels** and extend
  the canvas **upwards** to `45 * N` pixels high for `N` screens. Keep old pixels
  at the bottom, where screen 1 lives.

Smaller square atlases and rectangles with another row count also work. Their
width must be a multiple of 60, their height a multiple of 45, and their capacity
must cover the authored screen count. The checked build packs those cells into
the square game atlas in the same column-first order.

The atlas numbers screens down each column, then continues in the next column:

```text
column 1   column 2   ...   column 13   column 14
    1         14               157         170
    2         15               158         171
   ...        ...              ...         ...
   13         26               169         182
```

This is file packing, not the route's direction in the game. For `N` screens,
a 13-row atlas needs `max(13, ceil(N / 13))` columns. Unused cells are padding,
not additional authored screens.

| Authored screens | `level.png`, width by height | `visual_level.png`, width by height |
| --- | --- | --- |
| 169 | 780 by 585 | 60 by 7605 |
| 182 | 840 by 585 | 60 by 8190 |
| 200 | 960 by 585 | 60 by 9000 |
| 256 | 1200 by 585 | 60 by 11520 |

To enlarge a map to 200 screens:

1. Back up your collision source. Enlarge **one** of the images to its dimensions
   above and draw the new collision. Add backgrounds and other assets for the
   new screens as needed.
2. Open the editable project through `WorldsmithExtension.exe`. In **Build /
   Workshop**, set **Authored screens (1–4096)** to `200`.
3. Under **Authoritative collision image**, select **atlas** for `level.png` or
   **strip** for `visual_level.png`, then click **Save layout**. Only that source
   is used; timestamps don't switch it. Saving settings doesn't resize images.
4. Reopen the project to refresh previews.
5. Choose **Build checked copy** or **Test in-game**. Test new screens and
   transitions before publishing.

Rectangular atlas sources require **0.1.0 Preview 9 or later**.
Earlier builds require a square `level.png`; a longer strip also
works in those builds. Existing square atlas sources remain supported.

The game does **not** correctly sample a rectangular collision atlas directly:
it uses the number of columns when stepping between screen rows. The checked build
therefore repacks your editable PNG into a **square** `level.xnb`, preserving screen
numbers and exact RGBA collision values. A 200-screen source of 960 by 585 becomes
900 by 675 (15 by 15 cells) in the game. The source PNG is left intact.

New layouts store `<MapLayout version="2" source="native" screens="200" />`
inside `mapping_settings.xml`. The game reads the square atlas capacity from
`level.xnb`; the authoring source choice stays in `worldsmith-extension.xml`,
which is excluded from Workshop output. Metadata doesn't create geometry or
backgrounds. Move old `map.xml` layouts into the current settings format before
building with MME 1.0; the extension doesn't read that old layout file.

For integer side links beyond screen 255, follow the
[Mapping layout guide](https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/docs/getting-started/worldsmith.md#side-links-and-runtime-dependencies)
and declare **Mega Mapping Expansion** and **JK Runtime** as Workshop dependencies.

## Screen transitions

Open **Transitions...** in a level's project actions to edit
`screens/mega-mapping-expansion/transitions.xml`. Use **Add random pool example**
for a starting point, set your screen numbers, then **Validate** and **Save**.
The previous file remains as `.worldsmith-backup`.

Pools support random choices with or without repeats, weighted choices, shuffled
bags and sequences. Bind a pool to specific exits; reference the same pool ID
to share history. Complete schemes change several links together, with touch,
screen-entry or scene-event rerolls. These remain screen-edge transitions.

Select a pool and **Preview 30 selections** to inspect its stream without
changing game state. A fixed XML seed overrides the preview seed. For held
attempt/event choices, each preview line represents a selection, not another
crossing. Layout saves and builds preserve the complete definition.

Players need **MME 1.0+ and JK Runtime 2.1.0+**. Extension is optional for
playing and ordinary Worldsmith compilation. Read the
[screen teleports guide](https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/docs/teleports.md)
for complete examples, native one-link behavior, triggers, saves and limits.

## Pixel collision with level+

Use Mapping's `MapLayout version="2"` to paint the entire map in
one `level+.png`, with up to 480 by 360 collision pixels per screen. A 13 by 13
atlas is 6240 by 4680 pixels; 20 by 13 is 9600 by 4680 and holds 260 screens.
Numbers run down a column, then right. Screen 1 is at the bottom of a strip.

The `level+` filename and alternate source workflow originated in **Zebra's
More Block Sizes**. Players need **Mega Mapping Expansion 1.0+**
and **JK Runtime 2.1+** for the current features. More Block Sizes is optional for that format;
legacy level+ maps retain its original behavior.

Individual screens can use a coarser logical resolution inside the same image.
For example, enter `1 60x45` in **Optional screen resolutions** and enlarge that
screen's old artwork 8 times with nearest-neighbour scaling. Its 8 by 8 blocks
and slopes keep their size while other 480 by 360 cells remain pixel-precise.
Legacy square level+ atlases and their `sizes.png` offsets also have a preview;
they still compile and run through More Block Sizes.

In **Build / Workshop**, select **level+ atlas** or **level+ strip**, set the
screen count, grid and pixels per screen, then **Save layout**. Use **Create
blank level+.png** only when starting an empty collision image; it refuses to
overwrite an existing PNG. Reopen the project to refresh previews. Saving never
resizes your image. **Build checked copy** validates the source and compiled
pixels, preserves the grid and writes the required native scaffold.

Keep normal project files, but edit collision only in `level+.png`. The playable
map uses `level+.xnb` and `mapping_settings.xml`. Migrate old MME layouts to this
settings file first. Ordinary Worldsmith can compile these too, although its
collision preview remains stock.

## MME authoring files

MME 1.0 uses `mapping_settings.xml` for components, recipes, graphs, bindings,
effects and control trees. Edge links live in
`screens/mega-mapping-expansion/transitions.xml`. Saving a layout preserves the
other settings sections. Checked builds validate the scene and compile its
optional construction cache; PNG assets stay ordinary map resources.

See the [MME file guide](https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/docs/getting-started/files.md)
for paths, roots and examples. MME 1.0 doesn't load pre-1.0 `scene.xml`, `map.xml`
or separate screen, prop, particle and GUI scene definitions. Port their behavior
to components and recipes before updating a map's runtime dependency.
Ordinary Worldsmith can copy the new XML paths and compile scene textures too.

Follow the [complete pixel collision guide](https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/docs/collision.md)
for XML examples, colour codes, conversion precautions, custom blocks and limits.

## Installation paths

If the launcher can't find Worldsmith, select `JKWorldsmith.exe` when prompted.
You can also put its directory in `worldsmith-path.txt` beside the launcher, or use
`WorldsmithExtension.exe --worldsmith "EDITOR DIRECTORY"`.
The [development guide](development.md#supported-worldsmith-build) identifies the
supported executable. Legacy can't host the extension.

For scene compilation with Jump King in another Steam library, put the game
directory in `game-path.txt` beside the launcher.

Worldsmith keeps its own settings in its existing directory. Extension logs, test
builds, updates and rollback copies are under `%LOCALAPPDATA%/WorldsmithExtension`.
Projects stay in their original folders. Launch the original Worldsmith to use it
without the extension.
