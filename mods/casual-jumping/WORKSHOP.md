[h1]Casual Jumping[/h1]

[b]Version 2.2.0[/b]

[b]Requires JK Runtime 1.34.[/b]
Adds three optional control modes to Jump King.

[h2]Control Modes[/h2]
[b]Casual[/b]
Keeps the original charge-and-release jump while adding directional control in the air.

[b]Casual+[/b]
Press to jump, hold for extra height and steer in the air. Includes a short jump grace period after leaving a platform.

[h2]Level Tag[/h2]
[b]AllowCasualJumping[/b]
Allows Casual and Casual+ without marking the run as modified.

Compatible with Jetpack from More Items and [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3779149753]Sprinting[/url].

[h2]Map-controlled activation[/h2]
Requires JK Runtime 1.34+. Add these exact Tags entries to level_settings.xml for the mechanics your map controls:
[list]
[*]JKRuntime.MapControlled:casual.controls
[/list]
Opaque screen markers (RGB; all are nonblocking):
[list]
[*]Casual controls: On (173,104,211), Off (173,105,211), Local (173,106,211).
[/list]
On allows the mechanic, Off blocks it, and Local allows only a local trigger or XML zone. On a tagged map, unmarked screens are off and the global switch is locked. Entering saves that switch as Off; leaving doesn't turn it back on.
[b]Use either a screen pixel or an XML Screens rule. You don't need both.[/b] If both exist, they must agree. XML also supports ranges, zones and parameters; it never overrides a pixel. Neither form adds the separate map tag.
Put XML rules in jk-runtime/mechanics.xml at the map root. Screens start at 1 and zones use 480 x 360 local coordinates. See docs/map-authoring.md for the full example.
