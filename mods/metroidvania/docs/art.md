# Adapt Quest artwork to Jump King

The player body comes from the installed Jump King. Menus use its native idle
sprite and the world keeps its normal player renderer. Do not substitute a Quest
character or a hand-drawn replacement king.

The initial artwork comes from the installed Quest build 24080145, inspected on
2026-10-26. The complete import receipt identifies source bundles by SHA-256.
`content/jkq-import.json` currently selects six equipment prefabs and the Hotdog
icon. It also declares atlas cells, visual tokens and render settings. The importer decodes meshes,
transforms, material albedo maps and item icons without running Quest assemblies.
Missing selected items or renderable meshes stop the import.

## Rebuild

Use Python 3.10 or newer, the installed games and Windows' .NET Framework compiler.
Install the authoring dependencies into a virtual environment, then run:

```powershell
python -m pip install -r mods/metroidvania/build-support/requirements-art.txt
mods/metroidvania/build-support/import-jkq.ps1 -Python python
```

The importer writes to a named history under `build/_work/metroidvania/content-imports`.
The history retains two recent imports and the newest copy of every distinct
source receipt. These decoded assets contain no game saves or settings. Earlier
research extracts are separate. See the [complete import workflow](import-pipeline.md).

`BakeQuestModels.cs` renders selected meshes with their albedo maps through
MonoGame. It doesn't need the Unity editor. `BuildQuestSprites.cs` fits the renders
to the game's pixel grid, adjusts contrast and produces three atlases:

| Atlas | Contents |
| --- | --- |
| `quest-icons.png` | Seven 32-pixel inventory icons |
| `quest-weapons.png` | Three weapons and the Buckler, with a shared grip pivot |
| `quest-armor.png` | Archer and Iron Guard material fitted to 13 native poses each |

Armor follows the original Tunic's alpha masks and shading, preserving the native
King outside that clothing layer and keeping its pose silhouettes. It adapts the
material to those silhouettes. Weapons are baked
from their full models; cropped inventory icons are not used as held weapons.

## Runtime cost

The package embeds the three small atlases. The first active world or open
character page acquires them on the game thread; consumers share them through
reference-counted leases. The last lease disposes the GPU textures. Rendering
uses textured sprites. No model loading, mesh skinning, texture readback or 3D
rendering runs in gameplay.

The visual bridge carries six logical bytes: weapon, shield, action, action age,
armor and native pose. An unchanged pose reuses the serialized state. A viewer
uses the same atlases locally. Keep mesh and texture data out of state packets.

Read the [artwork credits](../THIRD_PARTY_NOTICES.md) before maintaining this set.
