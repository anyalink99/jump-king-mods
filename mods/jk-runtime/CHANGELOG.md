# JK Runtime changelog

Runtime and UI changes share this changelog. Earlier UI-specific release notes
are retained in the [UI history archive](docs/history/ui-changelog.md).

## 1.32.0 - material contracts and movement evidence

- Add pure, budgeted support queries and scoped typed material declarations with
  optional read-only state capture and links to geometry/state services.
- Observe actual native/additional contact acceptance and before/after changes
  at seven native movement stages, shared by body and dormant without consumers.
- Extract shared Harmony method validity leases and use them in both arithmetic
  and movement-stage observation. Preserve explicit unknown coverage after mutation.
- Add opt-in 256-event movement traces, detached JSON export without overwrite,
  diagnostics coverage, a compiled SDK example and the interop handbook.
- Verify independent providers, actual collision callbacks, ownership, failures,
  mutation, both preparation orders, bounded recording and allocation-free sampling.

## 1.31.0 - observed movement arithmetic

- Add scoped horizontal motion observations, shared per-body capture, on-demand
  handler diagnostics and explicit reports for participating providers.
- Analyze bounded managed arithmetic without provider-name allowlists. Capture
  operands from the single real invocation, including stateful property reads.
  Identity handlers need no method instrumentation. Unknown branches, escaped
  speed, unsupported operations and changed patch graphs remain unknown.
- Check ordered scaling/carry, side effects, exceptions, shared consumers,
  disposal, patch invalidation, steady-state allocations and installed Harmony
  versions. Mega Gameplay Expansion consumes this API for No Walk Off.

## 1.30.5 - persistent activation failures

- Add a standalone LessAutoEquipping migration example with legacy preference
  compatibility, scoped Harmony patches, native-method fixtures and packaged
  menu/data-path checks. Include it in the SDK and its build validation.

- Preserve the original module and preparation errors in
  `JKRuntime.ActivationError.log` beside the Runtime DLL before teardown.
  Steam launches no longer require a captured console to diagnose a missing
  required module. The file holds the latest fatal activation failure.

## 1.30.4 - optional map integrations

- Apply map-owned package ordering only when the target module is installed.
  Capability dependencies and map-policy requirements remain mandatory.
- Allow Stereo Madness to load with Runtime and MME alone, preserving controller
  ordering when Ball King, Casual Jumping, Smooth Camera or Subframe Charge exist.
- Keep standalone module ordering validation unchanged.

## 1.30.3 - Workshop map transitions

- Run world teardown, map-package discovery and SDK preflight at native
  LoadAssets after the Workshop menu selects content, before assets/screens load.
- Detect direct root changes before attempt preparation as a fallback.
- Rebuild authored intros and controller resources for the selected world.

## 1.30.2 - embedded map packages

- Discover SDK packages in `jk-runtime/modules/<package>/*.jkmod` before map
  preparation. Companion dependency DLLs stay beside the package shell.
- Remove map-owned registrations on world exit and restore them on re-entry,
  without adding entries to the native mod or map menus.
- Keep DLLs out of map roots so Worldsmith classifies these bundles as levels.

## 1.30.1 - loaded-map navigation in Jump King Manager

- Add Settings > Mod compatibility fixes, enabled by default and persistent.
- Adapt the reviewed Jump King Manager's area buttons and screen selector to
  loaded location definitions and screen count; refresh on world changes and
  restore its original controls immediately when disabled.
- Navigate using the current player's hitbox and loaded collision data, support
  screens beyond the vanilla enum, and retain the Manager's save/coordinate tools.
- Report unsupported foreign builds and missing Harmony in Runtime diagnostics.

## 1.30.0 - shared page sessions and reproducible SDK builds

- Unify modal/embedded Cancel policy, release boundaries and page ownership.
  Embedded draw failures terminate the lifetime instead of reopening every tick.
- Add UiPageStack for owned nested editors with top-only input/drawing, preserved
  parent state and exhaustive teardown. Keep the existing IUiPage ABI.
