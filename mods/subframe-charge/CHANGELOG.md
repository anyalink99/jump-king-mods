# Changelog

## 0.25.0 — automatic jump diagnostics

- Include the bounded input/charge evidence recorder in every normal build.
  Unsupported launches retain recent history and 30 subsequent player frames
  without consuming input or changing charge rules.
- Use the standard `SubframeCharge.log`, with three 16 MiB generations and an
  `evidenceTrace=sfc-evidence-v1` header. Separate diagnostic DLLs are no longer needed.
- Run recorder isolation/history tests with the normal build checks.
- Build against JK Runtime 1.30 and declare that API requirement in the package.

## 0.15.3 — external jump cancellation

- Clear active sampled charge and deferred timer correction when a block calls
  ResetResult. Preserve idle early presses and queued input transitions.
- Verify the original installed ConveyorBlockMod with the compiled SFC node,
  including exit momentum/reset conditions and repeated graph restoration.
- The conveyor crash repair itself is in JK Runtime 1.15.2; update both packages.

## 0.15.0 — JK Runtime migration (unpublished)

- Hard dependency on JK Runtime 1.x, using its common generated package SDK.
- Remove old first-party reflection/optional-runtime compatibility paths.
- Use shared lifecycle, settings and gameplay/state contracts; preserve feature behavior.
- Install only as part of the coordinated runtime release; manual combined acceptance is pending.

## 0.14.0

- Preserve native power, release timing and automatic maximum for true buffered
  holds: the press precedes the first native-eligible player update. Do not
  round these tick-aligned charges or replay a buffer that vanilla did not accept.
  Ordinary post-landing presses retain physical timestamps and SFC correction.
- Show `SFC: Buffered` during and after measured buffered charges, both enabled
  and disabled. Leave native Jump% frames/percentage unchanged; omit ms/would/max
  suffixes. Missing or invalid evidence still reports `Not supported`.
- Verify buffered release phases and automatic-max races against the installed
  native JumpState and Jump%, including underwater and observation-only mode.

## 0.13.1

- Show the neutral `SFC: -` placeholder before the first measurement and after
  resetting it (level/mode changes). Reserve `Not supported` for an actual
  charge/jump without valid timing evidence. Charging, physics and input are unchanged.
- Add startup and enabled/disabled reset regressions; retain checks for genuine
  unsupported charges and jumps.

## 0.13.0

- Exclude explicit native pauses from effective charge time. One persistent
  PauseManager observer survives menu toggles without mutating the component
  list currently being enumerated. Slow frames are not treated as pauses.
- Apply an already measured release before testing native automatic maximum,
  even if the game's input snapshot is still down. Keep an independent native
  timer until takeoff so later evidence loss cannot leave a half-applied fix.
- Give keyboard, every XInput slot and every DirectInput GUID independent
  workers and per-source health. No driver reads, joins or handle disposal on
  the game thread. A neutral device's loss does not poison another source's
  hold; a contributing device's loss invalidates rather than fabricating up.
- Require neutral re-arming after acquisition/loss for all physical backends.
  Bound pending edges to 256, invalidate overflow/out-of-order evidence, reject
  old configuration callbacks, and prevent duplicate reads/owned DI handles
  after a driver remains stuck through reinstall.
- Use owned SharpDX XInput controllers with installed MonoGame conversion;
  do not race MonoGame's shared connection/timeout arrays. Disconnected XInput
  retries are throttled. No additional Workshop dependency.
- Cache binding resolution while checking ordinary runtime rebinds immediately;
  retain periodic discovery of opaque third-party mappings and assembly-load
  invalidation for Controls+. Stop additional game-thread connection polling.
- Evaluate bound DI controls without constructing native list/array decoders
  every millisecond; compare the optimized path against the installed native
  decoder. Separate binding discovery and input bookkeeping from charge logic.
- Add per-source health diagnostics and rotate logs at 4 MiB, retaining two
  previous generations. Cap diagnostic lines and queues.
- Add permanent native pause/max-race, health, worker-blocking, epoch/queue,
  binding-cache, native decoder and log-rotation regressions. Hardware latency
  remains subject to the manual checks now collected in docs/testing.md.
- Companion Ball King 1.3.3 accepts replacement JumpState subclasses, re-resolves
  after toggles and no longer requires a native jump node for optional particles.

## 0.12.1

- Replace the stale pre-collision grounded check and held-input 50 ms heuristic
  with native JumpState visitation history. Landing/recovery establishes an
  eligibility interval; post-landing presses keep their physical timestamp.
- Anchor buffered charge to the player update before physics, not the later
  charge handler. Independent component observers also run with BodyComp
  disabled; native ice continuation remains in control of the game tree.
- Do not resurrect a completed airborne tap in a later eligibility interval.
  Keep the separate stale completed-tap deadline and device-loss watchdog.
- Log raw/effective hold provenance, eligibility/frame/origin timestamps,
  native update count, min/max update spacing, release delivery and build ID.
- Add ordered-frame regressions against the compiled mod and installed native
  JumpState/Jump%, plus landing-phase sweeps and native component dispatch/
  restoration tests. Retain all 35 dry and 71 underwater charge strengths.
