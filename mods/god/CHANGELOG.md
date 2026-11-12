# Changelog

## 0.3.5 - 2026-11-06

- Fix mouse selection, drawing and erasing on screens connected sideways, such
  as both challenge rooms in Babes of Absurd Timings. Project from the screen
  interior so its upper boundary doesn't select the neighbouring screen's
  coordinate chart. Preserve camera presentation offsets.

## 0.3.4 - 2026-11-05

- Cache platform and slope selection between screen visits, refreshing it after
  terrain changes. Avoid repeated pixel traversal and redundant region scans.
- Skip GPU texture readback, artwork fingerprints and donor sampling when only
  visiting a screen. Prepare artwork for edits and saved-screen restoration;
  keep existing saved fingerprints compatible.

## 0.3.3 - 2026-10-31

- Make foreground transfer optional and off by default, with a toggle in the
  main and pause menus. Leave the original foreground and its animation owner
  alone while disabled; capture the current texture when enabling it.
- Update only changed artwork regions, reuse GPU textures and unchanged blocks,
  and cache decoration sampling to reduce stalls during live editing.
- Show square and oriented triangle icons in the Shape control.

## 0.3.2 - 2026-10-31

- Keep drawn and resized slopes at 45 degrees in all four orientations. Build
  them from square slope cells and Solid foundations so moves, copies, screen
  clipping and eraser cuts preserve the diagonal.

## 0.3.1 - 2026-10-31

- Crop moved, copied and resized platforms at screen edges instead of showing
  an out-of-bounds error. Keep selection outlines within the screen and retain
  the complete pre-drag platform for undo.

## 0.3.0 - 2026-10-31

- Draw and pick slope surfaces through Runtime visual geometry, preserving
  actual native slope lines during copies. Restrict midground transfer to the
  platform body. Keep texture-free native debug rendering available.
- Compact the toolbar to one control row. Reset restores all screens and saved
  edits in the current map, with one undo/redo operation and rollback on failure.

- Group straight slopes with their triangular Solid foundation; draw all four orientations with slope-only samples and mirrored fallback.
- Copy complete platforms with Ctrl + drag.

## 0.2.0 - 2026-10-31

- Keep the toolbar compact: controls and values only, with errors when needed.

- Drag the king through the native Debug teleport path with Smooth Camera support.
- Reuse native wheel screen switching and Runtime Prompted Jump Power.
- Separate full-height boundary walls from attached platforms; exclude walls
  from Auto sampling. Distinguish platforms by block type and parameters.
- Copy native and mod block data, retain unchanged instances, and use collision
  cell units for sizes. Remove the pixel-grid control.
- Transfer top decorations up to the king's height, replacing destination
  foreground. Keep bare collision maps editable.

## 0.1.1 — 2026-10-28

- Replace the editor chrome with Jump King's native frame and separate tools,
  surface settings and action buttons. Remove the bottom status panel.
- Make Tab completely hide and restore the top panel, including its pointer area.

## 0.1.0 — 2026-10-28

- Add a live mouse editor for drawing, cutting, moving and resizing ordinary
  solid platforms, with configurable binding, donor sampling and undo/redo.
- Match platform artwork to the combined collision contour and keep edited
  collision rectangles compact.
- Save edits outside the map with source checks and atomic file replacement.
