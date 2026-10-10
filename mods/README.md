# Mods

Install [JK Runtime](jk-runtime/README.md) with the mods you want to use, then
open each mod's README for its controls and settings. Packages built from this
checkout require Runtime **2.0+** because the builder records the SDK version
used at build time. Older published binaries may still support an older Runtime.
Runtime 2.0 also loads existing API 1.0–1.44 packages. Check a mod's README for
its published release's minimum rather than assuming every old package needs 2.0.

For a manual installation, close Jump King, install Runtime and copy each mod's
release package into `Content/JKMods/`. Keep only one active copy, including
Workshop copies. Open **Mods** in the main or pause menu for settings.

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
| [Multiplayer Expansion](multiplayer-expansion/README.md) | Ghost or solid players, head stacking, kicks and optional shared-world state; requires original Multiplayer |
| [Run Verifier](run-verifier/README.md) | Completion history, results-screen seals and optional online reports |
| [Wardrobe+](wardrobe-plus/README.md) | Mixed skins, outfit presets and sprite fitting |
| [Prism](prism/README.md) | Music-driven visual themes, cosmic scenery and glowing platforms |

## Map and mod authors

| Mod | What it adds |
| --- | --- |
| [Mega Gameplay Expansion](mega-gameplay-expansion/README.md) | Warp Jump, No Walk Off and Air Dash, with map-controlled activation |
| [Mega Mapping Expansion](mega-mapping-expansion/README.md) | Scenery, animation, lighting, reflections, narrative and map policies |
| [Stereo Madness controller](stereo-madness/README.md) | Cube/ship movement bundled with the matching map; not a standalone gameplay mod |
| [Screen Solver](screen-solver/README.md) | Experimental route search using native physics; supported configurations are listed in its guide |
| [JK Runtime SDK](jk-runtime/docs/getting-started.md) | Module loading, UI, input and shared gameplay services for mod authors |

Start a map scene with Mapping's [first example](mega-mapping-expansion/docs/authoring.md#first-scene).
Start a mod with Runtime's [working SDK template](jk-runtime/docs/getting-started.md).
For multiplayer map tests, Expansion's [two-client Debug mode](multiplayer-expansion/docs/debug-testing.md)
runs two connected games in one window. Runtime's
[Mod Inspector](jk-runtime/docs/mod-inspector.md) browses loaded blocks and state.

## Documentation layout

Start with a mod's `README.md` for installation, controls and everyday use. If a
mod has deeper technical guides, the README links its `docs/index.md`. A small
mod can keep everything in one README instead of creating an empty handbook.

Put technical guides under `docs/` and use lowercase hyphenated filenames. Give
each topic one full guide; repeat short explanations wherever they help the
reader find the answer, then link to the full guide. API
contracts, architecture, validation and release steps belong here.

Use `CHANGELOG.md` for release history and `WORKSHOP.md` for store text. Keep
licenses, credits and third-party notices at the mod root. Old detailed histories
may live under `docs/history/` when the main changelog links them. Tool, example
and asset READMEs stay next to what they explain. Generated references stay with
their generator and are not hand-edited. An exported SDK or authoring kit must
explain itself without assuming the reader has this full repository.

Run `python scripts/check-mod-docs.py` from the repository root to check entry
points, guide placement and local links. Add `--mod jk-runtime` to check one mod.
Package checks also open the detached SDK and authoring kit. Link every new guide
from its mod's documentation index, write instructions in English and record new
releases in the main changelog.

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

Close Jump King before installing. The coordinated Runtime installer preserves
settings and keeps rollback backups. It updates an existing
Workshop copy when one is present, otherwise it uses `Content/JKMods`.
Prism's older installer still checks for Runtime 1.x; use its
[manual theme installation](prism/README.md#prepare-the-local-theme) with Runtime 2.0.
For manual installation, copy the package contents and install Runtime separately.
Keep only one active copy of each mod.

Jetpack and Hammer King are part of More Items. Their old standalone DLLs should
be removed; the More Items installer handles that migration. The retained
[Hammer King folder](hammer-king/README.md) forwards old build commands to More
Items. Old `UIApiPlus.dll` consumers must be rebuilt against JK Runtime.
