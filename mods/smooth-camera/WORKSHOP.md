# Smooth Camera

[b]Version 0.8.0[/b]

A configurable continuous camera for Jump King. Follow the king across adjoining
screens with smooth acceleration, while physics and native screen events keep
their original behavior.

The main and pause menus have **Enable** and **Settings**. Pick vertical and
horizontal tracking separately: Direct Follow, a free movement Window, original
Jump King framing or native Screen framing. Adjust focus, window size, response,
anticipation, fast-motion help and optional idle return. Classic, Centered and
Platformer Window presets give you a quick starting point, and each map can have
its own profile.

Settings includes an animated preview, optional gameplay diagnostics, mouse and
controller support. Changes and Focus bindings remain in a draft until Apply;
Cancel discards them. Numeric values support navigation, wheel and dragging.

Tap Up/Down to latch a look above/below the king; hold for a temporary view.
Focus defaults to F on keyboard and centers the native screen. Change its
binding in Settings > Controls or Controls > Smooth Camera. The look distance
and tap/hold threshold are configurable (48 px and 250 ms by default).

Horizontal starts off. Enabling it reveals destinations beyond usable native
side exits, including one-way links. Solid edges hide their side views. Native
characters on neighboring screens are drawn without advancing their gameplay.

240 Hz is on by default and requests extra presentation frames, limited by the
display and hardware. Physics and input keep their native timing. Turning it off
uses native camera presentation cadence; player animation isn't interpolated
by Smooth Camera.

Maps can restrict camera use to marked areas or persistently disable Enable on
entry. Authored smooth screen ranges and rectangular zones **force** camera use,
even when Enable is off. Leaving those areas restores the underlying preference.
Map coverage boundaries prevent native-only neighbors from being shown early.
Author motion recommendations and personal map profiles are supported.
See the packaged map-authoring guide for tags, priorities and XML examples.

Requires **JK Runtime 1.34+ (1.x)** and the included shared Harmony engine.
Mega Mapping Expansion 0.3+ supports camera composition. Older or incompatible
compositors fall back to native framing. An interactive playtest remains useful
for custom map artwork and third-party render mods.

[h2]Map-controlled activation[/h2]
Requires JK Runtime 1.34+. Add these exact Tags entries to level_settings.xml for the mechanics your map controls:
[list]
[*]JKRuntime.MapControlled:smooth-camera.tracking
[/list]
Opaque screen markers (RGB; all are nonblocking):
[list]
[*]Camera tracking: On (173,83,211), Off (173,84,211), Local (173,85,211).
[/list]
On allows smooth tracking, Off blocks it, and Local allows only a local trigger or XML zone. On a tagged map, unmarked screens are off and the global switch is locked. Entering saves that switch as Off; leaving doesn't turn it back on.
[b]Use either a screen pixel or an XML Screens rule. You don't need both.[/b] If both exist, they must agree. XML also supports ranges, zones and parameters; it never overrides a pixel. Neither form adds the separate map tag.
Put XML rules in jk-runtime/mechanics.xml at the map root. Screens start at 1 and zones use 480 x 360 local coordinates. See docs/map-authoring.md for the full example.
