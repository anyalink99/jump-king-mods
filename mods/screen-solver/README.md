# Screen Solver

[Technical documentation](docs/index.md).

An experimental, read-only route planner for Jump King. Version 0.1.4 is an
installable gameplay module. Current source builds require JK Runtime 1.30+. It is not
yet a universal solver for modded gameplay.

## Use

Stand on a platform, release Jump, then press **F7**, or open **Screen Solver →
Solve screen** in the pause-menu mod settings. Change the binding in Controls+.
Bindings are saved in `ScreenSolver.Settings.xml` beside the mod.

The player is suspended while the window is open. Search advances in small
batches; Escape cancels and closes. Up/down selects a screen; left/right scrubs
the trajectory. The display shows terrain, takeoffs, inputs, relative ticks and
wind force at the selected point. Closing discards the result and resumes the
player. There is no autoplay, teleport or save-state rewind.

Targets are searched **up, then left/right, then down**. The side directions
share a tier. A teleport to a higher screen is still a side exit. A goal requires
three consecutive grounded, stationary, neutral-input ticks. An airborne screen
crossing is not enough. If an upper tier reaches its budget, any lower-priority
result explicitly leaves that upper route unresolved.

`hold Nt` means N simulated updates with Jump held, followed by release. It does
not mean N power frames: vanilla also advances charge on the release update, and
water affects charge accumulation. The complete input sequence is replayed twice
from the captured state before a route is displayed.

## Coverage and limits

The native model covers walking, charge/release, automatic maximum charge,
materials, Snake Ring friction, slopes, wind/NoWind, camera transitions and
native side teleports. Tests compare exact positions and velocities against the
installed executable, not a second copy of the model.

Vertical Wind (Workshop 3437222016, audited 1.0.0.0 binary) is supported. Its
screen markers rotate wind from horizontal to vertical without changing the
native wind latch, Snow or NoWind rules. Solve copies the marker set once; search
never reads the live mod's manager. The route viewer labels vertical wind UP or
DOWN. Changed binaries and additional patches still require an audit; see the
[adapter evidence](docs/vertical-wind.md).

Audited Jump% observation, JK Runtime 1.2.0 registration attribution and Mute
Jump SFX no longer block capture. Audio-only markers are omitted from the
simulation. More Block Sizes geometry is read after loading; Forced Slopes
collision lines are copied as they actually exist, including corrected
bottom-left slopes. These exceptions are scoped to exact patches, not entire
mod owners. See [passive integration evidence](docs/passive-adapters.md).

Custom Wind Switch's per-screen durations and strengths are copied into the
model, including its fixed-direction rules. Subframe Charge 0.15 is modeled for
supported, tick-aligned inputs: release uses its 17 ms quantizer and exact native
timer preload, including water. Buffered charges and automatic maximum charge
keep native timing. SFC action labels include milliseconds. A manually performed
input can start or arrive between updates; the planner does not promise that
arbitrary input phase, device latency or unsupported SFC devices reproduce the
simulated schedule.

The audited installed Switch Blocks, Expansion Blocks, Ghost of the Immortal
Babe Blocks, Anti Blocks, Movement Control Blocks, Conveyor, UpsideDownCore,
Sprinting, JumpKingPlus, High/Low Gravity and UpsideDown Blocks binaries can
coexist with Solve on native terrain when their effects
are inactive. This checks map-wide geometry and persistent modifier flags, not
just the current contact. It is **not support for their active custom mechanics**.

Known SFC observers, replay recording/ghosts, Save States and Jump King Manager
no longer block capture. Inactive Mega Warp/No Walk Off/Air Dash components are also
recognized. More Items' merchant dialogue guard, manual dispenser, jetpack
visuals and usage marker are classified separately from automatic pickups and
active rewind. The former do not block Solve; the latter still need providers.
These service objects are not invoked in search: no speculative input polling,
recordings, telemetry, save loading or foreign cache writes. Build identities
are checked; see [integration evidence](docs/workshop-coverage.md).