- Export a runnable scoped main/pause UI example as the default SDK entry point.
  Align the interaction example and handbook with the same authoring path.
- Check documented entry-point names and current SDK versions against compiled
  metadata. Compile/package examples and build the detached UI template.
- Validate in fresh isolated directories, include Steamworks.NET explicitly,
  and publish DLL/SDK only after checks, retaining previous artifacts for rollback.

## 1.29.0 — map policy and intro preparation

- Read world-owned module requirements, cooperative suspension, mechanic denials
  and explicit foreign-assembly conflicts before map callbacks. Preserve settings.
- Freeze module suspension consent at registration; reject missing/rejected map
  requirements before gameplay rather than silently running a degraded map.
- Prepare attempts before the native intro tree, consuming the following normal
  preparation hook once. Keep direct GameLoop entry covered.
- Add map-policy-v1 and intro-preparation-v1 with unchanged assembly identity.

## 1.27.1 - native inventory spacing and item colors

- Merge mod and cursed items into one column, leaving three unlabeled groups.
- Size columns to their contents and use the native inventory format for frame
  padding, anchoring, row spacing and Back placement. Remove duplicate cursor
  insets and the extra footer reservation.
- Preserve registered inventory item colors, with pixel comparisons against
  original row rendering. Skip inventory layout refresh on idle input ticks.

## 1.27.0 - compact inventory

- Add Compact Inventory to Runtime Settings, on by default, with a persistent
  opt-out to the original list. Keep native ownership conditions and item menus.
- Arrange registered mod items, cursed equipment, cosmetics and miscellaneous
  items in four unlabeled columns. Wrap names and show native equipped marks.
- Add column navigation, independent overflow scrolling and matching mouse hit
  areas. An open item menu retains input ownership; unknown custom row types
  retain native rendering and navigation.
- Add installed-game graphics/input regressions, settings round trips and the
  frozen 1.26 API surface. `UIApi.Supports("compact-inventory")` exposes support.

## 1.26.3 - segmented grid separators and native cursor spacing

- Split horizontal grid separators per column and add vertical segments between
  adjacent entries, limited to their content height. Keep gaps between segments
  and avoid cell outlines.
- Match native cursor geometry: 16-pixel text inset and a five-pixel selected
  shift. Reserve both when wrapping titles and metadata.
- Move Optimizations and Diagnostic mode into JK Runtime Settings in both main
  and pause menus, preserving their saved values and behavior.
- Validate separators and cursor geometry against actual native-font pixels.

## 1.26.2 - menu lifecycle, viewport safety and adaptive grid rows

- Clear interrupted menu decorators on exit/reentry, so Settings and vanilla
  Workshop Next require a fresh confirmation. Preserve genuinely active children
  and close interrupted embedded page lifetimes exactly once.
- Keep native settings and Workshop windows inside the game viewport. Oversized
  lists scroll to the selected row; mouse regions follow the visible rows,
  including native Workshop popup selectors.
- Size each three-column grid row by its tallest wrapped entry. Pack complete
  rows into the available height, add thin horizontal separators and reproduce
  the native five-pixel selected-text shift without changing wrapping.
- Add actual native lifecycle and nested-click regressions, pixel shift checks,
  viewport/scroll hit tests and headless adaptive layout/resource-lifetime tests.

## 1.26.1 — original menu behavior and compact Workshop layout

- Remove global selection/back audio hooks. Keep Move and Back silent, use
  native selectA for accepted actions/settings edits, and leave unavailable
  shared controls silent. Existing consumer binaries inherit this correction.
- Use three columns and up to twelve text entries per page, with the native
  selection arrow, no cell frames/highlights and no duplicate title footer.
- Recover complete Workshop author strings, retain status metadata and native
  completion icons, and wrap names using cached pixel-font layouts.
- Allow focused Runtime-only release installation without rebuilding or
  replacing unrelated installed feature mods.

## 1.26.0 — native UI feedback and readable Workshop names

- Add `UiSounds.Play(UiSound)` and `ui-feedback-v1`: shared native menu assets,
  existing SFX volume, semantic move/confirm/change/back/error cues.
