# Contributing

Public main contains one current snapshot commit and is replaced on updates.
Use a fresh clone or download for a new snapshot; keep local contributions
separately before replacing an older checkout. Release history is in the mod
changelogs.

Worldsmith Extension lives in `worldsmith-extension/`; follow its
[development guide](worldsmith-extension/docs/development.md) for builds and tests.
Keep each mod's code, assets, build script and documentation in `mods/<id>/`.
Read its README and build instructions before editing. Use English for code
comments, documentation and tools; keep deliberate localization.

Run `scripts/check-mods.ps1 -Mod <id>` for the affected mod. Add integration
checks when changing interactions between mods, installation or package contents.
See [testing](docs/testing.md) for the available tiers.

Keep public API IDs and saved-data keys. Document dependencies and ship all
other required files with the package. Add regression tests for behavior and data
safety rather than incidental file names or source spelling.

Write documentation for the person trying to use or change the mod. Put the goal
and prerequisites first, use plain English, split overloaded sentences and keep
commands exact. Edit sections by meaning instead of running blanket wording
replacements. Describe current behavior, keep release history in changelogs and
update examples when their APIs change. Preserve asset provenance and third-party
license notices.

Follow the [mod documentation layout](mods/README.md#documentation-layout):
README for normal use, `docs/index.md` for technical guides, one primary guide
per topic and one current changelog per mod. Keep licenses/notices at the root
and tool/example READMEs beside their material. Run
`python scripts/check-mod-docs.py` after moving or adding guides, and validate
detached SDK/authoring-kit links when changing packaging.
