# Import Quest content through one manifest

The maintained source is `content/jkq-import.json`. It selects Quest assets,
assigns stable mod IDs and declares the adaptations needed for native Jump King.
`assets/imported` is compiled output. Don't edit its catalogue, atlases or tables
to change a weapon or enemy.

## Build the pack

With both games installed, Python 3.10+ and the authoring dependencies available:

```powershell
python -m pip install -r mods/metroidvania/build-support/requirements-art.txt
mods/metroidvania/build-support/import-jkq.ps1 -Python python
scripts/check-mods.ps1 -Mod metroidvania
mods/metroidvania/build.ps1 -Graphics
```

The former `build-art`, `build-enemies` and `import-audio` entry points forward to
this complete import. They don't publish partially updated packs.

The pipeline validates recipes, resolves source graphs, bakes enemies and native
King equipment, renders selected FMOD events to WAV, then checks every declared
output, fully decodes each PNG and records its SHA-256. Checking PNG chunks alone
isn't enough: an unfinished compressed stream can still have valid chunk CRCs.
Promotion starts only after those stages pass.
It copies the catalogue last and restores replaced files if promotion fails.
A source build rejects a changed manifest, changed importer tools or altered
compiled asset until the pack is rebuilt. No generated C# becomes a second
source of gameplay rules.

Recipe and tool fingerprints normalize UTF-8 BOMs and Git line endings. A fresh
checkout or source export can use its compiled pack without importing Quest again.

Histories and reports live under `build/_work/metroidvania/content-imports`.
Keep two completed runs and the newest receipt for each distinct source bundle
set. Incomplete runs retain their failure report. Staged game libraries and
compiled authoring drivers are removed after each run. Receipts identify Niotid,
the manifest, source bundles, clips and FMOD events. The playable DLL contains
only compiled data, sprites and PCM sounds, without Unity or FMOD libraries.

## Add an enemy using an existing behavior profile

List source prefab/brain pairs without rendering or modifying the game:

```powershell
python mods/metroidvania/build-support/list_quest_content.py `
  --quest-dir "C:/Program Files (x86)/Steam/steamapps/common/JUMP KING QUEST" `
  --output build/_work/metroidvania/source-enemies.json
```

The list is discovery metadata, not a promise that every listed configuration is
supported. Pick the intended prefab and brain, add an enemy recipe, then import.
The existing recipe shows all required fields:

- `id` is the stable map/content ID; `name` is its display name.
- `source_prefab` selects a unique prefab suffix or exact source path.
  `source_brain` selects its intended `EnemyBrainConfig` variant.
- `behavior` chooses a reviewed controller. Currently this is `grounded-melee`.
- `states` selects source fields for idle, walk, hurt and collapse. The importer
  follows the brain's decision tables and animation references, so the recipe
  doesn't repeat clip names or attack sound paths.
- `render` sets the pixel scale, shared pivot, frame rate and atlas layout.
- `adaptation` defines the movement body and deliberate balance/control changes.
  Keep the reason in `notes`.

The importer reads source health, weighted attack configurations, `activeStart`
events, root movement, combat collider references and assigned sound GUIDs.
Attack markers use Quest's inspected fixed timestep and the mod's 60 Hz clock.
FMOD resolves those GUIDs to events during authoring. Shared events are captured
once even when several enemies reference them. Static attachments and skinned
meshes are baked through the same transform hierarchy.

One enemy can contain several attacks and up to eight hurt/attack boxes. The
shared controller chooses an attack and samples its own timeline. Adding another
definition using those mechanics doesn't require an enemy enum, another C# AI
branch, a new sound enum or a new package-resource list.

## Equipment and appearance

Each item recipe declares its stable saved ID, source prefab or icon, equipment
slot, stats, sound roles and atlas recipe. A weapon can override `attacks.light`
with `start`/`active`, and `attacks.heavy` with those fields plus
`recovery_extra`/`multiplier`. Otherwise it uses the manifest's `player_attack`
defaults. Consumables declare `heal`; their effect isn't inferred from a name.

`visual_id` identifies the item within its equipment slot. `art.index` identifies
the atlas cell. Keep existing visual IDs when reorganizing recipes or atlas
cells: recorded appearance uses those tokens. Existing item-save IDs and six-byte
appearance payloads keep their meaning in 0.4.0. The player still uses native
Jump King poses; imported armor follows the native Tunic masks.

The source catalogue is immutable to consumers. Combat resolves stats and action
parameters when equipment changes; draw selects the matching prepared art. New
mechanics such as a projectile weapon or a status consumable need a shared
behavior/effect handler before an item can declare them.

## Supported boundary

The current adapter handles generic transform clips, albedo-based baking and
grounded melee attacks with horizontal root movement. It replaces Quest's full
AI/navigation with the declared controller. It doesn't import factions, loot,
boss phases, vertical locomotion, arbitrary decision nodes, status effects or
Quest's generic flying-death behavior. Conditional/unknown decisions,
unsupported attack mechanics, multi-impact or multi-hit behavior, poison/terror
impacts, unknown animation events, layered/retimed clips and unsupported
animation bindings fail import instead of becoming
silently incomplete enemies. Richer materials and exact impact behavior need
their own reviewed adapters.

Budgets are explicit: 32 enemy definitions, eight attacks per enemy, eight boxes
per collision role, 64 selected cues, 64 markers per timeline and four markers
at the same tick. Combat retains 16 living enemies and a 256-event buffer. New
content exceeding a budget needs a pack split or an intentional measured change.
Idle, walk, hurt and death markers use the same cue catalogue as attacks.
Sound markers retain their stoppable flag in source receipts/catalogue; the
current shared single-voice presenter doesn't provide per-actor cancellation.

Frame tables are immutable and atlases/audio are prepared during Runtime loading
phases. Map placement prepares only requested enemy types plus the default
training type. Attempts reuse those world resources. No source graph traversal,
animation decoding, skinning, texture construction or WAV decoding occurs in
combat Update/Draw. Busy sound cues reuse their prepared voice or are dropped.
