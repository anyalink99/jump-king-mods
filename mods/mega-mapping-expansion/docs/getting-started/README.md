# Install MME and prepare a test map

[Handbook](../README.md) / [Getting started](README.md)

You need Jump King, a working map and two mods: Mega Mapping Expansion and JK Runtime. You can use MME without writing C# or installing an MME compiler.

## Before you start

This handbook describes **MME 1.4.0 with JK Runtime 2.2.1 or later**. Check the versions shown by your installed mods when a field from the guide is rejected. Worldsmith Extension is optional. Smooth Camera 0.13.0+ is needed only for smooth camera travel; cuts work without it.

## Install the mods

1. Subscribe to **Mega Mapping Expansion** and **JK Runtime** in Jump King's Steam Workshop.
2. Wait for Steam to finish downloading, then start Jump King with both mods enabled.
3. Check that the MME menu includes its enable checkbox and **Mapping inspector**.

If your map uses blocks from another mod, subscribe to that mod too.

## Prepare a map for learning

Use a separate copy of an ordinary map that already loads, has a spawn and collision, and lets you jump on screen 1. Keep its native resources, Babe and ending settings. MME's lesson folders add scenes; they don't contain a complete native level.

Keep your normal `level.png` grid, including its unused cells. A lesson using
screen 1 doesn't mean the map must contain only one screen. MME scenes don't
require switching to `level+`; see [the map guides](../maps/README.md) for collision changes.

If you don't have a map yet, first create a normal map in Worldsmith and make sure it launches. The MME lessons start after that baseline. Don't try to launch a folder containing only `mapping_settings.xml`.

Call your editable copy `My MME Test`. Keep it outside the Steam download folder so a mod/map update won't replace your work. Inside it, find `level_settings.xml`:

```text
My MME Test/
  level_settings.xml
  level.png or level.xnb
  screens/
  props/
  ...the rest of your working native map
```

The **map root** means this directory. The **source** is the editable map you keep. A **playable build** is the copy your editor creates for the game. A file edited in source only reaches the game after a build or an explicit copy to that playable build.

## Choose your first task

- For moving or reactive scenery, follow [your first image](first-scene.md), then the [seven lessons](lessons.md).
- For pixel collision or side links only, go to [geometry and navigation](../maps/README.md). You can use those features without learning scene graphs.
- For an NPC, sound, inventory or ending change, start with [native game systems](../game/README.md).

[Loading and testing](worldsmith.md) gives the editor steps. [Files and packaging](files.md) explains what belongs in the final map. If a mod or scene fails, use [troubleshooting](../reference/troubleshooting.md).
