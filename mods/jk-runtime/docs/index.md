# JK Runtime developer handbook

Build a mod with the Runtime SDK, then add the services your feature needs.
The current SDK targets API **2.0** and Runtime **2.0.0**. Its assembly identity
stays **1.0.0.0**; that number isn't the API requirement.
Players looking for controls and settings should use [Using JK Runtime](players.md).

## First mod

1. [Build the example and open its Settings page](getting-started.md).
2. [Learn when your module starts and what owns its resources](lifecycle.md).
3. [Prepare assets and map data before play](preparation.md).
4. Choose the feature you need below.
5. [Check and package your release](testing-and-release.md).

For an existing native mod, follow the [migration walkthrough](migrating-a-mod.md).
It includes a complete LessAutoEquipping example, build script and checks.
[Why Runtime exists](why-runtime.md) explains the shared services and when they help.

## Find a guide

| Task | Guide |
| --- | --- |
| Build a package, declare dependencies and locate data | [Getting started](getting-started.md), [packaging](packaging.md) |
| Register a module or service and clean up on restart | [Lifecycle](lifecycle.md), [module API](api.md#graph-and-lifetime) |
| Load assets or cache map data | [Preparation](preparation.md) |
| Add settings or save preferences | [Settings and commands](settings-and-commands.md) |
| Create a page, list, slider or native menu row | [UI pages](ui-pages.md), [UI API reference](ui-api.md) |
| Add bindings or observe jump, land and pause | [Events and input](events-and-input.md), [binding pages](ui-api.md#focused-binding-pages) |
| Change movement or replace the charge controller | [Gameplay ordering](api.md#gameplay-ordering) |
| Query terrain, connect screens or declare materials | [Geometry and mechanics](geometry-and-mechanics.md), [material interop](interop.md) |
| Share world state with multiplayer and replays | [World execution](world-execution.md) |
| Run behavior trees or handle shared world interactions | [World execution](world-execution.md) |
| Capture local mod state or own a playback clock | [State and time](state-and-time.md) |
| Draw player appearance, a trail, sound or particles | [Player presentation](player-presentation.md), [small effects](common-effects.md), [particles](particles.md) |
| Compose scenes and cameras or select a camera subject | [Runtime coordination](runtime-coordination.md) |
| Activate a feature from map tags, pixels or XML | [Map mechanics](map-mechanics.md), [map policy](map-policy.md) |
| Observe foreign movement or contacts | [Motion observation](motion-observation.md), [interop](interop.md) |
| Simulate an explicitly supported world | [Simulation](simulation.md) |
| Inspect blocks and experimental overrides | [Mod Inspector](mod-inspector.md), [controls](inspector-controls.md), [inspector lifecycle](inspector-lifecycle.md) |
| Diagnose a load failure, stall or resource leak | [Diagnostic workflow](diagnostic-workflow.md), [troubleshooting](troubleshooting.md) |

## References and maintenance

Use the [API map](api-map.md) to find a public type's guide. `PublicApi.md` in
the exported SDK lists exact signatures; `JKRuntime.xml` supplies IDE help.
The SDK includes the same handbook and example sources as the repository.

- [Core API contracts](api.md) and [UI API reference](ui-api.md)
- [Compatibility](compatibility.md): Harmony, foreign mods and run attribution
- [Architecture](architecture.md): the host, shared services and adapters
- [Verification and release](testing-and-release.md), [UI validation](ui-validation.md)
- [API policy](api-policy.md) and [documentation maintenance](maintaining-docs.md)
- [First-party mechanic migration contracts](mechanic-migration-contracts.md)

## Terms and rules

A **world** is a loaded map and its resources. An **attempt** is a run within
that world, including a restart. **Activation** attaches modules to the new
player. A **scope** or **lease** owns a resource and releases it when its work ends.
A **capability** is a versioned service published by a module.

Call live game, UI and module APIs on the game thread. Register cleanup as soon
as you acquire a resource, and don't dispose resources borrowed from another
owner. Saved preferences, live player state and replay clocks have different
lifetimes; the relevant guide explains each one.

Runtime coordinates participating mods. A declaration or diagnostic inventory
doesn't prove that an arbitrary foreign mechanic can be simulated. Keep missing
coverage visible rather than substituting a guessed result.
