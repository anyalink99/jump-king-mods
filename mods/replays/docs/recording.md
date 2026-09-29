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

Version 4 stores authoritative frame snapshots and a sparse vanilla-equipment
track. Equipment is written at frame zero and when the ordered set of worn
items changes. Workshop-reskin identity is not part of that track. Prerelease
formats have no decoder. Versions 2 and 3 remain readable; version 2 has an empty cosmetic track.

The sparse cosmetic track introduced in version 3 stores bounded Wardrobe+
presentation events. Playback uses
the current cosmetic packages and reconstructs at most 30 seconds of effects on
seek. It does not embed assets or freeze package versions. Older recordings still
animate but cannot reproduce cosmetic events that were not recorded. Ghosts and
seek reconstruction are silent; every actor owns its own particle and animation
state. Replay actors never add camera shake. A live player's effects are suspended
while the viewer owns presentation.

Version 4 adds each frame's draw-anchor offset relative to the body position.
Formats 2 and 3 retain their original `(9, 26)` anchor. Custom hitboxes can now
record their actual visual placement without changing collision or old files.

Frames also include the offset between the active sprite's origin and the canonical
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