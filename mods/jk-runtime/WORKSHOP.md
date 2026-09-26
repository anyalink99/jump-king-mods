[h1]JK Runtime[/h1]

Shared services for Jump King mods, including the former UIApi+ and Controls+.
Install one runtime alongside the mods that require it.

[h2]Why use JK Runtime?[/h2]
Mods share many of the same difficult jobs: starting in a useful order, releasing
old player resources on restart, editing bindings, building menus and finding
the cause of a conflict. Runtime gives participating mods common services for
these jobs, so each feature does not need its own competing implementation.

For players, Controls+ brings supported bindings and chords into one interface.
Native-style menus share navigation, mouse input and text editing. Diagnostic
reports expose module failures and loaded patches, while startup measurements
help authors find work that delays the first playable frame.

For authors, versioned dependencies order preparation and activation. Resource
scopes distinguish reusable map assets from each attempt's player controllers.
Movement and jump-controller services coordinate participating features, and
registered state can be validated and restored through a shared API.

[h2]Included[/h2]
Controls and chords, pinned settings, compact grids, inventory/equipment UI,
interactions, merchant APIs, input measurements, gameplay phases, module
dependencies, state snapshots and local diagnostics.

Controls+ supports left, right, middle and two side mouse buttons in the
keyboard profile, including keyboard + mouse chords. Wheel scrolling is not
a held-button binding.

When Jump King reports modified player behaviour at the end of a run,
Runtime lists the recorded contributing mods. Unknown sources remain labelled
unknown. This does not change the native flag or achievements.

[h2]Compatibility[/h2]
Runtime does not bundle or load its own Harmony engine. It uses an engine already
loaded by the game or another mod and does not force, upgrade or downgrade its
version. Support depends on the actual API and patch; no single version number
guarantees compatibility with every Workshop mod.

With a compatible Harmony 2 already loaded, Runtime can observe third-party
body-behaviour registrations, including short-lived markers. It cannot
identify every private patch or recover missing history from earlier sessions.

Reviewed compatibility adapters address specific API, controller-discovery and
map-interface assumptions. They validate supported builds and apply targeted
changes in memory without replacing Workshop files or Harmony DLLs. Their
coverage is limited to those validated cases; technical documentation describes
each adapter's requirements and behavior.

Runtime coordinates participating modules, not every Workshop hook. It does
not change FPS or bundle Harmony. Diagnostics can export local text/JSON reports.

[h2]Upgrading from UIApi+[/h2]
Update dependent mods to their JK Runtime versions and remove UIApiPlus.dll.
Old UI settings are converted once with a backup. Saves, inventory and replay
formats are preserved.