- Add menu navigation/back feedback through menu hooks (removed in 1.26.1);
  complete feedback for debug actions, controls, bindings, text entry, pins,
  merchants, diagnostics and shared list/number/command controls.
- Use white normal text, a separate disabled gray and readable secondary copy.
- Replace the compact Workshop grid with two wide columns, native ornamental
  frames and cursor, wrapped pixel-font names and a selected-title caption.
  Cache name layout and reuse the visibility scratch list between updates.
- Retain the released 1.25 API surface; add native GPU/name-capacity and
  warmed-menu feedback regressions. No gameplay or input binding changes.

## 1.25.0 — infrastructure and composition

- Add protected settings recovery, stale-writer detection and atomic durable commits.
- Add bounded background work, native component ownership and explicit movement leases.
- Share physical keyboard observations and the presentation scheduler across consumers.
- Add scene/camera target ownership and final frame styles; preserve native simulation cadence.
- Move the optional native performance caches and migrated preference into Runtime.
- Add retryable shared Harmony patch groups and an embeddable text editor with bounded asynchronous clipboard work.
- Isolate incomplete discovery metadata and retain the complete released API 1.24 baseline.
- Compile Runtime suites once while keeping process isolation; coordinate content-verified Fast reuse and explicit integration fixtures.


## 1.24.0

- Add per-producer keyboard capture gates with independent release draining and
  capture-generation invalidation for queued action streams.
- Expose `UIApi.AcquireTextInput()` for custom editors using the same ownership
  and cleanup contract as the shared on-screen keyboard.
- Clear cached native keyboard/menu frames on text editor transitions; retain the
  permanent keyboard gate underneath modal input layers, including when no custom
  bindings have previously been configured.
- Preserve gamepad/pointer text entry and update editor focus history while inactive.

## 1.23.0

Append JumpEvidence.BufferedHold for a physically measured release whose charge
starts at native buffer acceptance. Existing enum values remain unchanged.
BufferedNative continues to describe uncorrected native buffers, including
automatic maximum charge and unavailable physical release evidence.

## 1.22.0

Add exact fractional hold-frame counts to JumpResult and an extended constructor.
Existing constructors and integer properties remain binary compatible; fractional
results leave integer counters null instead of silently rounding. Surface-scaled
charge time remains separate from the input-frame count.

## 1.21.1

Diagnostic mode separates map-switch content groups, native texture/audio/font/effect
readers and audio-thread unload synchronization. Each performance window also
records source/destination map paths and the selected title. The hooks are removed
when diagnostics is disabled; loading order, content ownership and saves are unchanged.

## 1.21.0

- Add `UI.MenuTreeSession`: one tree lifetime, explicit native/presentation
  dispatch, sticky completion and failure, and a pending-result handoff retained
  when the clock changes. Presentation updates cannot start or restart a tree.
- Attribute native map-load costs separately in Diagnostic mode: SetContent,
  resource reinitialization/loading, screen loading, intro/entity/prop creation,
  attempt preparation and gameplay handoff. No save or load timing is changed.

## 1.20.0

- Add `NativeWorldGeometry.TryReadNativeBounds` to read stored bounds of exact
  native block types without invoking virtual/custom geometry code. Unknown
  types and subclasses return false; they must not be treated as empty space.
- Keep assembly identity 1.0.0.0. New packages declare API 1.20 through the SDK.

## 1.19.0

- Add `Input.NativeInputFrames` for complete native pad snapshots and menu input
  scopes that clear consumed input on exit, including exception cleanup.
- Preserve active menu rows by identity when pinning or updating integrations;
  defer removal of an open row and compose later edits with the pending list.
- Forward pinned wrapper resets to native child settings, preventing old open
  pages from reopening without confirmation. Pinning refreshes only the pins.
- Preserve disabled rows, prepare layout before publication, and restore pinned
  settings in memory if persistence fails.

## 1.18.0

- Add a persistent native Diagnostic mode toggle: reversible measurement hooks,
  bounded rolling performance reports, startup tracing and callback profiling.
