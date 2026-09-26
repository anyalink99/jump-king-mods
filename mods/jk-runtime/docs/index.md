# JK Runtime developer handbook

Target: Runtime API **1.32**, runtime release **1.32.0**, assembly identity
**1.0.0.0**. The SDK packager stamps its API version into each package.

## Start here

Start with [why Runtime exists](why-runtime.md) for the problems it solves,
examples of cooperation and the boundary between Runtime, Harmony and the native
mod loader. [Architecture](architecture.md) explains how those services fit together.

1. [Build and install a first package](getting-started.md).
2. [Understand loading, activation and teardown](lifecycle.md).
3. [Place expensive work before player handoff](preparation.md).
4. Choose a service using the [API map](api-map.md).
5. [Verify behavior and package a release](testing-and-release.md).

Already have a native mod? Follow the [LessAutoEquipping migration walkthrough](migrating-a-mod.md)
and its complete source, build script and automated checks.

The repository and standalone SDK contain the same guides and example sources.
The SDK also includes `JKRuntime.xml` for IDE help and a generated
`PublicApi.md` signature reference at its root. The signature reference describes
what is public; the guides describe when and why it is valid to call it.

## Guides by task

| Task | Guide |
| --- | --- |
| Query declared support, observe contacts/stages, export bounded traces | [Material and movement interop](interop.md) |
| Observe foreign speed transformations without replaying callbacks | [Motion observation](motion-observation.md) |
| Declare dependencies, publish capabilities, own registrations | [Lifecycle](lifecycle.md), [module API](api.md#graph-and-lifetime) |
| Package a feature, ship a contract DLL, locate settings | [Packaging and dependencies](packaging.md) |
| Load assets, cache data across restarts, avoid first-frame stalls | [Preparation](preparation.md) |
| Save preferences, apply changes, migrate files | [Settings and commands](settings-and-commands.md) |
| Add a page, list, slider, native menu row or binding | [UI pages](ui-pages.md), [UI reference](ui-api.md) |
| Observe actions, sample physical buttons, suspend a component | [Events and input](events-and-input.md) |
| Change movement or replace the native charge controller | [Gameplay ordering](api.md#gameplay-ordering) |
| Read geometry, declare materials, describe a mechanic | [Geometry and mechanics](geometry-and-mechanics.md) |
| Capture mod state, restore it, own a playback clock | [State and time](state-and-time.md) |
| Simulate an explicitly supported world | [Simulation](simulation.md) |
| Diagnose startup, callbacks, compatibility or leaked resources | [Diagnostic workflow](diagnostic-workflow.md) |
| Verify builds, UI rendering and release packages | [Verification and release](testing-and-release.md), [UI validation](ui-validation.md) |
| Find the failing stage and decide how to recover | [Troubleshooting](troubleshooting.md) |
| Understand Harmony boundaries and run attribution | [Compatibility](compatibility.md) |
| Update Runtime without breaking consumers | [API policy](api-policy.md), [documentation maintenance](maintaining-docs.md) |
| Migrate existing first-party mechanics | [Migration contracts](mechanic-migration-contracts.md) |

## Core rules

- Game objects, module services, UI and live state belong to the game thread.
  A simulation session instead belongs to its creation thread; its providers
  must operate only on isolated data. See the service-specific contract.
- Register ownership immediately. Dispose only what you acquired. Cleanup failure
  must remain visible and retryable where the API supports retry.
- Preparation is synchronous loading work. A delayed callback, constructor,
  getter or first `Update` is not a preparation phase.
- Saved settings, active player state, input timestamps, snapshots and durable
  run history have different lifetimes. Do not treat them as one state store.
- A capability/geometry/mechanic declaration is not proof of complete simulation
  coverage, native compatibility or ownership of another mod's controller.
- Runtime orders participating SDK modules. It cannot reorder arbitrary native
  mods, undo untracked side effects or make every callback inexpensive.

## Terminology

**World** means one loaded map and its resources. **Attempt** means a play session
within that world, including a restart. **Activation** attaches SDK modules to the
new player after synchronous native start callbacks. **Scope/lease** means an
owned resource with explicit release. **Capability** is a versioned service
published by a provider; a `Supports` feature flag only advertises Runtime API
availability. **Native** refers to Jump King's implementation. **Evidence** states
what was observed or tested, not universal compatibility.
