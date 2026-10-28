# Build and validate Powers

Build the matching JK Runtime SDK and run the focused fast checks:

```powershell
scripts/check-mods.ps1 -Mod powers
scripts/check-mods.ps1 -Mod powers -Integration
```

The Powers checks also build No Walk Off and exercise both independent assemblies
on a protected edge followed by a Warp landing.

The release package is `build/powers/UPLOAD_TO_WORKSHOP/`.
It contains one mod DLL. Install Runtime and a Harmony 2 provider separately.
Logs rotate at 512 KiB with one previous copy. Generated build output is reusable;
temporary investigations belong under `build/_work/` with bounded retention.