- Add `RuntimeApi.MeasurePerformance` for game-thread module substages, and
  `NativeFrameDispatch` for explicit non-inlined presentation-scheduler boundaries.
- Add a native `CanChange` dependency overload to `SettingToggle`, retaining the
  existing constructor and assembly identity for binary compatibility.


## 1.17.0 — shared text entry

- Add `UiTextEntryPage` for modal, embedded and composed pages: native Windows
  character input, caret/selection, paste, and gamepad/pointer character keys.
- Isolate typing from native keyboard bindings and Runtime shortcuts; wait for
  release on normal close and drain held keyboard input after forced teardown.
- Add page-owned Latin/Cyrillic fallback rendering and `text-entry-v1` discovery.
  Existing page contracts and stable assembly identity remain compatible.

## 1.16.2 — preserve flag sources across native resets

- Observe native save reset and teardown to retain the sources of an inherited
  modifier count. Do not carry removed handlers or assume matching counts imply
  matching provenance. Unknown/unobserved resets remain conservative.
- Display `Flag sources` with a registration explanation. Every native marker
  follows the same attribution policy; no per-mod usage filters or exemptions.
- Persist inherited sources separately and expose them in diagnostics. Native
  saves, counters, achievements and third-party mod registration are unchanged.

## 1.16.1 — run modifier diagnostics

- Preserve the reason for unknown native flags in the attribution sidecar and
  explicit diagnostics report. Older unknown records remain unknown, with a
  legacy-history explanation.
- Add opt-in bounded registration tracing with call stacks, behaviour types,
  before/after native counters, attempt identity and observer status. Each launch
  writes a separate local file through a background worker.
- Clarify that attribution records handler registration, not actual feature use.
  Native flags, achievement policy and mod gameplay remain unchanged.

## 1.16.0 — focused binding pages

- Add public UiBindingsPage and binding-pages-v1 capability for an explicit list
  of registered IDs. Keep constructors idle and use existing binding storage.
- Share primary/secondary slot editing with Controls+; support clear/default,
  mouse navigation, one/two-button capture and device/focus cancellation.
- Migrate Smooth Camera, MGE and Ball King to this common component.
- Preserve previous API signatures, binding IDs and device profiles.

## 1.15.2 — conveyor jump discovery

- Repair the reviewed ConveyorBlockMod exit lookup in memory. Native exact-type
  discovery cannot find JumpState subclasses; resolve the unique live node.
- Preserve conveyor reset conditions and horizontal momentum. Do not patch shared
  generic native methods or suppress exceptions from unrelated conveyor logic.
- Prepare compatibility before gameplay, recheck at activation and refuse managed
  jump replacement before graph mutation when a loaded conveyor cannot be protected.
- Report adapter state and test the original installed DLL across installed Harmony
  engines, native/subclass graphs, restoration and actual compiled SFC state.

## Documentation maintenance (unreleased)

- Added a developer handbook, lifecycle/ownership reference, settings/commands,
  state/time, packaging, troubleshooting and verification guides.
- Clarified preparation boundaries, disabled-path costs, native pause observations,
  command inline/deferred failures, snapshot epochs and durable-file limitations.
- Export all example sources and handbook dependencies in the standalone SDK.
  Generate a public signature index from the built assembly and validate source/SDK
  links, anchors, guide routes and matching exported copies during the build.
- Compile and exercise preparation/state examples; move the UI integration sample's
  map reads into attempt preparation. Runtime gameplay behavior is unchanged.

## 1.15.1 — Controls+ discovery inside SDK packages

- Inspect the loaded implementation behind each SDK shell for legacy bindings.
  Ball King's Morph binding is visible again without per-mod registration code.
- Preserve the shell-based binding ID and saved Controls+ assignments; keep
  cached metadata, retry uninitialized settings and read replacement settings live.

## 1.15.0 — scoped preparation before player handoff

- Add optional SDK OnWorldReady and BeforeAttempt callbacks with RuntimeScope
  ownership, dependency ordering, cancelled-intro cleanup and retryable failures.
