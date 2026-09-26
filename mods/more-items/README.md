# More Items

[Technical documentation](docs/index.md).

More Items adds a persistent inventory, Rewinder, Jetpack, Hammer and new
Bargainburg merchant offers. Version **2.2.1** requires
[JK Runtime 1.30 or newer 1.x](../jk-runtime/README.md).

Other mods can register items, pickups and dispensers through the [item API](docs/api.md).

The optional Jetpack pose editor takes an extracted King PNG sprite sheet:
`open-jetpack-editor.ps1 -AtlasPath C:\Assets\king-base.png`.
It edits `assets/jetpack-poses.txt`; game artwork is supplied locally.

## Items and controls

- **Rewinder** returns the King to the takeoff point of the current or most
  recently completed jump. The default Controls+ binding is `R`.
- **Jetpack** is permanent equipment. Buy it for 12 Ghost Fragments, then use
  the ordinary Inventory's `Equip` action. After takeoff, release Jump and
  press and hold it again while airborne to apply thrust.
- **Hammer** is permanent equipment costing **3 Silver Coins** at the
  Bargainburg merchant. Equip it in Inventory and move the mouse to climb.
  Unequip it to restore normal walking and jumping. Its compact sprite, contact
  physics, native landing effects and wood/stone/snow sounds come from Hammer King.

Owned stacks are shared across worlds. Restarting a run respawns that world's
placed pickups without clearing the inventory. Debug actions include
`Add Jetpack`, `Add Hammer` and `Add 10 Rewinders`. Granting a Hammer does not
automatically equip it, and repeated grants do not duplicate it.

## Settings

`Enable Rewinders`, `Enable Jetpack` and `Enable Hammer` control the modules.
`Items Settings` opens a card for each enabled module: Rewinder's path display,
or Jetpack's visibility, fire trail and volume. Jetpack settings can be edited
while it is unequipped; ownership and equipment state are managed in Inventory.

**ModsDebugActions → Hammer: 100%** changes hammer force with Left/Right or
the arrow buttons, in 5% steps from 50% to 125%. Confirm resets it to 100%.
**100% matches the former Hammer King 120%**; the upper endpoint
125% retains the former 150% maximum. Changing force does not reset the planted
hammer. Force and equipment choice are saved in `MoreItems.Settings.xml`;
`HammerSensitivity` defaults to 1 and can be edited with the game closed.

## Integration

Map authors can add `AllowHammer` to `Tags` in `level_settings.xml`, alongside
`AllowJetpack` and `AllowRewinder`. These tags authorize each item's contribution
to native run eligibility; they do not restrict ownership, purchase or use.
Without `AllowHammer`, equipping Hammer marks the run as modified because it
immediately replaces movement. With the tag, Hammer adds no modified-run mark.
Unequipping never clears existing run history or another mod's markers.
The permission is read again when equipment is installed in the next world.

Runtime prefetches the inventory before player handoff. Pickup resets and saves
occur at activation, so cancelling an intro does not change a saved attempt.
Menu and API writes invalidate older reads.

Jetpack publishes the runtime's `player.thrust:1:0` capability and cooperates
with form and movement controllers. Rewinder restores registered
Casual/Ball/Jetpack movement state, but not world state, inventory or the clock.

The former standalone Jetpack and Hammer King mods are replaced by More Items.
Do not install `Jetpack.dll` or `HammerKing.dll` alongside `MoreItems.dll`; the
installer backs up and removes those obsolete DLLs. Old standalone settings are
retained but no longer read. No item is granted or equipped by the migration.
See [Hammer physics and checks](docs/hammer.md).

## Build

From the repository root:

```powershell
.\mods\more-items\build.ps1
```

The script builds and verifies JK Runtime first, then writes
`build/more-items/UPLOAD_TO_WORKSHOP/MoreItems.dll`. Install it with
`JKRuntime.dll` (Workshop item 3793086563).

`scripts/check-mods.ps1 -Mod more-items -Integration` additionally checks Hammer
audio, sprite layering and real inventory equip/unequip, module and level
lifecycle. `mods/jk-runtime/verify-ui-graphics.ps1` verifies the native Debug
Actions arrows/value and merchant presentation. The `hammer-king` check alias
also selects More Items; it no longer builds a separate DLL.
