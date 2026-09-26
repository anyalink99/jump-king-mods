# Settings, commands and files

Native menu edits preserve the selected row by object identity, including its
running child. Pin changes refresh only the pinned projection, keeping existing
pages alive. Removing an open row is deferred until its child completes or the
menu resets; subsequent edits compose with the queued list. Native hidden rows
stay hidden and layout is prepared before publishing the list. Pinned wrappers
forward tree resets to their inner native setting so a previously open page
cannot reactivate merely by selecting its row.

`Setting<T>` gives UI, bindings and commands one validation/persistence/application
path. It does not choose a file format, make reads asynchronous or guarantee that
the caller's delegates are cheap. All setting mutations and `Commands` operations
belong to the game thread.

## Read, persist, apply

```csharp
var enabled = new JKRuntime.Settings.Setting<bool>(
    "example.feature.enabled", "Enabled",
    () => preferences.Enabled,
    value => { preferences.Enabled = value; SavePreferences(); },
    () => ReconfigureActiveController());
```

This is a fragment: `preferences`, `SavePreferences` and controller recomposition
are owned by the consumer. Keep the getter side-effect-free and backed by memory.
`SettingToggle` reads the value in its constructor and while drawing, so a getter
that scans assemblies or opens files can stall even dormant menu construction.

`Set(value)` validates first, saves immediately and then requests `changed`, if
provided. The save delegate must propagate failures. Failed persistence attempts
to persist the prior value; failed rollback is reported with the original error.
The consumer owns any in-memory mutation made by that delegate too.

When the active command pump exists, application is deferred to its game-thread
update. Repeated writes to one setting supersede earlier pending application
callbacks; do not depend on queue storage being one entry per setting. Teardown
cancels pending application, retaining the saved preference for the next start.
Failed application attempts both restoring the last applied preference and
reapplying that old configuration. On the deferred pump path, the command system
records failure and rejects further runtime-changing commands until the game is
restarted. Settings with no `changed` callback do not use that command gate.

## Commands are not a background or startup scheduler

`Commands.Enqueue(action, cancelled)` queues against the active level pump.
**When no pump exists it executes the action immediately.** It does not promise
"next frame" in a main menu, before activation or after teardown. The pump drains
the number pending at the start of that update; newly enqueued work waits for a
later drain. On a drained-command failure, remaining commands are cancelled and
the queue is poisoned. On the immediate path an exception propagates directly to
the caller; there is no queue cancellation, `LastError` update or automatic poison
from that call. A `Setting<T>` application still attempts its own rollback before
propagating. Do not use these different reporting paths interchangeably.

Use commands for safe controller recomposition outside the callback currently
using that controller. Use `BeforeAttempt` for expensive loading. A timer or
one-frame delayed callback merely moves the cost to another gameplay frame.
Give cancellation callbacks bounded cleanup; they cannot enqueue replacement work
while cancellation/teardown is in progress. Check `Commands.LastError` when a
setting stops applying.

## Package data and durable files

Use `PackageHost.GetDataDirectory(typeof(YourEntry).Assembly)` for data beside the
package shell. Embedded implementation assemblies may have no useful `Location`;
do not build a settings path from that location or from the current working directory.
Choose stable filenames and keys; moving the DLL must not silently rename data.

`AtomicXmlFile.Load<T>` returns null for a missing file. Malformed/unreadable files
can throw. Distinguish absence from corruption: defaults may keep the mod usable,
but opening a menu must not overwrite the unreadable original.

`SettingsFile<T>(path, defaults, validator)` loads once and exposes `Value`,
`Status`, `Error` and `CanSave`. Missing files use defaults without writing.
Unreadable or unsupported XML stays intact; a valid `.bak` can supply read-only
values. Unknown elements/attributes are treated as unsupported data, preventing
an older mod from silently removing newer fields. Supply a validator for known
schema versions and semantic constraints. Restore/move the original and reload
before editing recovered settings. Load the store in preparation or an explicit
menu action; getters read only its in-memory value.

`SettingsFile<T>.Save(candidate)` rejects recovery mode and external changes,
retains a backup and publishes its value only after a successful commit. Prefer
a candidate copy rather than mutating `Value` before saving. Its fingerprint
checks run only on load/save, not per tick. Concurrent processes are not a shared
transaction: serialize ownership and do not write the same path from workers.

`AtomicXmlFile.Save` is the lower-level primitive: it serializes to a uniquely
named temporary file, flushes it, then replaces/moves the destination, retaining
the prior bytes in `.bak`. It is synchronous durable IO and does not detect stale
writers. Retain separate backups for migrations or installation. Avoid saving
from getters, Draw or every tick.

`DataMigration.MigrateUi(directory)` is specifically the one-time UIApi+ settings
conversion. It skips an existing Runtime settings file and retains the old file
plus a migration backup. It is not a generic migration API for your mod's schema.
Own and version those migrations explicitly.

`NativeSaveFiles.GetPath(folder, file)` resolves a mod-owned file against the
installed game's current content prefix. It validates the relative path without
reading or mutating the game's private save cache. The native generic loader can
turn damaged XML into a missing result; use a checked reader for mod-owned data.
Respect native reset/context ownership and never recreate a removed save folder
from an old pending operation.

## Owned work and components

`BackgroundWorkQueue(owner, maximumPending)` reserves bounded capacity before
`TryPrepare` calls a synchronous snapshot factory. It then executes the returned
action on one lazy worker. `BackgroundWork.State`/`Error` are memory-only completion
signals for game-thread UI; workers must never access engine objects or invoke
UI observers. Completed records release their action/payload. Failed preparation
and execution release capacity. `Dispose` rejects new work without blocking;
accepted writes drain. The temporary worker is foreground so normal process exit
does not silently kill accepted writes, and terminates whenever the queue empties.
Only use bounded managed/file operations; hung external IO can delay process exit.
`Drain(timeoutMilliseconds)` belongs at explicit loading/shutdown boundaries,
never in a playable tick or Draw. No worker exists while an unused queue is idle.

`Gameplay.ComponentAttachment(entity, component)` owns a native component slot.
Disposal disables and removes it, preventing retained payloads and growing lists
of inactive components. Use it on the game thread outside the target entity's
component iteration (for example activation, teardown or a safe queued command).
The lease can be owned by a Runtime scope. Component construction and feature
payload cleanup remain the consumer's responsibility.

## Settings-dependent preparation

Read immutable preferences or map files in `BeforeAttempt` and build a plan without
touching the previous player. At activation bind that plan to the new player.
If preferences can change between preparation and activation, invalidate or
validate the plan; never silently apply a stale one. Expensive rebuilds should
happen in loading or on an explicit paused UI action, not on the first playable tick.

Use [state participants](state-and-time.md) for temporary movement snapshots,
not automatic settings rollback. Run-modifier history and inventory have separate
durability/permission rules. See [verification](testing-and-release.md) for failure,
cancellation and corrupted-file cases.


`new SettingToggle(setting, text, canChange)` projects a dependency through native
`IToggle.CanChange`: the label becomes grey and native confirm/click cannot change
it while the condition is false. It reevaluates without rebuilding the menu.
Keep persistence validation too, since non-UI callers can still set the underlying
setting. The original two-argument constructor remains binary compatible.
