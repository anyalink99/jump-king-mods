# Testing Subframe Charge

## Automatic evidence recording

Normal builds run `EvidenceTraceTests` alongside the native input/charge fixtures.
The tests check ordered bounded prehistory, late events after an incident,
overlapping incident windows, and unchanged native `_can_jump`, physical history
cursors/generation and sampler queues after capture. The log fixture checks
asynchronous flush and three bounded 16 MiB generations. Compiled Jump% tests
read the normal package's log and confirm charge failure/correction evidence.
The capture benchmark reports synthetic idle overhead; it is not a hardware
latency measurement or a guarantee for a particular PC.

## Native visual endpoints

`NativePredictionTests` compares production prediction with the installed
BodyComp's next actual update. The fixture covers all four slope orientations,
a chain of 8-pixel slopes above ice, sand from above and the side, sand in water,
both horizontal directions and neutral input, rising/zero/falling initial
velocity, and both positive and deep negative world coordinates. Collision,
gravity and native material behaviours run unchanged; AV, wind, screen capping
and teleport services are omitted in the isolated fixture.

Every intermediate draw must remain outside initially clear native solids, and
prediction must leave body position, velocity and knocked state unchanged.
Endpoint comparisons exclude initial solid overlap and leaving the supported
screen region. They allow at most two pixels of error for discrete edge cases;
they do not assert that prediction is exact for unknown modded collision rules.
Separate regressions cover outgoing sand speed, water scaling, near-zero speed
rounding, and collision/normal hooks that throw if speculative code calls them.
The synthetic benchmark reports the complete bounded scan, contact endpoint
and three intermediate draws, separately from the legacy sweep-only path.

## Text input ownership

The performance fixture includes `TextInputRoutingTests`: a real native
ControllerManager, MenuController and wrapped keyboard pad with injected physical
samples. It checks all eleven bindings (editing keys and a Ctrl+A chord), capture
after publication, forced close while held, independent release, fresh presses,
gamepad input, completed between-tick taps, a whole edit between worker deliveries,
and disabling Inputs during capture. Native and high-rate input are exercised
with both presentation settings. No getter hooks replace published pad fields.
Runtime's text tests cover editing commands, nested/custom captures, mouse hold
draining and independent slow/fast sources. MGE tests acquire/reopen/dispose its
custom editor through the public lease.

These fixtures validate routing against installed native classes; they do not
replace an interactive check of the user's keyboard, window focus and loaded
third-party mods. Physical evidence samplers deliberately keep diagnostic history.

## Buffered quarter steps

`PerformanceTests.exe --buffered-jumps` constructs the installed game's complete
player behavior tree. It compares original JumpState against whole-step SFC and
quarter-step SFC across dry/ice, water, landing, walking off, short/long input
buffers, release phases, native input delivery lag and automatic charge maximum.
The native ice/water behaviours remain in place; the fixture supplies ground
occupancy and input edges without simulating a level. Launch ticks, charge poses
and horizontal velocities must match the original tree, including both accepted
and rejected releases around its 1/30-second airborne leniency window.

Compiled state tests separately check quarter buffered power and Jump% after
native acceptance. A release observed by the sampler while native Jump remains
held must not preload the timer or change the native maximum frame. Whole-step
and observation-only buffers retain their previous exact native power.

## Quarter-step charge

Quantizer tests sweep dry, water, 0.75 and 2x multipliers at quarter-frame
centers and both sides of rounding boundaries. They retain integer powers,
minimum/full charge, the native release increment and independent saved settings.
The compiled state fixture sends measured press/release edges through the installed
native JumpState release path for all 137 dry and 281 underwater powers. It
checks Runtime exact counts, the actual Jump% calculator and fractional labels,
plus completed taps. Native landing-buffer comparisons run with quarter mode
both on and off. Runtime checks legacy constructor compatibility and rejects
inconsistent or nonfinite fractional event data. These fixtures do not establish
physical device timing precision or arbitrary third-party compatibility.