- No frame-rate change, adaptive calibration or new Workshop dependency.

## 0.12.0

- Shorten the Jump% measurement label to SFC. Add a separate, saved Show SFC
  checkbox in both menus; hiding the label does not interrupt measurement.
- Keep sampling with correction disabled through a native-pass-through state.
  Do not change the native timer/strength or replay missed taps in this mode;
  register its observation-only lifecycle marker without a modified-run flag.
- Show measured ms instead of Disabled. For measured releases only, compare
  SFC's predicted input frames against actual native Jump% and append
  `(would Nf)` only on disagreement. Never overwrite native Jump% when disabled.
- Verify old-settings migration, display independence, all 35 dry/71 underwater
  native powers and actual Jump% counters through the installed native routine.

## 0.11.0

- Add high-rate native legacy/DirectInput input through an independent,
  nonexclusive device connection using the game's GUID and physical Jump binds.
  Reuse the installed native button/axis/POV decoder and Controls+ binding API.
- Run driver reads on their own worker, aggregate with keyboard/XInput holds,
  and feed genuine timestamps into the existing charge correction path.
- Reject missing/lost observations instead of generating releases. Retry failed
  devices with backoff and wait for neutral after acquisition/reconnection.
  Ignore old-generation callbacks after rebinding/removal; isolate device cleanup.
- Extend remote diagnostics with DI connection/readiness/error/edge records.
  Add mixed-input, native decoder, disconnect, stale-data and compiled timer tests.
- No extra Workshop dependency/DLL. Physical 8BitDo validation remains pending.

## 0.10.2

- Add automatic device diagnostics: native pad type/assembly, display name,
  save identifier, legacy product GUID when available, backend, XInput slot,
  enabled Jump bindings and resolved physical buttons/chords.
- Associate cached per-device Jump edges with charge diagnostics; distinguish
  unsupported input, missing sampled edges and mid-charge binding changes.
  Log connection/binding changes without polling extra physical buttons.
- Add session/version headers and batch log writes off the gameplay thread.
  Diagnostic metadata failures do not interrupt gameplay.
- Keep contract-test logs in _INTERNAL, archive old payload logs and fail the
  build if a test log leaks into UPLOAD_TO_WORKSHOP.

## 0.10.1

- Accept a still-held sampled press when the native game starts a buffered
  charge, instead of rejecting it after 50 ms. Count effective charge time from
  native entry; do not grant charge for time held in the air.
- Track ground/recovery readiness so short pre-landing buffers also exclude
  airborne hold time. Preserve queued new presses when the jump node resumes.
- Keep the stale released-tap deadline, unsupported-device checks, early-input
  handling and water/ice contracts. Add buffered/resumed and fast-release tests.

## 0.10.0

- Remove `(charging)` from the live SF Charge measurement.
- Round measured input frames before scaling by the native water multiplier.
  Preserve underwater half-strength steps and the scaled release increment;
  show the correct input frame count and percentage in Jump%.
- Replace and restore all references to the shared native JumpState, including
  the airborne running check and continuation, preserving ice jump leniency.
- Keep PlayerEntity's registration target and shared custom sound/particle
  registries consistent while the mod is enabled and after disabling.
- Compare all 35 dry and 71 underwater release powers with the installed
  native JumpState; exercise ground-to-air continuation and its native expiry.

## 0.9.5

- Fix a 0.9.4 regression: idle JumpState Failure cleared an already-sampled
  press before the game's frame input saw it. Preserve pending input across
  idle failures, while still clearing cancelled/completed charges and rejecting
  stale taps at entry. This restores actual timer correction, not just the label.
- Add a compiled-state lifecycle regression test for both press delivery
  orders, early release delivery, timer preloading and unsupported mixed input.
- Log missing versus stale press timestamps and unsupported-device rejection
  separately instead of reporting every rejected press as age zero.

## 0.9.4

- Correct the wall-clock grid to 17 ms: Game1's .NET Framework FromSeconds
  rounds its nominal 1/60 target to whole milliseconds. Keep the simulation
  timer at 1/60 second. This corrects the 0.9.1 timing assumption below.
- Never manufacture a sampled press timestamp for unsupported input. Leave
  native Jump% values untouched unless a measured release actually corrected
  the native timer; mixed unsupported input invalidates that charge.
- Add measured milliseconds beside Jump%: `SF Charge: 200 ms` or
  `SF Charge: Not supported`. Auto-launch times carry `(max)`; disabled mode
  shows `SF Charge: Disabled`. Reuse Jump%'s own Harmony dependency.
- Check every charge window, the 459 ms / 27f regression, native-value
  preservation, and the installed Jump% draw patch in both discovery orders.

## 0.9.3

- Add the `AllowSubframeCharge` custom-level tag.
- Let opted-in maps use Subframe Charge without incrementing Jump King's
  external player-behaviour counter or disabling achievements.
- Keep the modified-run marker enabled by default on every map that does not
  declare the permission.

## 0.9.2

- Replay a complete Jump press/release that occurs between two game updates
  through JumpState's native start and release path.
