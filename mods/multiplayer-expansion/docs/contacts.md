# Contact ownership

Each client advances its own native king. Contact prediction gives that king an
immediate response while confirmed impacts reconcile the result. The grounded
king doesn't yield to an airborne king, regardless of who owns the lobby.
This page describes the update path; [player interactions](player-interactions.md)
explains the rules as they appear in the game.

## One body step

1. Receive and validate snapshots without changing native body velocity. Keep
   ordered peer history and put new impact records in `ImpactInbox`.
2. Before `BodyComp.UpdateInternal`, observe presence transitions, detect a moved
   body, prepare contact predictions, and drain eligible impact records in peer/ID
   order. Recheck membership, attempt, map, rules, state revision and age here.
3. `ContactPipeline` carries an attached rider in Runtime's `BeforeWind` phase.
   Resolve contacts in `AfterYCollision`, before native gravity and materials.
   Native input and brain processing follow the body step. Carry, recovery and
   pushes query map geometry; passenger clearance only limits airborne ascent,
   never rewinds a completed terrain landing.
4. Publish snapshots at up to 60 Hz. Draw from received-frame playback, gradually
   blending toward the prepared contact body near the local king. Resolve support
   chains from the same parent position and cache each peer once per draw frame.

`AdvancedSession` connects these phases to the game. `InteractionTransport` handles
Steam and local delivery. `InteractionWire` validates the protocol. `RemoteMotion`
owns prediction; `RemotePlayback` owns the render clock and bounded interpolation.
`VisualCorrection` reconciles predictions in the contact approach region.
`ContactConstraints` chooses which body can yield before `PlayerContacts` and
`ContactRecovery` make corrections.
`ContactLedger` owns numbered impacts; `ImpactInbox` stages received ones.
`SupportSurface` extends one-pixel read-only foot probes with player head planes.
It preserves the native collision result's wind/slope data and never changes
full-body terrain collision. Support consumers use this shared query rather than
mod-specific multiplayer hooks.

Contacts borrow adjacent charts from Runtime's `MapTopology`. Carry runs before
the native collision cache, so a passenger and its carrier use one portal route
even across a height band. Seam shifts rebase samples and support together;
ordinary teleports still reset the contact transition. Playback and contact
positions are aligned to one chart before blending.

Proximity indicators use Runtime's default region map for distant targets.
The camera may select a repeated image when that linked column is actually
visible. Self links retain their repeated local images without changing the
default placement of a distant branch.

## Identity and presence

Each body attempt has an epoch. Packet sequences and simulation ticks can't go
backward within it. Presence distinguishes Playing, Paused, Spawning, Teleporting
and Unavailable. Paused bodies remain solid with zero transmitted velocity, without
claiming to be grounded. Only Playing and Paused have active collision presence.
Transitions advance a separate revision; jumps retain their own counter. Impact
records target the attempt, jump and state revision they observed.

Event IDs remain monotonic during an attempt/session. Pause, teleport and rule
changes clear transient predictions and pending events without reusing IDs or
forgetting consumed events. A new session can reset numbering because it creates
a new epoch. Peers reject up to eight retired epochs.

## Carrying and correction

A passenger publishes its carrier ID, attempt ID and relative position. The
carrier's client draws the passenger from its current position, avoiding another
packet round trip. Support chains reject cycles and stale attempts.

Carry runs before native movement; head contact resolves after native Y collision
and before gravity and material behavior. Passenger clearance limits airborne
ascent, but can't undo a completed terrain landing. Read-only foot probes expose
head planes to support consumers without adding heads to native terrain or saves.

The peer with the lower Steam ID confirms airborne pair impacts; in Debug that
is client 1. Numbered records repeat for 400 ms. Confirmation adjusts a predicted
impulse once while preserving subsequent gravity. Records older than 250 ms,
from another attempt or before a new jump can't replace the current transition.
Pause, teleport and resume revisions reject impacts from a previous state.

This is bounded prediction and reconciliation. It doesn't rewind the whole game
or rerun map scripts. See [network limits](networking.md#bounds-and-tradeoffs).
