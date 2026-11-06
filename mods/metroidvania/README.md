# Metroidvania

Metroidvania **0.4.0** is an offline combat and character prototype for Jump King.
It requires **JK Runtime 2.4.0 or newer**. It has its own character, inventory and
equipment screens and doesn't require More Items, Multiplayer Expansion or
Jump King Quest at runtime. Equipment artwork is adapted from Quest's models,
textures and icons. The character remains the native Jump King, including its
regular poses. The combat rules and balance are a separate prototype. The first
adapted Quest enemy is the Famished Crawler, with animated attacks and hitboxes.
Weapon swings, hits, blocks and parries use sounds adapted from Quest.

[Technical guides](docs/index.md) · [Asset credits](THIRD_PARTY_NOTICES.md)

## Start playing

Open **Mods > Metroidvania > Character**, switch to **Camp**, and turn **Mode** on.
Start or restart an attempt. The mode starts off so installing it doesn't change
an existing climb. Changes to Mode apply to the next attempt. Enabled attempts
are marked as modified through Runtime's native body pipeline.

Keyboard defaults:

| Key | Action |
| --- | --- |
| I | Open the character menu |
| J | Light attack |
| L | Heavy attack |
| U | Guard / parry |

Use **Camp > Binds** or Controls+ to change bindings. Controllers start unbound;
bindings keep their device identity and support two-button chords. The character
menu accepts keyboard, controller and pointer input. Left/Right or the displayed
Next tab command switches pages; Up/Down selects a row.

In Camp, choose **Spawn Famished Crawler** while standing on a wide, flat
platform, then resume. Nothing spawns automatically on ordinary maps. The crawler
has 200 health, walks on its supported ledge and rises before lunging. Its attack
follows the original Quest animation, movement and contact boxes. Dodge it,
interrupt its preparation, or time a directional parry. Training victories award XP and count toward
equipment unlocks; this is a repeatable sandbox, not a competitive progression
system. Rest restores health when no enemy is nearby.

## Character and equipment

The character starts with a Side Sword, Buckler, Archer Coat and three Hotdogs.
The first victory unlocks a Bastard Sword; the third unlocks a Crown Spear and Iron
Guard Armor. Equip and unequip from Inventory, or clear a slot from Equipment. Hotdogs
restore 40 health and are consumed only when they heal. Native inventory and
Quest save files are separate from this character.

Each victory awards 25 XP. Leveling grants an unspent attribute point. Character
shows four opposing axes: Mettle/Guile, Impulse/Composure,
Refinement/Crudeness and Faith/Spite. Choose one side of an axis, then keep
increasing that side, up to 20. There is no respec in this version. The displayed
health, damage, reach, armor and guard update when attributes or equipment change.

Light attacks start after 8 ticks and remain active for 4. Heavy attacks start
after 20 ticks, remain active for 6 and deal double damage. Each swing hits each
target at most once and cannot pass through blocking terrain. Guard lasts 30
ticks, covers the direction faced and needs a shield. Its first 8 ticks can parry
without damage; later blocks reduce damage. Zero health prevents combat actions
for 150 ticks, then restores health with a short grace period. This prototype
doesn't add a game-over screen or change native jump controls.

A heavy landing at the native maximum fall speed costs 8% of maximum health,
rounded up, as in Quest's shipped fall-damage setting. Guard and armor don't
reduce it. Ordinary jumps back to their takeoff height are safe. Teleports and
Runtime state restores don't count as falls.

## Saves and compatibility

`Metroidvania.Character.xml` stores this mod's inventory and progression beside
the installed package. `Metroidvania.Settings.xml` stores mode and bindings.
Runtime keeps the previous saved file as `.bak`. Unsupported, damaged or
externally changed files are not silently overwritten. A save failure appears in
the character screen and the game log. Rewards save after a quiet encounter;
opening or closing Character, saving explicitly and ending the attempt also flush
pending changes. Unsaved changes can be lost if the process crashes.

This version supports one native king with the normal 18 by 26 body. Combat
pauses for Runtime modals, native pause and foreign player-control ownership.
Form controllers and custom body sizes aren't supported. Shared-world host,
replica and replay execution don't advance the offline battle. Online play and
full encounter replay are not implemented. The compact equipment/action visual
state is available to Runtime's actor presentation consumers, with local drawing
on the viewer; this does not synchronize health, rewards or enemies.

## Map encounters

Map authors can place `props/metroidvania/encounters.xml` under their map root:

```xml
<Metroidvania Version="2">
  <Enemy Id="courtyard-guard" Type="fslave-crawler"
         Screen="1" X="220" Y="308" Patrol="48" />
</Metroidvania>
```

Coordinates are the crawler's top-left corner in a 480 by 360 screen. Screens
start at 1. The Quest crawler's movement body is 32 by 22 pixels and needs solid support immediately below
its initial position. Patrol bounds extend left/right from X; movement probes
live collision and stops at ledges. There is no jumping AI, slope traversal
or dynamic navigation. Up to 16 authored enemies are allowed, with unique IDs.
An invalid encounter file prevents mode activation for that attempt.

Read the [map-authoring guide](docs/map-authoring.md) for accepted values and
the current map format. The [development roadmap](docs/roadmap.md) covers editor
placement, triggers, rewards, more enemies, co-op, replays and duels. Those later
systems aren't implemented in 0.4.0.

## Extend the content

The maintained import recipe is `content/jkq-import.json`. It selects enemies,
equipment and shared sounds, and declares scale/balance changes. The importer
follows Quest's prefab/brain references to obtain clips, weighted attacks,
colliders, movement, health and assigned sound GUIDs. Runtime uses shared
definitions and a common grounded-melee controller, with no named-enemy branch.

Use [the import guide](docs/import-pipeline.md) to rebuild the complete validated
pack. [The architecture plan](docs/architecture.md) describes the current layers
and the next steps for actions/effects, behavior profiles, maps, saves and online
adapters. New content using supported mechanics is a data change; new mechanics
need a reviewed common handler. Unsupported source features fail import.

## Build and install

From the repository root:

```powershell
scripts/check-mods.ps1 -Mod metroidvania
scripts/check-mods.ps1 -Mod metroidvania -Integration
mods/metroidvania/install.ps1
```

The build validates compiled asset hashes and rejects stale recipes/import tools,
then runs progression, command, collision, snapshot and save-safety tests.
Integration also renders the four menu tabs with the installed game's fonts and
frame assets, checks native armor poses and plays the prepared sounds through
the native audio engine with an isolated muted mixer. The package is
`build/metroidvania/UPLOAD_TO_WORKSHOP/Metroidvania.dll`.
Install that DLL with Runtime separately. No other implementation or game DLL is
part of the package. Close Jump King before installing.

The combat kernel accepts numbered logical commands, stable actor IDs and deep
snapshots. It owns no input devices, rendering, saves or Steam calls. A later
online adapter still needs actor membership, authority, delivery and latency
handling. No network transport or background worker runs in this version.
