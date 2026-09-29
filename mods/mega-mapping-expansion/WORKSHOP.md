[h1]Mega Mapping Expansion[/h1]

[b]Version 0.7.1[/b]

Build animated scenes, lighting, narrative and map-wide presentation rules from XML. Requires JK Runtime 1.35 or newer 1.x.

Use the Mega Mapping Expansion checkbox in the main or pause menu to pause its
visual effects. The choice is saved, but scene state and routes stay intact.
Smooth Camera 0.6+ composes with Mapping while both are enabled.

[h2]Scene logic and map flow[/h2]
[list]
[*]region-triggered scene states, texture variants and timed effects
[*]original Hidden Walls events, native platform support tests and native sound cues
[*]custom ending behavior trees in ending/custom_*.xml
[*]lights and props attached to the player or another prop
[*]conditional event rules, run/save flags and rewind-aware scene state
[*]screen-entry sprite animation
[*]custom map intro text and map-forced timer hiding
[/list]

[h2]Drawing and motion[/h2]
[list]
[*]rotation, orbit, sway, linear, bob and path trajectories
[*]background/world/foreground layers
[*]reactive 2.5D bushes
[*]radial lights with player-cast shadows
[*]water with live King reflections and ripples
[*]selected reflection participants and authored rectangular light blockers
[*]layered moving fog
[*]horizontal/vertical whole-game mirror filters, including menus
[/list]

[h2]Authoring and integration[/h2]
[list]
[*]in-game inspector, debug editing and reusable effect recipe export
[*]public scene API for other mods
[*]one-based screen IDs tested through screen 300
[/list]

Configuration lives in props/mega-mapping-expansion/scene.xml inside the map. The authoring kit includes a complete handbook, recipes and parameter reference.
