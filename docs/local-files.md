# Local files

Build scripts write packages and internal test outputs under `build/<mod-id>/`.
Only the `UPLOAD_TO_WORKSHOP` directory is a distributable mod package.
Other outputs may contain game assemblies copied from the local installation.

Keep temporary builds, logs, export checks and research under `build/_work/`.
Worldsmith Extension puts candidates and tests there; its ready-to-run package
stays in `build/worldsmith-extension/PORTABLE` and its latest archive in `RELEASE`.
Mod `_INTERNAL` directories stay beside their packages for dependent builds.

Runtime, Mega Mapping, Wardrobe+, Multiplayer and Worldsmith Extension builds
trim named generated histories before building and on exit, including failures.
Mod checks also clean when build results are cached. Generated test and candidate
categories keep three directories. Worldsmith limits native tests, candidates and
dated archives separately. Mod checks keep three directories under `build/_work/exports/`; use
that folder only for disposable export checks.

Completed Runtime validation, Mapping focused tests and Worldsmith native tests
can be trimmed immediately. Unfinished or unmarked directories less than an hour
old stay until a later run. Active processes and explicitly preserved paths are
protected. Preview with `mods/jk-runtime/build-support/clean-build.ps1`; add
`-Apply` to remove the listed history. Linked paths are rejected.

Subframe Charge removes its padded rotation-test files as soon as the test ends.
Cleanup leaves current packages, SDKs, installed game data and manual projects
alone. Other scratch directories need inspection.

Installers keep two complete rollback copies per component. Incomplete recovery
work and active processes are protected. Mapping keeps two package and two kit
backups; Worldsmith keeps two previous portable and two previous release folders.
Before removing an older backup, cleanup preserves settings, saves, source files
and editable assets in `build/_work/preserved-history/<component>.zip`. Identical
payloads share a SHA-256 object; manifests record their original paths. Archives
are verified before deletion and omit superseded compiled payloads.

To restore preserved data into a new directory under `build/`:

```powershell
python mods/jk-runtime/build-support/history-maintenance.py --restore build/_work/preserved-history/Runtime.zip --destination build/_work/restored-history
```

Restoration refuses to overwrite different existing files. Installer tests keep
their backup history inside their own fixture.

Full cleanup compresses inactive logs and known CLR trace files under
`build/_work/` after 24 hours. Gzip contents are verified before removing an
original, and reused names preserve different contents separately. Regular mod
checks run this maintenance on exit, including failed checks. Unique source
changes and diagnostics remain available as compressed history.

Build outputs, downloaded game files, local settings and environment files are
not source files and should stay out of commits.
