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

Version 5 stores authoritative frame snapshots and a sparse vanilla-equipment
track. Equipment is written at frame zero and when the ordered set of worn
items changes. Workshop-reskin identity is not part of that track. Prerelease
formats have no decoder. Versions 2–4 remain readable; version 2 has an empty cosmetic track.

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

Version 5 adds the recorded `TargetElapsedTime` interval, optional completion
IGT and a sparse native player-sound track. Completion IGT comes from the game's
saved victory statistics, not the last pose or frame count. A manual snapshot
has no completion result. Clip duration measures captured frames only; a clip
started after Continue can be shorter than the full attempt's completion IGT.

Formats 2–4 use the installed game's historical .NET Framework interval of
17 ms. They have no authoritative victory time. New readers correct their clip
duration without rewriting the files or guessing the missing final ticks.

Native player-sound calls are observed by a world-scoped hook and stored at the
next pose capture. Up to 1,000,000 events and 32 sounds per frame are retained.
Playback borrows loaded game assets, preserving map replacements and volume
preferences. Pausing suspends voices; seeking and closing stop them. A missing
legacy track infers basic jump, land, bump and splat sounds from pose changes;
an empty recorded track remains silent. Custom mod sound implementations and
arbitrary scripted audio are not recorded. Native screen ambience continues to
follow the viewer's camera; playback does not dispatch gameplay screen events.

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

Runtime 1.40.1 or newer keeps viewer controls exclusive across native and
Subframe Inputs paths, including 240 Hz menu updates. HUD, seek, play/pause and
close bindings cannot also toggle equipment, move, restart or pause the live
game. Hiding the HUD keeps the modal input lease. Closing waits for release;
forced teardown also prevents held buttons from reaching gameplay afterward.

The viewer owns a scoped Runtime camera target, publishing its world center,
recorded screen, velocity and playback pause state with the displayed pose.
Smooth Camera 0.10 or newer follows that target, including map-controlled ranges
and zones. Seeking explicitly resets camera framing, even while paused. Closing
the viewer releases the target; ordinary race ghosts never acquire it.
Formats 2–5 use the viewer's existing `(9, 13)` center offset because they do not
store a camera-center track. Native screen selection follows the recorded screen.

The recording stores both parts of the initial IGT state: live update ticks
and persisted seconds. Playback and seeking update the native timer from that
state. Wind consequently starts at the recorded cycle phase, including for
recordings made after Continue or partway through a run.

Native simulation updates still pass a normalized `1/60` delta. The viewer
advances one recorded frame per native tick at matching cadence; using 17 ms for
the displayed clock must not slow playback or alter cosmetic simulation ages.

The ghost is drawn immediately before the native player entity, allowing the
live King and equipment to cover it.

## Storage

Compression runs off the game update thread. Final filenames are published
atomically; header caches are invalidated after saves and deletions.
