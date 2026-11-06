[h1]Subframe Charge[/h1]

[b]Version 0.28.1[/b]

[b]Requires JK Runtime 2.0.1+. Shared Harmony 2.3.6 is included.[/b]

Same jump physics, more consistent charge timing. SFC measures how long you hold
Jump between game updates and selects the nearest native charge step. You still
choose the position, direction and timing. The mod does not aim, solve jumps or
change flight physics.

[h2]Substep Charge[/h2]
Turn on Enable Substep Charge, then choose Half-step (8.5 ms), Quarter-step
(4.25 ms), Eighth-step (2.125 ms) or 1 ms. Quarter-step is the default selection;
existing settings keep their enabled state. The selector is disabled while
substeps or Enabled are off. Neither 240 Hz nor Subframe Inputs is required.

Water scaling follows rounding. Minimum/full charge, snow thresholds, native
release/max timing, ice drift and airborne leniency are preserved. Buffered
holds start at native charge acceptance. Jump% reports fractional frames and
scaled percentages. Debug Mode's Prompted Jump uses the same effective step.
Player-enabled substeps change trajectories and mark the run as modified even
on maps that allow ordinary correction. Map-authored substeps use map attribution.
Map authors can choose screen/zone intervals from 0.1 through 17 ms.

[h2]Optional responsiveness features[/h2]
[list]
[*]Subframe Inputs: keep short presses for all eleven native pad actions,
including directions, equipment and menu buttons. Keyboard/mouse actions use a
1 ms polling target; other native pads are sampled between physics updates.
Controls+ bindings and chords remain available.
[*]240 Hz: more presentation frames and predicted king
position without an extra interpolation frame of delay. It requires Subframe
Inputs. Turning Inputs off also turns 240 Hz off.
[/list]
Physics, jump strengths and world animation timing keep their original cadence.
Visible frame rate depends on the display, VSync and
GPU. Prediction clips against native floors, walls, ceilings and slopes without
changing physics or adding an interpolation frame. Unknown collision shapes and modified collision methods keep smoothing active.
Unsupported geometry can cause a brief visual correction; actual physics stays native.
Prediction follows material-modified movement, including water.
Startup Space remains native; actual menus use the fast input path. Smooth Camera 0.5.4+ shares
clock ownership; older builds need updating. Arbitrary mods that update gameplay
inside Draw or bypass the native input API need separate compatibility work.
The new options default to off when upgrading existing settings.

[h2]What happens in vanilla?[/h2]
Vanilla reads your Jump input on game updates. Your press and release can
happen between those updates, so the result depends on both how long you hold
the button and where that hold falls relative to the game's ticks.

Imagine holding Jump for 171 ms. On a perfectly regular 17 ms update clock,
that hold can contain either ten or eleven input checks. Most starting
positions give ten; a small part of the cycle gives eleven. The same hold
duration can therefore produce two neighboring jump strengths.

Players often call this RNG. More precisely, it is frame-phase-dependent
input sampling: there doesn't need to be a random-number generator choosing
your jump. The difficulty is that you don't normally know the exact phase
of that clock when you press the button.

The installed Windows game's update target is 17 ms, approximately 58.82
updates per second, even though its physics advances by 1/60 second per
update. That is why SFC uses 17 ms for input timing, not 16 ms or 16.67 ms.
Actual update intervals vary with system scheduling.

[h2]Are frame-perfect jumps really 50/50?[/h2]
[b]No. There is no general 50% ceiling.[/b]

Here, a "1f window" means that only one charge level makes the intended jump.
It doesn't mean a minimum-power jump.

Take a jump that needs exactly 10f. With perfectly regular 17 ms ticks and
an equally likely starting phase:
[list]
[*]169 ms gives 10f about 94% of the time and 9f about 6%.
[*]Exactly 170 ms gives 10f every time in this ideal model.
[*]171 ms gives 10f about 94% of the time and 11f about 6%.
[*]178.5 ms gives a 50/50 split between 10f and 11f.
[/list]
The chance of getting 10f rises from zero at 153 ms to 100% at 170 ms, then
falls back to zero at 187 ms. It is a triangle, not a flat 50% chance. More
accurate timing can absolutely produce a higher success rate in vanilla.

For example, a player whose holds are uniformly spread across a 17 ms range
centered on 170 ms would average 75% success under these assumptions. A more
tightly centered timing distribution can do better. These are mathematical
examples, not measured success rates for real players.

Real devices and game updates aren't perfect clocks. Physical button timing,
device reports and Windows scheduling can differ, so "exactly 170 ms always
works" isn't an unconditional guarantee for a real keyboard or controller.

[h2]What does SFC change?[/h2]
SFC samples supported controls between game updates, measures the hold and
selects the nearest charge step. The native jump code then performs takeoff.

For the same ordinary 10f example, a reliable measured hold from 161.5 ms up
to, but not including, 178.5 ms selects 10f. At the upper boundary it rounds
up to 11f. The triangular probability window becomes a fixed 17 ms timing
window: inside it you select 10f; outside it you select another level.

This doesn't create new dry-land jump strengths or increase the game's FPS.
It changes which existing strength your input selects. Native direction
handling, material scaling and movement after takeoff remain in use. The
native release-update increment is retained, so a displayed frame count is
not simply the final charge timer divided by 1/60 second.

