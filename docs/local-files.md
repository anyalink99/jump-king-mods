# Local files

Build scripts write packages and internal test outputs under `build/<mod-id>/`.
Only the `UPLOAD_TO_WORKSHOP` directory is a distributable mod package.
Other outputs may contain game assemblies copied from the local installation.

Keep temporary builds, logs, export checks and research under `build/_work/`.
Worldsmith Extension puts candidates and tests there; its ready-to-run package
stays in `build/worldsmith-extension/PORTABLE` and its latest archive in `RELEASE`.
Mod `_INTERNAL` directories stay beside their packages for dependent builds.

Successful Runtime and Wardrobe+ builds keep the three newest validation,
example and test directories per category. Mod checks keep three directories
under `build/_work/exports/`; use that folder only for disposable export checks.
Directories less than an hour old are left alone until a later successful run.
Subframe Charge removes its padded rotation-test files as soon as the test ends.
This cleanup leaves packages, SDKs, install backups and real game logs alone.

Installers can keep rollback copies and user settings. Keep those until you
have verified the installed mod. Settings and logs belong to the installed mod
or the game's data directories; consult its README for exact paths.

Build outputs, downloaded game files, local settings and environment files are
not source files and should stay out of commits.
