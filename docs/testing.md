# Mod checks

Run `python scripts/check-mod-docs.py` to check all mod documentation layouts and
local links. Selected mod builds validate their guide entry points before
compilation; SDK and authoring-kit checks also verify exported documentation.

Run checks from the repository root on Windows with Jump King installed.
Use `-GameDir` when the game is outside the default Steam library.

```powershell
.\scripts\check-mods.ps1 -Mod subframe-charge
.\scripts\check-mods.ps1 -Mod mega-mapping-expansion -Integration
.\scripts\check-mods.ps1 -Full
```

The default Fast tier builds selected mods and runs focused tests. Integration
adds package, installer or graphics checks where supported. Full includes the
retained Screen Solver conformance suite and can take substantially longer.
Some integration fixtures require a graphics device or installed Workshop content;
read the selected mod's validation guide for those inputs.

Dependencies are built before their consumers. Select interacting mods together
to exercise their composition with fresh outputs. `hammer-king` selects its
current owner, More Items.

Use `-List` to inspect a plan without executing it, `-ExcludeMod` to omit a
mod, or `-ChangedSince <ref>` to select affected mods from a Git comparison.
Content-based caching skips unchanged stages; `-NoCache` forces them to run.

```powershell
.\scripts\tests\check-mods.Tests.ps1
.\scripts\tests\mod-build-cache.Tests.ps1
.\mods\mega-mapping-expansion\tools\check-docs.ps1
```

JK Runtime's build also validates its SDK examples and documentation.
Compilation and package validation do not replace testing the mod in the game.
