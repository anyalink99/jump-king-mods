# Local files

Build scripts write packages and internal test outputs under `build/<mod-id>/`.
Only the `UPLOAD_TO_WORKSHOP` directory is a distributable mod package.
Other outputs may contain game assemblies copied from the local installation.

Installers can retain rollback copies and user settings. Keep those until you
have verified the installed mod. Settings and logs belong to the installed mod
or the game's data directories; consult its README for exact paths.

Build outputs, downloaded game files, local settings and environment files are
not source files and should stay out of commits.
