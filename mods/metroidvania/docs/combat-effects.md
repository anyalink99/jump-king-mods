# Attack audio and falls

The current content pack embeds 16 mono 16-bit PCM WAV clips at 48 kHz. Loading
prepares shared combat cues and the requested enemy types through Runtime's
`PreparedSound`; attempts reuse the same voices.
Disabled mode prepares none. Turning mode off releases them before the next
attempt, and leaving the world releases all prepared resources.

The active attack frame emits a swing even when it misses. Side Sword and Bastard
Sword have separate light/heavy cues; Crown Spear uses Quest's shared spear
attack cue. Unarmed attacks use the punch cue. The Quest crawler has its own
voice, attack, walk, movement and pain cues. Successful hits select
slash, stab or blunt impacts; shield blocks and parries have distinct cues.

A cue plays once per rendered update, with one voice per cue. If that voice is
still playing, another request is dropped. This bounds overlap and avoids
decoding or replacing a busy voice during combat. Native SFX/master preferences
apply. One pause subscription controls the whole set, and one restore
subscription stops it. Attempt teardown stops playback; world teardown disposes
the sounds. No FMOD code, file reads or audio decoding run on attack or draw.

## Reimport the source sounds

With Quest installed, run from the repository root:

```powershell
mods/metroidvania/build-support/import-jkq.ps1
scripts/check-mods.ps1 -Mod metroidvania -Integration
```

The importer uses the installed matching `FMODUnity.dll` and x64 `fmodstudio.dll`
with the non-real-time WAV writer. It loads the Master, Weapons, Player, Enemies
and Other banks, uses a fixed random seed and captures selected one-shot events.
It folds stereo into mono and trims silence around each sound. The tool rejects
silent or looping events and captures longer than eight seconds.

Equipment cue roles come from the manifest. Enemy cues and marker times come
from the selected source actions, with sound GUIDs resolved during authoring.
`assets/imported/audio/source-receipt.json` records source bank hashes, event paths
and output hashes. Raw captures and the complete pack receipt stay under
`build/_work/metroidvania/content-imports`. The history retains two completed
imports and the latest receipt for each source bundle set; incomplete work stays
available for diagnosis. Staged FMOD libraries and authoring executables are
removed after each run. The playable package includes WAV resources without
those authoring dependencies. See the [complete import workflow](import-pipeline.md).

## Fall damage

Quest's installed Fall Damage Settings asset was inspected on 2026-10-26
(Steam build 24080145). It sets `fallDamage` to 0.08. Quest applies this percentage
to maximum health and rounds up. The mod uses that rule without Quest's curse
variants.

The triggering condition is adapted to native Jump King: landing on solid support
at `PlayerValues.MAX_FALL`. This matches the native heavy-landing threshold and
keeps normal jumps to the same height safe. Damage bypasses shield, armor and
enemy-hit invulnerability. Zero health enters the existing combat recovery.
The observer doesn't scan height, rerun collision or simulate another body.

Each confirmed landing has a monotonic identity. The combat snapshot preserves
the last consumed identity. Native teleport flags, large position discontinuities
and changed Runtime restore epochs suppress false landings. Pausing after a
confirmed landing preserves it until combat resumes; pausing halfway through a
collision observation cannot create a new one.
