# Movement, transport and rendering

Original Multiplayer supplies lobbies, identity and native ghost entities.
Expansion owns its movement snapshots, equipment rendering, contacts and shared
rules. Online extension packets use a separate Steam P2P channel.
For the update sequence, read [contact ownership](contacts.md).

## Version compatibility

Use the current Expansion 0.8.0 on every participant for the documented feature
set, with Runtime 2.0 and matching world mods. Older movement protocols 1–3 can
still be read for presentation; protocol 4 enables physical peers. Physical
contacts were introduced in Expansion 0.6.0, actions in 0.7.0 and Runtime world
state in 0.8.0. These are separate requirements.

Unmodified peers keep Multiplayer's original renderer. Sessions containing only
current expansion peers stop sending the redundant legacy movement stream.

## Movement and render clocks

Movement snapshots are sent at up to 60 Hz, even while stationary. They include
body, pose, equipment, jump and support state. Packet echoes estimate round-trip
delay without counting time waiting for the next send. Prediction uses up to
100 ms of the latest trajectory; airborne acceleration is used only when
successive samples agree. Teleports, map changes and long gaps reset it.
Paused snapshots stop prediction while retaining collision presence.

Rendering has a separate monotonic playback clock. It buffers two frames locally
or three online, plus observed jitter. Packet bursts adjust playback speed
instead of jumping the clock. Every remote sprite and rider uses the same frame
timestamp. Sprite origins use native pixel alignment.

When Smooth Camera composes several screens, Expansion submits players to
Runtime's world draw layer in each screen pass, before the foreground. Original
wall-triggered draws and late duplicates are suppressed while that presentation
owns the frame. Native rendering keeps its fallback.

## Bounds and tradeoffs

| Mechanism | Limit |
| --- | --- |
| Prediction | 100 ms ahead, 32 snapshots |
| Render playback | 32 snapshots; 2 local/3 online frames plus up to 100 ms jitter buffer |
| Render clock adjustment | 90–110% playback speed; no arrival-driven position jump |
| Collision freshness | 500 ms since the last packet |
| Peer retention | 2 seconds without packets |
| Impact staging | 64 records; maximum application age 250 ms |
| Impact transmission | Up to 8 records, repeated for 400 ms |
| Visual correction | 8 pixels; 45 ms exponential decay; snap beyond 32 pixels |
| Recovery | 96 pixels, at most 256 candidate placements |
| Test delivery queue | 512 packets and 4 MiB, plus the same bounds for ready packets |
| Recording | 120 seconds or 4 MiB; three trace files per client |

Grounded players don't yield to airborne impacts. Without pushing, an established
walking contact can't move the other player's starting edge. The approaching
client resolves its own movement; genuine initial overlaps still need recovery.
When no terrain-safe position exists, recovery leaves ordinary map movement
available and retries without adding bounce impulses.

`RemotePlayback` interpolates positions using monotonic cubic segments, with
same-clock pose and support offsets. It never extrapolates beyond received
positions. Arrival bursts adjust playback rate rather than jumping the cursor.
Attempt, map, presence, transition, teleport and long-gap boundaries clear history.

Rendering never changes the collision body. In Solid mode, proximity smoothly
blends playback toward the prepared physical sample over a 48-to-4-pixel gap.
Immediate contact and support chains involving the local king use the physical
position. Bounded visual prediction corrections apply only in the approach region;
teleports and state changes reset them. Ghosts always uses received-frame playback.
`RemotePresentation.BeginFrame` captures the draw timestamp and clears the render
cache, so ghost updates, repeated draws and support lookups can't advance playback
several times during the same native frame. Sprite origins snap to native pixels
after applying the anchor, matching `Sprite.Draw` for the body and equipment.

This is bounded prediction and event reconciliation, not full-world rollback.
Long interruptions, high latency and custom map behavior can still require visible
corrections. The solver doesn't rerun map scripts or rewind native entities.

## Local and Steam tests

The Debug transports carry the same extension packets across a real process
boundary. Local uses framed, ordered named-pipe messages. Steam adapts the
original P2P boundary to `CreateListenSocketIP` / `ConnectByIPAddress` with
reliable ordered delivery. Both require the session's random handshake token.

Steam tests bind only `127.0.0.1`, use two local peer IDs, and disable lobby and
invitation handling. They check packet and ghost handling, not separate-account
authentication, NAT traversal, internet latency or Steam Datagram Relay.
See [Debug testing](debug-testing.md) for setup and fault injection.

## Legacy packet safety

`LegacySafety` drains at most 64 legacy packets per poll, caps JSON payloads at
4 KB, checks lobby membership and values, and applies the newest state per peer.
It also guards late/failed lobby callbacks, refreshes ownership and announces
stationary players on join. Legacy trackers retain at most 32 smoothing samples,
or only the newest sample when original smoothing is disabled.

The reviewed Multiplayer fixture dated 2026-10-07 has SHA-256
`4679E787A31504FD598E691C078866A612E861C2073463E90C7174ADC88670D0`.
Its private methods are compatibility inputs; a different build needs validation.
See [validation](validation.md) for the checks.
