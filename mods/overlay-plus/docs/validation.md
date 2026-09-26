# Validation

`scripts/check-mods.ps1 -Mod overlay-plus -Integration` builds a fresh Runtime,
runs its API and foreign-patch compatibility stages, then builds and checks this
mod. Overlay's graphics tier runs behavioral assertions against the
installed Jump King/MonoGame assemblies, including:

- Enable defaults/missing-field migration, independence from Hide HUD, disabled
  preparation without a game/device, interrupted tracking teardown, drained
  pending preference writes and locked-file rollback. Native GPU tests verify
  immediate SpriteBatch/target disposal, removal of every Overlay patch (including
  adapters), repeated re-enable/disable and world exit. Disabled execution has no
  installed per-frame hooks; enabled per-frame paths are unchanged by this switch.

- All 27 native Areas partitioned as 10/9/8; explicit starts and Continue; GoTB
  authored order; overlapping unlock boundaries; full custom Area lists.
- Grounded transitions, falls, repeated entries, skipped Areas, final victory,
  independent pause clocks, restoration, category/rules PB separation and golds.
- Full-window coordinates, side-bar anchors, missing bars, rescue and deep history.
- Per-arrangement Controls size restoration over repeated switches/XML reloads,
  custom sizes, immediate physical presses/releases, reuse of native controller samples,
  zero additional driver reads across 120 draws, isolation from reused input arrays,
  rebinds, focus loss and optional minimum flash without a long-hold release tail.
- Native timer method execution with isolated settings/stats: original time format,
  precision, visibility, foreign postfix preservation and safe settings updates.
- GPU scene/UI separation without rebinding or erasing the native scene target.
  HUD text and one-pixel borders match direct output rendering pixel for pixel at
  1280x720, 1920x1080 and 1440x900; native translucent/opaque UI stays above them.
  Closing a menu leaves no stale UI pixels, and repeated capture entry is harmless.
- Legacy presentation migration preserves positions and optional clock content.
- XML round trips (including repeated input-list clones), Unicode, nonfinite
  imported values, archive validation, atomic backups and corrupt-file recovery.
  Detached save snapshots survive subsequent nested edits; coalescing keeps the
  newest checkpoint and the worker recovers after an injected write failure.
- Provider discovery/visibility restoration and installed-game Harmony contracts;
  unused and disabled providers do not execute content callbacks;
  coexistence with current Smooth Camera patches when explicitly selected.
  Independent patch leases sharing the Overlay owner ID survive presentation
  teardown. Explicitly selected Replays validates current adapter discovery,
  while an injected failed ghost commit preserves the previous selection.
- Actual native fonts/frames and native map textures on a MonoGame GPU
  fixture; HUD pixels in the black side bars; native Cyrillic metrics; device-state
  restoration; native foreign-text capture scope and stale-frame clearing.
- Real editor handler sequences for drag, resize, undo/redo, duplication, Unicode
  text entry, locked deletion, cancellation and panel hit blocking.

The integration tier writes previews under
`build/overlay-plus/_INTERNAL/graphics`: HUD/editor at 1280x720 and editor at
960x720 and 1920x720, an opaque foreground-layer fixture, and sharp-HUD composition
fixtures at all three tested output resolutions. These are isolated render
fixtures using installed native assets, not screenshots of a live game session.

Runtime tests also cover native cursor bitmap/handle reuse and restoration,
window-editor lease ownership, nested disposal, reset and draw persistence.

Every build also checks adverse native package discovery and an isolated installer
fixture: focused scope, local/Workshop placement, user-data preservation,
duplicates, failed verification, locked dependencies, running-game guard and
rollback after an injected partial installation failure. No test targets real
player saves. Release packages contain three DLLs and no native game/font assets.

Not established by these tests: a complete live playthrough of all campaigns,
physical controller/mouse behavior on every device, long-session disk failures,
Steam overlay focus behavior, and end-to-end replay/Jump% operation in a live
modded session. Those remain live smoke-test items. The tests do not certify
speedrun eligibility or universal compatibility with unregistered foreign HUDs.

## Performance probes

Run the built `OverlayPlusTests.exe <game-directory> --profile` for input and
archive CPU measurements, or `--profile-render` for GPU-backed draw submission.
The probes use isolated data and reuse render targets during the measured loop.
Initialization, cold device polling, XML snapshot capture and background encoding
are different costs; report them separately. These probes do not measure live FPS
or end-to-end input latency.
