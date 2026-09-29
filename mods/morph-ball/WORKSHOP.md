[h1]Ball King[/h1]

[b]Version 2.3.0[/b]

[b]Requires JK Runtime 1.36.[/b]
Curl into a rolling ball and travel across floors, slopes, walls and ceilings.

[h2]Controls[/h2]
Use the configurable [b]Morph[/b] binding to curl up or unfold.
Open [b]Binds[/b] for primary/secondary slots, two-button chords, Clear and Default. Existing Controls+ bindings are preserved.
[list]
[*]Use left and right to roll along the current surface.
[*]Press and hold Jump for a variable-height platformer jump.
[*]Release Jump before pressing it again to use a compatible Jetpack.
[/list]

[b]Sticky[/b] enables rolling across slopes, walls, ceilings and corners. Disable it for ground-only rolling.
[b]Double jump[/b] adds one aerial jump before the next surface contact.

Jumps have a short grace period after rolling off a surface. A jump made in
this window doesn't consume the optional double jump.

The ball keeps the king's active reskin and equipped clothing, including Workshop clothing layers.
The ball never splats. Falls of at least 100 pixels bounce once; falls from the vanilla splat height bounce twice.

[h2]Level Tag[/h2]
[b]AllowBallKing[/b]
Allows Ball King without marking the run as modified.

Works independently and is compatible with Casual Jumping and Jetpack.
[h2]Custom Map Pixels[/h2]
[b]RGB(160, 64, 255)[/b] - Nonblocking screen marker enabling Ball King: jumps use a shorter 75%-height profile, or 35% for both jumps while Double jump mode is active. Global Sticky is disabled.
[b]RGB(255, 96, 64)[/b] - The same restriction, with Double jump disabled.
[b]RGB(64, 128, 255)[/b] - The same restriction, with Double jump forced on.
[b]RGB(255, 64, 160)[/b] - Solid sticky material: the ball clings to platforms and inferred slopes made from this colour regardless of the Sticky setting.

[h2]Map-controlled activation[/h2]
Requires JK Runtime 1.36+. Add these exact Tags entries to level_settings.xml for the mechanics your map controls:
[list]
[*]JKRuntime.MapControlled:ball-king.form
[/list]
Opaque screen markers (RGB; all are nonblocking):
[list]
[*]Ball form: On (173,80,211), Off (173,81,211), Local (173,82,211).
[/list]
On allows morphing, Off blocks it, and Local allows only a local trigger or XML zone. On a tagged map, unmarked screens are off and the global switch is locked. Entering saves that switch as Off; leaving doesn't turn it back on.
[b]Use either a screen pixel or an XML Screens rule. You don't need both.[/b] If both exist, they must agree. XML also supports ranges, zones and parameters; it never overrides a pixel. Neither form adds the separate map tag.
Put XML rules in jk-runtime/mechanics.xml at the map root. Screens start at 1 and zones use 480 x 360 local coordinates. See docs/map-authoring.md for the full example.
Existing restricted-screen colours remain nonblocking On declarations with their jump presets; Sticky (255,64,160) remains solid terrain and doesn't grant morph permission.
