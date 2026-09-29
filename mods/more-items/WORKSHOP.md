[h1]More Items[/h1]

[b]Version 2.4.0[/b]

[b]Requires JK Runtime 1.35 or newer 1.x.[/b]
Adds persistent items, equipment, consumables and merchant trading to Jump King.

[b]The old standalone Jetpack is obsolete.[/b] More Items replaces it. Unsubscribe from the old item or remove Jetpack.dll before installing this mod.

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
Rewinder, Jetpack and Hammer each have their own enable switch. Open Items Settings for Rewind-path drawing and Jetpack appearance, trail and volume; you can edit Jetpack options while it is unequipped. Hammer force remains under ModsDebugActions.

[h2]Modding[/h2]
More Items exposes one API for item modules, permanent stacks, equipment, physical pickups, appearance-free dispensers, custom currencies, settings cards and offers. Debug commands register into JK Runtime's generic ModsDebugActions page.

Map authors can add AllowHammer, AllowJetpack or AllowRewinder to Tags in level_settings.xml to authorize the corresponding item without its modified-run mark. Items remain usable without these tags; Hammer marks an unauthorized run from equip. Existing marks from earlier use or other mods are preserved.

[h2]Map-controlled activation[/h2]
Requires JK Runtime 1.35+. Add these exact Tags entries to level_settings.xml for the mechanics your map controls:
[list]
[*]JKRuntime.MapControlled:more-items.jetpack
[*]JKRuntime.MapControlled:more-items.hammer
[*]JKRuntime.MapControlled:more-items.rewinder
[/list]
Opaque screen markers (RGB; all are nonblocking):
[list]
[*]Jetpack: On (173,95,211), Off (173,96,211), Local (173,97,211).
[*]Hammer: On (173,98,211), Off (173,99,211), Local (173,100,211).
[*]Rewinder: On (173,101,211), Off (173,102,211), Local (173,103,211).
[/list]
On allows the item, Off blocks it, and Local allows only a local trigger or XML zone. On a tagged map, unmarked screens are off and the global switch is locked. Entering saves that switch as Off; leaving doesn't turn it back on.
[b]Use either a screen pixel or an XML Screens rule. You don't need both.[/b] If both exist, they must agree. XML also supports ranges, zones and parameters; it never overrides a pixel. Neither form adds the separate map tag.
Put XML rules in jk-runtime/mechanics.xml at the map root. Screens start at 1 and zones use 480 x 360 local coordinates. See docs/map-authoring.md for the full example.
Jetpack and Hammer authored activation temporarily equips a loan by default without changing inventory; equipment="owned" requires owned, equipped items. Rewinder never receives free charges.
