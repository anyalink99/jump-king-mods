# Runtime UI validation

Run from the repository root with Jump King installed:

```powershell
.\scripts\check-mods.ps1 -Mod jk-runtime -Integration
.\mods\jk-runtime\verify-ui-graphics.ps1 -Audio
.\mods\jk-runtime\tests\InstallerTests.ps1
```

Graphics and audio checks are explicit; Fast builds need no GPU or audio device.
The graphics script accepts `-HarmonyPath` to test a particular shared engine.
It uses copied game assemblies and native assets in an isolated test process.

## Behavior and compatibility

- Public API checks, frozen binary consumers, compiled SDK examples and handbook
  links. See [API policy](api-policy.md) for the supported ABI baselines.
- Native menu lifetime, fresh confirmation after interruption, active child
  preservation, click-through suppression and cleanup after failures.
- Scoped page sessions, nested pages, cancellation, text capture and held-input
  release boundaries.
- Move/Back silence, Confirm/Change using native selectA, explicit failures using
  MenuFail, disabled controls and SFX volume.
- Workshop grids: three columns, measured row heights, original titles/authors,
  completion icons, native selection arrow, text inset and segmented separators.
- Viewport bounds, overflowing menus, pointer targets after scrolling, debug
  actions and current-device binding hints.
- Compact inventory: item grouping, native equipment icons, independent column
  scrolling, wrapping, ownership changes, original colors and row spacing.
  Unsupported foreign rows fall back to the native list.
- Settings migration, explicit opt-out, restored native menus, installer
  selection, duplicate detection, unrelated-file preservation and rollback.

## Outputs and limits

Native PNGs and timing output remain under
`build/jk-runtime/_INTERNAL/ui-graphics/`. Compare the same scene, item set,
viewport and warmed sample loop when measuring changes. CPU draw submission is
not GPU execution time or a whole-game FPS measurement.

Exceptionally long labels may be truncated with an ellipsis; original detail
pages retain the full stored text. Controller ergonomics, physical listening
and interaction with an arbitrary installed mod set require an in-game check.
New release history belongs in [the Runtime changelog](../CHANGELOG.md).
Earlier UI-specific notes are preserved in the [UI history archive](history/ui-changelog.md).
