# Graphics+

Drag the Jump King window to any size while keeping the game in its original
4:3 proportions. Version **1.0.4** needs **JK Runtime 2.8.0+**.

Open **Options > Graphics** and turn on **Free scaling**. It starts off.
All settings live here; Graphics+ adds no settings to its own mod menu.
The game enters a resizable window. The native screen-mode selector and 1x/2x/3x
selector stay visible but turn grey and cannot be changed until Free scaling
is off. Turning it off restores the previous window mode and size.

**Pixels** selects the final filter while Free scaling is on:

- **Sharp** keeps pixel interiors flat and blends only their boundaries. This
  is the default and gives more consistent widths at fractional scales.
- **Nearest** keeps hard edges, with uneven pixel widths at fractional scales.

The minimum client size is 480x360. Other window shapes get black margins.
The normal free window's size and position are remembered automatically, including
moves to another monitor. There is no extra switch. The next launch or enable
restores that placement. If a monitor is disconnected, the window is kept inside
an available screen's working area. Maximizing or minimizing doesn't replace the
saved normal placement.

Window placement and the filter are saved in `FreeScaling.Settings.xml` beside
the mod. Free scaling keeps the native screen-mode and integer-size preferences.

Smooth Camera **1.1.0+** uses the same Runtime filter and preserves fractional
camera movement. Adjacent captured screens are joined before sampling so their
shared edges can blend correctly. Preview/capture paths keep their own sampling.
Native scene flips use Runtime's shared frame style.

**Smooth Rendering** adds high-rate drawing and movement/menu polling without
changing native Jump snapshots, charge strength or physics cadence. It shares
one saved choice and one Runtime implementation with Subframe Charge 1.1.0+;
changing either menu updates both. SFC's separate All Inputs mode can still opt
into buffered Jump input, and Inputs + Physics can supply actual fractional
positions. Neither mode is enabled by the Graphics checkbox.
If SFC is installed, use version 1.1.0 or newer; older versions own separate
input and drawing hooks.

Smooth Rendering remains available when Free scaling is off.

Windows may pause rendering while a window border is being dragged. The new
backbuffer size is applied when the drag ends; this doesn't change simulation
timing or run rules.

## Build and install

With Jump King installed, run from the repository root:

```powershell
scripts/check-mods.ps1 -Mod free-scaling,smooth-camera,subframe-charge -Integration
mods/free-scaling/install.ps1
```

Close the game before installing. The installer updates Runtime and the selected
mods, preserves settings, and uses existing Workshop folders when present.
The package is `build/free-scaling/UPLOAD_TO_WORKSHOP/`. Runtime is a separate
dependency. See [release history](CHANGELOG.md) and [notices](THIRD_PARTY_NOTICES.md).
