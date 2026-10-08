# Jump King Mods

Gameplay, camera, replay and mapping mods for Jump King, with a shared runtime
and tools for building and testing them.

<p>
  <img src="mods/jk-runtime/workshop-preview.png" width="160" alt="JK Runtime">
  <img src="mods/subframe-charge/workshop-preview.png" width="160" alt="Subframe Charge">
  <img src="mods/replays/workshop-preview.png" width="160" alt="Replays">
  <img src="mods/wardrobe-plus/workshop-preview.png" width="160" alt="Wardrobe Plus">
</p>

See the [mod catalog](mods/README.md) for features, dependencies and installation
instructions. Install JK Runtime when a mod lists it as a dependency.

## Build

Use Windows with Jump King installed, Windows PowerShell, the .NET Framework
C# compiler and Python 3.10 or later. Optional asset tools use the packages in
`requirements-dev.txt`.
Use a short checkout path: some .NET Framework tests inherit Windows' 260-character
path limit.

```powershell
python -m pip install -r requirements-dev.txt
.\scripts\check-mods.ps1 -Mod subframe-charge
```

The default game path is Steam's `steamapps/common/Jump King` under
`C:\Program Files (x86)`. Pass `-GameDir` for another installation.
Packages go to `build/<mod-id>/UPLOAD_TO_WORKSHOP`. Building doesn't install them.

## Development

- [Contributing](CONTRIBUTING.md)
- [Checks and test tiers](docs/testing.md)
- [JK Runtime SDK](mods/jk-runtime/docs/index.md)
- [Mapping authoring guide](mods/mega-mapping-expansion/docs/authoring.md)
- [Credits and third-party notices](CREDITS.md)

## Worldsmith

[Worldsmith Extension](worldsmith-extension/README.md) adds project folder opening,
large-map support, in-game testing and Workshop publishing to Jump King Worldsmith.
It also fixes startup delays and several asset-loading and saving issues.

[Download Worldsmith Extension](https://github.com/anyalink99/jump-king-mods/releases/tag/worldsmith-extension-v0.1.0-preview.15)
and follow the [installation instructions](worldsmith-extension/README.md#install).
