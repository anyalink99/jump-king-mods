# Playing with No Walk Off

Hold a direction to walk to the edge. The King stops at the last whole-pixel
position with one pixel of support and stays there without drifting. Release
that direction, then press it again to walk off. Jumping remains unrestricted;
turning back lets you retreat and arms the next approach normally.

No Walk Off only intervenes in verified ordinary walking. One motion observer
compares commanded speed, actual displacement and post-material momentum.
Wind, ice/Snake Ring inertia, conveyor offsets and other nonstandard motion
release the guard even while a direction is held. JK Runtime observes supported
arithmetic in the actual modifier calls: water, low gravity and unfamiliar pure
speed scaling do not need per-material coefficients. Protection resumes after an ordinary movement
cycle has been observed. Unknown altered movement is left untouched rather than
treated as walking. Unsupported arithmetic or a changed movement patch graph
releases protection and is available through Runtime's observation diagnostics.

JumpKingPlus low-gravity walking is supported, including its legacy speed mode
and water combinations. Top-facing JumpKingPlus one-way platforms count as
support while standing on their top edge; upward passage remains unchanged.
One-way ice still follows the inertia rule above.

Snake Ring makes grounded movement use the ice model on every surface. Its
acceleration and fractional displacement explain the fine approaches previously
made with tiny inputs. No Walk Off does not replace that model.

| RGB | Variant | Activation |
| --- | --- | --- |
| 173, 50, 211 | Solid | While supported by this terrain |
| 173, 51, 211 | Screen | One nonblocking marker affects the whole screen |
| 173, 52, 211 | Zone / area | While the player's hitbox overlaps this volume |

The **No Walk Off** checkbox applies the same rule globally on unrestricted
maps, subject to explicit screen permission. An ordinary
neighbouring platform does not inherit Solid activation; leaving a Zone removes
its rule. Joined surfaces do not create artificial edges. A lower platform
does not count as support at the current height.
At a flat-to-descending-slope seam, the King stops at the last flat support;
release and press again to enter the slope. The slope itself is not flat support.

The mechanic guards grounded movement only. It does not catch an airborne King,
stop native slope sliding, change teleport destinations, or control Ball form
and active Jetpack thrust. Direction is read through native input, including
keyboard, mouse bindings, gamepads and Controls+ chords. An analog stick must
return to neutral before a second outward input can release the edge stop.

With Powers installed, Warp Jump still handles actual departures; a stopped walk does not start a warp.
Global edge stops use `AllowNoWalkOff` for modified-run permission. Merely enabling the checkbox does not mark a
run until it changes movement. See [implementation](no-walk-off.md).