- Make sub-frame taps produce the minimum 1f jump instead of being invisible
  to the game, without retaining stale taps as a delayed jump buffer.

## 0.9.1

- Include JumpState's native release-update timer increment when converting a
  rounded physical hold into jump power.
- Eliminate the invalid 0f charged release that could return to idle without
  calling the native takeoff routine.
- Align Jump% labels and percentages with vanilla semantics: 68 ms now selects
  displayed frame 4 and its corresponding release-inclusive strength.
- Keep the timing grid at Jump King's exact 60 Hz (`16.666... ms`) rather than
  introducing cumulative drift with literal 17 ms intervals.

## 0.9.0

- Sample connected XInput controllers alongside the keyboard at 1 ms.
- Respect each Xbox controller's current in-game Jump binding and user slot.
- Combine simultaneous keyboard and XInput holds with Jump King's native OR
  semantics, including Steam Input controllers exposed as virtual Xbox pads.
- Resolve Controls+ virtual Jump buttons back to their physical single-button
  or chord alternatives without adding a hard UIApi+ dependency.

## 0.8.0

- Renamed the mod to Subframe Charge.
- Renamed the DLL, settings, log, namespaces and build paths.
- Use only the new Subframe Charge API and settings contract.

## 0.7.6

- Coordinate jump-node ownership with Casual Jumping independently of DLL
  load order.
- Keep precision charging active in Casual mode and yield the native jump node
  to Casual+.

## 0.7.5

- Add optional runtime integration with Last Jump Value / Jump%, correcting
  its displayed frame count and percentage after a precision jump.
- Keep the Workshop payload independent of Jump%.

## 0.7.4

- Keep the passive trajectory observer active while Subframe Charge is
  disabled and label measurements as `precision` or `vanilla`.

## 0.7.3

- Record each jump's post-physics launch velocity, apex position and actual
  rise so fixed-duration macro tests validate the trajectory rather than the
  frame counter used by Last Jump Value / Jump%.

## 0.7.2

- Replaced the `Enabled: Off / On` text selector with Jump King's standard
  `Enabled` checkbox.

## 0.7.1

- Stop correcting the native timer while Jump is held, leaving maximum-charge
  activation and buffered-jump entry entirely untouched.
- Apply the phase-independent rounded timer only once, on a sampled release
  while the native JumpState is already charging.

## 0.7.0

- Restored Jump King's complete native `JumpState.MyRun` ordering, input
  buffering, animation, walking interaction and takeoff path.
- Limit the mod to correcting the native private charge timer from the
  millisecond input timestamps immediately before native release handling.
- Stop launching jumps directly from the sampler queue.

## 0.6.1

- Log the exact physical Jump key codes and compare the 1 ms sampler edges
  against Jump King's own per-frame input edges.
- Log release delivery and measured hold time to isolate delayed activation
  from missed release events.

## 0.6.0

- Replaced the low-level keyboard hook with direct 1 ms Win32 physical-key
  polling after live logs showed hook-to-game delivery was fast only for the
  events the hook actually received.
- Discard input edges older than 50 ms instead of allowing them to become
  delayed jumps.

## 0.5.1

- Install the precision state even when the game window is not focused during
  level startup.
- Discard queued global keyboard events whenever game focus changes, so input
  outside Jump King cannot become a delayed in-game press.

## 0.5.0

- Removed the experimental 600-step `Precision 1000` mode.
- Reduced the menu setting to `Enabled: Off / On`.
- Keep only phase-independent rounding to the 36 vanilla charge strengths.

## 0.4.2

- Fixed delayed ghost presses caused by treating Windows keyboard auto-repeat
  as a new Jump press after clearing the pending event queue.
- Track every bound Jump key independently and release the effective Jump only
  after all bound keys are up.
- Log hook-to-game delivery latency when a charge begins.

## 0.4.1

- Fixed keyboard Jump binding discovery when the pause-menu context exposes no
  keyboard through `GetConnectedPads()`.
- Resolve the same in-game keyboard profile directly without reintroducing
  frame-timed input.

## 0.4.0

- Removed frame-input fallback from assisted modes.
- Resolve Jump keys from the connected keyboard pad instead of the current
  main/combined controller.
- Leave the native jump state untouched when the keyboard hook or binding is
  unavailable.

## 0.3.2

- Fixed frame-input fallback resetting and restarting charge every frame when
  the active pad had no keyboard-hook key array.

## 0.3.1

- Stopped clearing the precision charge state from `ResumeRun`.
- Added a compact transition log for charge progress and native jump velocity.

## 0.3.0

- Replaced background-thread `Keyboard.GetState()` polling with a Windows
  low-level keyboard event hook.
- Added an automatic frame-input fallback when the event hook is unavailable.
- Added focused hook installation, press, release and repeat-filter tests.

## 0.2.0

- Removed the unsuccessful experimental 1000 FPS game-loop mode.
- Moved precision charging into a `JumpState` replacement at the native
  behaviour-tree position so walking, charging and release use the correct
  state order.

## 0.1.0

- Added phase-independent 36-step charging from a 1000 Hz keyboard sampler.
- Added a millisecond-quantized 600-step charge mode.
- Added main-menu and pause-menu charge settings.
