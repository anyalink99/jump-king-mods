# Screen Solver design

## Implementation status

Screen Solver is an installable experimental module with Solve, cancellable search
and a route viewer. Native body, controller, wind application, slopes and teleport
motion have dedicated parity tests; see the README for current counts. Initial
capture is restricted to grounded, non-charging, non-splat states. Giant Boots
and most modded gameplay adapters remain unimplemented. Vertical Wind has an
audited screen-marker adapter with installed-patch parity tests. Jump%, Runtime
attribution, Mute SFX, More Block Sizes and Forced Slopes have scoped passive
integration rules and snapshot tests. Custom Wind Switch and tick-aligned SFC
release timing are modeled; known inactive Workshop/player integrations are
covered by a combined patch-set test. See `workshop-coverage.md` for the boundary
between coexistence and active-mechanic support. The coverage gate
rejects unknown registrations and relevant patched methods rather than treating
them as ordinary terrain. Live wind-phase rebasing and manual in-game validation
remain pending.

The shared kernel is demand-driven. No captures, geometry scans, speculative
ticks or background tasks run without an explicit Solve session. Cancellation,
completion and level teardown release that session. Provider declarations may be
registered once per level, but may not eagerly build simulation worlds.

## Installed-game findings

Check native signatures and behavior against the installed executable when
changing the simulator. The build and conformance fixtures exercise the game
assemblies directly; a cached decompilation may describe a different version.

Mega Gameplay Expansion's `NativeFlight` already demonstrates isolated BodyComp
updates using independent collision queries. Its `FlightWorld` deliberately
refuses wind, unsupported block types and active side teleports. It is a flight
forecaster, not a whole-player or whole-world simulator; copying it unchanged
would not satisfy this mod's requirements.

JK Runtime's `SnapshotService` provides transactions over registered state
participants. It does not clone the world, native inventory, every foreign
static field, native resources or arbitrary mod callbacks. Rewinder's snapshot
is therefore not a safe speculative-execution sandbox.

Native `WindManager.CurrentVelocityRaw` depends on the current screen and
achievement time. `PlayerStats.timeSpan` derives that time from `_ticks`, the
game's actual `TargetElapsedTime`, and its legacy time offset, then constructs
a TimeSpan. Preserve that conversion and its rounding. Do not substitute a
17 ms accumulator or wall-clock time. Screen intensity and fixed-direction
overrides also matter.

The ordinary wind waveform is a clamped sine/cosine function. Direction alone
does not identify its state: equal directions can have different force and
different time until reversal. `WindVelocityUpdateBehaviour` also retains its
own activation flag, considers entry direction, Snow and NoWind regions.

Native charge advances after the body update, includes the release update,
and uses `BodyComp.GetMultipliers()`. Water can change that multiplier during
one charge. Direction history, ice momentum, snow minimum power, coyote time,
buffered input and boots belong to control simulation, not an impulse lookup.

## Runtime simulation service

JK Runtime provides a versioned simulation-provider registry.
This is separate from restoration of live state. Participation is opt-in;
ordinary mod registration must not imply simulation support.

Each provider must:

- Identify the exact mechanics, block/controller types and versions it covers.
- Capture its initial state at a single shared game-tick boundary.
- Create independent branch state, including clocks and random-generator state.
- Apply input and tick in the same order as the installed game's controller.
- Query branch-local collisions and world effects, never live mutable objects.
- Provide a complete equality key for pruning equivalent search states.
- Emit semantic events such as takeoff, wind reversal, trigger and landing.
- Report unsupported external effects before speculative updates execute.

The registry resolves provider dependencies and ordering once for the session.
Two providers claiming the same mechanic must be rejected, not picked by load
order. Every relevant active mechanic must be covered. A foreign mod can supply
an adapter without Screen Solver hard-coding its assembly or block colours.

Opaque Harmony patches cannot be made universally simulatable by reflection.
Patches, changed controller trees and unknown block behaviours require coverage
evidence. A provider declaration is necessary but not sufficient: parity tests
must establish that its model matches the actual combination of mechanics.

Providers need a headless execution mode that suppresses presentation, audio,
saves, achievements, Steam calls and external I/O by construction. Do not call a
foreign live callback and hope that restoring the player's position undoes it.

## First-party integration