- Share native intro/world-exit hooks across mods; retain synchronous fallback
  for loaders without an early preparation callback.
- Prime native item reads and dialogue compatibility before intro entities.
- Add allocation-free opt-in startup substage scopes. Capture preparation and
  native handoff without spending the gameplay trace budget on intro frames.
- Migrate Camera, Replays, Mapping, Mega Gameplay/Ball audio and More Items inventory.
  Keep player attachment, save changes and native simulation at their existing stages.

## 1.14.0 — adjustable Debug Actions

- Add live labels, Left/Right adjustment callbacks and confirm hints to debug
  actions without changing the existing constructor or plain action behavior.
- Support mouse arrow targets and native binding hints for value actions.
- Retire standalone Hammer King DLLs when installing More Items, retaining
  rollback backups and user settings.

## 1.13.6 — cache repeated menu discovery

- Remember file assemblies without SDK manifests instead of probing them again
  for every settings entry. Newly loaded assemblies still participate in discovery.
- Cache the static metadata used to find legacy Controls+ binding containers.
  Retry null/failed getters and partial type loads; retain live settings getters
  for replacement containers. Dynamic assemblies remain discoverable.
- Validate those discovery/retry contracts and compare native menu construction
  with the installed mod set using the temporary automatic menu probe.
- No changes to native input, physics, behavior-tree order or frame scheduling.

## 1.13.5 — remove restart I/O while retaining native scheduling

- Queue immutable run-modifier records on one background writer. Coalesce pending
  changes, preserve current attempt evidence before persistence, retain atomic
  durable writes and wait briefly on normal process exit.
- Prime missing native inventory/settings cache entries at level start, using
  the existing forecast cache adapter. Preserve native writes and restart resets.
- Remove the elapsed-time reset introduced in 1.13.3: Runtime again leaves the
  native update accumulator and first-frame scheduling untouched.
- Verify background-write coalescing/races, resume/new-attempt isolation, storage
  failures, native cache results/writes and unchanged inventory file contents.

## 1.13.4 — identify slow callbacks in opt-in restart traces

- Separate native update phases and retain the type of entity/component callbacks
  taking at least 0.5 ms; increase bounded capture storage to avoid losing the end
  of a restart with many catch-up updates.
- Separate run-modifier snapshot reads, sidecar reads and writes. Extra native
  hooks remain opt-in and names are formatted off the gameplay thread.
- Verify actual native dispatch records slow callbacks. This diagnostic update
  does not claim to resolve the remaining live restart hitch.

## 1.13.3 — prevent simulation catch-up after restart initialization

- Reset the native elapsed-time accumulator once after all startup callbacks
  finish, at the end of the outer update. Expensive initialization no longer
  creates a burst of gameplay updates with no intervening draw.
- Retain the configured simulation interval and total game clock. Verify the
  actual native reset removes accumulated startup time and leaves ordinary
  update accumulation intact.

## 1.13.2 — opt-in restart tracing

- Add bounded in-process startup measurements for diagnosing remaining live
  restart stalls: module activation, Runtime setup, updates, draws/presentation,
  frame spacing and GC counts. Capture first 120 draws, retain six reports.
- Keep tracing off unless explicitly enabled before process startup; snapshot
  formatting and disk writes occur on a bounded background worker.

## 1.13.1 — remove diagnostic work from playable frames

- Stop collecting assembly/Harmony inventories and exporting files during the
  first entity update of every attempt. Explicit diagnostics export remains.
- Coalesce pointer status snapshots on a bounded background writer; avoid
  building unchanged diagnostic text every frame. Contain storage failures.
- Verify no automatic report files on activation/restart and retain manual
  text/JSON exports, pending-write coalescing and failure isolation.

## 1.13.0 — controller and charge-policy ownership

- Add independent native charge suspension leases with load-order-independent
  policy registration, settings refresh, release and unload.
- Reattach only after the final reservation is released; suppress native charge
  observations while another controller owns jumping.
- Clean partially acquired policies, retain both errors when controller changes
  and reattachment fail, and refuse controller mutation after failed detach.
