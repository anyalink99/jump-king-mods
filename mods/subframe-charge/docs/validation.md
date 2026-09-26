# Subframe Charge validation

Run from the repository root with Jump King installed:

```powershell
.\scripts\check-mods.ps1 -Mod subframe-charge
.\scripts\check-mods.ps1 -Mod subframe-charge -Integration
```

Fast checks cover charge quantization, buffered releases, input evidence,
ownership, settings, diagnostics and package loading. Integration adds native
presentation, graphics and installed-map fixtures. The detailed
[testing guide](testing.md) describes each group and optional dependencies.

## Compatibility checks

Select related mods together to pass freshly built implementations to the
combined fixtures:

```powershell
.\scripts\check-mods.ps1 -Mod subframe-charge,smooth-camera -Integration
.\scripts\check-mods.ps1 -Mod subframe-charge,more-items,morph-ball -Integration
```

These cover presentation-clock handoff, charge ownership and restoration across
movement modes. Missing optional Workshop fixtures are reported as skips.
A passing build is not evidence that a skipped integration ran.

## In-game checks

Test short taps, buffered holds, maximum charge, water, ice, snow and slopes.
Repeat after focus loss, pause, restart, map changes and controller reconnects.
Check quarter-step charge, measurement-only mode and fully disabled operation,
as well as transitions to other movement controllers.

Unsupported measurements preserve the native jump. To investigate one, collect
`SubframeCharge.log` and its rotated files following the
[diagnostics guide](diagnostic-build.md). Keep the build ID, settings,
controller transport and approximate incident time with the report.

Automated fixtures do not measure physical input latency or establish
compatibility with every Workshop mod. GPU captures and local timing samples
remain under `build/subframe-charge/_INTERNAL/`; they are not full-game FPS
measurements. Release history is in [CHANGELOG.md](../CHANGELOG.md).
