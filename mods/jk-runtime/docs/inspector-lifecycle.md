# Inspector lifecycle

Runtime installs observation at loader call sites. It records the real factory
instance, RGB, returned block reference and source screen. It never adds hooks
to foreign factories or constructors. Records are cleared for each world load
and bounded to 131072 calls. Unknown loader paths remain usable through loaded
geometry, with unknown provenance.

`BeforeAttempt` owns preparation of enabled saved recipes, handler metadata and
selected state paths. It doesn't compile a geometry plan from unfinished slopes.
After native `ModLoader.CallOnLevelStartMethods` returns, selected geometry is
compiled from the finalized live arrays. Terrain preparers registered during
world preparation run against that plan before activation. The normal activation
barrier binds player behavior, handlers and snapshots. Stale arrays or changed settings refuse
the prepared plan. Restart, cancelled startup and world exit dispose its scopes.

An empty configuration performs no catalogue, map or state scan at startup.
Opening Mod Inspector explicitly discovers loaded materials and state, and starts
the file-only map index. Opening a material card doesn't build a sample. Preparing,
previewing or applying one is an explicit action. Native slope templates copy the
stored collision lines without running constructors; copies never join a foreign
mod's constructor-collected correction list.

The More Block Sizes adapter is checked against a reviewed binary and two exact
factory call sites. Unsupported revisions report reduced provenance coverage.
See [compatibility](compatibility.md#block-observation-and-ceiling-slopes).

## Validation

```powershell
scripts/check-mods.ps1 -Mod jk-runtime,mega-gameplay-expansion
scripts/check-mods.ps1 -Mod jk-runtime,mega-gameplay-expansion -Integration
```

The Runtime build runs inspector regression tests, including settings persistence, failed
settings commits, 120 idle ticks, selected preparation, stale-plan refusal,
transactional geometry, state leases, exact empty-space filling and search.
`verify-inspector.ps1 -Integration -Graphics` additionally tests the installed
Forced Slope Blocks and More Block Sizes fixtures in both startup orders, with
Harmony 2.2.2 and 2.3.6, and renders native UI captures. Each loader case performs
three loads, checks real constructor collection and finalized slope lines, and
ensures inspection creates no extra slopes. Fixture hashes must match the
reviewed binaries. Reports and captures are under
`build/_work/inspector-validation/` in the source workspace.

The read-only map audit is `tools/audit_gimmicks.py` in the Runtime source tree.
It doesn't enable any controls. These tests don't establish compatibility with
every Workshop mod or replace a full in-game playthrough.

For an in-game trace, create `JKRuntime.StartupTrace.enabled` beside Runtime,
load/restart a map and inspect `JKRuntime.Startup.txt`. Inspector measurements use
the `runtime.inspector` prefix. Remove the marker and restart to disable tracing.
