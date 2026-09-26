# Prepare resources before player handoff

Runtime 1.30.2 also loads map-owned SDK packages before these preparation phases.
Place a generated SDK shell under `jk-runtime/modules/<package>/<name>.jkmod`
(the same bytes as the SDK's `.dll` output), with its declared dependency DLLs
beside it. Keep all DLLs out of the map root: Worldsmith classifies roots with
DLLs as mods before inspecting level settings. The `.jkmod` suffix also prevents
the native recursive local mod scan from globally registering the map controller.
Registrations and prepared resources end with the world; attempts reuse them.
Assemblies remain loaded in the CLR, so changing package bytes requires a game
restart. Do not install the same controller as a separate global mod.

Since Runtime 1.30.4, map-owned packages' Before/After declarations apply only to
installed modules. Use Requires capabilities and map policy for mandatory
dependencies. This lets bundled controllers order optional integrations without
forcing players to install them. Standalone module ordering remains strict.

Read the [complete lifecycle](lifecycle.md) for the native/SDK activation barrier,
callback signatures and graph ordering. Start from the compiled
[PreparationExample.cs](../examples/PreparationExample.cs) for a complete pattern.

Runtime advertises `module-preparation-v1` and `startup-measurements-v1`.
Runtime 1.30.3 observes native `JKContentManager.LoadAssets` on menu transitions:
the game's native `BeforeLevelLoad` callback runs only at process startup.
World exit and SDK preflight happen after the new root is selected, before
the menu node loads its assets/screens. Attempt preparation also checks for
loaders that change the root directly. Same-world restarts retain world resources.
SDK packages opt in with attributes from `JKRuntime.Modules`. Existing packages
keep their activation order and require no changes.

| Phase | Use it for | Ownership |
| --- | --- | --- |
| `BeforeLevelLoad()` | Register factories, definitions and bindings needed during native loading | Existing registration lifetime |
| `OnWorldReady(RuntimeScope scope)` | Decode immutable audio, validate loaded-map contracts, create reusable resources | Until world exit or the next world load |
| `BeforeAttempt(RuntimeScope scope)` | Read current map files and settings-dependent data | Until attempt teardown, replacement preparation or world exit |
| `OnLevelStart(ModuleContext context)` | Attach to the new player, publish capabilities and subscribe to gameplay | Existing module activation lifetime |

Preparation runs synchronously on the game thread, in the resolved dependency
order. Rejected graph entries are skipped. World preparation runs once at the
first attempt, after native screens have loaded. Attempt preparation runs at every
`IntroState.OnNewRun` before its intro tree is built, including
restarts that skip BeforeLevelLoad. The following `GameLoop.OnPreGameStart` consumes
that prepared attempt instead of preparing twice; paths without IntroState prepare
at OnPreGameStart. Intro entities are constructed afterward. Runtime does not reset elapsed time or change
simulation cadence. This is a loading phase, not a background worker.

Do not attach player behavior, publish/resolve gameplay capabilities, start
playback or change saved attempt state during preparation. Module registrations
are frozen during these callbacks. A previous player's static reference can still
exist: never use it here. Native graphics/audio access belongs on this game thread.

## Ownership

The following is a fragment: `MyResources`, `MyAttemptData` and
`MyPlayerController` stand for consumer-owned implementations.

```csharp
private static MyResources resources;
private static MyAttemptData pending;

[OnWorldReady]
public static void PrepareWorld(RuntimeScope scope)
{
    var value = scope.Own(new MyResources());
    resources = value;
    scope.Defer(delegate { if (ReferenceEquals(resources, value)) resources = null; });
}

[BeforeAttempt]
public static void PrepareAttempt(RuntimeScope scope)
{
    scope.Defer(delegate { pending = null; });
    using (RuntimeApi.MeasureStartup("example.read-map"))
        pending = MyAttemptData.ReadCurrentMap();
}

[OnLevelStart]
public static void Start(ModuleContext context)
{
    context.Track(new MyPlayerController(resources, pending));
}
```

Own each disposable immediately after successful construction; clear retained
references during cleanup too. Controllers borrow world resources and must not
dispose them at attempt end. Active modules release their resources first,
followed by preparation scopes in reverse module order. World scopes survive
restarts. Cancelled intros clean up without requiring activation. Failed releases
remain retryable and prevent replacing that scope with new resources.

A throwing callback rolls back its partial scope and prevents that module's
activation this attempt. Other modules can prepare; normal capability checks
prevent activation of consumers whose provider failed. The next attempt retries.
Successful world preparation survives an attempt-only preparation failure.
Catch optional prefetch failures only if activation has a correct fallback.

Shared process hooks use the already-loaded single Harmony engine. If hooks are
unavailable or another loader skips the intro, StartLevel performs preparation
synchronously before SDK activation. Correctness is retained; the handoff saving
is unavailable. Preparation code must therefore use loaded-world data, never a
player, regardless of whether the player has already been constructed.

## Invalidation and measurement

World scopes suit immutable resources. Attempt scopes suit files that authors
can edit between restarts, replay digests and fresh inventory reads. Do not cache
a digest forever by pathname or treat equal file length as equal content. Clear
prefetched snapshots when menu/API mutations make them stale. Inventory resets
and saving the new attempt remain at actual activation.

Enable `JKRuntime.StartupTrace.enabled` before launch. The bounded trace includes
`module.world-ready:<id>`, `module.prepare-attempt:<id>`, activation and optional
`RuntimeApi.MeasureStartup` substages. It retains preparation and the first 120
gameplay draws; a long intro cannot exhaust that window. Native handoff capture
starts before pause-menu/player construction. Nested durations overlap.
Formatting/export run on the trace worker. Disabled measurement uses a value-type
scope without allocating objects or reading the performance clock.

Check cold loading, restart, cancelled intro and world switch. Measure total
loading too: moving 40 ms earlier is not eliminating 40 ms. Fixture timings are
not end-to-end game measurements.

Current consumers: Smooth Camera retains dormant patches across attempts;
Mega Gameplay and Ball King keep audio per world; Replays hashes assets per attempt;
Mapping reads layout/scene data before activation; More Items prefetches inventory
without applying or saving a new attempt early.

## Work that must not escape loading

| Operation | Place it here | Do not hide it here |
| --- | --- | --- |
| Assembly/type discovery, large catalogue scans | World/attempt preparation when needed; otherwise explicit browser opening | First `Update`, a delayed callback, menu constructors |
| Asset decoding and reusable immutable map data | `OnWorldReady` with world ownership | New player/controller constructor on every restart |
| Fresh settings-dependent recipes, file hashes or map reads | `BeforeAttempt` with invalidation | Getter, Draw, periodic gameplay refresh |
| Player-specific target binding | Activation, using prepared descriptors | Preparation reading an old static player |
| Applying an enabled mechanic | Activation or its normal bounded update | Catalogue discovery that silently enables it |
| Native UI layout and current value reads | Lightweight creation/visible drawing | Getter that recreates foreign menus or performs disk IO |

Runtime does not inspect or move your code automatically. `Commands.Enqueue`
and a one-frame timer are not loading phases. A mod can therefore follow the
attribute signatures and still reintroduce a handoff stall through an unreviewed
constructor, getter, first tick or deferred menu refresh.

The disabled path is part of the performance contract: without enabled overrides,
do not scan geometry, construct handlers, acquire state leases or "apply" an empty
configuration. Prepare only data needed by current settings. If browsing is the
only consumer, build its live catalogue on an explicit paused/menu action instead.

## Acceptance before shipping

Use [startup tracing](diagnostic-workflow.md#startup-and-restart-stalls) to confirm
named preparation stages precede `runtime.gameplay-handoff`. Check the first
playable ticks and deferred callbacks as well as `OnLevelStart`. Record cold and
warm attempts, cancelled intro and world switch, with the feature off and on.
Assert absence of unnecessary work; do not rely solely on a timing threshold.
See the [verification matrix](testing-and-release.md#acceptance-matrix).
