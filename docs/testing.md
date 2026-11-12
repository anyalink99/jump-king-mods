# Mod checks

Run `python scripts/check-mod-docs.py` to check all mod documentation layouts and
local links. Selected mod builds validate their guide entry points before
compilation; SDK and authoring-kit checks also verify exported documentation.

Run checks from the repository root on Windows with Jump King installed.
Use `-GameDir` when the game is outside the default Steam library.

```powershell
.\scripts\check-mods.ps1 -Mod subframe-charge
.\scripts\check-mods.ps1 -Mod mega-mapping-expansion -Integration
.\scripts\check-mods.ps1 -Tooling
.\scripts\check-mods.ps1 -Full
```

The default Fast tier builds the selected mods and runs their focused tests.
Integration adds native integration and graphics for explicitly selected mods.
Dependencies use Fast build/API checks and can reuse matching results. Full also
audits dependencies, runs Runtime's historical foreign-provider fixtures and
includes retained Screen Solver conformance when selected. With no `-Mod`, Full
selects the solver and takes much longer.
Some integration fixtures need a graphics device or installed Workshop content;
the selected mod's validation guide lists those inputs.

Dependencies are built before their consumers. Select interacting mods together
to exercise their composition with fresh outputs. `hammer-king` selects its
current owner, More Items.

Use `-List` to inspect a plan without executing it, `-ExcludeMod` to omit a
mod, or `-ChangedSince <ref>` to select affected mods from a Git comparison.
Content-based caching skips unchanged Fast stages; `-NoCache` forces them to run.
Integration and Full stages always execute.

Use `-Tooling` after changing the planner, caches, documentation checker or
release-candidate tools. It runs their regression suites without building mods.
Full includes them too; ordinary mod checks keep documentation/package validation
but don't rerun tests of the check tools themselves.

```powershell
.\scripts\tests\check-mods.Tests.ps1
.\scripts\tests\mod-build-cache.Tests.ps1
.\mods\mega-mapping-expansion\check-docs.ps1
```

JK Runtime's build also validates its SDK examples and documentation.
Compilation and package validation don't replace testing the mod in the game.

## Reproducible stages and release candidates

`scripts/components.json` declares component ownership, dependencies and check
scripts. Each check has its own stable stage identity. Fast receipts include
arguments, tools, source inputs and output hashes; a failed or skipped check
can't seed a passing receipt. Integration and Full execute their checks again.

The compiler cache is separate: it restores verified DLL, EXE, XML and debug
symbol bytes for identical sources, references, resources and compiler options.
It never replaces a behavior check. `-NoCache` forces Fast checks to execute;
set `JK_COMPILER_REBUILD=1` as well to measure fresh compilation. Direct mod
builds use the same compiler helper and take a component writer lease. The
command's total time includes bounded history maintenance (at most once daily).
Keep process isolation for static-state tests; don't remove cheap assertions
just because their count looks large.

For a release, select only the packages being released and use `-Release`.
This requires their Integration checks and applicable installed-provider fixtures
without optional skips. Dependencies get Fast checks unless explicitly selected
too. MME-only releases don't need the old MoreTextOptions DLL; that fixture stays
in `-Full` or its direct verification script.
The result is `build/_work/check-receipts/latest.json`, including
provider hashes and each stage's status. Prepare a fixed candidate immediately:

```powershell
./scripts/check-mods.ps1 -Mod mega-mapping-expansion -Release
. ./scripts/release-candidate.ps1
$candidate = New-ModCandidate -RepoRoot $PWD.Path -CheckReceipt build/_work/check-receipts/latest.json -Components mega-mapping-expansion
./mods/jk-runtime/install-release.ps1 -OnlyRequested -Include mega-mapping-expansion -Candidate $candidate
```

Candidate creation rejects stale inputs, changed packages, missing stages and
weaker stage tiers or arguments. Dependencies support installation; only explicitly
selected release components can be uploaded. To also release Runtime, include
`jk-runtime` in both commands. Installation verifies candidate hashes, freezes the
operation bytes and preserves rollback copies and user settings. A candidate
isn't authorization to upload it. Workshop publication requires an explicit
request and the private workspace's maintained release helper.