- Test overlapping reservations, load orders, unload/reload, retry, thread and
  reentrancy guards, and failed cleanup. Retain the shipped 1.x assembly identity.

## 1.12.0 — scoped pages and reliable shared ownership

- Add ScopedUiPage, measured UiPageLayout, shared UiPageCommand routing/hints,
  stable-ID UiList and bounded UiNumberControl with captured pointer drag.
- Wrap all footer rows inside GuiFrame; wrap long text by text elements.
- Coordinate modal suspension with other owners, clean partial opens, preserve
  same-owner replacement registrations, and retain retryable failed cleanup.
- Cancel drags on page close, surface/focus changes and keyboard takeover.
- Freeze the 1.11 public ABI alongside 1.2/1.3 and update the SDK task guides.

## 1.11.2 — keep the cursor when pressing Escape

- Any physical keyboard key except Escape hides the cursor, including unbound
  keys and modifiers. Escape keeps mouse mode active while navigating back.
- Escape combined with another key still hides the cursor; Escape alone never
  reveals a hidden cursor. Controller input continues to hide it.

## 1.11.1 — responsive cursor movement

- Move the pixel cursor to the native Windows cursor plane instead of rendering
  it into the game's 60 Hz scene. Preserve the artwork, tip hotspot, integer
  scaling and first-click activation without changing simulation timing.
- Restore the previous window cursor on focus loss, UI exit and teardown;
  retain a software fallback if native cursor creation fails.
- Check native cursor colors, sizes, hotspot, reuse and ownership alongside
  existing activation, click, wheel and list-hover regressions.

## 1.11.0 — stable lists under the mouse

- Add `UiListViewport` to store scroll position separately from selected rows.
- Hover and clicks leave the viewport stationary; wheel explicitly scrolls it,
  and keyboard/controller navigation explicitly follows selection.
- Apply this to Controls+, debug actions, merchant lists, Replays and Wardrobe+.
  Wardrobe keeps an independent viewport for every page in its navigation history.
- Regress repeated edge-row hovering, wheel scrolling, keyboard wrapping and
  shrinking/empty lists, including real menu rendering and pointer dispatch.

## 1.10.2 — mouse in the initial main menu

- Initialize pointer hooks during native BeforeLevelLoad, before the title menu
  is built, instead of waiting for OnLevelStart and the first playable run.
- Keep pointer hooks across level-service teardown; title and Workshop menus
  do not need a player or an active Runtime module session.
- Write `JKRuntime.Pointer.txt` beside the DLL with startup, focus, viewport,
  surface and input status, including before the level report exists.
- Graphics tests invoke the native startup entry with Runtime idle and no
  player, then exercise the warmed scene loop and real menu click routing.

## 1.10.1 — activate mouse hooks after game warm-up

- Hook the real `JumpGame.Update/Draw` loop. The old `Game1.MyUpdate/MyDraw`
  forwarding methods could be inlined before mod startup, silently bypassing
  pointer input and hit-region registration despite successful patch installation.
- Exercise an optimized, already-warmed native caller in graphics regression
  tests, using the same shared Harmony version as the installed game.

## 1.10.0 — mouse navigation

- The first left click reveals a cream and gold pixel cursor; a subsequent click
  activates the hovered item. Keyboard/controller input hides it again.
- Wheel navigation and right-click Back cover native menus, Workshop/mod grids,
  Controls+, pinned settings, merchant pages, Wardrobe+ and Replays.
- `UiPointer` lets pages register hover, click, action and scroll regions during
  Draw; callbacks execute in Update. Only the top surface receives input.
- Map window coordinates through the actual game viewport, ignore letterboxes,
  release mouse controls on focus loss and let Controls+ capture mouse bindings.

## 1.9.0 — frame-aware footers

- Add `UiTheme.FooterRow` with 16 px frame padding and 4 px row spacing.
- Anchor controls, mod settings, merchant and replay footers to their owning
  frames. Wardrobe reserves room above its two footer rows for status and hints.