The installed DoJump impulse fixture additionally checks every dry/water-scaled
fraction with snow on/off and left/neutral/right input. It invokes native velocity
and threshold arithmetic while suppressing audiovisual side effects; its snow
occupancy is an injected block state rather than a rendered map.

## Continuous terrain presentation

The optional installed-campaign fixture loads the actual level XNB, constructs
native blocks and installs the current SwitchBlocks/UpSideDownCore slope patches.
It checks intermediate ice/slope movement against surrounding campaign geometry.
Unknown geometry and throwing collision callbacks must not freeze presentation
or execute speculative side effects. Bounds, scan budgets, sand and absent worlds
are checked for continued motion. Position history tests cover material-modified
movement, grounded ice, stopping and a fresh walk/takeoff after body integration.

## Title completion and loading

The transition fixture runs the installed GameTitleScreen.Run/MyRun inside a
native parent BTsequencor, then an observed loading node and gameplay node.
Only content construction and Steam downloads are replaced with test endpoints.
It presses Continue through a presentation input frame, tests immediate/delayed
outros, both refresh modes, switching Inputs off at exit, and returning to a new
menu lifetime. Menu activation, loading and title construction must each happen
once. Runtime session tests additionally cover failure and terminal outcomes.

These lifecycle tests do not measure real map I/O. Diagnostic mode attributes
resource loading, screen loading, entity/prop creation and attempt startup by
native method. Capture a real affected map before changing load/save timing.

## Startup and input ownership

Startup fixtures execute the installed WaitForPressStart and private PressStart
nodes after a completed Space tap, with both presentation rates. The intro keeps
native delta/order and the fast menu cannot drain its pending edges. A second
native tick must not replay the tap. Existing keyboard fixtures retain a real
charge owner while exercising delayed native snapshots.

Tap-ownership checks distinguish the actual live correcting node from Enabled,
an old player retained on the title screen, measurement-only mode, suspended
controllers, unknown devices, unhealthy samplers and changed player references.
Runtime geometry checks reject foreign getters and subclasses without calling
them. Prediction tests install throwing native collision hooks and verify continued
native shape clipping without invoking the hooks. Complete mod compatibility is not implied
by these fixtures; undeclared native detours still need individual investigation.

## Collision-aware presentation

The native geometry fixture samples 101 positions along each predicted path:
thin floors, walls, ceilings, diagonal corners, wall sliding, negative world Y
and all four native slope orientations. Live LevelScreen fixtures cover neighbor
discovery, geometry replacement, teleport boundaries, scan/shape limits, sand
and missing worlds. Unknown shape callbacks must not execute. GPU checks draw
the native player approaching a floor and verify that its foot marker never
passes through it while body position/velocity remain unchanged. Synthetic
timings cover local boxes and a 1,001-block screen with a native slope.

In-game acceptance should include falling onto platform corners, walking into
both sides of a wall, ceiling hits, slopes and screen transitions with Smooth
Camera on/off. The path does not simulate speculative bounce or custom block
behaviour; those still resolve on the next authoritative native update.

## Input publication and mode transitions

The isolated `--input-startup` process warms the installed ControllerManager and
native getters before installing production hooks. It checks all eleven keyboard
actions across 48 refresh mode selections and 24 complete input enable/disable
cycles. Real ControllerManager.Update publishes pending taps before selecting
the main device and updating menus. No fixture intercepts PadInstance.Update,
GetState or GetPressed. Physical reads and device enumeration are deterministic
test doubles; this is not a hardware latency measurement. Held Confirm is tested
through a refresh switch and a physical/native release delayed across input
deactivation; it cannot replay into native menus, while a fresh press works.

Menu tests retain a single pause-tree owner through refresh toggles. Runtime's
tests repeatedly insert/remove pins with an active child, preserve hidden rows,
defer removal of an open child, retain the tree after failed layout, and verify
pinned native button resets. Menu frame tests reject nested dispatch and clear
input on normal/exception exit without restoring stale confirmation.

## Presentation and optimization checks

