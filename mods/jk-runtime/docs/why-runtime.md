# Why JK Runtime exists

JK Runtime is shared infrastructure for Jump King mods. Its value is that
features can use the same lifecycle, ownership and integration services instead
of independently replacing the same game systems. One fix to a shared service
can benefit its consumers without each author maintaining another implementation.

Players get Controls+, native-style menus, diagnostic reports and specific
compatibility adapters. Authors get an SDK for building features that can explain
their dependencies, release their resources and cooperate with other features.
The benefit grows when mods use those contracts; installing Runtime does not
automatically convert unrelated mods into managed modules.

## Problems it addresses

| Recurring problem | What Runtime provides | Practical benefit and boundary |
| --- | --- | --- |
| Discovery fails before a feature can explain a missing dependency | SDK discovery shells, checked manifests and versioned capabilities | Implementations load through the package host; missing requirements and cycles produce module errors. Foreign DLL discovery still belongs to the game. |
| Two features start in an accidental order | Dependency-ordered preparation and activation | Providers become available before their consumers activate. This orders participating modules, not every Workshop callback or Harmony patch. |
| Restarts leave stale controllers, event handlers or assets | World, attempt, activation and page owners with explicit cleanup | Reusable world assets survive same-map attempts while player resources end with activation. Only tracked resources can be unwound. |
| The first playable frame performs asset decoding or file reads | World/attempt preparation and named startup measurements | Authors can prepare expensive work during loading and measure the handoff. Runtime does not move hidden work automatically or promise a fixed speedup. |
| Movement mods compete for the same player or jump node | Body phases, movement leases and coordinated jump-controller bindings | Participating movement, thrust and charge controllers express ownership and ordering. Undeclared private mutations remain outside this coordination. |
| Every mod implements another binding editor and input reader | Controls+, chords, supported legacy binding discovery and shared physical input services | Players configure actions in one place; clients can share bounded input history with explicit loss/focus evidence. Unsupported bindings and devices are not guessed. |
| Custom menus handle navigation, text entry and close paths inconsistently | Native-style pages, page scopes, pointer support and shared text editing | Features reuse interaction and cleanup behavior while supplying their own content. Arbitrary foreign interfaces are not automatically rewritten. |
| Settings or state changes leave partially applied data | Settings helpers, game-thread commands and validated state restoration | Authors have common persistence and rollback tools. Snapshots cover registered participants, not arbitrary world state or native achievements. |
| Camera and scene effects both try to present the same frame | Presentation requests, scene composition and owned clock overrides | Participating features share explicit rendering/time boundaries. Runtime alone does not enable a higher frame rate or change physics. |
| A compatibility report says only that two mods conflict | Loaded-engine and patch-owner reports, module errors, resource evidence and opt-in timing | Reports distinguish discovery, preparation, activation and patch failures. An inventory is evidence for investigation, not proof of causation. |
| Known third-party assumptions break with another feature | Reviewed, narrowly targeted compatibility adapters | Validated integrations can work without replacing Workshop files. Unknown builds and unrelated failures are outside those fixes. |

See the [API map](api-map.md) for service selection and the
[architecture](architecture.md) for implementation boundaries.

## What cooperation looks like

A feature can decode reusable audio in world preparation, read fresh options in
attempt preparation, then attach a controller to the new player at activation.
It registers the controller and subscriptions with their owner immediately.
Restarting releases player resources, retains valid world resources and prepares
the next attempt. A failed start unwinds the tracked portion rather than leaving
the next attempt to discover an invisible half-started feature.

For movement, cooperation is more than choosing which DLL appears first. A mod
that replaces the native jump node can use Runtime's binding service to update
the shared references and coordinate charge decoration. A mod that only observes
a committed jump can consume that result instead of estimating it from velocity
after other behaviors have changed the player. See
[gameplay ordering](api.md#gameplay-ordering).

Shared services also reduce repeated polling and discovery. Physical keyboard
clients use independent cursors over one shared history; immutable package and
binding metadata is cached. Fresh settings, live player references and changed
world resources still need their own lifetime and invalidation rules. Shared
infrastructure helps avoid duplicate work, but measurements are still needed to
establish a performance improvement for a particular mod set.

## Harmony is a tool, not the module system

Harmony patches selected native methods. Runtime's module host decides which
participating services can start and who owns their resources. Those are separate
jobs: Runtime does not replace Harmony, and Harmony alone does not supply the
module lifecycle and ownership contracts described here.

Runtime's DLL has no direct Harmony assembly dependency and its Workshop payload
contains no Harmony DLL. Its hook adapters inspect engines already loaded in the
process. They do not install, upgrade or downgrade Harmony. A loaded version can
therefore come from another mod even when Runtime uses it first to install a hook.

There is no universal promise that "newer Harmony works" or "only 2.2.2 works".
Compatibility depends on the actual engine ABI, the mod's patch and when it is
installed. Runtime validates the requirements of its adapters, exposes degraded
or unsupported states, and provides fallback only where the service documents it.
See [Harmony and other mods](compatibility.md#harmony-and-other-mods).

## Existing Workshop mods keep their own lifecycle

An unmodified foreign mod still registers through Jump King's loader and retains
its own native callbacks, factories and patches. Runtime can observe supported
registrations, discover compatible legacy bindings and apply a specifically
reviewed adapter. It does not assume ownership of all of that mod's state.

Compatibility adapters address specific assumptions, such as a missing method
signature, a lookup that rejects controller subclasses or an interface that
assumes a fixed map layout. Each adapter has its own detection, ownership and
refusal rules in the [compatibility guide](compatibility.md).

These repairs apply only to validated cases. Runtime does not reorder foreign
patches, clear the native modified-player flag or infer the author of every
private mutation. It also cannot undo untracked side effects or sandbox code
running inside the game process.

## Where to go next

- Players: [compatibility](compatibility.md),
  [collecting diagnostics](diagnostic-workflow.md) and [troubleshooting](troubleshooting.md).
- Mod authors: [first package](getting-started.md), [lifecycle](lifecycle.md),
  [preparation](preparation.md) and the [API map](api-map.md).
- Maintainers: [architecture](architecture.md), [API policy](api-policy.md)
  and [verification](testing-and-release.md).
