# Adapted Quest enemy assets

The first enemy uses `FSlaveCrawlerGlue` from Quest build 24080145, inspected on
2026-10-26. Complete receipts under `build/_work/metroidvania/content-imports`
record source bundle hashes, clip identities and output hashes. Nexile owns the source assets; see the
[asset credits](../THIRD_PARTY_NOTICES.md).

## Rebuild the atlas and collision table

Use the installed games, Python 3.10 or newer and Windows' .NET Framework compiler:

```powershell
python -m pip install -r mods/metroidvania/build-support/requirements-art.txt
mods/metroidvania/build-support/import-jkq.ps1 -Python python
scripts/check-mods.ps1 -Mod metroidvania -Integration
```

Select prefabs, brains, state fields and adaptations in `content/jkq-import.json`.
The importer follows the selected brain's attack table, collider references and
sound GUIDs. It doesn't maintain a per-enemy clip or sound list in C#.
See the [import guide](import-pipeline.md) for supported behavior and diagnostics.

The Python importer resolves the selected prefab's skeleton, bind poses, skin
weights, material albedo maps and generic transform animation bindings. It samples
streamed polynomial curves, dense curves and constant channels. Missing paths,
unsupported animation bindings, invalid weights and non-finite poses stop import.
MonoGame renders the sampled geometry offline. Static mesh attachments use the
same transform hierarchy. No Unity editor is required.

The current crawler resolves these clips from its source graph:

| Clip | Use |
| --- | --- |
| `FSlave_crawler_idle` | Stationary pose loop |
| `FSlave_crawler_walk` | Walking loop |
| `FSlave_crawler_atk_lunge` | Windup, attack and recovery |
| `FSlave_crawler_poise` | Hit/parry reaction |
| `FSlave_crawler_stagger` | Collapse on defeat, followed by a local fade |

The result is 219 poses at 15 fps in one 1280 by 1120 atlas. Each 80-pixel cell
shares a pivot. The source's positive X faces right in gameplay; the opposite
view mirrors both the artwork and collision boxes around the same pivot.

`assets/imported/enemies/fslave-crawler.bin` contains the matching 15 fps
hurt/attack rectangles and root displacement. Format `MVE2` carries variable clip
and collider counts; each attack has its own timeline. Native combat still
advances at 60 Hz. Clip samples change every
four ticks, matching the displayed pose; root displacement interpolates between
those samples. No mesh or bone data is included in the playable package.

The source attack activates at 1.7 seconds and ends with its 4.267-second clip.
Its actual collider collapses outside the contact window, and both attack boxes
share one consumed-hit flag. The baked forward displacement is 16 pixels. Native
terrain checks can stop that displacement without moving the visual pivot away
from the actor.

Quest's fixed timestep is 1/30 second. The source sound markers at frames 8, 46
and 107 become combat ticks 16, 92 and 214. These values are source-derived,
not runtime constants. The attack and reaction events are under
`event:/Game/SFX/Enemies/Medium/Famished_FSlaves/Crawler/`: `VO_attack`,
`PJC_Attack_1`, `PJC__Movement_rr` and `VO_pain`. They are rendered into short WAV
files by the audio importer and reuse prepared native voices. Its walk loop has
an additional assigned event; the complete pack currently contains five distinct
crawler cues, including that loop marker.

The adaptation retains the source's 200 health. Its base attack damage is 32,
scaled for this prototype's player stats; source damage scaling, loot, factions,
generic flying-death behavior and full Quest navigation aren't included. Defeat
plays the first second of the stagger clip, holds the collapsed pose and fades
over the next half second.

## Ownership and cost

Loading prepares the training type and the types requested by the map. Each
type shares one immutable definition/table and GPU atlas across its actors.
Resources belong to Runtime world scopes. They survive attempt restarts and are released when mode is disabled for a new
attempt or the world ends. Gameplay only selects a frame, samples a few numbers
and queries native blocking terrain. Drawing doesn't sample animation curves,
skin vertices, read files or rebuild textures.

The build history lives under `build/_work/metroidvania/content-imports`. Keep two
completed bakes and the newest source receipt for each distinct source bundle
set. Incomplete work remains available for diagnosis. The wrapper removes staged
game libraries and its compiled authoring drivers after each run.
