# Third-party assets

The equipment icons, models and textures adapted by this mod come from
**JUMP KING QUEST**, by **Nexile**. The initial set uses Side Sword,
Bastard Sword, Red Crown Spear, Buckler, Archer torso, Iron Guard torso and
the Hotdog icon. These assets remain Nexile's artwork.

The Famished Crawler uses Quest's `FSlaveCrawlerGlue` model, materials and
`FSlave_crawler_*` animation clips, adapted into sprites and collision tables.
Its assigned voice, attack, movement and pain events also come from Quest.
The selected prefab, clips and bundle hashes are recorded in
the complete import receipt under `build/_work/metroidvania/content-imports`
in the source workspace. The maintained selections are in `content/jkq-import.json`.

Weapon, unarmed, impact and shield sounds also come from **JUMP KING QUEST**.
The authoring importer renders selected one-shot FMOD events into PCM WAV files.
It uses Quest's installed FMOD libraries only during authoring; no FMOD library,
bank, Unity runtime or Quest executable is included in the playable package.
The event paths and source bank hashes are recorded in
`assets/imported/audio/source-receipt.json` in the source workspace.

The player remains the original **Jump King**. Armor uses the original game's
Tunic silhouettes to fit the source material to its regular poses. The mod loads
the player's native sprite from the installed game; it doesn't package a
replacement character.

The authoring importer uses **UnityPy** by K0lb3 under the MIT license and
**TypeTreeGeneratorAPI** by K0lb3 under the MIT license. These tools are authoring
dependencies and are not included in the playable mod.
The enemy importer also uses NumPy for offline matrix and skinning calculations;
NumPy isn't included in the playable mod.

- https://github.com/K0lb3/UnityPy
- https://github.com/K0lb3/TypeTreeGenerator
