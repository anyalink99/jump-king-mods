# God

Press **F8** while playing to edit platforms and drag the king in real time.
The world keeps running and keyboard/controller gameplay controls stay active.
Change the bind in **God > Binds** or Runtime's shared bindings menu. God 0.3.5
needs JK Runtime 2.6.0+ (SDK API 2.10) and Harmony 2.3.6. Use Smooth Camera 1.0.1+
for camera-aware king dragging.

Grab the king with the left mouse button, move him and release. The picked point
stays under the cursor. This invokes native Debug teleporting, including Smooth
Camera's framing and cursor reprojection. The mouse wheel uses native Debug
screen switching. Neither action changes terrain undo history.

**Prompted Jump Power** uses the same controller as Debug mode. Right-drag over
the world to set `P:` above the king, then release. Tap Jump to execute that power;
hold or tap Left/Right during charging to select direction. Right-click without
moving disables the prompt. Subframe Charge's effective step applies here too.
Right-click during a terrain or king drag cancels that gesture without arming
the prompt. The toolbar doesn't accept wheel teleports or prompt gestures.

Use **Draw** to drag a rectangle, **Erase** to cut a rectangular hole, and
**Select** to move a connected platform. Drag its bottom-right handle to resize
it; **Delete** removes it. **Sample** copies the block type, parameters and art.
Click the panel's **Sample** field to restore automatic donor selection by shape
and size. A pinned donor is outlined in gold.

Sizes use collision cells: the smallest rectangle is **1 x 1**. The step comes
from the loaded geometry, starting at the native eight-pixel cell and reducing
to match smaller aligned hitboxes. There is no pixel-grid control. Rectangles
of the same block type and parameters can join; different types stay separate.
Rendering follows the combined outer contour, so collider boundaries don't add
texture seams.

Full-height solid strips at the left and right screen edges are separate walls.
Their width stops at the first column that isn't solid all the way down. A
shallow cavity therefore leaves the outer wall and separates the projections
above and below it. Auto never samples these walls; explicit Sample still can.

**Shape** cycles a square and four triangle icons. Each triangle shows the
filled side of the slope. Auto uses slope samples for slopes and rectangular samples for
rectangles. If an orientation is missing, another slope sample is mirrored;
without a slope sample, new ramps use bare collision rendering.
Draw and resize keep slopes at 45 degrees in all four directions. The diagonal
uses square collision cells with Solid filling underneath. Cropping removes
cells without changing the angle; artwork follows the combined surface.

A straight slope chain and its Solid foundation inside the resulting triangle
form one selectable platform. Adjacent platforms, edge walls, holes and other
block types stay separate. **Ctrl + drag** copies the selection, preserving its
donor. Edited triangles use square slope cells and Solid foundation blocks;
untouched map blocks keep their original instances.
Moves, copies and resizes crop at screen edges instead of rejecting the gesture.
Undo restores the complete platform from before cropping.

**1/2/3/4** select tools. **Ctrl+Z / Ctrl+Y** undo and redo terrain edits.
**Tab** completely hides or restores the top panel, including its pointer area.
**Esc** cancels a drag or closes God. Hidden controls don't block editing.
Release a terrain drag to save it as one undo operation. **Reset** restores
the entire current map, including saved edits on other screens. One undo or redo
reverses the whole reset. Failed resets restore the prior geometry. **Close** or the God bind exits the editor.

Native solid, ice, snow, sand, water, no-wind, quark and slope blocks retain their
classes. Slopes use Runtime visual contours for their artwork and selection;
copying transforms their actual loaded collision lines without reconstructing them. Mod blocks with self-contained data are copied with detached mutable
state. Blocks owning native handles, entities, services or other external state
need a dedicated adapter and remain untouched. Overlapping collision layers are
also retained instead of being flattened. Another mod replacing a screen's
collision array or an editor-owned texture requires reloading the map before
further editing. Foreground stays with its original owner while transfer is off.
New blocking terrain can't overlap the king.

Art comes from the current screen's original midground, limited to the platform
body. Foreground transfer is off by default. Enable **Transfer foreground** in
God's main-menu or pause-menu settings to include it. The option applies to
existing edits too; turning it off restores the borrowed foreground texture.
While off, foreground drawing and texture changes remain under their original
owner's control.

When enabled, foreground uses the current texture at activation. It covers the
platform body and decorations above exposed tops, up to the king's height,
stopping at other collision. Claimed pixels replace existing art, including
transparent gaps; deleting a platform clears its sampled fringe. Unrelated
decorations immediately above a platform can be included by this heuristic.
The background layer is never copied or changed. God doesn't reconstruct scenery
hidden in an opaque platform image. Native layers must be 480 x 360.

Editing updates changed texture regions through reusable GPU buffers and caches
decoration samples and unchanged generated blocks. The toolbar has one control
row and expands only for errors.

Screen visits reuse selection geometry until terrain changes. Entering an
unedited screen doesn't read back GPU textures or build texture donor samples.
Artwork is captured when first editing that screen or restoring its saved edits.

Maps without artwork still support editing, with collision outlines while God
is open. An empty screen can create its first ordinary solid cell without a
donor. Terrain edits save in `Jump King/Content/God`, outside maps and game saves.
They restore only against matching source geometry, art and material schema;
older unmatched records are left on disk until that screen is edited again.
XML writes keep the previous file as `.bak`. Undo keeps 32 operations per screen
for the current world. Terrain edits, king movement and prompted jumps mark the
attempt as modified. Snapshots from a different terrain generation are rejected.

Build and install with the game closed:

```powershell
.\scripts\check-mods.ps1 -Mod god,smooth-camera -Integration
.\mods\jk-runtime\install-release.ps1 -Include god,smooth-camera -OnlyRequested
```

The God package is `build/god/UPLOAD_TO_WORKSHOP`. Distribute `God.dll` and
`0Harmony.dll`, with Runtime installed separately. Installers preserve settings
and rollback history. Build tools overwrite fixed test outputs under
`build/_work/god/validation`; they don't create growing fixture histories.
Automated native and graphics fixtures don't replace an in-game map playtest.
