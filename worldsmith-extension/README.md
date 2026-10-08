# Worldsmith Extension

Worldsmith Extension adds project folders, one-click in-game testing and Steam
Workshop uploads for maps, skins, sets and mods. It works with the regular
Worldsmith release, so publishing no longer requires switching to Legacy.

It also speeds up startup and previews, fixes MP3 compilation, keeps unknown
props intact and backs up assets before replacing or deleting them.

[Download 0.1.0 Preview 15](https://github.com/anyalink99/jump-king-mods/releases/tag/worldsmith-extension-v0.1.0-preview.15)
[User guide](docs/index.md) | [Fixes](docs/fixes.md) | [Changelog](CHANGELOG.md)

## Install

1. Install Jump King and Jump King Worldsmith through Steam, then start Steam.
2. Download the ZIP above and extract it into a folder you can write to.
3. Run `WorldsmithExtension.exe`. If prompted, select the installed `JKWorldsmith.exe`.

Requires Windows x64 and .NET Framework 4.8. Keep the extracted files together.
Use `WorldsmithExtension.exe` whenever you want to run the extended editor.

## Using the editor

Choose **Open project...** on the home page or drag a project folder into the
window. **Recent** and **Favorites** make it easy to reopen projects.

Source projects open for editing. You can test or publish a compiled package as
it is, or copy it to a separate folder to recover editable images and audio.
**Test in-game** starts the map in Jump King. **Build / Workshop** in project
details prepares an upload or updates an existing Workshop item. The Workshop
pages also open your items' linked folders and update their files. Existing folder
links from Legacy are recognized automatically.

**Extension updates** on the home page downloads new releases and lets you restart
when ready. Upgrading from Preview 1 requires downloading the current ZIP once.

See the [guide](docs/index.md) for mod projects, recovery limits and
[large-map support](docs/index.md#large-map-support).
For maps beyond the standard 13 by 13 template, enlarge the collision source
and set its authored count in **Build / Workshop**. See
[map expansion](docs/index.md#add-screens-beyond-13-by-13) for atlas and strip
dimensions, screen numbering and the compiled format.
[Development](docs/development.md) covers building from source and the supported
Worldsmith version.
