# Changelog

## 0.12.1

- Put Focus first in the camera binding list, followed by Look Up and Look Down.
- Use the first-column Mode selector and explicitly offer Hold, Press and Both.
- Require JK Runtime 1.42 or newer 1.x.

## 0.12.0

- Add a three-action Binds page for Look Up, Look Down and Focus Camera, also
  available in Controls+. Look defaults follow native menu bindings; overrides
  and explicit clears are saved per device.
- Add Hold / Press / Both activation modes to each action. Hold is the default
  for new and existing settings; Both retains the earlier tap/hold behavior.
- Keep binding and mode edits inside Settings in its Apply/Cancel draft.
- Require JK Runtime 1.41 or newer 1.x for shared binding mode selectors.

## 0.11.1

- Update the native physical screen during debug dragging, so releasing after a long climb uses the destination's collisions instead of falling through its platforms.
- Check native landing after ten-screen drags in both directions and Mega Mapping Expansion side links above screen 255.

## 0.11.0

- Extend debug dragging to the horizontal framing window, including diagonal movement, cursor repositioning and continuous native side crossings.
- Recognize Expansion Blocks MultiWarp destinations during ordinary tracking and debug dragging, including height-dependent exits, offset screen numbers and one-way arrivals.
- Hide the previous side view completely before switching between incompatible destinations on the same screen.
- Keep Focus and background-window input guards for both drag axes.

## 0.10.0

- Follow Runtime presentation camera targets during replay viewing, including map ranges, position zones and horizontal tracking.
- Freeze following on replay pause, reframe immediately after seeking and restore the live target when viewing ends.
- Require JK Runtime 1.39 or newer 1.x.

## 0.9.2

- Keep native debug dragging in held or latched Focus mode without capturing the framing window or repositioning the cursor.
- Check the Windows foreground window before debug mouse input and cursor movement. Cancel background dragging and require mouse release after returning to the game.

## 0.9.1

- Fix debug mouse teleports to use the visible camera position and window scaling.
- Keep vertical dragging inside the configured framing window; move the camera and cursor together at its edges or after a click outside it.
- Preserve physical mouse movement across cursor repositioning without drift on extra presentation frames.

## 0.9.0

- Add independent `allow-focus` and `allow-look` map restrictions in mechanics.xml and camera.xml, including screen and zone rules and camera.xml map-wide defaults.
- Clear forbidden manual views and unfinished gestures on entry. Require release and a fresh press after restrictions change, without changing saved bindings or native input.

## 0.8.0 — map-controlled activation

- Use JK Runtime 1.34 authority and shared On/Off/Local screen markers and XML zones.
- Import legacy camera screen rules into common permission, retaining profiles, coverage and clipping.
- Persist global enable Off on controlled map entry and lock the switch until leaving the controlling map.

## 0.7.0

- Added independent axis modes, focus, free windows, directional response,
  anticipation, idle return and configurable manual-view controls.
- Added native-style Settings pages with staged changes, bindings, presets,
  personal map profiles, animated preview and gameplay diagnostics.
- Added temporary and persistent disabling map tags, forced smooth/native screen
  ranges and prioritized rectangular zones. Smooth markers override Enable.
- Added bounded composition, author recommendations, map validation and legacy
  settings migration. Requires JK Runtime 1.32.1 or newer 1.x.
