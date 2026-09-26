# Contributing

Public main contains one current snapshot commit and is replaced on updates.
Use a fresh clone or download for a new snapshot; preserve local contributions
separately before replacing an older checkout. Release history is in the mod
changelogs.

Keep each mod's code, assets, build script and documentation in `mods/<id>/`.
Read its README and build instructions before editing. Use English for code
comments, documentation and tools; preserve deliberate localization.

Run `scripts/check-mods.ps1 -Mod <id>` for the affected mod. Add integration
checks when changing interactions between mods, installation or package contents.
See [testing](docs/testing.md) for the available tiers.

Preserve public API IDs and saved-data keys. Document dependencies and ship all
other required files with the package. Add regression tests for behavior and data
safety rather than incidental file names or source spelling.

Documentation should describe the current behavior. Keep release history in
changelogs, and update examples when their APIs change. Preserve asset provenance
and third-party license notices.

Follow the [mod documentation layout](mods/README.md#documentation-layout):
README for normal use, `docs/index.md` for technical guides, one primary guide
per topic and one current changelog per mod. Keep licenses/notices at the root
and tool/example READMEs beside their material. Run
`python scripts/check-mod-docs.py` after moving or adding guides, and validate
detached SDK/authoring-kit links when changing packaging.
