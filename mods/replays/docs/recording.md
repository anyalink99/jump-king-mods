# Recording and playback

Each frame is captured by the player's last `LateUpdate` component, after
physics and movement-mod presentation. Position, pose and visual origin are
therefore sampled from the same tick.

Physics suspension is not necessarily a pause: Warp Jump continues animating
while its body is disabled. Runtime's owned `PresentationActivity` lets the
recorder retain those ticks and their origin/destination poses. Ordinary disabled
bodies without that declaration remain excluded. Pausing the game still stops
capture through the native update loop. This preserves duration and clock phase;
the pose format does not reproduce Warp's individual RGB particles.

## Format

Version 2 stores authoritative frame snapshots and a sparse vanilla-equipment
track. Equipment is written at frame zero and when the ordered set of worn
items changes. Workshop-reskin identity is not part of that track. Prerelease
formats have no decoder.

Frames include the offset between the active sprite's origin and the canonical
pose origin. Both the viewer and ghost subtract the origin before rounding the
top-left point, matching vanilla `Sprite.Draw` and preserving half-pixel
animation anchors.

World identity uses the Workshop ID when available, otherwise the local name,
author and screen count. A gameplay-asset hash detects map revisions.

## Playback session

An owned runtime session manages the visual proxy, camera, victory guard and
exclusive game-clock lease. The live body remains at its original position.
Recording is suspended for the duration of playback.

The recording stores both parts of the initial IGT state: live update ticks
and persisted seconds. Playback and seeking update the native timer from that
state. Wind consequently starts at the recorded cycle phase, including for
recordings made after Continue or partway through a run.

The ghost is drawn immediately before the native player entity, allowing the
live King and equipment to cover it.

## Storage

Compression runs off the game update thread. Final filenames are published
atomically; header caches are invalidated after saves and deletions.
