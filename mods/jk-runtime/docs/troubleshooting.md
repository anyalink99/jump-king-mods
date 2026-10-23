# Diagnose the failing boundary

Start with the active DLL paths/versions, map, exact reproduction and an explicit
[diagnostics export](diagnostic-workflow.md). A menu row appearing is not proof
that its gameplay module activated; an inventory entry is not proof that an
arbitrary foreign mechanic can be instantiated or simulated.

| Symptom | Check | Recovery/next evidence |
| --- | --- | --- |
| Feature missing entirely | Native loaded-mod list, generated shell, enabled dependency, duplicate copies | Fix the installed payload and relaunch; do not install the implementation DLL separately |
| Package rejected before activation | `PackageHost.Errors`, manifest API/schema/hash/entry identity | Rebuild with matching inputs or install a compatible Runtime; keep the original error |
| Menu exists but mechanic is inactive | Module status/order, required capabilities, provider errors | Fix the earliest failed provider; menus can be constructed before activation |
| `BeforeLevelLoad` failed | Original definition/factory exception | Correct the cause and relaunch; this package error persists through attempt restarts |
| Preparation failed | World/attempt callback and cleanup errors | Fix cause, then retry an attempt; failed cleanup must succeed before replacing that scope |
| Activation failed | First install/start exception and tracked cleanup | Correct the cause; incomplete shared cleanup can require process restart |
| Setting saved but current behavior unchanged | Whether a `changed` callback exists, pending/cancelled application, `Commands.LastError` | Saved state survives teardown; inspect deferred failures rather than repeatedly toggling |
| Command changes rejected | Prior drain/teardown failure | Correct cause and restart the game; attempt restart does not clear command poison |
| Immediate command threw without `LastError` | Whether the pump existed | Handle the propagated exception; the inactive inline path has no pump failure report |
| Snapshot rejected | Registration generation, participant identities/versions, custom validation | Capture fresh compatible state; do not force an old snapshot into a new player |
| Restore rollback incomplete | Original and rollback exceptions, participant ownership | Stop relying on that state service and restart after correcting the faulty participant |
| Event listener stops receiving | Callback error in journal, lease disposed, native coverage | Correct callback and acquire a new owned subscription; throwing gameplay observers are disabled |
| Physical timing unavailable | Focus, neutral/rearm, device health, overflow, binding authority/epochs | Re-establish valid evidence; do not synthesize a duration from a slow frame |
| Simulation refuses a world | Exact requirement/provider versions, conflicts, fingerprint and evidence | Add/test the missing isolated contract or report unsupported; do not omit active mechanics |
| Geometry query fails | Exact actor profile/version, provider exception/conflict, current world | Preserve failure; substituting an unrelated shape would change physics |
| Resources remain after unload | Owning module, outstanding tracking records and release exceptions | Retry where the scope/API supports it; never acquire a replacement over an unreleased owner |

## Handoff stalls with no feature enabled

Trace preparation and the first playable updates/draws separately. Check native
pause-menu construction, setting getters, lazy static initialization, reflection,
asset/file reads, catalogue construction and delayed callbacks. A checkbox being
off does not prove that any of those are dormant. First verify that the early
preparation hook succeeded, then inspect where the actual expensive work runs.

Compare cold load, repeated same-map restart, cancellation and world switch.
Use the same installed builds/configuration and retain total load time, GC and
dropped-record information. A removed first-tick scan must not reappear as an
automatic deferred menu refresh. See [preparation](preparation.md).

## Harmony debug log failures

If a crash names `HarmonyLib.FileLog.Log` followed by `Harmony..ctor`, check
Harmony's debug log before changing map XML. Its default location is
`harmony.log.txt` on the Desktop; `HARMONY_LOG_FILE` can select another path.
An I/O error there can stop patch initialization before the map loads.

Update Runtime to 2.3.3 or later, fully close Jump King and relaunch it. Runtime
guards Harmony's writer during native mod discovery, before level callbacks
run. This covers other mods' direct Harmony calls and engines loaded later;
no environment-variable changes are needed. Runtime doesn't replay callbacks
that already failed in the running process.

Writable logs still receive output. Default file writes stop at 4 MiB or the
first I/O failure without deleting existing contents. Custom writers receive
output until they fail; Runtime doesn't dispose them or change Harmony's debug
mode. The guard leaves a later replacement writer alone. Patch errors still
propagate normally. Logging done before Runtime is discovered isn't covered.

Runtime diagnostics shows a paused logger and includes `harmonyLogError` in
the exported report. `harmonyLogGuardError` records an unsupported or failed
writer installation. Preserve the original log when investigating its storage
problem. If `HARMONY_DEBUG` was enabled for an investigation, disable it when
that investigation ends; a mod can also enable debug output itself.

A later `BodyComp.UpdateInternal` null reference needs its own reproduction,
map and active mod list. That stack alone doesn't establish that XML files
were changed or that uninstalling a mod left its patches behind. Harmony
patches belong to the running process and don't survive a full game exit.

## What to include when reporting

- Game/Runtime/feature versions and active paths; relevant Harmony versions.
- Map identity, affected settings and exact steps from launch.
- Whether the failure happens cold, on restart, on world switch or after a restore.
- Original report/trace, including the first error and cleanup/rollback errors.
- What was measured in a headless fixture versus actually observed in game.

Keep reports local until explicitly shared. Preserve settings, failed files,
backups and raw captures. A diagnostic inventory does not need a new per-frame
scanner, and a support investigation does not require changing gameplay timing.
