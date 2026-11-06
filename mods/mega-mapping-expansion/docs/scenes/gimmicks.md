# Build a gimmick from actions

[Handbook](../README.md) | [Painted placement](../maps/painted-areas.md)

MME 1.6.0 can hold a player, read the currently held directions and change their
velocity. Combine those operations with sensors, graphs, trees, effects and
recipes. The C# service owns the native player safely; XML defines the interaction.

Start with the [complete example](../../examples/painted-gimmicks/README.md).
Copy its files to a test map, compile its collision PNG and touch the painted
marker. The player pauses for half a second and then leaves in the direction
held at release. A light marks the position. Copying a second marker creates a
second independent instance without copying XML.

## The parts you need

1. A **sensor** detects contact with the painted position or area.
2. A **playerControl** names the interaction and optionally its capture transform.
3. A **tree** captures, checks success, waits, sets velocity and releases.
4. A **graph** calculates velocity from input, saved entry velocity or other data.
5. Ordinary sprites, materials, audio and emitters provide feedback.

Use `input.direction` to read held directions as a Vector2. Right/down are
positive; left/up are negative. Each axis is -1, 0 or 1. Opposite buttons cancel
on their axis. Normalize this vector before multiplying by speed if diagonals
should have the same speed as straight directions. Reading it after a wait uses
the buttons held at that moment; it doesn't remember earlier presses.

All velocity actions use **pixels per second**. A native Jump King velocity of
7 pixels per tick is 420 px/s at 60 Hz. A zero vector means no impulse; gravity
resumes after release. Allowed directions are graph conditions, not a built-in
list. For example, `sign` followed by `normalize` converts entry velocity into
one of eight directions, while zero stays zero.

## Player control and actions

`playerControl` has two fields:

| Field | Meaning |
|---|---|
| `transform` | Optional capture center. Empty keeps the player's current position. The transform must be on the current screen; screen 0 uses the current screen. |
| `hidePlayer` | Hide native player drawing while held. Default false. Draw a sprite bound to the control's captured signal for a replacement. |

The target of each player action is a playerControl component, including an
exported recipe port.

| Action | Value |
|---|---|
| `capturePlayer` | None. Obtain exclusive control, record entry velocity, center the player if requested, stop motion and suspend native movement. |
| `releasePlayer` | None. Restore native movement and drawing; retain the velocity set during the hold. Consume the pending jump input. |
| `setPlayerVelocity` | Vector2 literal or graph source, in px/s. Also works without capturing. |
| `addPlayerVelocity` | Vector2 delta, in px/s. Also works without capturing. |

Capture refuses blocked destinations, a transform on another screen, suspended
movement or another mechanic's ownership. It never takes control away from a
different interaction. Check `<control>.captured` immediately after capture;
put that check before sounds, effects or a wait. A denied action fails its tree leaf (so a selector can try another branch) or stops the current rule.

The control publishes `<control>.entryVelocity` in px/s and
`<control>.captured` as a Boolean. Entry velocity comes from the triggering
contact event when available, otherwise from the live body. It is recorded
once on acquisition. `<control>.acquired` and `<control>.denied` are events.
Release and denied acquisition don't erase the recorded entry velocity.

Velocity is bounded to ±6,000 px/s per axis, including the result of addition.
Actions respect other mechanics' player ownership. An unavailable graph source
faults its rule/tree instead of silently substituting zero.

## Keep state and visuals together

Use an owned Effect to hide the idle sprite and disable its sensor while the
tree is active. Bind the inside sprite and looping audio to `control.captured`.
After setting velocity and releasing, wait for the respawn delay. Finishing the
tree removes the Effect and restores the sensor and idle sprite. This also
restores them when the tree is cancelled or faults.

Each tree action owns its capture. Completion, clear, restart, cancellation or
failure releases any capture still owned by that tree, with zero velocity.
Use an explicit release to preserve the chosen launch velocity. A capture in a
Rule lasts until explicitly released, its owner faults, or the attempt ends;
use a tree for timed interactions.

Gameplay waits stop while paused. Snapshots restore the sequence and native
capture together. Disabling MME cancels active player-action trees and releases
the player; leaving the map, restarting or completing the map also releases
control. Shared-world followers don't execute player actions locally.

Variables are assignments, not owned effects: cancelling a tree doesn't undo
them. Use Effects for temporary visibility/enabled state, and variables for
values that should survive until changed or restored.

## What still needs code

This set supports interactions assembled from existing observations and actions.
It doesn't add arbitrary collision shapes, a replacement physics solver or
unrestricted access to game internals. Add a reusable native operation when a
new mechanic needs a capability the current components cannot express. Keep
the mechanic's timing, conditions, placement and appearance in its recipe.
