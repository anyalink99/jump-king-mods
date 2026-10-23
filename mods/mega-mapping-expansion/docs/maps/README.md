# Platforms, screens and routes

[MME handbook](../README.md)

Keep using your normal `level.png` workflow. MME scenes, sound and screen links
don't require a different collision image.

## Your usual level.png grid

Zebra's template generator makes a square grid of **60 by 45 pixel cells**.
Each cell is one game screen. Number them down the first column, then down the
next. For a 13 by 13 template, the first few columns look like this:

| Row | Column 1 | Column 2 | Column 3 | … |
| --- | --- | --- | --- | --- |
| First | 1 | 14 | 27 | … |
| Second | 2 | 15 | 28 | … |
| … | … | … | … | … |
| Last | 13 | 26 | 39 | … |

The whole image is **780 by 585 pixels** and has room for **169 screens**.
That is capacity: a map using screens 1–40 still has 40 authored screens.
Other template sizes work the same way: an `m` by `m` grid is `60 × m` pixels
wide, `45 × m` pixels high, with room for `m × m` screens.

The generator's numbers and coloured borders help you find cells while drawing.
They aren't MME settings. Keep them on a separate guide layer if you plan to
use `level+`; its collision loader rejects colours it doesn't recognize.

## Pick what you want to change

| I want to… | Start here |
| --- | --- |
| Use my existing grid as MME collision | [Switch to level+](collision.md) |
| Make platforms smaller than the usual 8-pixel blocks | [Increase collision detail](finer-collision.md) |
| Add screens or make the grid larger | [Add screens](large-maps.md) |
| Leave one screen and arrive at another | [Connect two screens](teleports.md) |
| Choose a different room each time | [Choose a random room](random-rooms.md) |
| Give a modded block a triangular shape | [Make a sloped block](slopes.md) |
| Tell players that the map needs MME | [Require MME](map-policies.md) |
| Move an existing challenge to another screen number | [Move a room](physics-frames.md) |

## Before trying an example

Use a copy of a map that already launches. Keep its complete grid, spawn, art
and other map files. You can test a change on one screen without reducing the
whole map to one screen.

Enable **Mega Mapping Expansion** and **JK Runtime** in the game. See
[setup](../getting-started/README.md) if you haven't used MME yet.

The **map folder** below means the folder containing `level_settings.xml`.
Edit the source map, build it with your map editor, then open the built copy in
Jump King. After changing collision or layout, restart the map. MME's scene
reload button only refreshes scenes.

## Looking for a particular setting?

[Layout fields](../reference/map-layout.md) ·
[Collision colours and settings](../reference/collision.md) ·
[All route options](../reference/transitions.md)
