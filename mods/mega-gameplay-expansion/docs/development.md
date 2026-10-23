# Build and validate Mega Gameplay Expansion

Packages built with the current SDK require Runtime 2.0+. The published 0.15.2
release retains its original Runtime 1.44 minimum.

```powershell
.\mods\mega-gameplay-expansion\build.ps1
.\mods\mega-gameplay-expansion\build.ps1 -Tier Integration
```

Default builds run the fast regression tier, including all Dash and No Walk Off
cases and a representative native trajectory grid. Integration retains the full
2160-case trajectory sweep, installed-map/wind fixtures and native audio checks.
To refresh the Runtime SDK as well, use
`scripts/check-mods.ps1 -Mod mega-gameplay-expansion`. See [testing](../../../docs/testing.md).

Output: `build/mega-gameplay-expansion/UPLOAD_TO_WORKSHOP/MegaGameplayExpansion.dll`.
No separate Harmony or Runtime DLL is included in this package.

`MegaGameplayExpansion.log` is beside the DLL and rotates at 512 KiB.
See [implementation](implementation.md) for update order, cancellation
and test coverage.
