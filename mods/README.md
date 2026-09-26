# Mods

Install [JK Runtime](jk-runtime/README.md) alongside the mods you use. Each mod's
README lists its controls and settings. Packages built from this checkout require
Runtime **1.30+**: the package builder records the API version of the SDK used to
build it, even when a feature uses older APIs. An older published binary may have
a lower requirement.

## Playing

| Mod | What it adds |
| --- | --- |
| [Subframe Charge](subframe-charge/README.md) | Jump timing between game updates, buffered input and optional 240 Hz presentation |
| [Casual Jumping](casual-jumping/README.md) | Air control and an optional automatic-jump mode |
| [Ball King](morph-ball/README.md) | Rolling, climbing and movement along walls and ceilings |
| [More Items](more-items/README.md) | Inventory, Rewinder, Jetpack, Hammer and merchant offers |
| [Smooth Camera](smooth-camera/README.md) | Continuous camera movement across screen boundaries |
| [Overlay+](overlay-plus/README.md) | In-game HUD editor, timers, splits and local records |
| [Replays](replays/README.md) | Recording, playback and racing ghosts |
| [Run Verifier](run-verifier/README.md) | Completion history, results-screen seals and optional online reports |
| [Wardrobe+](wardrobe-plus/README.md) | Mixed skins, outfit presets and sprite fitting |

## Map authoring and experiments

| Mod | What it adds |
| --- | --- |
| [Mega Gameplay Expansion](mega-gameplay-expansion/README.md) | Warp Jump, No Walk Off, Air Dash and an experimental gimmick browser |
| [Mega Mapping Expansion](mega-mapping-expansion/README.md) | Scenery, animation, lighting, reflections, narrative and map policies |
| [Stereo Madness controller](stereo-madness/README.md) | Cube/ship movement bundled with the matching map; not a standalone gameplay mod |
| [Screen Solver](screen-solver/README.md) | Experimental route search using native physics; supported configurations are listed in its guide |
| [JK Runtime SDK](jk-runtime/docs/getting-started.md) | Module loading, UI, input and shared gameplay services for mod authors |

## Documentation layout

Each mod's `README.md` is the entry point for installation, controls and normal
use. Mods with separate technical guides link to `docs/index.md`; small mods can
keep all necessary instructions in their README without an empty handbook.

Keep technical guides under `docs/`, using lowercase names separated by hyphens.
Give each topic one primary guide; other pages should summarize and link to it.
API contracts, architecture, validation and release procedures belong there.
Keep `CHANGELOG.md` for current release history, `WORKSHOP.md` for store copy,
and licenses, credits and third-party notices at the mod root. Earlier separate
histories can be retained under `docs/history/`, linked from the main changelog.
Tool/example/asset READMEs stay beside the material they explain. Generated
reference artifacts retain their generator-owned location and are not edited as
source. SDKs and authoring kits preserve guide paths; their root README explains
the exported package rather than assuming the full repository is present.

From the repository root, run `python scripts/check-mod-docs.py` to check all
mod entry points, guide placement and local links. Use `--mod jk-runtime` to
limit it to one mod. Packaging-specific checks also validate the detached SDK
and authoring kit. New guides must be reachable from their mod's documentation
index. Keep instructions in English and new changes in the main changelog.

## Build and install

Run these from the repository root with Jump King installed:

```powershell
.\scripts\check-mods.ps1 -Mod subframe-charge
.\mods\subframe-charge\install.ps1
```

The check builds the selected mod and Runtime, then runs their focused tests.
Packages go to `build/<id>/UPLOAD_TO_WORKSHOP/`; use the mod's `build.ps1` or the
check command to create them. Intermediate DLLs under `_INTERNAL` are not release
packages. See [testing](../docs/testing.md) for integration checks.

Close Jump King before installing. Install scripts use the coordinated Runtime
installer, preserve settings and keep rollback backups. They update an existing
Workshop copy when one is present, otherwise they use `Content/JKMods`.
For manual installation, copy the package contents and install Runtime separately.
Keep only one active copy of each mod.

Jetpack and Hammer King are part of More Items. Their old standalone DLLs should
be removed; the More Items installer handles that migration. The retained
[Hammer King folder](hammer-king/README.md) forwards old build commands to More
Items. Old `UIApiPlus.dll` consumers must be rebuilt against JK Runtime.
