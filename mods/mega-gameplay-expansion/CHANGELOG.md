# Changelog

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
