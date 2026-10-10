# Camera controls and Debug dragging

Use Look Up, Look Down or Focus to inspect the route without changing the King's
movement. Choose the bindings and activation mode in **Mods > Smooth Camera >
Binds**. The camera must be enabled and allowed by the map.

## Bind and use a view

Open **Binds** in the Smooth Camera main or pause menu to edit **Focus Camera**,
**Look Up** and **Look Down**. Each row starts with **Mode**, followed by
primary and secondary key slots. The same actions and modes appear in **Controls+**.
**Settings > Controls > Binds** edits a draft until you select Apply.

Look Up/Down inherit the game's current Menu up/down bindings by default.
A camera rebind overrides that action for the current device without changing
menu navigation. Clear leaves it unassigned; Default on a key slot restores
inheritance. Focus defaults to **F** on the keyboard and unassigned on controllers.
Alternatives and two-button chords are saved separately for each device profile.

Up looks above the king, placing his center 48 logical pixels from the bottom;
Down places him 48 pixels from the top. Map boundaries can limit this framing.
Focus smoothly centers the active native screen on both axes, then follows
native screen changes exactly. Input stays at the native update rate.

Choose a mode independently for each action; all three default to **Hold**,
including settings upgraded from an older version:

- **Hold** selects the view immediately while held. Release returns to the
  previous latched view, or automatic tracking when none was latched.
- **Press** toggles on each new press. Holding doesn't repeat the toggle.
- **Both** preserves the earlier behavior: a tap latches the view, while a hold
  restores the previous view on release. The threshold defaults to 250 ms.
  Cancelling a latch takes effect on tap release; a hold takes over at the
  threshold without flashing normal framing first.

Pressing either direction cancels an existing directional latch. Focus toggles
its own latch; moving between Focus and a direction selects the requested view.
Opposing simultaneous directions cancel unfinished input and require release.
An explicit Focus chord takes precedence over directions it contains.

Pause, Runtime pages, Steam overlay and window focus loss cancel unfinished
gestures and require release before rearming. Restart, unloading, disabling the
camera or changing an activation mode clears the latch. Active views aren't
saved to disk. Mode preferences are global and saved independently of bindings.

Automatic tracking continues underneath temporary views, so returning keeps
its normal framing behavior. These controls are active only while Smooth Camera
is enabled and its compositor is available. Native buttons remain unconsumed.

Maps can restrict these camera views with `allow-focus=false` and
`allow-look=false`. A restriction clears forbidden latches and cancels unfinished
gestures; exiting requires release before a fresh press. Saved bindings and
native menu controls stay unchanged.

## Debug dragging

In debug mode, left-click places the king at the visible mouse position. Hold
and drag to move him inside the configured framing window on both axes. Crossing
an edge moves the camera immediately and keeps the cursor over
the king at that edge. Clicking outside the window selects that world position,
then shifts the camera and cursor together. Map boundaries limit scrolling.
Horizontal capture requires Horizontal tracking and a usable side exit; closed
sides keep the full map width. Side teleports preserve the held drag without
moving the king back to the departing screen.
Dragging across vertical screens updates the game's physical screen immediately,
so releasing resumes collision checks against the destination's platforms.
Release returns to normal tracking from the current view. The mouse wheel keeps
its native screen teleport behavior. Focus uses native debug dragging without
framing-window capture or cursor repositioning. Background windows cannot drag
the king or move the cursor. After returning to the game, release the mouse
button before starting a new drag.

Read [camera settings](settings.md) for framing distances and profiles, and
[side links and rendering](rendering.md) for what appears beyond an edge.
