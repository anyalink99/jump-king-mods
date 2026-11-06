# Build and validate No Walk Off

Build the matching JK Runtime SDK and run the focused fast checks:

```powershell
scripts/check-mods.ps1 -Mod no-walk-off
scripts/check-mods.ps1 -Mod no-walk-off -Integration
```

Integration loads installed JumpKingPlus, Expansion and Blocks2 fixtures to check
inactive helpers, water, low gravity, one-way support and conveyor behavior.

The release package is `build/no-walk-off/UPLOAD_TO_WORKSHOP/`.
It contains one mod DLL. Install Runtime and a Harmony 2 provider separately.
Logs rotate at 512 KiB with one previous copy. Generated build output is reusable;
temporary investigations belong under `build/_work/` with bounded retention.