`build.ps1` also runs `PerformanceTests.exe` against the installed MonoGame and
native classes. It checks 2,400 presentation steps without physics-clock drift,
inputs-only presentation, partial-time handoff, toggling during catch-up,
all eleven action latches, a completed 1 ms keyboard tap, focus/release gating,
native PadInstance dispatch, and fast native pause/title trees without double
advancement. The XML watcher fixture replaces data with the same size/timestamp,
checks parse failures/recovery, and verifies player-lookup invalidation on reorder
and removal. GPU fixtures exercise patched PlayerEntity.Draw, quarter-tick
prediction, stationary HUD, foreground occlusion and unchanged body state.

After building Smooth Camera, run:

```powershell
& build/subframe-charge/_INTERNAL/PerformanceTests.exe "$PWD/build/smooth-camera/_INTERNAL/SmoothCamera.Module.dll"
```

This adds both clock patch orders and live ownership transfers. Fixture PNGs are
in `build/subframe-charge/_INTERNAL/presentation-graphics`. These are isolated
tests, not a full playthrough, physical-device latency measurement or proof of
universal mod compatibility. Test actual menus, short taps, focus changes, water,
ice, wall/ceiling impacts, map changes and alternate movement modes in game.

Builds run unit tests and installed-game contract tests. They cover dry/water
strengths, native and measurement-only modes, buffered jumps, missed taps,
pause and maximum-charge races, ice continuation and graph restoration.
The fully disabled fixture checks that repeated refreshes retain the original
native charge node, timer, sprite and component order. Enabling measurement then
disabling both options must release all SFC player components and sampler leases.
This does not by itself reproduce a reported transient in-game buffered pose.

The optional installed ConveyorBlockMod fixture first reproduces its original
exit NullReferenceException with a real compiled SFC subclass, then checks the
Runtime adapter across installed Harmony engines, exit velocities, reset guards
and repeated graph restoration. Missing Workshop item 3330536917 is reported as
a skip. Charge lifecycle tests also check external cancellation clearing active
evidence/deferred correction without losing an idle early press. For interactive
acceptance, walk off and release Jump near both belt edges with correction on,
measurement-only, and both settings off; repeat after a restart and a mode toggle.

`scripts/check-mods.ps1 -Mod @('subframe-charge','hammer-king')` also runs both
compiled modules through Runtime's real charge registration and recomposition:
both load orders, correction on/off, repeated toggles, sampler/graph cleanup,
undeclared custom JumpState ownership and malformed graph refusal. This fixture
stubs physical input/window access, not the installers or graph bindings.

Input tests inject observations into production reader/publication paths.
They cover loss, reconnect, changed bindings, blocked readers, mixed devices,
axes, POV hats and queue limits. Matching timestamp streams must select
matching strengths; that does not prove different physical devices supply
identical timestamps.

## Reproducing a device report

The native keyboard regression separates physical press time from MonoGame's
cached held snapshot by one physics update, then runs the installed game's
InputComponent.Update and TryConsumeJump. It checks both 240 Hz settings, native
jump eligibility, no repeated held jump, a second real press, resume suppression
and no late native Confirm replay. Pad-bit assertions alone do not verify this
contract: an early Jump edge without held state is rejected by the native game.

Use one known DLL build and restart the game before testing. Record transport
(USB, wireless receiver or Bluetooth), Steam Input state and Jump bindings.

- Try primary/secondary buttons and any chord or axis binding.
- Repeat short, medium and near-maximum holds, with correction on and off.
- Test water, landing buffers, ice continuation and pause during charge.
- Disconnect a neutral controller while holding keyboard Jump, then repeat
  with the controller itself holding Jump.
- Reconnect while held, release once and start a fresh press.
- Toggle SFC and other movement modes; check for stale charge or input state.

After exiting, preserve `SubframeCharge.log` and its rotated generations,
including the session header and build ID. Give the approximate time of any
visible anomaly. Use the measured duration, not a macro's requested sleep,
when comparing powers.

Keyboard state merges physical keyboards. A permanently stuck driver cannot
safely be killed by managed code; its source becomes unmeasured. These are
platform limits, not evidence that the game used a precise timestamp.
