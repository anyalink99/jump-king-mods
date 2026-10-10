# Compatibility and diagnostics

## Harmony and other mods

JK Runtime does not ship `0Harmony.dll`, reference Harmony directly or load an
engine for its hooks. It finds a Harmony engine already running in the game and
uses it through reflection. Another mod may have supplied that engine. A DLL next
to one Workshop mod does not prove that the game is using that particular copy.
Runtime never pins, upgrades or downgrades Harmony to 2.2.2 or another release.

Most hook services need one loaded Harmony 2 engine and validate only the members
they use. The modifier observer also checks existing registration patches before
choosing an engine; its rules are described below. Runtime does not merge several
independent engines into one imaginary patch store.

Missing or unsupported hooks appear in diagnostics. Some services have a stated
fallback, such as synchronous preparation before activation. Others stay
unavailable. Core module coordination and explicit modifier attribution do not
need Harmony.

Several adapters are tested with installed Harmony 2.2.2, 2.3.3, 2.3.5 and 2.3.6.
That is evidence for those adapters and fixtures, not certification of every
third-party patch on those versions. The dialogue adapter below is a concrete
example of why checking the actual method signature matters more than assuming
that every newer version is compatible or incompatible.

Runtime orders participating SDK modules during preparation and activation.
It does not sort arbitrary Workshop DLLs, reorder foreign native callbacks or
rewrite other authors' Harmony priorities. `BeforeLevelLoad` definitions have
their own package-ID order. A foreign mod continues to own its original
lifecycle even if Runtime discovers its bindings or observes its registrations.

Runtime normally inspects foreign patches without modifying them. The reviewed
compatibility adapters documented below are explicit exceptions, with specific
build checks, supported behavior and failure rules. They operate in memory;
they do not replace Workshop DLLs. Runtime-owned
patch groups remove their own callbacks, not every patch on a shared target.

### Investigating a load-order report

A failure that changes with load order is useful evidence, but does not by itself
identify a Harmony version conflict. Initialization timing, a failed patch,
another callback or a feature's own data lifetime can produce similar symptoms.
Likewise, one feature working does not establish that every patch was installed
or that every initialization callback ran successfully.

Record the map, exact mod versions/order and whether the failure occurs on cold
boot, Continue, restart or map switch. Preserve `ModLoadLog.txt` from the game
directory and export `JKRuntime.report.txt/json` from Runtime diagnostics for
both working and failing orders. Compare loaded engine identities/locations,
patch owners, callback exceptions and module failures. See the
[diagnostic workflow](diagnostic-workflow.md) for collection steps.
A patch inventory helps narrow an investigation, but cannot prove that every
patched call site executed.

## Modified-player attribution

When the native results screen displays `Player Behaviour Modifier(s) Detected`,
Runtime displays the contributing mod names under `Modified by:`. Registration
does not establish that the player used a mod's controls. There are no per-mod
usage filters or exemptions. Runtime body registrations are
recorded on success, including More Items' same-tick Jetpack usage marker.
Disabling/removing a modifier does not erase its contribution to that attempt.
Map-authorized behaviours bypass marking and are not listed as contributors.
Attribution itself changes neither the game's flag/counters nor its achievement
policy.

