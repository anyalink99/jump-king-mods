[h1]Mega Gameplay Expansion[/h1]
New mechanics for custom maps, with matching global settings.
Every mechanic comes in three forms: Solid, local non-solid Zone, and Screen marker.
Requires JK Runtime 1.30 or newer.
Open [b]Binds[/b] in this mod's settings to edit Air Dash only: primary/secondary, chords, Clear and Default.

[h2]Gimmick library (experimental)[/h2]
Start with clear sections: Active & reset, Pinned, Maps & regions, Installed mods,
Behaviour / Wind, Search, Saved searches and Refresh. Reset MGE overrides and
its three built-in modes individually or together, keeping configurations and pins.
Third-party toggles are separate Mod settings; bulk reset never changes them.
Individual setting edits and pins still use the owning mod's persistent controls.
Restore original map state is also directly on the library home: remove only
MGE-applied overrides without disabling global mod settings.
Browse installed mod settings, factory materials and advanced live states.
Combine provider, map, numbered region, behaviour and availability filters.
Search exact HEX/RGB or use the clickable collision-colour palette; save queries.
Edit a draft and Apply it explicitly. Pin saved configurations, including named
copies with different target regions. The original three mechanics start pinned.
Auto preserves ordinary rectangular surfaces and fills empty space for inferred
nonblocking media, retaining solids and slope collision. Advanced modes retain
replacement of existing objects, whole space including air, and overlays.
Native Wind is available on vanilla maps: direction, strength, timing and scope.
Discovery is generic: there are no per-mod adapters. Supported unseen materials
can be constructed on vanilla maps without visiting their source map. Explicit
native block-handler registrations are resolved when enabled. Complex startup
dependencies, metadata-only and nonportable entries show why they cannot be
applied. Advanced states are raw fields/properties, not
a promise of universal semantic compatibility. Map artwork is unchanged.

[h2]Air Dash[/h2]
Press Jump again in the air to dash horizontally in the direction of flight.
One dash per flight, refreshed on landing. Travel is 80 pixels in 14 game
ticks (about 238 ms). A vertical jump uses facing direction. The dash pauses vertical motion;
free completion carries half its horizontal speed into normal falling physics instead
of restoring the pre-dash velocity. The following free flight is not part of
the 80-pixel distance. Hitting a wall gives the native
half-impulse bounce. Thin walls cannot be skipped.

The dash press is consumed. Release and press Jump again, even while dashing,
to buffer the next jump normally. Uses your Jump binding, not a hardcoded key.
Native outfits stay visible over short cyan/violet echoes and impact sparks.
A short 8-bit edit of SoundReality's Whoosh Velocity accompanies the dash and
follows the game's SFX volume setting.

Enable [b]Air Dash[/b] globally, or use:
Solid: RGB 173,53,211 (jump from it to earn a dash; not a walk-off).
Screen: RGB 173,54,211 (non-solid marker).
Zone: RGB 173,55,211 (activate within its non-solid volume).
Leaving the Zone or Screen after activation does not interrupt the dash.
Ball/Casual/active Jetpack and an ongoing Warp presentation cannot dash.
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
Screen and Zone are non-solid. The rule does not persist after leaving its scope.
It guards grounded walking, not airborne movement, slopes, teleports or Ball form.
An analog stick must return to neutral before the second outward input.
Global use marks the run modified unless the map has [code]AllowMegaGameplayExpansion[/code].
Map-authored use does not add that mark.

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
foreign blocks and active alternative movement are not forecast yet; these retain
ordinary movement. An inactive teleport link on the current or adjacent screen
does not disable Warp Jump. Refusal reasons are recorded in MegaGameplayExpansion.log.
Global successful warps mark the run modified unless the map allows Mega Gameplay Expansion.
