# Changelog

## 2.6.1

- Use Runtime's shared list scroll indicator. Reserve space below a full library list for save status and commands.
- Require JK Runtime 1.44 or newer 1.x.

## 2.6.0

- Use the native timer step for clip duration and seek distances. Existing 17 ms
  recordings no longer appear about 2% shorter than the game's IGT.
- Save the authoritative victory time separately from clip length. Capture the
  initial timer at the first recorded pose, rather than recorder construction.
- Record and play native player sounds with their surface and equipment variants.
  Older files infer basic sounds from poses. Pausing, seeking and closing control
  playback voices; ghosts remain silent.
- Keep short cosmetic seeks silent as well as long ones.
- Add format 5 with bounded sound events and clock metadata; retain formats 2–4.
- Require JK Runtime 1.40 or newer 1.x.

## 2.5.0

- Publish the viewed replay as Runtime's camera target, including pause and seek changes. Smooth Camera can follow playback while the live player stays suspended.
- Preserve recorded native screen selection and refresh pointer seeks together with the displayed pose. Existing replay formats remain supported.
- Require JK Runtime 1.39 or newer 1.x.

## 2.4.0 - shared player presentation

- Use typed Runtime cosmetic actors and write replay format 4 with the recorded draw anchor. Continue reading formats 2 and 3 with their original native anchor.
- Requires JK Runtime 1.35 or newer 1.x.

## 2.3.0 — advanced cosmetic playback

- Write format 3 with bounded sparse Wardrobe+ events; retain the format 2 decoder.
- Give replay and ghost actors independent cosmetic clocks, particles and audio
  ownership through the optional Wardrobe+ bridge. Ghosts remain silent.
- Reconstruct up to 30 seconds of effects on seek without playing seek audio.
- Preserve animated sprite rendering alongside native texture fallbacks.

## 2.0.0 — JK Runtime migration (unpublished)

- Hard dependency on JK Runtime 1.x, using its common generated package SDK.
- Remove old first-party reflection/optional-runtime compatibility paths.
- Use shared lifecycle, settings and gameplay/state contracts; preserve feature behavior.
- Install only as part of the coordinated runtime release; manual combined acceptance is pending.

## 1.0.4

- Record equipped and unequipped player poses by comparing their base sprite
  layers symmetrically, so the vanilla wall-bounce pose is no longer reduced
  to an ordinary rising or falling frame.
- Moved `Record new runs` from the replay library into the ordinary Replays
  settings in both the main and pause menus.

## 1.0.3

- Bind the embedded UI bridge to the exact byte-loaded UIApi+ assembly already
  discovered by Jump King, just as it does for the owning Replays assembly.
- Exercise byte-loaded Replays and UIApi+ together in the load-order regression
  check.

## 1.0.2

- Resolve the embedded bridge back to the byte-loaded Replays assembly used by
  Jump King's native Workshop loader.

## 1.0.1

- Made Workshop loading independent of whether Replays or UIApi+ is discovered
  first. Replays now attaches its embedded UI bridge after both mods have been
  discovered, without bundling a second copy of UIApi+.

## 1.0.0

- Added frame-accurate replay recording saved only on victory or explicit
  manual request.
- Added a replay library, playback with seeking and a selectable racing ghost.
- Added root main-menu and pause-menu integration through UIApi+.
- Added a sparse vanilla Equip track containing the initial outfit and only
  subsequent equipment changes.
- Added a compact GZip-compressed replay format with no prerelease migration
  paths.
- Added a replay-owned IGT clock containing both vanilla time components so
  the native timer and wind cycle reproduce correctly even when recording
  begins midway through a run.
- Added deterministic vanilla Equip rendering, gameplay-asset revision checks,
  validated replay payloads and background atomic saves.
- Matched active sprites by rendering geometry and recorded their exact visual
  origin correction, removing half-pixel jumps at pose transitions.
- Made translucent ghost layers use the same post-origin integer pixel snap as
  Jump King's native `Sprite.Draw` instead of subpixel `SpriteBatch` origins.
- Moved recording to the player's final `LateUpdate` phase so body position,
  pose and sprite origin cannot straddle landing or takeoff updates.
- Added one-second `Saved!` feedback through UIApi+'s generic timed-action
  result contract.
- Advanced the unreleased replay format to version 2 so recordings captured
  before coherent late-update snapshots cannot masquerade as corrected files.
- Moved the racing ghost out of the foreground pass and directly before the
  player in native entity draw order, so the real King and all equipped sprite
  layers fully occlude it.