- Check footer containment and rendered empty space above the frame border.

## 1.8.0 — consistent pixel UI

- Use the native GuiFrame gray (#727272) for neutral button, card and panel borders.
- Size command groups to their content, with fixed gaps and centered text.
- Snap shared text and centered grid labels to integer game pixels.
- Resolve physical hints in Controls+, mod settings and merchant pages. Native
  menus can request distinct Confirm and Jump bindings without modal fallbacks.
- Mod settings use Boots for Make bindable, avoiding the default Jump/Confirm
  binding collision while keeping the displayed button and action consistent.
- Replays and More Items adopt the shared renderer and control hints.

## 1.7.0 — Workshop pages and physical button hints

- Add `UiMainMenuPlacement.Workshop` for pages inside the Workshop submenu,
  preserving Mods and Back without adding a Pause entry.
- Add `UiInputHints` to resolve modal actions to current device bindings and
  Controls+ chords. Shared command badges allow longer physical key names.
- Document the button-and-action hint standard and use it in the SDK example.

## 1.6.0 — isolated flight teleports

- Add optional `INativeFlightTeleports` at the native body teleport stage.
  Worlds without it retain side-exit refusal; live teleport behavior is unchanged.
- Mega Gameplay Expansion 0.7 follows native link destinations, coordinates and
  camera rules in its private forecast world, within the ordinary flight budget.

## 1.5.1 — restart forecast cost

- Populate missing native inventory/equipment read-cache entries during flight
  forecasts. Preserve existing entries and updates; write no saves. Avoid
  repeated inventory reads after a restart clears the native cache.

## 1.5.0 — native flight continuation and wind

- Add an optional clocked wind world for ballistic forecasts, with native
  activation, NoWind and snow rules. Preserve the wind latch on transfer.
- Resume a captured BeforeXMovement tick without applying wind or the water
  cache twice. Clone collision-frame containers instead of sharing them.
- Mega Gameplay Expansion 0.5 uses this contract for cooperative Warp forecasts
  and water/sand/wind regression coverage. Older wind-free worlds remain valid.

## 1.4.0 — shared gameplay contracts

- Observe native gameplay without SFC; attach its physical timing to one jump
  event while retaining the existing Jump% hook and buffered-jump behavior.
- Add cached logical actions and shared physical action streams with independent
  bounded queues, one binding authority and unchanged backend quality rules.
- Share native pause observations and multi-owner component suspension. SFC and
  Warp use these services; no high-rate reader or simulation starts by default.
- Add mechanic composition/conflict metadata and first-party activation reports.
  Keep Ball King's contour correction local and preserve native geometry.
- Embed the shared colour catalogue and register existing Ball/Mega variants,
  keeping scope separate from solidity and rejecting conflicting colours.
- Move Warp's reviewed native ballistic adapter into Runtime. Add first-differing
  tick/field conformance and compare Mega flight against each native body tick.
- Add bounded owner-labelled history, opt-in callback trace in Runtime diagnostics
  and outstanding-resource reporting. Preserve explicit unsafe-rollback failures.
- Freeze the 1.3 ABI alongside 1.2 and run an unchanged old-reference consumer.
  Expand SDK examples, XML service documentation and author/player guides.

## 1.3.0 — scoped geometry and SDK safeguards

- Add explicit versioned geometry profiles without global collision replacement.
  Ball King exports its existing contour and keeps third-party geometry providers.
- Centralize native screen/block reads and constructor-free slope copies. Preserve
  the loaded native geometry, including intentional third-party corrections.
- Add an on-demand mechanic inventory to diagnostic reports; Ball King registers
  its current form. Inventory entries do not grant simulation coverage.
- Add actor-local presentation leases. Warp and Replays use them to retain ticks
  while the body's physics is suspended; replay particles are not serialized.
- Guard scope teardown against reentrancy and new registrations during cleanup.
- Freeze the previously installed 1.2 public ABI and check it during every build.
  Keep assembly identity 1.0.0.0. Structural ABI checks do not prove all semantics.
- Stamp package minimum API from the SDK version instead of always claiming 1.0.
- Build a standalone SDK template and XML documentation alongside the payload.
- Extend coordinated installation to both Mega mods, with dependency collision
  checks and an explicit, backed-up local Screen Solver removal option.

## 1.2.0 — simulation foundation (unpublished)

- Harden body registration thread/lifetime checks; reject unknown phases and
  continue cleanup after individual failures, retaining failed leases for retry.
- Drain all pending command cancellations even when callbacks throw, preserve
  their errors and tear down the command pump. Reject work during cancellation.
- Enforce game-thread jump event subscription, delivery and unsubscription.
- Add lazy, explicitly driven simulation sessions; no idle capture or update hook.
- Resolve exact-version coverage, provider conflicts and phase/dependency order.
- Copy serialized branch state, bound payloads/events and reject expired contexts.
- Invalidate sessions on provider changes and level teardown; retain the 1.0 ABI.
- Add native wind-waveform parity tests. Full player/world and mod adapters remain
  in development; no universal simulation-support claim is made.

## 1.0.1 — foreign run-modifier attribution (unpublished)

- Observe native registration/removal through an already loaded Harmony 2;
  no extra Harmony DLL or compile-time dependency.
- Match behaviour assemblies to native mod names; label shared libraries as
  unresolved authors instead of guessing. Capture pre-start/same-tick markers.
- Share evidence with the explicit API: deduplicate overlapping observations,
  preserve native registration multiplicity, permissions, results and exceptions.
- Prefer the engine already patching registration methods, refuse independent
  conflicting patch tables, and detect later hook loss without fighting other mods.
- Add actual native-call fixtures on Harmony 2.2.2/2.3.3/2.3.6, both patch orders,
  nested calls, skipped/throwing foreign patches and mixed-engine conflict tests.
- Keep the 1.0 assembly/API ABI; no dependent-mod migration is required.

## 1.0.0 — clean-break runtime migration (unpublished)

- Add native results-screen contributor names, persistent per-attempt history,
  bounded paginated foreground layout and an opt-in attribution API for other mods.
- Preserve the vanilla legitimacy flag, map permissions and historical contributions;
  label unidentifiable foreign/previous-session causes instead of guessing names.
- One JKRuntime.dll and JKRuntime.* API; UI/Controls+ fully included.
- Common generated package SDK and native-discovered manifest loader.
- All five feature mods require the runtime; no first-party reflection bridges.
- Shared input evidence, body phases, native jump ownership and jump events.
- Typed settings, queued safe application, common checkbox UI and atomic XML.
- Transactional movement snapshots, exclusive replay clock and owned lifetimes.
- Module/dependency failure isolation and explicit unsafe-cleanup diagnostics.
- One-time backed-up UI data conversion, coordinated release installer.
- Native-game regressions, adverse discovery orders, actual participant schema
  checks and migration/rollback tests. These do not replace in-game testing.

## 0.1.0 — superseded prototype: unified runtime and UIApi+

- Merge UIApi+ into one JK Runtime mod and source tree. Retain UIApiPlus.dll,
  assembly version 2.1.0.0, public UI API 2.1 and existing settings/control IDs.
- Add deterministic module/capability resolution, version constraints, explicit
  ordering, exclusive resource checks, level contexts and tracked rollback.
- Resolve after synchronous level-start callbacks; preserve early registration
  and process definitions across reloads. Block reactivation after failed cleanup.
- Add read-only installed-game contract/fingerprint checks, Runtime diagnostics
  menu page, text/JSON exports and non-mutating Harmony version/patch inspection.
- Keep the old UI development commands as forwarding wrappers, update first-party
  build references and prevent duplicate UI/runtime installs in the installer.
- Add frozen ABI, precompiled UI consumer, graph permutation, failure injection,
  reload and real Harmony inspection fixtures; retain existing UI tests/examples.
- No gameplay-provider migration, snapshot service or generic load-order fix is
  claimed in this release. No game executable changes or bundled Harmony.

Earlier UI changes: [UI history archive](docs/history/ui-changelog.md).
