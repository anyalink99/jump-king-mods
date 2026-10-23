# Camera settings

Open **Mods > Smooth Camera > Settings** in the main or pause menu. **Enable**
is the saved user preference. A map's `smooth` marker forces the camera even
when Enable is off; the status explains the current effective state.

Settings uses the shared Runtime page host, native fonts and current-device
button hints. Select a row with navigation or the mouse. Select the Page row,
use left/right navigation, or use the General shortcuts to change sections.
Numeric editors support adjustment navigation, wheel and dragging.

The page keeps changes in a draft until you select **Apply**. In a numeric
editor, **Keep** puts the new value in that draft and **Cancel** restores the
value from before the editor opened. **Cancel** on the settings page discards
the whole draft. Opening a binding editor by itself does not change the saved
binding.
The animated illustration uses the production tracking calculation with a
synthetic ascent, landing and fall; it doesn't run or predict gameplay.

## Profiles and presets

General selects Global or This map as the editing scope. The latter becomes
available after an attempt loads the map. Personal map profiles are keyed by
the normalized content root, so moving a local map to another folder gives it
a separate profile. Remove this map profile and Apply to resume inheritance.
Enable, 240 Hz, diagnostics, recommendations, device bindings and activation
modes are global.
Axis behavior, horizontal enable, look distance and hold timing belong to the
motion profile. A personal map profile takes precedence over author recommendations.

- **Classic** keeps the original asymmetric vertical tracking and symmetric
  horizontal retained band. Horizontal starts off in new and legacy settings.
- **Centered** uses Direct Follow on both axes.
- **Platformer Window** uses a 100 px vertical window, 60% vertical focus and
  a 120 px horizontal window.
- **Custom** indicates manual changes.

Selecting a preset keeps horizontal enable and manual-view settings.
Reset motion profile restores all motion settings to Classic. Axis resets and
control resets affect only their own section. These operations don't erase
bindings, Enable, 240 Hz or map rules.

## Independent axes

| Control | Meaning / range |
| --- | --- |
| Follow | Jump King, Direct Follow, Window or Screen |
| Focus | 15–85% from the top (Y) or left (X); 50% centers, 60% Y leaves more room above |
| Window size | Total free span, 0–240 game pixels, for Window mode |
| Upper/left band | Retained negative-direction band, 0–120 px, for Jump King mode |
| Lower/right band | Retained positive-direction band, 0–120 px, for Jump King mode |
| Up/left response | 2–60; higher catches up faster |
| Down/right response | Independent response in the opposite direction |
| Fast movement assist | Raises response during high-speed travel |
| Look ahead | 0–96 px of additional room in the direction of travel; zero disables it |
| Direction delay | 0–1 s before anticipation changes direction |
| Idle recenter | Optional return to focus after stopping; defaults off |
| Idle delay | 0–5 s before the optional return begins |

Jump King keeps a target band through an apex and small landing corrections.
Window checks the actual viewport and starts pulling at the free window's
edges. Direct Follow continuously targets the selected focus, with smoothing.
Screen follows the native logical screen on that axis.

Horizontal tracking still requires a usable side portal. Different follow
settings can't invent neighboring scenery or bypass a closed edge. Map/area
boundaries override focus, window and anticipation. Visibility guards override
slow following when needed; horizontal guards operate only toward usable sides
and don't prevent an obsolete portal view from settling out of sight.

Controls exposes the Look Up/Down edge margin (24–144 px), the Both threshold
(100–750 ms), and a three-action Binds page. The defaults are 48 px and 250 ms.
Look Up and Look Down inherit native menu bindings until overridden for the
current device. Focus defaults to F on the keyboard. Each action has a Mode
selector: Hold (the default), Press or Both. Hold ends on release, Press toggles,
and Both combines tap-to-toggle with temporary holding. The threshold applies
only to Both. Edits made here, including bindings and modes, stay in the settings
draft until Apply. The separate mod Binds page and Controls+ save immediately.

A map can disable Focus with `allow-focus=false` or both directions
with `allow-look=false`: use Parameter children of Screens/Zone in
`jk-runtime/mechanics.xml`, or attributes on SmoothCamera/Screens/Zone in
`props/smooth-camera/camera.xml`. Both default to true. Camera.xml parent bans
and denials from either file take priority. These rules clear forbidden latches
without changing saved bindings or native menu input; see the complete examples
in [map authoring](map-authoring.md#restrict-manual-camera-controls).

## Map rules and diagnostics

Map rules displays the current policy, screen ranges, rectangular zones,
recommendations and any validation error. Diagnostics overlays the focus,
tracking window, authored zone outlines and active rule during gameplay.
Camera rectangles use logical game pixels; UI remains fixed in the viewport.

Map recommendations can be declined without bypassing forced markers, native
areas, manual-control restrictions or coverage boundaries. See [map authoring](map-authoring.md).

Legacy `SmoothCamera.Settings.xml` files load with Classic motion, their existing
switches and device bindings. Settings use Runtime's atomic writes and backups.
Invalid or unsupported settings stay protected instead of being overwritten.
