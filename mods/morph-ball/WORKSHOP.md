[h1]Ball King[/h1]

[b]Requires JK Runtime 1.30.[/b]
Curl into a rolling ball and travel across floors, slopes, walls and ceilings.

[h2]Controls[/h2]
Use the configurable [b]Morph[/b] binding to curl up or unfold.
Open [b]Binds[/b] for primary/secondary slots, two-button chords, Clear and Default. Existing Controls+ bindings are preserved.
Use left and right to roll along the current surface.
Press and hold jump for a variable-height platformer jump.

[b]Sticky[/b] enables rolling across slopes, walls, ceilings and corners. Disable it for ground-only rolling.
[b]Double jump[/b] adds one aerial jump before the next surface contact.

Jumps have a short grace period after rolling off a surface. A jump made in
this window does not consume the optional double jump.

The ball preserves the king's active reskin and equipped clothing, including Workshop clothing layers.
The ball never splats. Falls of at least 100 pixels bounce once; falls from the vanilla splat height bounce twice.

[h2]Level Tag[/h2]
[b]AllowBallKing[/b]
Allows Ball King without marking the run as modified.

Works independently and is compatible with Casual Jumping and Jetpack.
[h2]Custom Map Pixels[/h2]
[b]RGB(160, 64, 255)[/b] - Solid screen-rule block: jumps use a shorter 75%-height profile, or 35% for both jumps while Double jump mode is active. Global Sticky is disabled.
[b]RGB(255, 96, 64)[/b] - The same restriction, with Double jump disabled.
[b]RGB(64, 128, 255)[/b] - The same restriction, with Double jump forced on.
[b]RGB(255, 64, 160)[/b] - Solid sticky material: the ball clings to platforms and inferred slopes made from this colour regardless of the Sticky setting.
