# Worldsmith authoring

[Handbook](authoring.md) | [Large maps](large-maps.md)

Use [Worldsmith Extension](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/README.md)
to edit, build and publish from Jump King Worldsmith.
[Download the portable release](https://github.com/anyalink99/jump-king-mods/releases/tag/worldsmith-extension-v0.1.0-preview.11)
and follow its [installation guide](https://github.com/anyalink99/jump-king-mods/blob/main/worldsmith-extension/docs/index.md).

## Editor and runtime limits

| Area | Ordinary Worldsmith | With the extension |
| --- | --- | --- |
| Collision atlas | Stock conversion uses a 13 by 13 atlas: 169 slots | Checked builds use the authored count and atlas size, up to 4096 screens |
| Screen navigation | Native preview navigation has a 255 limit | Preview navigation follows the authored count |
| Side teleport destinations | RGB markers address targets 1-255 | Layout metadata accepts integer destinations within the authored count |
| Scene authoring | No dedicated editor for Mapping scenes | Scene files and assets compile during a checked build; effects and behavior trees are authored as XML |
| Workshop workflow | Native upload controls | Build, upload and update from the regular Worldsmith release |

The extension is an authoring tool. Players need **Mega Mapping Expansion and
JK Runtime** for its runtime features, including integer side links; declare
these Workshop dependencies. `expectedScreens` validates the authored count and
does not generate collision or background images. See [large maps](large-maps.md)
for topology, texture budgets and in-game checks.

## Expand a map beyond 169 screens

Keep using the existing **atlas** or **strip** source. There is no separate
expansion mode or resize command: increase the PNG's **canvas size** in your image
editor, preserving pixel scale and collision colors.

### Choose the source image

**Atlas (`level.png`):** for the standard 13-row template, keep the height at
**585 pixels** and add **60-pixel columns to the right**. Anchor existing pixels
at the left. Each 60 by 45 pixel cell is one screen. Numbers run down a column,
then continue in the next column: 1–13, 14–26, ..., 157–169, 170–182. Adding rows
would change the existing screen numbering. This layout is file packing, not the
route's direction in the game.

For `N` authored screens, use `max(13, ceil(N / 13))` columns and multiply that
count by 60 for the image width. The final column can contain unused padding.

**Strip (`visual_level.png`):** keep the width at **60 pixels** and increase the
height to `45 * N`. Add space **above** existing pixels: screen 1 is at the bottom.

| Authored screens | Editable atlas, width by height | Editable strip, width by height |
| --- | --- | --- |
| 169 | 780 by 585 | 60 by 7605 |
| 182 | 840 by 585 | 60 by 8190 |
| 200 | 960 by 585 | 60 by 9000 |
| 256 | 1200 by 585 | 60 by 11520 |

Rectangular atlas sources require Worldsmith Extension **0.1.0 Preview 9 or later**. Earlier builds
require a square `level.png`; they already support longer strips. Existing square
atlas sources remain readable. Don't change a larger square source's row count
by simply cropping it: its cells must be rearranged to preserve screen numbers.

### Save the layout and build

1. Back up the source and enlarge either PNG as described above. Draw new
   collision and add backgrounds and other screen assets.
2. Open the editable map through `WorldsmithExtension.exe`, then open
   **Build / Workshop** and set **Authored screens (1–4096)** to the actual total.
   For a 200-screen map, use `200`, not the 208 slots in a 16 by 13 source atlas.
3. Select **atlas** or **strip** under **Authoritative collision image**, then
   click **Save layout**. Only the selected image is used, even if the other
   file has a newer timestamp. This button saves settings; it doesn't resize PNGs.
4. If the scene sets nonzero `expectedScreens`, update it to the same count.
   Reopen the project to refresh previews.
5. Choose **Build checked copy**, then test the prepared output or use
   **Test in-game**. Check new screens and transitions before publishing.

### Why the compiled atlas is still square

The game calculates a screen's cell position using the texture width for both
column and row stepping. It cannot directly sample an atlas with 14 columns and
13 rows correctly. Allowing more pixels in a PNG is not sufficient.

The extension repacks the editable atlas into a supported **square** `level.xnb`
while preserving screen numbers and exact RGBA values, including magenta and alpha.
It leaves your source PNG intact. For 200 screens, a 960 by 585 source (16 by 13
cells) becomes a 900 by 675 compiled atlas (15 by 15 cells, with 25 padding slots).

`props/mega-mapping-expansion/map.xml` describes the **compiled** atlas and authored
count, not the editable PNG dimensions. The extension writes this for that example:

```xml
<MapLayout version="1" screens="200" atlasSide="15" />
```

Don't set `atlasSide` to the source column count. Increasing this value or
`expectedScreens` alone doesn't create geometry, backgrounds or transitions.
`worldsmith-extension.xml` stores `collisionSource="atlas"` or `"strip"` and stays
out of Workshop content. New compiled layouts use at least 13 by 13 cells;
explicit existing smaller layouts retain their size when the count is unchanged.

## Side links and runtime dependencies

Add side links as `screen left|right target`, one per line, then **Save layout**.
Screen IDs are one-based and must be within the authored count. For a map with
at least 256 screens, these links connect targets beyond the RGB marker range:

```text
170 right 256
256 left 170
```

RGB side markers can address only screens 1–255. Integer side links require
**Mega Mapping Expansion** and **JK Runtime** on the player's machine; declare
both as Workshop dependencies. Enlarging the source image doesn't change these
runtime requirements. See [large maps](large-maps.md) for single-link behavior,
restart checks and the compiled topology contract.

## Scene compilation

Keep `scene.xml`, includes and assets under `props/mega-mapping-expansion`.
The portable scene compiler validates them and writes `scene.mmgfx`. Python and
separate compiler tools are not needed by authors. Scene compilation reads the
installed game's dependencies; for another Steam library, put the game directory
in `game-path.txt` beside the extension launcher.

## Existing packages

Mixed maps with editable image sources open in the editor. Compiled maps open
in a package browser, where **Test in-game** runs their existing assets. **Create editable working copy** recovers supported PNG/WAV
resources into a separate folder and retains the original XNB files.

Compressed XNB and unsupported readers remain compiled. Layered artwork and
audio loop metadata cannot be reconstructed. Review the recovery report and
screen count when opening a package without layout metadata.
