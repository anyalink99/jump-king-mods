# Collecting a useful report

## Choose the capture

Fatal activation failures automatically save their original module/preparation
errors to `JKRuntime.ActivationError.log` beside the DLL before cleanup. The
latest failure replaces the file; successful runs leave the previous report
intact. Check its timestamp when reporting a new failure.

| Symptom | Capture | Output beside Runtime DLL |
| --- | --- | --- |
| Missing/rejected module, dependency or native contract | Explicit diagnostics export | `JKRuntime.report.txt` and `.json` |
| A callback is slow/fails during normal gameplay | Short callback trace through diagnostics page | Bounded journal in the exported report |
| Cold-start/restart pause or hitch when control returns | Startup trace enabled before launch | `JKRuntime.Startup.txt` |
| Cursor/pointer startup or input takeover | Changed-state pointer diagnostics | `JKRuntime.Pointer.txt` |
| Physical button quality/timing | The input provider's device-health evidence | Provider-specific output; logical event timing is insufficient |

## Persistent diagnostic mode

Use **JK Runtime > Diagnostic mode** in the native main or pause mod settings.
It is off for legacy/new settings. Enable, reproduce, then disable to export the
partial window and remove performance/startup measurement hooks immediately.
The setting persists across launches. No extra probe DLL is needed.

While enabled, Runtime records 45-second windows across title, gameplay, pause,
focus loss and restarts. Four owned text/CSV pairs named `JKRuntime.Performance.0`
through `.3` rotate beside the DLL. Manual report export also closes the current
window. Normal exit exports a partial window and waits up to two seconds for the
bounded writer. Crashes/forced termination may lose the active or pending window.
Copy a report before more windows replace it.

Measurements include actual native Update/Draw, Present, whole Tick (including
sleep/wait), frame gaps, supplied simulation time and update frequency, controller,
world, weather and scrolling-background phases, entity/component inclusive costs,
allocation counts when supported, all GC generations and boolean settings read
from known loaded implementations. The full manual report retains assembly,
Harmony-owner, mechanics, resource and error inventory. Report status/write errors
are visible on Runtime diagnostics; there is no upload or general key logging.

Cold frames are retained. Nested timings, allocations and GC counts overlap: do
not sum across groups. CPU draw/present measurements are not GPU execution or
physical input-to-photon latency. Profiling changes performance. Storage is capped
at 65,536 samples, 2,048 types and 256 module stage labels per window. Overflow is
reported. One writer and one pending snapshot bound the queue; replaced pending
samples are counted as dropped. Formatting and writes run off-thread.

`using (RuntimeApi.MeasurePerformance("module.operation"))` adds a stable named
substage on the game thread. Disabled scopes allocate nothing and do not read the
clock. Do not copy scopes; capture transitions discard unfinished scopes.

Diagnostic mode also enables startup capture for subsequent attempts and callback
profiling. The legacy startup marker still independently requests startup-only
tracing. When present, turning Diagnostic mode off leaves the marker request
active; remove the marker and restart to stop it too.

Runtime records individual native loading operations in the
performance report: SetContentNode, ReinitializeAssets, LoadAssets, LoadScreens,
IntroState initialization, PropManager.Load and GameLoop preparation/handoff.
These are nested inclusive timings; do not add their durations together.

## Module and gameplay callbacks

Open **Runtime diagnostics** in the pause menu. The page shows module status,
mechanic activation reasons, conflicts, input streams and outstanding resources.
These are observations, not proof that a mod is safe or that a run is legitimate.

For a timing or ordering problem:

1. Press Left or Right to enable callback trace.
2. Close the menu and reproduce the problem briefly.
3. Return to the page and press Enter to export the report.
4. Turn the trace off. It also stops when the level ends/unloads.

The report is saved beside JKRuntime.dll as `JKRuntime.report.json` and
`JKRuntime.report.txt`. It contains the Runtime/game fingerprints, loaded module
identities, Harmony targets/owners, active mechanic explanations, tracked
resources and the last 256 journal records. Details are capped at 2048 characters
per record. A busy trace can overwrite older entries; reproduce just before
exporting. There is no telemetry upload.

Callback timing is off by default. Turning the page trace on explicitly requests
gameplay observations and timing; closing the page leaves that short diagnostic
session active until it is disabled or the level ends. No simulation starts.
SFC's own device-health log remains the source for raw input quality and binding
details; gameplay delivery timestamps are not physical button timestamps.

For mod authors, `RuntimeJournal.ProfileCallbacks` enables callback timings and
`RuntimeJournal.Record` adds a bounded owner-labelled note. `RuntimeResources`
tracks successful acquisitions until successful release. Compare `Inspect()`
before and after teardown: an outstanding active-level resource is normal, while
a resource whose owner has ended needs investigation. Failed releases remain
visible instead of pretending cleanup succeeded. Track custom resources through
`ModuleContext.Track` or `RuntimeResources.Track` and dispose them on the game thread.

Errors are handled according to their effect. A failed observation subscriber
is disabled locally. An inventory reader reports its own error. A geometry
failure rejects that query because selecting a different shape would be wrong.
A failed shared-state rollback blocks further restores. Dependency activation
remains transactional; reporting an error is not permission to continue from
half-installed gameplay state.

## Startup and restart stalls

1. Close the game. Create an empty `JKRuntime.StartupTrace.enabled` file beside
   the **active** `JKRuntime.dll` (Workshop or local installation).
2. Launch, load a map and allow at least 120 gameplay draws. Repeat a same-map
   restart separately. Record which attempt was cold and which settings were enabled.
3. Read/copy `JKRuntime.Startup.txt` beside the same DLL. It retains the latest
   six captures. World exit or another attempt can finish a shorter capture too.
