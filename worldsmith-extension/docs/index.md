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
folder. Mixed projects with the resources needed for editing compile their sources;
packages without those resources keep their compiled assets. The same build rules
apply when publishing through project details or Workshop.

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

Preview 1 needs one manual upgrade to obtain the updater. To return to an earlier
copy, run its `WorldsmithExtension.exe --ignore-updates`.

## Large-map support

For [Mega Mapping Expansion](https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/README.md)
maps, **Build / Workshop** includes screen count, collision layout and side-link
settings. The bundled compiler also builds XML scenes and their assets.
See the [mapping guide](https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/docs/worldsmith.md)
for formats, limits and required game mods.

### Add screens beyond 13 by 13

The two existing collision sources grow in different directions. There is no
special expansion mode or resize command in Worldsmith; edit the PNG's **canvas
size** in an image editor, keeping its pixels at their original scale.

- **atlas** uses `level.png`. For the standard 13-row template, keep the height at
  **585 pixels** and add **60-pixel columns on the right**. Keep old pixels
  anchored at the left. Every column adds 13 screen slots.
- **strip** uses `visual_level.png`. Keep the width at **60 pixels** and extend
  the canvas **upwards** to `45 * N` pixels high for `N` screens. Keep old pixels
  at the bottom, where screen 1 lives.

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
4. If your Mapping scene has a nonzero `expectedScreens`, also set it to `200`.
   Reopen the project to refresh previews.
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

`props/mega-mapping-expansion/map.xml` describes the **compiled** atlas, so its
`atlasSide` is `15`, not the editable width of 16 columns. The source choice is
stored in `worldsmith-extension.xml`, which is excluded from Workshop output.
Neither this metadata nor `expectedScreens` creates geometry or backgrounds.

For integer side links beyond screen 255, follow the
[Mapping layout guide](https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/docs/worldsmith.md#side-links-and-runtime-dependencies)
and declare **Mega Mapping Expansion** and **JK Runtime** as Workshop dependencies.

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
