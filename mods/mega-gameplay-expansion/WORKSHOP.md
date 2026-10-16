[h1]Mega Gameplay Expansion[/h1]

[b]Version 0.15.2[/b]
New mechanics for custom maps, with matching global settings.
Every mechanic comes in three forms: Solid, local non-solid Zone, and Screen marker.
Requires JK Runtime 1.44 or newer.
Open [b]Binds[/b] in this mod's settings to edit Air Dash only: primary/secondary, chords, Clear and Default.

[h2]Mod Inspector[/h2]
The former Gimmick Library is now in JK Runtime -> ModsDebugActions -> Mod Inspector.
The inspector stores its own configurations and searches. MGE's settings contain
Warp Jump, No Walk Off, Air Dash and Binds.

[h2]Air Dash[/h2]
Press Jump again in the air to dash horizontally in the direction of flight.
One dash per flight, refreshed on landing. Travel is 80 pixels in 14 game
ticks (about 238 ms). A vertical jump uses facing direction. The dash pauses vertical motion;
free completion carries half its horizontal speed into normal falling physics instead
of restoring the pre-dash velocity. The following free flight isn't part of
the 80-pixel distance. Hitting a wall gives the native
half-impulse bounce. Thin walls can't be skipped.

The dash press is consumed. Release and press Jump again, even while dashing,
to buffer the next jump normally. Uses your Jump binding, not a hardcoded key.
Native outfits stay visible over short cyan/violet echoes and impact sparks.
A short 8-bit edit of SoundReality's Whoosh Velocity accompanies the dash and
follows the game's SFX volume setting.

Enable [b]Air Dash[/b] globally, or use:
Solid: RGB 173,53,211 (jump from it to earn a dash; not a walk-off).
Screen: RGB 173,54,211 (non-solid marker).
Zone: RGB 173,55,211 (activate within its non-solid volume).
Leaving the Zone or Screen after activation doesn't interrupt the dash.
Ball/Casual/active Jetpack and an ongoing Warp presentation can't dash.
Global use follows [code]AllowMegaGameplayExpansion[/code] and run marking.

[h2]No Walk Off[/h2]
Hold a direction to reach the last pixel of support without walking off.
Release that direction and press it again to leave the platform. You can still
jump or walk back normally. No Walk Off is inactive on ice and with Snake Ring
enabled. Native walking speed, water scaling and airborne physics stay unchanged.
Flat-to-descending-slope seams count as edges too: press again to enter the slope.
JumpKingPlus low gravity (including legacy speed) and top-facing one-way platforms
are supported. Jumping upward through a one-way platform remains unchanged.
JK Runtime observes supported speed transformations directly, including unfamiliar
scaling handlers. Unsupported movement remains under its original mod's control.

Enable the global [b]No Walk Off[/b] checkbox, or use these map pixels:
Solid: RGB 173,50,211. Screen marker: RGB 173,51,211. Local Zone: RGB 173,52,211.
Screen and Zone are non-solid. The rule doesn't persist after leaving its scope.
It guards grounded walking, not airborne movement, slopes, teleports or Ball form.
An analog stick must return to neutral before the second outward input.
Global use marks the run modified unless the map has [code]AllowMegaGameplayExpansion[/code].
Map-authored use doesn't add that mark.

[h2]Warp Jump (preview)[/h2]
Charge your normal jump, dissolve into compact red, green and blue pixels, then reassemble where
that jump would first land. Enabled falls are calculated too. The real position
changes after about 133 ms, followed by 107 ms of reconstruction; movement and charge
are locked during the 240 ms transition. Your sprite and outfit supply the pixels.
Particles bounce off solid terrain. Reconstruction ends with the native landing sound.
Splat landings reassemble into the native splat pose, with the usual recovery.
Press and hold Jump during the effect to buffer your next jump. Charge starts
after reconstruction and any splat recovery; releasing early cancels the buffer.
The global Warp Jump checkbox is off by default; authored map blocks work independently.

Warp surface: RGB 173,47,211 (solid).
Warp screen marker: RGB 173,48,211 (non-solid).
Warp zone: RGB 173,49,211 (local, non-solid).
Zones activate on player overlap, including entry during a jump or fall. They
can be placed above normal or supported special terrain without replacing it.
Screen markers enable the mechanic across their entire screen, not just nearby.

Initial support covers native static geometry, ice, snow, sand, water, slopes and
wall/ceiling collisions and vanilla wind (including direction changes and NoWind
zones). Long calculations are spread across frames during the departure effect.
Activating side-exit teleport links,
foreign blocks and active alternative movement aren't forecast yet; these keep
ordinary movement. An inactive teleport link on the current or adjacent screen
doesn't disable Warp Jump. Refusal reasons are recorded in MegaGameplayExpansion.log.
Global successful warps mark the run modified unless the map allows Mega Gameplay Expansion.

[h2]Map-controlled activation[/h2]
Requires JK Runtime 1.44+. Add these exact Tags entries to level_settings.xml for the mechanics your map controls:
[list]
[*]JKRuntime.MapControlled:mega.warp
[*]JKRuntime.MapControlled:mega.no-walk-off
[*]JKRuntime.MapControlled:mega.air-dash
[/list]
Opaque screen markers (RGB; all are nonblocking):
[list]
[*]Warp Jump: On (173,86,211), Off (173,87,211), Local (173,88,211).
[*]No Walk Off: On (173,89,211), Off (173,90,211), Local (173,91,211).
[*]Air Dash: On (173,92,211), Off (173,93,211), Local (173,94,211).
[/list]
On allows the mechanic, Off blocks it, and Local allows only a local trigger or XML zone. On a tagged map, unmarked screens are off and the global switch is locked. Entering saves that switch as Off; leaving doesn't turn it back on.
[b]Use either a screen pixel or an XML Screens rule. You don't need both.[/b] If both exist, they must agree. XML also supports ranges, zones and parameters; it never overrides a pixel. Neither form adds the separate map tag.
Put XML rules in jk-runtime/mechanics.xml at the map root. Screens start at 1 and zones use 480 x 360 local coordinates. See docs/map-authoring.md for complete examples and mechanic-specific rules.
