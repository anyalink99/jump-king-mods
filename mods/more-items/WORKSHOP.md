[h1]More Items[/h1]

[b]Requires JK Runtime 1.30 or newer 1.x.[/b]
Adds persistent items, equipment, consumables and merchant trading to Jump King.

[b]The former standalone Jetpack Workshop mod is obsolete.[/b] More Items replaces it completely. Unsubscribe from the old item or remove Jetpack.dll before using this mod.

[h2]Jetpack[/h2]
Buy the Jetpack from the Bargainburg merchant, then equip it through the ordinary Inventory. Release Jump after takeoff, then press and hold it again while airborne for smooth, momentum-preserving thrust.

[h2]Rewinder[/h2]
Rewinds the current or most recently completed jump and returns the King to its takeoff point. The default Controls+ binding is R.

[h2]Hammer[/h2]
Buy Hammer for 3 Silver Coins or use Add Hammer in ModsDebugActions, then equip it in Inventory. Move the mouse to climb; unequip to restore ordinary movement. ModsDebugActions also contains its force control: 100% matches the former standalone mod's 120%, with Left/Right adjustment and Confirm to reset. Remove the obsolete HammerKing.dll before using More Items.

[h2]Merchant[/h2]
The expanded Bargainburg merchant sells vanilla equipment, 10 Rewinders for 1 Ghost Fragment, Jetpack for 12 Ghost Fragments and Hammer for 3 Silver Coins. It also supports currency exchanges and offers registered by other mods.

[h2]Persistent inventory[/h2]
Owned stacks are shared by Vanilla and every custom world. Restarting a run respawns placed pickups without deleting collected items.

[h2]Settings[/h2]
Rewinder, Jetpack and Hammer can be enabled independently. Items Settings shows enabled items with settings cards and keeps their enable switches at the root. Rewind-path rendering and Jetpack appearance, trail and volume can be configured there whether or not the Jetpack is currently equipped. Hammer force is in ModsDebugActions.

[h2]Modding[/h2]
More Items exposes one API for item modules, permanent stacks, equipment, physical pickups, appearance-free dispensers, custom currencies, settings cards and offers. Debug commands register into JK Runtime's generic ModsDebugActions page.

Map authors can add AllowHammer, AllowJetpack or AllowRewinder to Tags in level_settings.xml to authorize the corresponding item without its modified-run mark. Items remain usable without these tags; Hammer marks an unauthorized run from equip. Existing marks from earlier use or other mods are preserved.