4. Remove the enable marker and restart the game to disable tracing and its
   additional native dispatch hooks. Closing the diagnostics page does not disable it.

This is separate from the page's callback-trace switch. Startup tracing retains
preparation plus the first 120 **gameplay** draws; a long intro does not consume
that gameplay window. Capture starts before native player/pause-menu construction.
Named module preparation/activation substages, update/draw/frame-gap timings,
GC counts and slow entity/component callbacks help locate the actual cost.

Add stable module-prefixed labels with `RuntimeApi.MeasureStartup("example.asset-read")`.
It returns a value-type scope and does no clock read/allocation while disabled.
Use it on the game thread with `using`; do not retain/copy an active scope across
attempts. Formatting/type-name resolution and trace writes run on a bounded worker.

Check for `module.world-ready:<id>`, `module.prepare-attempt:<id>` and your own
labels before `runtime.gameplay-handoff`. If preparation appears at activation,
inspect the early-preparation hook status in the diagnostics report: a loader or
Harmony conflict can force the synchronous fallback. Also inspect first-update
and delayed-callback work: correct preparation timings do not excuse work moved
into an unmeasured getter or first tick.

Nested timings overlap, so do not sum them as independent cost. Draw includes
presentation waiting; a large frame gap is not automatically physics work.
First-use JIT/hook installation can affect cold startup. Entity/component records
below 0.5 ms are omitted, and capture storage is capped at 8192 entries; inspect
dropped-record counts before interpreting an incomplete trace. Compare repeated
attempts and total loading time. Do not reset the native clock to conceal a stall.

## Specialized probes and report handling

### Unexpected run modifier attribution

`Flag sources` records successful native handler registrations. It does not show
which controls the player used. Enabled Save States and Ball King, for example,
can register handlers at level start without loading a position or morphing.

For detailed evidence, create `JKRuntime.RunModifiersTrace.enabled` beside the
active Runtime DLL before launching the game. Each launch creates a separate
`JKRuntime.RunModifiersTrace.<UTC timestamp>.txt` there. The trace includes early
registration/removal call stacks, behaviour types, native/tracked counts, the
attempt key, prior history and the first reason for each unknown-evidence kind.
Disk writes use a bounded background worker; the first 4096 events are retained
with an explicit truncation marker. Individual events are capped at 8192 characters.
Remove the enable marker and restart to disable it. No execution hooks or per-frame
stacks are added. The extra stack capture is for diagnosis, not timing benchmarks.

Start a new attempt for a fresh test. A short start/restart can reveal startup
attribution; completing the run is only necessary to investigate a later change.
Retain the trace and `Content/Saves/JKRuntime.RunModifiers.xml` from the game folder.
The explicit Runtime diagnostics report also includes `runModifierUnknownReasons`.
It also exposes `runModifierInheritedPeak` and `runModifierInheritedSources` for
an observed reset; the trace records this as `native-reset`. Inherited names
appear in results alongside new registrations, with duplicate names combined.
Reasons distinguish an initial flag without matching history, a higher saved peak,
an unobserved native increase and disagreement between native/tracked counts.
An old `Unknown=true` record cannot reconstruct its original cause retroactively.
Native new-save initialization can retain the current modifier count, so an initial
flag is not by itself proof of a separate mod or of previous feature use. The trace
does not change that flag and cannot attribute arbitrary unobserved memory writes.

The repository contains temporary restart/menu probes under
`mods/jk-runtime/tools/restart-probe/`. Use their README for installation and
removal; they are not part of the standalone SDK or normal release. Keep Runtime's
startup trace disabled while using the restart probe as instructed there.

An explicit diagnostics export can enumerate assemblies/patches, hash files and
write output on demand. Do not call it automatically at startup or every frame.
Reports are local and may contain file paths, installed mod identities and error
details. Review before sharing. Preserve original captures, exact DLL versions,
map/settings, reproduction steps and whether the evidence came from an isolated
fixture or a live game. The diagnostics page, journal and startup trace have
different scopes; none is proof of universal compatibility.


### Native animation audit

Diagnostic mode also observes visible looping props, scrolling layers and weather
animation state. Each report includes update/draw counts, total supplied delta,
minimum/maximum update delta, visible index changes and state changes outside an
observed update or during drawing. It reads private fields without modifying them;
objects and descriptors are bounded to 512 per capture. This is diagnostic
instrumentation, not an animation-speed adjustment. Actual effect identity and a
same-screen comparison still matter when interpreting an apparent speed change.

### Map-switch assets

Reproduce vanilla to Workshop, Workshop to vanilla or Workshop to Workshop, then
exit normally. The report lists source/destination paths and title together with
the inclusive SetContent time. LoadAssets is split into native content groups,
concrete texture/audio/font/effect readers and audio-thread unload synchronization.
Nested times overlap. Shared generic Load<T> bodies are not patched.

The native menu runs SetContent before starting its Loading node. A frozen
confirmation screen may therefore already represent asset loading, not a delay
before loading begins. A large `subframe-input.menu` sample includes any synchronous
load it invoked; inspect the nested stages before attributing it to input polling.

`tools/TextureLoadProbe.cs` is a standalone read-only texture-directory probe.
Compile against the installed MonoGame with the .NET Framework compiler and
System.Windows.Forms; run beside the Runtime build dependencies, passing map roots.
It loads XNB textures into a hidden-window graphics device without starting the
game or loading mods. Its totals exclude non-texture LoadAssets work, and OS file
cache state is uncontrolled; it is not an end-to-end map-switch A/B test.

## Movement evidence capture

Prepared `MovementObserver` subscriptions can explicitly enable a shared 256-event
ring and export a detached trace to a new file. Runtime diagnostics report stage
coverage without enabling capture. See [interop tracing](interop.md#trace-export-and-diagnostics)
for limits, ownership and the compiled example.