Takeoff still happens on a game update. SFC improves charge measurement; it
doesn't remove all input latency. Near a rounding boundary, small variations
in device reporting or sampling can still change the result.

Very short, reliably captured eligible taps produce a minimum jump rather
than disappearing between updates. Minimum and automatic-maximum jumps have
their own limits; the centered-window example above describes an ordinary
intermediate charge level.

[h2]Why I don't consider it a cheating tool[/h2]
I made SFC because I want the challenge to be choosing and executing the right
input, rather than having that input interpreted differently depending on
its alignment with an unseen update clock.

Vanilla's sampling can work both against you and in your favor. It can turn
a nearly correct hold into the wrong jump, but it can also rescue a hold
outside the nearest timing window. SFC removes both effects.

For example, if you need 10f but hold for 180 ms, vanilla can still give you
10f about 41% of the time in the ideal model. SFC selects 11f for a measured
180 ms hold. It doesn't know that you wanted 10f and doesn't choose the
result that helps you reach the platform.

That is why I see it as an input-consistency and quality-of-life mod, not
an auto-play tool. [b]That doesn't mean it has no competitive advantage.[/b]
A precise player can become substantially more consistent, and maps built
around repeated 1f windows can become much easier. Removing favorable and
unfavorable uncertainty doesn't guarantee that they cancel out for every
player or every map.

Vanilla timing skill is real. Players may also learn useful rhythmic or
audio cues. SFC changes the input rules, including how useful adaptation to
the original sampling behavior is. It doesn't make achievements under those
original rules any less impressive.

[b]Please disclose SFC use in speedruns, difficult-map clears and record
submissions, and follow the relevant community or category rules.[/b]
My design philosophy isn't a ruling on what a leaderboard must allow.
An SFC-assisted clear shouldn't be presented as an unmodified vanilla clear.

[h2]Settings and measurements[/h2]

[b]Enabled[/b] applies correction. With it off, the mod still measures input
but leaves native jumping unchanged.
[b]Show SFC[/b] displays timing beside the frame count in Last Jump Value / Jump%.

The overlay shows [code]SFC: 200 ms[/code], [code]SFC: Buffered[/code], or
[code]SFC: Not supported[/code] when reliable timing is unavailable.
Before a measurement exists, it shows a dash. With correction disabled,
[code](would 15f)[/code] appears only when the predicted frame count differs.
Automatic maximum jumps show press-to-takeoff time with [code](max)[/code].

Jump% is optional. Its SFC timing display isn't an always-on enabled-status
badge: measurements also work with correction off, and the display can be
hidden. A timing number alone doesn't establish which rules a run used.

With Substep Charge off, landing buffers keep native power. With it on,
buffered hold time starts at native charge acceptance and the fractional result
is applied only on the native release tick. The display adds [code](buffered)[/code].
Underwater scaling, automatic maximum and vanilla ice jump leniency are preserved.

[h2]Diagnostics[/h2]
Unsupported charge measurements automatically record recent input evidence in
SubframeCharge.log beside the mod. Normal jumps stay in a bounded memory buffer;
incident windows are written in the background. Logs rotate at 16 MiB, retaining
three files. Include these logs when reporting a rare Not supported message.

[h2]Devices and compatibility[/h2]
Supports keyboard, mouse buttons, XInput and native DirectInput controls,
including primary/secondary bindings and Controls+ chords. Steam Input's
virtual Xbox controllers use the XInput path.

A 1 ms polling target doesn't guarantee 1 ms hardware precision. Device report
rates, drivers and Windows scheduling still matter. Unreliable evidence leaves
the native jump intact. Release Jump after connecting or reconnecting a device
before starting a measured hold.

Casual Jumping's Vanilla and Casual modes can use SFC. Casual+ and Ball King's
ball jumps use their own controllers.

[h2]For level authors[/h2]
Without a map opt-in, enabled correction uses the game's normal
modified-player-behaviour flag. Measurement-only mode doesn't add that flag.

Add [code]AllowSubframeCharge[/code] to permit correction on your map without
SFC adding its own modified-run restriction. This doesn't clear restrictions
from other mods or override a community's record-submission rules.

[h2]Map-controlled activation[/h2]
Requires JK Runtime 1.44+. Add these exact Tags entries to level_settings.xml for the mechanics your map controls:
[list]
[*]JKRuntime.MapControlled:subframe-charge.timing
[/list]
Opaque screen markers (RGB; all are nonblocking):
[list]
[*]Charge correction: On (173,107,211), Off (173,108,211), Local (173,109,211).
[/list]
On allows correction, Off blocks it, and Local allows only a local trigger or XML zone. On a tagged map, unmarked screens are off and the global switch is locked. Entering saves that switch as Off; leaving doesn't turn it back on.
[b]Use either a screen pixel or an XML Screens rule. You don't need both.[/b] If both exist, they must agree. XML also supports ranges, zones and parameters; it never overrides a pixel. Neither form adds the separate map tag.
Put XML rules in jk-runtime/mechanics.xml at the map root. Screens start at 1 and zones use 480 x 360 local coordinates. See docs/map-authoring.md for the full example.