The native provider owns movement, charge, material effects, camera transitions,
side teleports, native wind and equipped gameplay items. It must use isolated
native services where possible and version-checked ports where the game exposes
only global state. Unknown executable fingerprints require a new parity audit.

Additional first-party providers cover:

- Subframe Charge's quantization, input evidence policy and buffered-jump rules.
- Casual Jumping's active control mode and jump permissions.
- Ball King's form, collisions and movement rules.
- More Items' active item state and effects, including Jetpack.
- Mega Gameplay Expansion's Warp transition and No Walk Off latch.
- Mega Mapping Expansion's triggers and timed/world state.

Providers must compose through the same ordering contract as live gameplay.
Implementing these adapters entails changes outside the new Screen Solver mod.
Third-party Workshop mods require their own adapters or inspected, maintained
integrations; there is no blanket claim of support for arbitrary DLLs.

## Search

Capture position, velocity, hitbox, grounded/knocked state, controller state,
input history, equipment, camera screen, world clocks and provider state together.
The initial state must not be silently snapped to a convenient takeoff point.

Search actions include directional walking, waiting, charge/release and each
provider's additional controls. A flight edge contains every intermediate tick,
not just its endpoint. For timed mechanics, equal positions reached at different
times are not equivalent states. Wind cannot be discretized to left/right alone.

Use incremental, cancellable search with limits on time, memory, branches and
per-transition ticks. Show progress. A bounded search that finds nothing reports
`Search limit reached`, not `Impossible`. Unknown mechanics report `Unsupported`
with provider/type information. No fallback route may carry a verified label.

A candidate succeeds only after landing on a usable platform in the selected
destination screen. Validate a settling interval so slope contact, a disappearing block
or a one-tick ground flag is not mistaken for a usable destination. Side-teleport
maps need the native screen topology, not assumptions from vertical coordinates.

Target order is upward exits first, left/right side teleports second, downward
exits last. Left and right share one search tier. Targets retain both the actual
exit direction and the destination screen: a side link to a lower screen is not
a downward exit. Search nodes include their last screen transition in their
identity, and replay verifies that transition before accepting the destination.
Two physically identical endpoints reached through different exits must not be
merged if their target eligibility differs.

Each tier starts from the same captured state and clock. Exhausting the finite
action set permits the next tier; reaching a budget also permits it, but marks
any result as leaving higher-priority routes unresolved. A simulation error,
unknown mechanic or replay mismatch stops all tiers. Cancellation never opens
another session, and constructing the priority controller does not capture or
advance the world.

Replay the complete candidate from a fresh isolated initial snapshot before
publishing it. Initially this is a simulated route, not a manually proven route.
Prefer routes with wider measured input windows, but never infer robustness from
geometric distance alone.

## UI

Use JK Runtime Controls+ for a remappable `Solve` action and provide a menu action
for players without a keyboard. The default binding must be checked for conflicts.
Solve, cancel and clear-route must not send gameplay input through to the player.

Display current and next-screen geometry, numbered actions, trajectory segments,
charge instructions, takeoff and landing points. Allow inspection of one step at
a time; a whole route must remain readable at the game's resolution.

Annotate wind changes at the actual trajectory points with direction, force and
relative tick. Show the required starting phase and waits. If live world time
continues while viewing a result, invalidate the launch schedule or explicitly
rebase and revalidate it. Never present an expired wind schedule as executable.

Changes to position, map, active mechanics, inventory or relevant world state
invalidate the captured route. Cross-screen drawing uses native camera transforms.
Closing, cancelling and unloading must release UI/input resources without altering
the run. No autoplay or automatic teleportation is part of this feature.

## Implementation gates

1. Add the Runtime simulation registry and isolation/parity test harness.
2. Implement native player/world simulation and compare it tick by tick with the
   installed game across materials, wind phases, charge, equipment and teleports.
3. Build cancellable multi-step search and replay validation on that contract.
4. Add the Solve UI and route/wind visualization; test schedule invalidation.
5. Integrate first-party providers and test combinations, not only each mod alone.
6. Publish the provider SDK and add verified third-party adapters incrementally.

Tests must detect shared mutable branch state, live-state changes, save writes,
random-state leakage, cancellation leaks, stale results and falsely accepted
unknown mechanics. Search tests include multi-jump routes, required waiting,
camera crossings without a landing, exhausted budgets and unreachable targets
in finite test worlds. Use native-versus-simulated per-tick traces as evidence.