Initial capture requires a grounded player outside charge or splat recovery.
Giant Boots, active Casual/Ball/Jetpack/Warp/No Walk Off/Air Dash gameplay, changed
controller trees, unknown entities/blocks and unhandled patches still require
simulation providers. Screen Solver never disables other mods itself.

When Harmony coverage blocks capture, the window lists all unhandled patch
owners and their target counts. The game log contains the corresponding methods
and patch kinds. Supporting one owner does not implicitly approve its other
patches or patches from other mods on the same method.

World time continues while the modal is open. Wind annotations describe the
captured starting phase; they are **not an executable schedule for the moment
the window closes**. Live-phase rebasing is not implemented.

Search uses a finite collection of holds, walks and waits. Each tier has node,
tick and memory budgets. Exhausting that set or reaching a limit does not prove
impossibility. A replay-checked route is not a manual playtest. No simulation
capture, scans, speculative ticks or background workers run while the window is
closed; only the ordinary Runtime input binding remains registered.

## Build and install

```powershell
.\mods\jk-runtime\build.ps1
.\mods\screen-solver\build.ps1
```

Payload: `build/screen-solver/UPLOAD_TO_WORKSHOP/ScreenSolver.dll`. Harmony is used
by the headless tests to suppress presentation and apply actual installed-mod
patches; it is not included in the payload or referenced by the gameplay
implementation. Tests require the audited VerticalWindMod.dll from Workshop
3437222016 (override its location with `-VerticalWindDll`) and the four fixtures
listed in the passive integration evidence (override the root with `-WorkshopDir`).
Those DLLs are test inputs, not runtime dependencies or redistributed payloads.
The combined integration suite also requires the installed first-party packages
and Workshop fixtures listed in `build.ps1`. Their module bytes are extracted
only into the generated test directory; their entry points are not started.

`install.ps1` builds both projects, backs up existing DLLs, installs JK Runtime
into its Workshop item and Screen Solver into local `Content/JKMods`. It refuses
a running game or duplicate installations and verifies copied hashes. Settings,
saves and other mods are left untouched.

## Verification

- 141,120 full native controller-tree comparison ticks, including ledge starts,
  charge/release, materials, water and Snake Ring combinations.
- Another 141,120 controller ticks with the actual passive integrations patched
  in and Mute SFX markers present. Simulated steps must leave Jump% and audio
  state unchanged. Capture is tested with the real Runtime observer hooks.
- 40,848 exact slope collision comparisons covering custom sizes, corrected
  geometry and snapshot isolation from foreign constructor lists.
- 8,000 world comparison ticks including 60 teleports, both wind clocks, NoWind,
  four slope orientations and camera transitions.
- 64,000 additional world comparison ticks against the actual Vertical Wind
  patch, with and without screen markers, including 480 teleports, both wind
  signs and Snow. Tests also check snapshot ownership and patch-audit refusals.
- 5,449 body comparison ticks across 96 trajectories with walls and ceilings.
- A native-physics multi-jump fixture finds a six-action route and replays it twice.
- The installed `Content/level.xnb` is decoded through the game's block factory;
  the main-game first-to-second-screen fixture finds and replays a four-jump
  route with native and SFC controls.
- The combined Workshop suite loads thirteen gameplay libraries alongside the
  five passive integrations. It tests native and SFC control sequences, capture
  with the installed replacement jump node and body pipeline, and route replay.
- 3,002 exact comparisons against the installed SFC quantizer, followed by
  141,120 native-controller ticks with that binary's timer preloads. This tests
  modeled release semantics, not end-to-end polling of a physical device.
- 61,740 Custom Wind Switch waveform samples and 16,000 world ticks, including
  custom durations/intensities and fixed-direction precedence.
- Synthetic tests cover priority, settling, timed routes, limits, cancellation
  and rejection of nondeterministic states/events.
- The complete installed More Items entity/component inventory is classified by
  a regression test. Capture, search and cancellation must leave the merchant's
  disabled dialogue tree and guard entity unchanged.

In-game appearance and manual route execution still require a playtest. See
[design](docs/design.md) for remaining integration work and the provider contract.
