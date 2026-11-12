# Multiplayer Expansion guides

For installation and a first lobby, start with the [README](../README.md).
Choose the guide for what you want to do:

| Task | Guide |
| --- | --- |
| Choose Ghosts/Solid, carry a player or change opacity | [Player interactions](player-interactions.md) |
| Bind Kick or teleport near another player | [Player actions](player-actions.md) |
| Share map mechanics and scene state | [World synchronization](world-synchronization.md) |
| Run two games in Debug and simulate packet faults | [Debug testing](debug-testing.md) |
| Understand packets, interpolation and compatibility | [Networking](networking.md) |
| Work on the contact solver | [Contact ownership](contacts.md) |
| Build, probe Steam or verify a packet recording | [Development and validation](validation.md) |

## One body step

Receive snapshots, prepare contacts, apply movement, then publish and draw.
[Contact ownership](contacts.md#one-body-step) gives the native phases and owners.

## Player actions

The host validates kicks and permissions. [Player actions](player-actions.md)
explains bindings, reach, placement and event delivery.

## Identity and presence

Attempt epochs and state revisions keep old packets from changing a new body.
See [identity and presence](contacts.md#identity-and-presence).

## Bounds and tradeoffs

Prediction, playback and recovery are bounded. See the
[network limits](networking.md#bounds-and-tradeoffs) for the values and behavior
when packets arrive late.

## Reproduction

Debug recording captures movement packets for repeatable acceptance/prediction
checks. [Reproduction](validation.md#reproduction) explains what those traces contain.

## Shared world state

The host advances declared world state; guests restore it. Mods integrate
through Runtime rather than a Multiplayer-specific provider. Read
[world synchronization](world-synchronization.md) for supported mechanics and the
[Runtime contract](../../jk-runtime/docs/world-execution.md) to add your own.
