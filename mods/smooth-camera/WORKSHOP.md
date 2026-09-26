# Smooth Camera

A continuous vertical camera for Jump King. Follow the king smoothly as he
climbs and falls, with neighboring screens joined into one moving view.

Use the **Smooth Camera** checkbox in the main or pause menu.
Uncheck it to restore the native fixed screens.
Tap the standard Up/Down bindings to latch a view above/below the king. Tap either
direction again to return to normal tracking. Hold instead for a temporary look;
release restores the previous view. The tap/hold threshold is 250 ms.
Use **F** (change it with **Bind Focus** in Smooth Camera settings, or in Controls)
to use the same tap/hold behavior for the
active native screen. Focus settles on the original screen
framing, including its horizontal position. Camera transitions remain smooth.
The separate **Horizontal** checkbox defaults to off.
Side views stay hidden behind solid edges. An opening must span at least two
adjacent collision cells (16 game pixels) to reveal that side's destination.
Enable it to reveal destination screens beyond side exits. Horizontal following
fades in as a teleport screen enters the view from above/below, and settles back
when leaving. Side teleports retain continuous framing, including one-way links.
Native characters on neighboring screens remain visible without running their
gameplay logic early.
The camera stops at map boundaries and snaps after distant teleports.
Movement physics and native screen events stay unchanged.
Small descents around the jump's apex and landing no longer pull the camera
down immediately. Smooth acceleration and braking reduce abrupt direction changes.
The **240 Hz** checkbox enables presentation up to 240 FPS (on by default), limited by your display and hardware,
while gameplay keeps its original update rate. Intermediate frames reuse the
world rendering, and enlarged views move in finer display-pixel increments.
Uncheck it to use smooth following at the native game cadence. This avoids
camera-only intermediate frames, which can make the king visibly step relative
to the camera during fast falls. Camera controls and physics remain unchanged.

Requires **JK Runtime 1.30+** and the included shared Harmony engine.
This is an experimental release. Custom render mods may need adapters;
Enabled Mega Mapping Expansion causes an automatic fallback to native screens;
uncheck its checkbox to use smooth following.
