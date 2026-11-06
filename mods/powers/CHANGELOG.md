# Powers changelog

## 0.1.0 — 2026-10-28

- Release Warp Jump and Air Dash as an independent mod with their own settings.
- Keep native prediction, effects, audio, bindings and map colours.
- Use `powers.warp`, `powers.air-dash` and `AllowPowers` for map integration.

## Historical MGE releases

The entries below describe the former combined mod.


## 0.17.0 - 2026-10-26

- Publish Air Dash wake and Warp phase/placement through Runtime actor visuals. Remote and replay actors calculate their own afterimages and RGB scatter from action state, with no particle-position track or gameplay simulation.


## 0.16.2 - 2026-10-21 update

- Run No Walk Off collision phases on every fractional body step. Its motion
  evidence uses actual displacement, with native timing for other controllers.
  Requires the matching Runtime build with API 2.7.
- Commit No Walk Off's input direction after each control pass, so releasing
  and repressing at an edge keeps working between native ticks.

## 0.16.2

- Remove return terrain, its palette, destination XML and copied warp sound handling.
  Maps should use the original block provider; Mapping can change its shape.

## 0.16.1

- Keep return terrain rectangular; slope geometry now belongs to Mapping's
  general collision-shape aliases.
- Restore the JK+ warp sound for return terrain when JK+ is loaded. Resolve its
  current attempt's sound and honor `MuteWarpTransitionSound`.

## 0.16.0

- Add map-authored return terrain: a rectangle or four native slope-shaped
  triggers with an exact destination in `return-terrain.xml`.
- Stop velocity on return and publish Runtime teleport observations for scene
  reactions. Register the palette and mechanic in Runtime's inspector.

## 0.15.2

- Declare Air Dash as a Press action in Controls+ and the binding editor.

## 0.15.1

- Discard retired Gimmick Library XML sections when reading old preferences.
  Mechanic switches and custom bindings remain editable after an update; removed
  library data isn't imported into Inspector or written back on the next save.

## 0.15.0

- Move Gimmick Library and block observation to JK Runtime's Mod Inspector.
- Keep Warp Jump, No Walk Off, Air Dash and Binds in MGE's settings.
- Keep only mechanic settings and bindings in MGE preferences.
- Prepare No Walk Off motion support for explicit Runtime terrain edits.
- Require JK Runtime API 1.38.

## 0.14.1

- Moved Dash/Warp input-phase callbacks to JK Runtime without reordering player components.
- Dash owns its afterimages using Runtime appearance capture and uses prepared audio with attempt cleanup. Requires Runtime 1.36.

## 0.14.0 - shared player presentation

- Capture Dash bodies through Runtime without CPU readback and place echoes, wake and impact details using the active visual geometry, including Jing. Share Warp capture contexts, keep detached pixel colors, and mark restored cosmetic signals.
- Requires JK Runtime 1.35 or newer 1.x.

## 0.13.0 — map-controlled activation

- Use JK Runtime 1.34 authority and On/Off/Local permission for Warp Jump, No Walk Off and Air Dash.
- Preserve Solid/Zone/Screen triplets; import legacy Screen pixels as On and prevent local triggers from bypassing denied screens.
- Persist controlled global modes Off and suspend generic saved/manual Gimmicks overrides on controlled maps while retaining discovery.
