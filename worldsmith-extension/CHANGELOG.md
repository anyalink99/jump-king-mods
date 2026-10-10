# Release notes

## 0.1.0 Preview 15

- Bound diagnostic logs to three 4 MiB generations, including oversized files
  left by older versions.
- Trim generated publication and test-build history while preserving active
  builds, current test copies, saves, replays and recovery backups.

## 0.1.0 Preview 14

- Keep the fixes reference focused on changed behavior and move the project-title
  loading fix to the end of the project history and loading list.

## 0.1.0 Preview 13

- Keep each scrolling background tied to its original XML file. A malformed or
  duplicate screen file no longer shifts later saves onto another screen; unreadable
  files stay intact until repaired and reloaded.
- Append audio beyond the last configured section without moving earlier sections.
  Keep screen boundaries and the newly added sound's editor binding intact.
- Clear audio metadata when loading a project, so identically named sounds don't
  inherit another project's category or accumulate duplicate settings on reload.
- Add a consolidated [fixes reference](docs/fixes.md) covering the extension's
  current corrections, safeguards and limits.

## 0.1.0 Preview 12

- Load Recent and Favorites titles containing characters such as `>` without
  treating them as folder names. Keep valid entries when another entry is invalid,
  try history backups when a file cannot be read, and preserve recovery files
  before saving the repaired list.

## 0.1.0 Preview 11

- Keep test builds at a stable path and reuse unchanged, verified content. Launch
  tests through the Steam client command line.
- Preserve Favorites membership and Workshop links when reopening projects, and
  avoid duplicate Recent entries for compiled packages.
- Wait for external editors to finish writing files before refreshing their assets.
- Keep a project's saved name when reopening it, even when its map settings still
  contain the template title or its Workshop item uses a different title.
- Load scrolling layers with an omitted animation frame list without failing with
  `Value cannot be null. Parameter name: list`.

## 0.1.0 Preview 10

- Inspect project folders in the background and reuse the result when opening the editor.
- Speed up file scans and preserve empty resource folders in native build output.
- Open maps with missing or empty optional prop and wardrobe settings without writing defaults to disk.
- Show underlying project-load errors with file context; copied reports include the full exception details.
- Release the loading state when project initialization fails so another project can be opened.

## 0.1.0 Preview 9

- Accept ordinary `level.png` sources with 13 rows and extra columns on the right. Preview and checked builds preserve screen numbers while repacking collision into the square atlas required by the game.
- Keep the selected atlas source intact during native preview regeneration and editable-copy recovery. Document how to enlarge atlas and strip sources in Worldsmith and Mega Mapping guides.

## 0.1.0 Preview 8

- Restore Worldsmith's embedded fonts when starting through the extension.
- Use the editor's text style in extension windows and added page controls.
- Organize source files by subsystem.

## 0.1.0 Preview 7

- Check source and build files in the background before publishing.
- Apply Workshop details received while a build is running.
- Use the same build rules for projects opened through the editor or Workshop.
- Retain newly created Workshop item IDs when local saving fails, so uploads can be retried.
- Recover damaged Workshop library data from its backup and preserve the damaged file.
- Continue restoring other mod files if one rollback operation fails.
- Separate publication state, native editor adapters, file installation and patch handlers.

## 0.1.0 Preview 6

- Recognize Legacy upload-folder links and open linked projects with their Workshop titles.
- Add Publish new, Open project, Open folder and Update actions to Workshop pages.
- Fill update forms with the item's title, description, tags and visibility.
- Remember replacement project folders and offer installed Workshop copies for subscribed items.
- Select existing publications by name and check the owner and project type before updating.
- Compile source folders opened from Workshop and preserve compiled release packages.

## 0.1.0 Preview 5

- Correct dates and chronological ordering for extension news, including cached posts.
- Credit extension release notes to Niotid.

## 0.1.0 Preview 4

- Load extension changelogs from the published release tag to avoid stale cached notes.
- Show the installed version's bundled changelog while newer posts are unavailable.

## 0.1.0 Preview 3

- Restore developer posts on the home page, with their source labels and full articles.
- Add Worldsmith Extension release notes alongside Worldsmith and Jump King news.
- Keep the latest posts available offline and retry loading when returning home.

## 0.1.0 Preview 2

- Test compiled maps and skins directly from the package browser.
- Open mixed projects in the editor when the required image sources are available.
- Include hidden-wall textures when deciding whether a project needs recovery.
- Check for extension updates in the background, download verified release archives,
  and restart into the selected version while retaining the previous copy.

## 0.1.0 Preview 1

### Projects and publishing

- Open project folders directly, drag them into the editor, or use Recent and Favorites.
- Build and upload/update maps, skins, sets and mods from the project page in Worldsmith.
- Identify source, compiled and mixed formats; recover supported PNG/WAV assets into a separate working copy.
- Open compiled mods directly from their release folders; build C# projects with a compatible installed SDK.
- Run checked builds in a hidden worker with progress and errors. Changes to sources or output require a new build before upload.
- Retain newly created Workshop IDs for retries and report Steam operation progress.

### Responsiveness and data safety

- Replace recursive editor-icon searches and move news fetching off the UI thread.
- Limit collision preview work to the visible screen and pause hidden animated thumbnails.
- Preserve unresolved prop placements; suppress loading-time wardrobe saves.
- Use atomic XML/image writes with rollback copies and show completed XML saves.
- Archive supported deleted assets and local projects for recovery.
- Correct MP3 compiler parameters and propagate build failures.
- Preserve empty resource folders when building, packaging and recovering projects, including the NPC folders required by Jump King.

### Distribution

- Load bundled assemblies when Windows retains Internet-zone metadata after extracting a downloaded ZIP.
- Packaged the extension as a Windows x64 ZIP.
- Loads game and editor dependencies from the installed copies.
- Includes optional large-map compilation support; see the user guide for format limits.
