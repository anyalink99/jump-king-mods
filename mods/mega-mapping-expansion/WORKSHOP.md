[h1]Mega Mapping Expansion[/h1]

[b]Version 1.1.0[/b]

Create reactive scenery, collision layouts and screen routes with map-local XML.
Requires JK Runtime 2.2.0 or newer and the shared Harmony included in the package.
Smooth Camera 0.13.0 adds smooth scripted focus and return.

[h2]Build scenes from common parts[/h2]
[list]
[*]sprites, meshes, text, masks, lights, particles, reflections and shadows
[*]typed signals, value graphs, property bindings and timed effects
[*]control trees for sequences, branching, waiting and cancellation
[*]reusable recipes for orbiting objects, gaze, streams, weather and instruments
[*]whole-pixel drawing by default, with explicit fractional rendering options
[*]attempt/save variables, snapshots and authority-aware scene state
[/list]

[h2]Connect the scene to the game[/h2]
[list]
[*]observe movement, walking, charge, impacts and native lifecycle events
[*]react to native Hidden Walls without replacing their carefully placed rectangles
[*]observe native NPC lines and items; give individual NPCs a voice and line pause
[*]reveal scene text and react to characters or completed lines
[*]play owned sounds and loops, with audio zones and volume fades
[*]show another screen or request a smooth camera shot without moving the King
[*]add victory requirements and hitboxes while keeping the native ending controller
[*]coordinate effects with custom native ending trees
[/list]

Native typing speed, quote selection, inventory and ordinary map assets stay in
their usual files. The handbook explains what the game already supports and when
an MME adapter is useful.

[h2]Collision and navigation[/h2]
[list]
[*]pixel collision atlases or strips, up to 480 by 360 pixels per screen
[*]custom collision shapes and slope aliases for mod blocks
[*]fixed screen links, weighted destinations, shuffled bags and sequences
[*]switchable route schemes, seeds and saved transition history
[/list]

The level+ workflow originated in Zebra's More Block Sizes.

[h2]Authoring[/h2]
Use mapping_settings.xml, safe relative asset paths and the supplied compiler.
The authoring kit includes a handbook, complete examples and a parameter reference.
The in-game inspector shows observations, effective properties, effects and
diagnostics. A text editor and ordinary Worldsmith are sufficient; Worldsmith
Extension is optional.

The menu checkbox controls scene presentation and logic. Collision and native
side links remain active. Pause holds gameplay logic; visual animation during
pause requires an explicit view setting.

Version 1 uses the construction format. Earlier scene formats need conversion;
there is no legacy reader. Keep old source and follow the handbook's conversion
guide before upgrading an older map.
