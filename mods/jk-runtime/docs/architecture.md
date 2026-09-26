# Runtime architecture

For the problems behind these design choices, read [why Runtime exists](why-runtime.md).

## Layers and ownership

| Layer | Owns | Does not own |
| --- | --- | --- |
| Jump King's native loader | DLL discovery and native mod callbacks | Runtime's capability graph and scoped feature resources |
| Runtime package host and kernel | SDK manifests, requirements, preparation, activation and tracked teardown | Arbitrary foreign initialization or untracked side effects |
| Shared services | Input histories, UI sessions, gameplay coordination, registered state and diagnostics | Every consumer's feature policy or all native world state |
| Hook and compatibility adapters | Specific validated native/foreign method interventions | A replacement Harmony engine or universal foreign patch ordering |
| Feature modules | Their mechanics, content, settings and declared integrations | Another module's resources without an explicit contract |

The native loader discovers a small SDK shell; the package host validates and
loads its implementation. Loading definitions precede native screen creation.
World/attempt preparation uses scoped resources, then activation attaches to the
new player after synchronous native start callbacks. Requirements order accepted
participants at preparation and activation. Early `BeforeLevelLoad` definitions
instead run in package-ID order; they must not depend on active services.
The [lifecycle guide](lifecycle.md) specifies each boundary and failure path.

Core module coordination is separate from optional Harmony-backed hooks.
Runtime neither bundles nor loads a Harmony engine for those hooks. The adapters
inspect loaded engines and their actual API. Most require one shared Harmony 2;
the modifier observer has its own reviewed engine-selection rules. Unavailable
hooks produce documented fallback or an unavailable feature, not a silent engine
replacement. See [Harmony and other mods](compatibility.md#harmony-and-other-mods).

## Packages and lifecycle

Feature packages use a small SDK-generated discovery DLL containing an embedded
implementation and checksummed manifest. Native type enumeration does not need
to resolve another feature's implementation before discovery completes.

The host validates versioned capabilities and ordering constraints, installs
modules in dependency order and owns their level resources. Installation
failure unwinds tracked resources. Modules resolve services at installation;
per-frame code uses those resolved interfaces.

Runtime cannot roll back untracked third-party side effects. Failed cleanup
can require a game restart. Module isolation is not a security sandbox.

## Gameplay

Startup follows the [preparation contract](preparation.md): scoped world resources
can survive same-map restarts; attempt preparation runs before each intro. Native
player activation remains behind the existing mod-start barrier.

Body behaviors register at named native phases. Form, thrust and movement roles
coordinate participating controllers. One owned native charge slot coordinates
charge decoration with other jump implementations; replacing it updates all
references to the shared node.

Jump observers receive the committed result. Charge displays can use that result
instead of inferring power from later flight velocity.

Settings are saved immediately. During an active level, controller changes can
be queued for the command pump's game-thread update; without that pump commands
execute immediately. Scopes own registrations and release them at unload.
See [settings/commands](settings-and-commands.md) and [lifecycle](lifecycle.md).

## Input

Keyboard/mouse, XInput slots and DirectInput devices have independent readers.
The readers record monotonic observations, detect gaps and require neutral
input after loss or reconnection. Queues are bounded. A blocked driver does not
make the game thread wait or fabricate a release.

The polling target is 1 ms, not a guarantee of hardware event resolution.
Keyboard and mouse share the Win32 reader. XInput uses an owned controller and
the game's conversion rules; DirectInput uses an independent nonexclusive
connection. Physical binding resolution includes Controls+ chords.

Mouse IDs are `0x10000` through `0x10004`; see
`JKRuntime.Input.MouseButtons`. Left/right refer to physical buttons even
when Windows swaps the primary button. Focus loss invalidates measurement.

## Discovery, caching and background work

Package discovery caches immutable loaded-assembly manifest presence/absence;
it continues to inspect newly loaded assemblies. Controls+ separately caches
immutable binding metadata, while null/failing legacy settings containers remain
retryable. Dynamic and partially loadable metadata is not permanently frozen.
Adapters read the current settings object, so replacing that object does not leave
bindings attached to an obsolete instance. Explicit bindings retain precedence.

These caches remove repeated discovery; they do not move arbitrary foreign menu
factories, getters or static initializers into a safe phase. In particular,
building a native pause menu can execute a feature's setting constructor before
its SDK activation. Keep such work bounded and back getters with memory.

Startup traces and pointer status use bounded/coalescing background formatting
and writes. Run-modifier history uses immutable coalesced snapshots: current
attempt evidence is visible in memory immediately, and normal exit waits briefly
for the last write. Interrupted persistence can leave attribution unknown on the
next launch; the native modified-run flag stays authoritative. This worker is
separate from a feature's synchronous `AtomicXmlFile.Save` delegate.

The simulation kernel performs no automatic search or ticking. Physical input
workers require clients; a diagnostic overlay can itself be such a client.
An explicit full diagnostics export can hash/read files synchronously and must
not become an automatic handoff callback. See [diagnostics](diagnostic-workflow.md)
and [preparation](preparation.md) for the relevant measurement boundaries.

## State and clocks

State participants capture versioned fragments. Restore validates the complete
snapshot before applying it and rolls back on failure. Participant changes
invalidate old snapshots. Restoration covers registered movement state, not
arbitrary inventory, achievements or world state.

Playback can use an exclusive clock lease. Physical input timestamps remain
monotonic and are invalidated, never rewound, after a restore.

## Build and installation

Use the SDK packager, not a raw csproj output, for release DLLs.
Feature packages must not contain loose implementation DLLs, game assemblies
or duplicate Runtime copies.

The coordinated installer checks destinations and package versions, refuses a
running game or duplicate installations, backs up replaced DLLs and rolls back
on failure. It also removes superseded UIApi+ and standalone Jetpack binaries.
Original settings and user data are retained.

Tests cover package discovery permutations, native contracts, settings and
snapshot rollback, input loss and isolated installer failures. Real hardware,
menu rendering and combined gameplay still need in-game testing.


## Optional frame diagnostics and presentation dispatch

Performance capture and startup capture own separate reversible groups in the
already-loaded Harmony engine. Pointer hooks no longer own startup measurement
hooks. Diagnostic mode persists with Runtime settings but disabled installations
add no profiling dispatch hooks. Named performance scopes let feature mods publish
costs without owning a profiler or file writer. Fixed capture capacity, one writer,
one pending snapshot and four report slots bound memory and disk growth.

NativeFrameDispatch exposes non-inlined native update/draw entry points. Feature
schedulers remain responsible for pacing and ownership arbitration; Runtime does
not silently install a second clock. The pacing owner gates scheduler call sites
before entering those native boundaries. Native UI pages use the menu-supplied delta.
