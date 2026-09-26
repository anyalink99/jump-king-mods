# Intro-to-control restart probe

This temporary native mod measures the transition from the visible intro king
to the controllable player. It has no JK Runtime dependency and is not included
in Runtime's Workshop package. It uses the installed shared Harmony DLL.

Build and run focused native checks with Windows PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File mods/jk-runtime/tools/restart-probe/build.ps1
```

When explicitly installing for a diagnostic session, copy only
`build/jk-runtime/_INTERNAL/restart-probe/JKRestartProbe.dll` to the game's
`Content/JKMods`. Keep `JKRuntime.StartupTrace.enabled` absent to avoid duplicate
dispatch instrumentation. Do not copy test executables or native dependencies.
Remove the probe DLL after the investigation; preserve the captured log.

`JKRestartProbe.txt` beside the DLL reports installation status, then the latest
six captures. Each capture begins at entry to native `GameLoop.OnNewRun`, before
player construction or any `OnLevelStart` callback, and ends after 120 draws.
It measures individual direct calls/constructors within that native method,
each mod callback (including throwing callbacks), native loader logging, full
updates and draw/present spacing. Entity/component phases above 0.5 ms are named.
Nested timings overlap. Negative frame start includes work earlier in the same
update or the preceding draw; intro frames themselves are not retained.

Fixed storage is filled on the game thread. A bounded background worker formats
and writes finished snapshots. Initial patching/JIT can affect a cold attempt;
use several warm restarts and leave at least three seconds after each. This is
instrumentation, not a performance fix. It does not change input, behavior-tree
states, save data, update count or framework timing. Tests verify native callback
exception/order behavior, IL stack/branch/ref semantics, component dispatch and
bounded capture storage. They do not establish live performance or visual parity.

## Automatic menu-only probe

`build-menu-probe.ps1` builds a separate `JKMenuProbe.dll`. For an explicitly
requested diagnostic session, install that DLL alone, then launch the game.
After native initialization it constructs options/inventory menus four times
using unregistered disposable owners, writes `JKMenuProbe.txt`, and exits the
game. It does not start an attempt, restart, synthesize input or replace the real
pause manager. It invokes the same mod menu factories as the native pause menu;
their normal initialization side effects can occur. Do not use during ordinary
play. Remove the DLL when the game closes and retain the report. Native menu
construction is an isolated measurement, not an end-to-end restart guarantee.
