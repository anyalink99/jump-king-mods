# Gimmick library startup

Preparation runs through Runtime before player handoff. Expensive discovery and
geometry work should not spill into an idle first gameplay tick.

## Preparation and activation

- Empty startup has no catalogue scan, geometry application or state binding.
  Geometry baselines are acquired only when an actual override is applied.
- `BeforeAttempt` owns a player-independent plan for enabled saved rules. It
  prepares block construction, handler registration metadata and selected state
  paths/getter hooks. It never reads a previous player's state or publishes
  gameplay behavior. Scope cleanup clears the plan, including cancelled intros.
- First-tick activation binds only the selected live paths. A settings change
  after preparation refuses the stale plan rather than rebuilding it at handoff.
- Full state discovery occurs on an explicit library visit. Existing saved state
  leases keep their exact slots while the remaining catalogue is populated.
- Deferred pin callbacks only refresh menu structure. Pin construction does not
  read foreign settings. Setting discovery/read happens when that menu is drawn
  or the library is explicitly opened.
- Unchanged collision arrays skip intersection checks; changed arrays use a
  hash set for authored-block membership instead of repeated array searches.

## Geometry preparation

Selected saved geometry compiles in `BeforeAttempt` under
`mega-gameplay.prepare-geometry-plan`. Empty-space fill rasterizes validated
blocking geometry, preserves native slope occupancy and merges empty rectangles.
Activation consumes this plan only for the same enabled configuration and the
same source arrays. An explicit paused edit compiles its own plan. No masks are
built by idle gameplay, drawing or unselected catalogue entries.

## Checks

```powershell
scripts/check-mods.ps1 -Mod mega-gameplay-expansion -Integration
& build/mega-gameplay-expansion/_INTERNAL/MegaGameplayExpansionTests.exe 'C:/Program Files (x86)/Steam/steamapps/common/Jump King' StartupAudit
& build/mega-gameplay-expansion/_INTERNAL/MegaGameplayExpansionTests.exe 'C:/Program Files (x86)/Steam/steamapps/common/Jump King' ConstructionAudit
```

Fast regressions verify 120 idle ticks without catalogue mutation, snapshot
invalidation or geometry rewriting; dormant pins without foreign reads;
preparation before new-player attachment; selected static/nested state binding;
restart and cancellation cleanup; stale-settings refusal; full-browser access
without replacing active state leases; and prepared block activation/release.
Integration retains native physics, menu rendering, audio, map and wind suites.
The installed construction audit still applies/releases 96 material candidates
and checks real Expansion Blocks gravity. The startup audit's assertions check
behavior and phase ordering, not fragile wall-clock thresholds.

The geometry fixtures count mask compilations: none during idle startup,
compilation during selected fill preparation and no repeat at handoff. An explicit
paused edit must not reuse a stale prepared plan.

For an in-game trace, create `JKRuntime.StartupTrace.enabled` beside Runtime,
reproduce a cold start and restart, then inspect `JKRuntime.Startup.txt`. Remove
the marker and restart to disable tracing. See the
[diagnostic workflow](../../jk-runtime/docs/diagnostic-workflow.md).
Fixture timings isolate MGE work; they are not end-to-end game measurements.
