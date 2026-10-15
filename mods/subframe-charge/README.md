# Subframe Charge

Catch Jump presses and releases between native game updates. Subframe Charge
measures the real hold, then releases with the matching strength through the
game's normal jump routine. Ordinary correction uses 17 ms steps; physics keeps
its native cadence.

## Install and try it

**0.28.1** needs [JK Runtime 2.0.1+](../jk-runtime/README.md) and the shared
Harmony 2.3.6 supplied in the package.
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
| Enable Substep Charge | Enable fractional jump strengths; requires Enabled. |
| Charge Step | Choose Half-step (8.5 ms), Quarter-step (4.25 ms), Eighth-step (2.125 ms) or 1 ms; requires Enable Substep Charge. |
| Show SFC | Show timing beside Jump%; it can measure without correcting. |
| Subframe Inputs | Sample native controls between simulation updates, including menus. |
| 240 Hz | Request up to 240 FPS presentation and predicted player drawing; requires Subframe Inputs. |

Runtime owns the separate **Optimizations** setting. Inputs, presentation and
charge correction have separate jobs. The display, VSync, device and driver can
still limit the result. **240 Hz doesn't mean 240 Hz physics.** Read
[all settings](docs/settings.md) for timing, buffering and render limits.

### Substep Charge

Quarter-step is the default selection. Existing quarter-step preferences keep
their enabled state and strength. Substeps provide intermediate jump strengths
and change gameplay. They don't require Subframe Inputs or 240 Hz. Player-enabled substeps mark
the run modified even when the map permits ordinary correction. Read
[substep rules](docs/settings.md#substep-charge). Debug Mode's Prompted Jump uses
the same effective interval, including map-authored steps.

## Input and compatibility

Keyboard, mouse buttons, XInput and native DirectInput devices use Runtime's
bindings and chords. A 1 ms polling target isn't a hardware guarantee.
Casual+ and Ball jumps use their own controllers. Unknown replacement jump
states keep native ownership. See [device and movement compatibility](docs/compatibility.md).

When Controls+ converts Jump to Press, its logical toggle runs at native cadence;
physical-tap replay and hold correction are suspended. During Expansion's
two-client Debug test, Subframe Inputs and 240 Hz are temporarily inactive;
saved choices return when the test stops.

## Map-controlled activation

Map authors can permit correction or require it in screens and areas through
Runtime. Tags, opaque markers and XML serve different roles. A controlled map
turns the global switch off on entry and leaves it off afterward. Read
[map authoring](docs/map-authoring.md) for exact colours and complete files.

## Diagnostics

Detailed jump diagnostics are included in every normal build. No replacement DLL
or diagnostic switch is needed. Unsupported launches save recent input/charge
history and the next 30 player frames. See [automatic diagnostics](docs/diagnostic-build.md).

`SubframeCharge.log` is beside the loaded DLL. For Workshop item 3794767640:

`steamapps/workshop/content/1061090/3794767640/SubframeCharge.log`

Play with the affected device, close the game and send the log with the rough
time of the problem. Include rotated logs if available. Logs contain Jump
timing, device identifiers, bindings and build IDs, not general keyboard typing.
They aren't uploaded automatically.

See [timing and input details](docs/timing.md) and
[testing](docs/testing.md) for limitations and reproduction steps.

## Build

Packages use the current Runtime 2.0 SDK. Run from the repository root:

```powershell
.\mods\subframe-charge\build.ps1
```

Output: `build/subframe-charge/UPLOAD_TO_WORKSHOP/SubframeCharge.dll`.
For a local installation, use `install.ps1` with the required Runtime.

Read [testing](docs/testing.md) for focused and integration checks.
The [guide index](docs/index.md) links settings, timing and diagnostics.