The optional [moving-platform adapter](#moving-platform-registration) changes
BoGMod3's registration before it reaches the native modifier counter. This is a
foreign-mod compatibility fix, not an attribution exemption. Without that fix,
BoGMod3 is observed and reported like any other mod.

History is kept in the game's `Content/Saves/JKRuntime.RunModifiers.xml`, separate
from native save data and independent of the DLL's Workshop/local location.
It is tied to the map root and native attempt-start statistics snapshot, survives
normal continue/relaunch, and resets when that attempt identity changes. Missing,
inconsistent or unreadable history is not reconstructed from the installed-mod list.
Disk errors are logged; in-memory results remain available for that session.

The native game initializes a new save's modifier peak from its last active
modifier count. Runtime observes `SaveLube.DeleteSaves` and keeps observing the
old body's teardown after freezing completed results. If that reset runs and the
native count exactly matches fully tracked remaining registrations, their names
are retained separately as inherited sources for the next attempt on the same
map. Names deduplicate if those mods register again. A skipped/failed reset,
unexplained count or different map cannot use this evidence. This fixes false
unknown entries after a witnessed restart without clearing native flags or
guessing which historical registrations survived. Old unknown records are not
retroactively repaired.
The save worker consumes an immutable registration snapshot published on the
game thread. Reset metadata is persisted even before the next attempt begins,
so quitting on the results screen retains it for the next launch.

An optional adapter uses an already loaded Harmony 2 to
observe the four native BodyComp registration/removal methods and native save
reset. It never loads or
ships another Harmony DLL. Successful registrations require actual changes to
the native behaviour list and modifier count, not just a `true` return value.
Events before Runtime's level-start callback and same-tick markers are retained;
our explicit API and the observer share one deduplicated event history.

Behaviour assemblies are matched to `ModLoader.LoadedMods` for native mod names.
A shared/unmapped DLL is labelled `Unknown mod (behaviour DLL: ...)`, not assumed
to identify its caller. Third-party authors can still use `Gameplay.RunModifiers`
without Harmony for explicit attribution.

The adapter prefers the loaded Harmony already patching these methods. Different
independent patch tables are rejected rather than mixed. A later conflicting
engine or removal of our hooks is reported as degraded (coverage is checked every
five seconds); Runtime does not overwrite foreign patches or start a re-patching
loop. See Runtime diagnostics → Modifier observer, or `foreignModifierObserver`
in the JSON report. Tested against installed Harmony 2.2.2, 2.3.3 and 2.3.6, with
both patch orders and separately loaded 2.2.2/2.3.6 engines.

This is not universal provenance tracking. Missing/unsupported Harmony, calls
before hook installation, already-inlined calls, off-thread mutations, direct
private-field writes and old saved flags can remain unattributed. Other patches
that fundamentally replace registration semantics are outside this guarantee.
Unexplained native count/peak changes retain `Unknown source / earlier session`.
Attribution lists the providers identified by the available evidence.
The native warning remains authoritative, including when no source is known.

## MoreTextOptions dialogue compatibility

Runtime automatically repairs the reviewed MoreTextOptions 1.6.2.1
`PatchTargetLine` crash when the loaded Harmony lacks the three-argument
`MethodDelegate` ABI (observed with VerticalWindMod's Harmony 2.3.5). Installed
2.2.2, 2.3.3 and 2.3.6 expose that ABI and are left untouched. Detection uses
the actual method signature, not an assumption that all newer versions break it.

The adapter checks the original DLL SHA256 and MVID, validates native delegates
and fields, then replaces just the known dialogue postfix in memory. Its native
formatting, owner ID, priority and before/after constraints are retained. The
foreign owner ID is intentional: other mods' ordering constraints still refer
to MoreTextOptions; the replacement method and diagnostics identify Runtime.
All other patches are unchanged. Equal-priority peer postfixes are refused
because adding a replacement would change Harmony's index tie-break ordering.

Runtime attempts installation during early attempt preparation and checks again
at Runtime's native OnLevelStart before the first entity update, so patches added
after preparation can still be recognized. It is
idempotent and handles MoreTextOptions re-registering on the next level.
Multiple loaded Harmony engines, unknown DLL builds and unreviewed engine ABIs
are not modified; failures are explicit in Runtime diagnostics and
`moreTextOptionsCompatibility` in the report. No per-frame re-patching loop.
This cannot protect an NPC invocation made by another mod before OnLevelStart,
or a foreign mod replacing patches after this startup point.

No Workshop files are edited, no extra Harmony is loaded, and the full foreign
mod is not disabled. This narrowly scoped adapter is an explicit exception to
Runtime's otherwise read-only foreign-patch policy. It does not fix the separate
generic XML translation patch failure reported by MoreTextOptions.

`verify-text-compat.ps1` runs the original DLL in isolated .NET Framework
processes against four installed Harmony versions. Each pass covers 96 NPC and
Rattman formatting cases, both native fonts, empty or unusual strings, peer
coexistence, late registration, reload and repeated installation. It also checks
refusal for a poisoned initializer, unknown build, multiple engines and bad
ordering. Rendering is checked separately in the game.

## ConveyorBlockMod jump discovery

Runtime adapts Workshop item 3330536917's reviewed conveyor exit method.
The installed game's `BTmanager.FindNode<JumpState>()` checks exact types. SFC
replaces that node with a subclass, so the conveyor gets null and crashes at
`ResetResult()` when leaving a belt after three tracked contact frames, with
nonnegative Y velocity and no ordinary box collision. Body behaviours execute
before the player's input/behaviour-tree components, which exposes this during
walk-off and jump timing near an edge.

The adapter replaces only that lookup call with a search for the unique live
JumpState, accepting subclasses and deduplicating shared graph references. Empty
or ambiguous graphs fail explicitly. The conveyor's conditions, virtual reset
and addition of belt speed are unchanged. Native generic lookups retain exact
type semantics; their shared CLR method bodies are never patched. SFC 0.15.3
also clears cancelled charge evidence on external ResetResult while retaining
idle early presses and the sampler queue.

The foreign DLL must match SHA256
`9ED9CAF95217D6D44F5AB580A5D118E4EC6A79AE2B3CA048E97ACE13D1709725`.
Runtime uses one already loaded Harmony and validates exactly one call site.
It prepares the hook during attempt loading, checks again at activation and
before a managed JumpNodeBindings replacement. Unknown builds, missing/ambiguous
Harmony or a removed hook prevent that replacement before graph mutation.
Diagnostics expose `conveyorCompatibility`. Hook installation rolls back only
its own method on failure; it does not edit Workshop DLLs or remove peer patches.
This adapter and the dialogue adapter above are the explicit exceptions to the
read-only foreign-patch policy.

`verify-conveyor-compat.ps1` first reproduces the original exception. It then
tests the installed conveyor with:

- native and subclass jump nodes;
- both belt directions and rising, level or falling movement;
- short contact histories, box contacts and staying on a belt;
- repeated replacement and restoration;
- shared graph references; and
- protection removed again.

SFC's build repeats the fixture with its real compiled state. Isolated runs cover
installed Harmony 2.2.2, 2.3.3, 2.3.5 and 2.3.6. They do not replace an
interactive map playtest or establish compatibility with future DLL builds.
An unrelated conveyor null reference (for example inconsistent cached block
contact) is not swallowed by this adapter. Foreign mutations after activation
and controllers that bypass Runtime's binding service remain outside its guard.

## Jump King Manager loaded-map browser

Settings > **Mod compatibility fixes** enables optional user-facing adapters.
It defaults to on, is stored as `ModCompatibilityFixes` in Runtime settings, and
controls Jump King Manager's loaded-map browser and the moving-platform adapter
below. The required dialogue and conveyor crash guards above retain their
separate automatic contracts.

The reviewed Jump King Manager 2.0.0.0 DLL has SHA256
`F97C0F9412DEA2456305B12DB1D42FB92C066FED3A1CDD1D8BAC77AB62AAF275`.
Runtime reads the game's loaded `LocationTextManager.SETTINGS` and screen count
during attempt preparation. Its detached browser data refreshes on restart and
world switch and clears on world exit. No map XML is guessed from the vanilla
content directory. Area ranges are one-based and inclusive; invalid ranges are
omitted, intersecting ranges retain their names, and unnamed screens remain in
the screen selector. The scrollable area list supports arbitrary area counts.

Runtime patches the Manager constructor, specific-screen handler, save-label
handler and level-start callback with its own Harmony owner. Existing and later
windows update immediately; disabling removes only these hooks and restores the
original controls and enum entries. The Manager's hotkey worker is marshaled to
the form/game thread. Its position save/load and coordinate entry remain native.
Screen navigation checks the current player's hitbox against the destination's
live collision data, preferring supported clear space and falling back to open
space. A fully blocked screen is refused. This is navigation assistance, not a
guarantee of a safe route or protection from custom hazards.

This adapter is another explicit exception to the read-only foreign-patch policy.
It edits no Workshop files and loads no Harmony or foreign mod. Unknown Manager
builds and missing/ambiguous Harmony are refused; `jumpKingManagerCompatibility`
in reports and Runtime diagnostics expose the result.

`ManagerMapTests` covers ranges, unnamed/high-index screens, native collision
arrivals and settings persistence. `verify-manager-compat.ps1` tests the installed
foreign DLL against each installed Harmony version in isolated processes:
existing/new windows, repeated installation, opt-out restoration, world changes,
high-index navigation, worker-thread hotkeys and missing-engine/unknown-build
refusal. These checks are not an interactive map playtest.

## Moving-platform registration

With **Mod compatibility fixes** enabled, the reviewed BoGMod3 updater registers
without marking the player as modified. The adapter changes only the three
registration call sites in `MovingPlatformMod.ModEntry.OnLevelStart`; it retains
the original updater, list placement, duplicate guard and block behavior.
Platform movement, collision handling and jump behavior stay in BoGMod3.

Reviewed on 2026-10-05: Workshop item **3786443795**, BoGMod3 **1.0.0.0**, SHA-256
`6BD709BBBF473A632686CF07103AE14EAA02DE6BB16072C98A62BE42C482B399`.
Installation needs one already-loaded Harmony 2 engine. Unknown builds or a
missing/ambiguous engine leave the original registration in place and report
the limitation through `movingPlatformCompatibility` and Runtime diagnostics.
The adapter edits no Workshop files and doesn't load BoGMod3 or Harmony.

Hooks are prepared before native level-start callbacks. Toggling the setting
changes subsequent registrations, normally on the next level start. Existing
player registrations and recorded attempt history aren't reclassified or erased;
continuing an already marked attempt retains that history. Disabling removes
only this adapter's hooks. Other mods still use the native modifier counter and
the normal attribution observer.

`verify-moving-platform-compat.ps1` runs the installed foreign DLL with each
installed Harmony version in isolated processes. It checks both observer orders,
empty and nonempty body lists, duplicate startup, platform travel and turnaround,
unrelated modifier registration/removal, setting changes, retained history,
foreign-patch preservation and missing-engine/unknown-build refusal. These are
code-level integration checks, not an interactive map playtest.

## Native item read cache

Runtime fills missing inventory/equipment entries in the native read cache during
attempt preparation, checks again at native level start and ensures them for
native flight forecasts. The game can clear the cache on restart without filling
it when reading a save; repeated native item checks would then decrypt/read the
same files repeatedly. Runtime retains existing cache entries and native writes.
It does not change item values or write a save as part of cache priming.

This is an internal performance intervention, not an inventory ownership API.
Native cache clearing remains valid; a later ensure repopulates missing entries.
Keep consumer inventory mutation/save timing at normal activation, and measure
read/parse/native snapshot/write stages independently before changing it.

## Runtime report files

Open JK Runtime → Runtime diagnostics. Reports
`JKRuntime.report.txt/json` are written beside the runtime DLL, including module
states/order, package and command failures, native game contract/fingerprint,
loaded Harmony versions and patch owners. Nothing is uploaded automatically.

Ordering applies to runtime participants. Workshop DLL discovery and foreign
Harmony patches retain their own ordering. Generic-method patching depends on
the loaded Harmony ABI.

Independent failure isolation is not a security sandbox. Untracked side effects
or direct third-party native mutations cannot be undone automatically.
An incomplete cleanup/rollback blocks unsafe reuse and requires restarting.
Unsupported hardware timing is reported as unavailable.

Snapshot restoration covers registered mod state in the current level, not
arbitrary world state, inventory or achievements. Participant changes invalidate
old snapshots. Monotonic physical input is invalidated on restore, never rewound.
Physical-device, menu rendering and combined gameplay acceptance still require
manual testing alongside the automated native-contract checks.

For exact capture/enable/disable steps, use the
[diagnostic workflow](diagnostic-workflow.md). For package/assembly version
requirements and binary compatibility guarantees, use [API policy](api-policy.md).

## Block observation and ceiling slopes

Mod Inspector observes the native loader's `IBlockFactory.GetBlock` call site.
It never patches foreign factory methods or constructors. This avoids forcing
those methods to compile before another mod installs its constructor patch.
Final templates are read after native `OnLevelStart` callbacks; exact native
slope copies preserve their stored lines without running constructors.

The old MGE observer could prevent Forced Slope Blocks' `FixMySlopes` correction
when Runtime/MGE loaded first. The 2026-10-06 regression matrix uses Forced
Slope Blocks 1.2.0 (SHA-256
`5AF16339E2D0E20E49569727A78006FC58E20E156B722313F52CCB73E7844C2D`), both
startup orders, Harmony 2.2.2/2.3.6 and ordinary, forced and explicitly fixed
ceiling slopes. Explicitly fixed blocks worked in the original reproduction;
that separate reported failure was not established by these fixtures.

More Block Sizes replaces the native path. Runtime adapts the `GetBlock` call
sites in `WithCustomSizes` and `WithMeshing` only for the reviewed assembly hash
`831F0897FCEB1D0D0D10C58388C53421779429862F6853A7E843FBE60912A494`
(1.3.0.1, reviewed 2026-10-10). Its two adapted methods match the earlier
1.2.0.1 contract. That earlier hash,
`2318F08EDD9D7EA8B039D3271E53C4320CE20280A8897DBF19D4259D009619CF`,
also remains accepted. The newer build adds `level+.xnb` loading before screen
construction and changes a draw-only field hook; neither changes these call sites.
Changed or unknown loaders retain loaded-world inspection, with unknown
provenance where no call was observed. The adapter doesn't make their geometry
portable or override their behavior. See [inspector lifecycle](inspector-lifecycle.md)
for the repeatable tests and their limits.
