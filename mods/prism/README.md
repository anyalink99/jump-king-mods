# Prism

Prism 0.2.0 adds music-driven visual themes to Jump King. The first theme,
**Event Horizon**, replaces native map layers with a slowly changing nebula,
an animated black hole and glass platforms traced from the loaded collision.
This build requires JK Runtime 1.37 or newer 1.x.

Open **Prism > Themes** in the main or pause menu and select **Event Horizon**.
Its settings open as a separate UIApi+ page. Every control takes effect and saves
immediately; there is no Apply button. Each theme keeps its own profile. This
version includes one theme. Existing Prism 0.1 preferences migrate automatically.

| Setting | Effect |
| --- | --- |
| Theme | Enable the selected theme or restore the original map. |
| Disable props | On by default. Hide native props, NPCs, Hidden Walls and MME scene drawing. Entity updates and map events keep running. |
| Keep map artwork | Retain native map layers and add luminous geometry edges. |
| Reflections | Reflect the world and King into the upper faces of glass platforms. |
| Gentle motion | Reduce pulses and remove note trails. Wind streaks remain visible. |
| Music volume | Set theme volume from 0 to 100%, combined with native master volume and the Music preference. |
| Glow | Adjust platform halos from 0 to 150%. Solid edges remain visible at zero. |
| Wind visibility | Adjust wind streak brightness from 50 to 150%. |

Numeric controls support arrows, mouse wheel and dragging. Returning from a page
keeps its changes. Choosing **Original map** restores native layers, props and
music while keeping theme settings for later.

The audio sample counter drives the animation; native pause freezes both.
Restarting the map or starting a new map resets the song and visual timeline.
Toggling the theme within an attempt keeps its place in the song. Music loops
at its decoded duration. Save-state restores do not rewind this cosmetic
timeline or change gameplay.

Star drift and horizontal streaks follow the game's wind direction and strength,
independently of the song. Wind streaks disappear in no-wind zones and when the
wind is calm. This shows the screen's wind field, not a prediction of the King's
movement: native snow, shelter and wind-activation rules still control the player.

The local theme uses xi's **.357 Magnum**, with Skrowell's osu! chart. Hit
times create light pulses, note positions place waves, jumps between notes
control particle direction and reach, and slider control points draw trails.
These trails interpret the chart visually; they are not an osu! rules engine.

## Build and install

From the repository root:

```powershell
.\scripts\check-mods.ps1 -Mod prism -Integration
.\mods\prism\import-theme.ps1 -SongDirectory 'C:\path\to\osu-song-folder'
.\mods\prism\install.ps1
```

The importer needs FFmpeg and one `.osu` file, or an explicit `-Beatmap`
filename. It decodes the referenced audio to PCM and keeps it under
`build/prism/LOCAL_THEME/event-horizon`. The installer puts that local theme
beside the mod, under `themes/event-horizon`, and preserves settings and backups.
Close Jump King before installation.

The distributable code package is `build/prism/UPLOAD_TO_WORKSHOP`.
Personal song files are deliberately separate. Import a theme before enabling
the installed mod; missing or invalid theme assets leave native rendering active
and report a Runtime preparation error.

## Compatibility and limits

Prism changes presentation only. Player physics, collision, NPC logic, saves and
native UI remain unchanged. The King and native pickups remain visible with
Disable props enabled. Ice, snow, sand, water and quark have fixed material
accents alongside the changing palette.

Water has its own translucent mask, so it cannot overwrite submerged platforms
or slopes. Southwest slopes use the same declared triangle as Ball King's
contour adapter: their visible diagonal is corrected while their unusual native
collision stays untouched. Other slopes follow their native triangles.

Screens containing unknown block types keep their original artwork and receive
only known-edge overlays. This avoids hiding a mod's unsupported geometry.
Dynamic custom blocks need an explicit geometry adapter before full replacement.
Screens use their own collision coordinates, including Smooth Camera's screen
passes. With Disable props off, existing MME scene props and effects still draw
and can change the final look. With it on, Prism suppresses MME scene layers,
scene text, shadows, rim lighting and composition without stopping their logic.
Third-party entities need an explicit adapter to join the props switch.
Special map controllers that replace all drawing, such
as Stereo Madness, are outside this first version's compatibility scope.

Reflections use the same pre-UI frame-copy and clipped scanline approach as MME.
They stay inside actual platform pixels and reflect at most 18 pixels of depth.
They are stylized glass reflections, not ray tracing. Prepared screen textures
cost about 3.3 MiB per screen, plus masks and shared textures. Decoded music and
its source PCM bytes stay prepared for the world; a restart creates a fresh audio
voice before player activation.

Build checks cover chart timing, settings migration, wind movement and package
compilation. Integration checks native audio restart/pause/volume, live UI
settings, draw suppression and underwater slopes, then renders native GPU
fixtures for visual inspection. If a built MME implementation is available,
integration also checks its optional scenery hooks.
These checks do not establish compatibility with every Workshop controller.
