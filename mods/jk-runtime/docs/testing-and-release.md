# Verification and release

Build success checks source/package contracts. It is not evidence of smooth
handoff, physical-device latency, correct rendering or every combination of mods.
Choose checks for the behavior changed and retain the evidence with that claim.

## Commands

Run repository commands from its root:

Requirements: Windows .NET Framework C# compiler, the installed Jump King game
assemblies and Python 3.10+ for the documentation verifier. The detached feature
template itself does not need Python.

```powershell
scripts/check-mods.ps1 -Mod jk-runtime
scripts/check-mods.ps1 -Mod jk-runtime -Integration
```

For a feature change, select that feature instead; its plan includes Runtime:

```powershell
scripts/check-mods.ps1 -Mod mega-gameplay-expansion -Integration
```

`mods/jk-runtime/build.ps1` builds Runtime, checks frozen public ABI and focused
tests, compiles/packages examples and exports the standalone SDK. It does not
install. Integration adds the planner's applicable native/Harmony/package checks;
inspect `scripts/check-mods.ps1 -Mod jk-runtime -List -Integration` for the exact
current plan. Do not infer that every optional probe ran from a generic success line.

The documentation verifier checks local links and heading fragments in both the
repository handbook and exported SDK, public API guide coverage, and standalone
example availability. It runs during the build. Compiled examples are the source
of truth for complete samples; fenced fragments are labelled as fragments.

## Acceptance matrix

| Change | Required evidence |
| --- | --- |
| Startup/loading | Cold load, same-map restart, cancelled intro, world switch; no enabled overrides; trace preparation and first gameplay frames |
| Settings | Validation failure, save failure/rollback, repeated changes, inactive immediate command path, active deferred path, cancellation, corrupt-file preservation |
| Lifetime/registration | Partial activation, reverse cleanup, duplicate/conflicting owners, release failure/retry, no previous-player references |
| State | Stale snapshots, validation before mutation, partial restore, rollback failure, input epoch behavior |
| Input | Keyboard/controller bindings, chords, loss/reconnect/neutral baseline, overflow/gaps; hardware timing separately |
| UI | Open/close/reopen, keyboard/controller/mouse, focus loss, drag cancellation, long text, no IO/discovery in hidden menu constructors |
| Geometry/simulation | No source mutation, exact coverage/version refusal, branch isolation, relevant native parity and dynamic-world invalidation |
| Compatibility | Frozen ABI, unchanged old consumer, actual affected provider/Harmony versions and load orders |
| Packaging | Detached SDK build, payload contents, declared minimum API, one active copy, settings preserved on installation |

For startup code, do not only time registration. Test the first 120 ticks/draws
with the feature disabled and with representative saved configuration. Assert
absence of scans, IO and foreign factory calls where none are needed; avoid
fragile wall-clock thresholds as the only regression guard. Measure named
substages, cold and warm attempts separately, and total loading cost. A faster
fixture is not a measured improvement to every in-game frame.

For the installed mod set, reuse an isolated staged game under `build/_work`
with copied `Content`, saves, Workshop packages and native dependencies:

```powershell
mods/jk-runtime/verify-full-mod-lifecycle.ps1 -StagedGame build/_work/rlab -RuntimeAssembly build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll
mods/subframe-charge/verify-full-mod-physics.ps1 -StagedGame build/_work/rlab -RuntimeAssembly build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll -KeepStagedPackages
```

The lifecycle runner uses native new-game confirmation, intro, same-map restart,
custom-map entry, return to the base game and Continue. It records the first 120
presented frames per attempt and checks activation, coordinates, menu bounds and
owned-resource counts. It mutes audio, supplies test input and blocks Steam stat
writes. Keep the user's installed game and saves outside this fixture. A short
staging path avoids Framework path limits in other mods' save files.

Add `-FailHarmonyLog` to inject a filesystem-limit failure before native Runtime
discovery. The runner enables Harmony debug output, checks that the broken writer
is paused after one write and confirms that the failure remains in diagnostics
while the full mod set completes the same transitions.

`-Attempts 1` isolates a first entry. `-Compilation off` and `-Compilation bytes`
are comparison controls for older Runtime assemblies: they suppress the previous
profile or also bypass its file image loader. Current Runtime has no profile.
Raw frame CSVs and `lifecycle-game.txt` stay in the staged directory; copy each
capture before repeating a run. Compare several fresh processes with the same
packages, input and diagnostic configuration. These checks exercise the chosen
set, not every Workshop mod or physical input device.

The physics runner's `-KeepStagedPackages` option retains the fixture's feature
DLLs so Runtime can be checked against exact installed package bytes. Without it,
the runner stages the current first-party physics packages from build output.

## Test inventory

`UiPageSessionTests` exercises both real hosts, matching Back policy, held-input
boundaries, terminal draw/update failures, explicit reentry, nested navigation,
partial opens and exhaustive/retryable cleanup. `PublishBuildTests.ps1` locks a
destination DLL to check that failed publication restores the previous SDK/DLL.

Each build validates in a new `_INTERNAL/validation/<id>` directory with explicit
game dependencies, including Steamworks.NET. Failed runs never replace the
Workshop DLL or exported SDK. Successful publication atomically replaces the DLL
and keeps previous DLL/SDK artifacts under that validation directory. Existing
logs, downloaded research and backups are not deleted. These directories can be
reviewed and archived separately; they are not Workshop payload.

Runtime's build executes suites for module graph/lifetime, preparation,
discovery, gameplay foundations, attribution, mouse/pointer behavior, simulation,
cleanup safety, geometry/mechanics, extensibility and charge ownership. The
`tests` directory contains their executable sources. `PreparationTests` covers
world/attempt scopes and failure paths; `LifecycleSafetyTests` covers commands,
threading and cleanup; `SimulationTests` covers isolated kernel behavior.
`DocumentationExamplesTests` executes the preparation/state examples, and
`test_docs.py` exercises broken links, anchors, SDK containment and missing API
guide routes. `build-support/DocumentationIndex.cs` reads assembly metadata without
instantiating providers; `build-support/check_docs.py` renders the SDK signature reference
and checks source/SDK links plus matching handbook/example copies.

`TextEntryTests` checks native Unicode window messages, handler cleanup, the
editing buffer, nested capture and held-key draining. Wardrobe's GPU fixture also
renders the shared text page with keyboard, controller and Cyrillic names.

Additional scripts include `verify-packages.ps1`,
`verify-text-compat.ps1`, `verify-manager-compat.ps1` and `verify-ui-graphics.ps1`. The graphics verifier uses
native fonts/devices in an isolated fixture; it is not a complete live playthrough.
Temporary probes under `tools` are development artifacts, not Workshop payload.
Consult each probe's README before installation and remove it after diagnosis.

## Release and rollback

1. Build and run the selected checks against the installed game.
2. Inspect the generated package manifest, minimum API and payload. A feature
   ships its generated discovery shell, not loose implementation/game/Runtime DLLs.
3. Close the game before replacing loaded binaries. Prefer its existing Workshop
   folder; avoid a second active local copy.
4. Back up replaced binaries and retain settings/data. An atomic settings write
   does not substitute for an installation or migration backup.
5. Verify destination hashes/version, then perform the relevant live acceptance
   checks. Retain startup traces or native parity evidence with its limitations.

The repository's coordinated installer is `mods/jk-runtime/install-release.ps1`.
Inspect its parameters and selected packages; it can retire superseded binaries.
Build scripts alone do not publish or install, and a documentation-only change
does not require replacing the installed Runtime DLL.
