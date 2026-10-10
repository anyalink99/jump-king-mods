# Kick and teleport

The host controls **Allow kicks** and **Allow teleport** under **Multiplayer
settings > Host Settings**. Both start on and work in Ghosts and Solid.
Current participants should use Expansion 0.8.0; action support was introduced
in 0.7.0. Older peers remain visible but can't receive kicks.

## Kick another king

Press **K** on keyboard to send a forward pixel shockwave. It adds horizontal
and upward momentum to the nearest eligible king in front of you. Controllers
start unbound. Open **Binds** to edit primary and secondary bindings for your
device, including chords.

The attack reaches 72 pixels beyond the bodies' edges, within 38 pixels
vertically. Terrain blocks it. The cooldown is 0.55 seconds. Paused or
transitioning players can't kick or receive a kick. Confirmed hits play the
native bump sound on the target and show a shared impact burst.

## Teleport near a player

**Teleport to <name>** buttons appear directly below **Multiplayer settings**,
one per connected peer. No peers means no buttons. The target must still be
available on the same map when you press the button.

Placement checks free space near the target, terrain and other kings using the
local body's size. If it can't find a position within 96 pixels, you stay put.
It doesn't load another map or move the target. Placement uses the target's
network coordinates and is independent of Smooth Camera.

## Files and event handling

Host permissions are saved in `actions.txt`; device bindings are in
`kick-binds.txt`, beside the expansion. Session permissions don't replace a
guest's defaults. Runtime's binding editor retains keyboard, mouse and
controller identity through wrapped devices.

## Network contract

`PlayerActionWire` uses a separate 120-byte message with tag 131. Hello packets
negotiate action support for the current attempt. The host sends a versioned
permission policy and confirms kick requests; clients never send an impulse value.
Session, map, rules revision, actor/target attempt and transition IDs bound events
to their observed state. The policy counter also invalidates requests from before
a permission change. Movement remains protocol 4.

`KickAuthority` checks a forward reach of 72 pixels beyond body edges, 38 pixels
vertically, terrain visibility and a 0.55-second cooldown. Requests expire after
350 ms. `ActionReplayWindow` remembers 64 confirmed event numbers. Delivery repeats
requests/events every 50 ms for 350/400 ms, with at most 32 outgoing entries.
`PlayerActionInbox` stages at most 32 confirmed impulses and drains them once at
the next body step. It rechecks target state and policy, expires entries after
450 ms and adds simultaneous impulses before advancing the local transition.

`PlayerActionBindings` owns saved device chords and registers Runtime input actions
for the attempt scope. `PlayerEffects` draws bounded, short-lived pixel effects
without creating textures. `TeleportPlacement` uses the same terrain and overlap
recovery search as contacts; a failed search doesn't mutate a native body.

Teleport actions read canonical received coordinates rather than a locally
unfolded contact sample. Runtime checks terrain around the destination screen,
normalizes a successful seam crossing and publishes the teleport event. Dynamic
native buttons use `NativeMenuRows.After` next to Multiplayer settings.

See [contact ownership](contacts.md) for where confirmed impulses enter the
body update, and [world synchronization](world-synchronization.md) for the
separate host authority over map state.
