# Subframe Charge

Catch Jump presses and releases between native game updates. Subframe Charge
measures the real hold, then releases with the matching strength through the
game's normal jump routine. Choose charge correction alone, responsive input,
or responsive input with fractional player physics.

## Install and try it

**1.0.2** needs [JK Runtime 2.3.2+](../jk-runtime/README.md) and the shared
Harmony 2.3.6 supplied in the package. Update both packages for the fractional
movement and material contact fixes.
For a manual install, close Jump King and copy the release package to
`Jump King/Content/JKMods/`, with Runtime installed separately.

Open **Mods > Subframe Charge**. **Enabled** controls charge correction.
Try a short press: a completed tap between updates becomes a minimum jump
instead of disappearing. Water scaling, direction and the release increment
stay native. Other movement controllers can reserve jumping and suspend correction.

## Settings

| Switch | What it changes |
| --- | --- |
| Enabled | Correct jump strength from measured hold time. |
| Substep Charge | Choose Off, Half (8.5 ms), Quarter (4.25 ms), Eighth (2.125 ms) or 1 ms; requires Enabled. |
| Mode | Charge keeps native input/physics; All Inputs samples controls between native ticks; Inputs + Physics also advances the player in smaller steps. |
| Smooth Rendering | Available in All Inputs and Inputs + Physics. Draw real positions with fractional physics, or predicted positions with native physics. Disabled in Charge. |
| Show SFC | Show timing beside Jump%, or on its own when Jump% is absent or disabled. It can measure without correcting. |

Runtime owns the separate **Optimizations** setting. Inputs, presentation and
charge correction have separate jobs. The display, VSync, device and driver can
still limit the result. Physics selection waits until the player stands still
on stable ground with movement and Jump released, outside a charge or external
controller. It applies during the same attempt and marks the run modified.
Unsupported combinations are recorded in `SubframeCharge.log`.
Read
[all settings](docs/settings.md) for timing, buffering and render limits.

### Substep Charge

Substep Charge starts Off. Existing preferences keep their enabled state and
selected interval. Substeps provide intermediate jump strengths
and change gameplay. They work in every Mode and don't require Smooth
Rendering. Player-enabled substeps mark
the run modified even when the map permits ordinary correction. Read
[substep rules](docs/settings.md#substep-charge). Debug Mode's Prompted Jump uses
the same effective interval, including map-authored steps.

## Input and compatibility

Keyboard, mouse buttons, XInput and native DirectInput devices use Runtime's
bindings and chords. A 1 ms polling target isn't a hardware guarantee.
Casual+ and Ball jumps use their own controllers. Unknown replacement jump
states keep native ownership. See [device and movement compatibility](docs/compatibility.md).

When Controls+ converts Jump to Press, its logical toggle runs at native cadence;
physical-tap replay and hold correction are suspended. Fractional physics
requires Hold for Left, Right and Jump. Custom block geometry and foreign
components no longer disable it merely by being present. Mod tick callbacks
retain native cadence; stateful material queries need adaptation. Read the
compatibility guide before using a different mod build. During Expansion's two-client Debug test,
local fractional simulation is suspended; saved choices are retained.

## Map-controlled activation

Map authors can permit correction or require it in screens and areas through
Runtime. Tags, opaque markers and XML serve different roles. A controlled map
turns the global switch off on entry and leaves it off afterward. Read
[map authoring](docs/map-authoring.md) for exact colours and complete files.

## Diagnostics

Detailed jump diagnostics are available in every normal build and start off.
Create `SubframeCharge.Diagnostics.enabled` beside the installed DLL and restart
to record input/charge history around unsupported launches. Remove the marker
after collecting the logs. See [jump diagnostics](docs/diagnostic-build.md).

`SubframeCharge.log` is beside the loaded DLL. For Workshop item 3794767640:

`steamapps/workshop/content/1061090/3794767640/SubframeCharge.log`

Play with the affected device, close the game and send the log with the rough
time of the problem. Include rotated logs if available. Logs contain Jump
timing, device identifiers, bindings and build IDs, not general keyboard typing.
They aren't uploaded automatically.

See [timing and input details](docs/timing.md) and
[testing](docs/testing.md) for limitations and reproduction steps.

## Build

Packages use the current Runtime SDK. Run from the repository root:

```powershell
.\mods\subframe-charge\build.ps1
```

Output: `build/subframe-charge/UPLOAD_TO_WORKSHOP/SubframeCharge.dll`.
For a local installation, use `install.ps1` with the required Runtime.

Read [testing](docs/testing.md) for focused and integration checks.
The [guide index](docs/index.md) links settings, timing and diagnostics.
