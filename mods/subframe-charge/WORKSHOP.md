[h1]Subframe Charge[/h1]

[b]Requires JK Runtime 1.30+. Shared Harmony 2.3.6 is included.[/b]

[h2]Quarter-step Charge[/h2]
Optional quarter-frame hold steps through the native jump routine. Requires
Enabled; does not require 240 Hz or Subframe Inputs. Water scaling is applied
after rounding, retaining all intermediate underwater powers. Minimum/full
charge and snow rules are preserved. Buffered holds use quarter steps from native
charge acceptance, preserving release/max timing, ice drift and native airborne
leniency. Jump% shows exact
fractional frames and scaled percentages. Off by default for existing settings.
This option adds new trajectories and marks the run as modified even on maps
that allow ordinary Subframe Charge timing correction.

[h2]Optional responsiveness features[/h2]
[list]
[*]Subframe Inputs: retain short presses for all eleven native pad actions,
including directions, equipment and menu buttons. Keyboard/mouse actions use a
1 ms polling target; other native pads are sampled between physics updates.
Controls+ bindings and chords remain available.
[*]240 Hz: more presentation frames and predicted king
position without an extra interpolation frame of delay. It requires Subframe
Inputs. Turning Inputs off also turns 240 Hz off.
[/list]
Physics, jump strengths and world animation timing retain their original cadence.
This is not 240 Hz physics. Visible frame rate depends on the display, VSync and
GPU. Prediction clips against native floors, walls, ceilings and slopes without
changing physics or adding an interpolation frame. Unknown collision shapes and modified collision methods keep smoothing active.
Unsupported geometry can cause a brief visual correction; actual physics stays native.
Prediction follows material-modified movement, including water.
Startup Space remains native; actual menus use the fast input path. Smooth Camera 0.5.4+ shares
clock ownership; older builds need updating. Arbitrary mods that update gameplay
inside Draw or bypass the native input API need separate compatibility work.
The new options default to off when upgrading existing settings.

Same jump strengths. More consistent charge timing.

Subframe Charge measures how long you hold Jump between game updates and
rounds that duration to the nearest native charge step. You still choose the
position, direction and timing. The mod does not aim, solve jumps or change
the flight physics.

[h2]What happens in vanilla?[/h2]
Vanilla reads your Jump input on game updates. Your press and release can
happen between those updates, so the result depends on both how long you hold
the button and where that hold falls relative to the game's ticks.

Imagine holding Jump for 171 ms. On a perfectly regular 17 ms update clock,
that hold can contain either ten or eleven input checks. Most starting
positions give ten; a small part of the cycle gives eleven. The same hold
duration can therefore produce two neighboring jump strengths.

Players often call this RNG. More precisely, it is frame-phase-dependent
input sampling: there does not need to be a random-number generator choosing
your jump. The difficulty is that you do not normally know the exact phase
of that clock when you press the button.

The installed Windows game's update target is 17 ms, approximately 58.82
updates per second, even though its physics advances by 1/60 second per
update. That is why SFC uses 17 ms for input timing, not 16 ms or 16.67 ms.
This is a scheduling target, not a promise that every real update arrives
exactly 17 ms after the previous one.

[h2]Are frame-perfect jumps really 50/50?[/h2]
[b]No. There is no general 50% ceiling.[/b]

Here, a "1f window" means that only one charge level makes the intended jump.
It does not mean a minimum-power jump.

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

Real devices and game updates are not perfect clocks. Physical button timing,
device reports and Windows scheduling can differ, so "exactly 170 ms always
works" is not an unconditional guarantee for a real keyboard or controller.

[h2]What does SFC change?[/h2]
SFC samples supported controls between game updates, measures the hold and
selects the nearest charge step. The native jump code then performs takeoff.

For the same ordinary 10f example, a reliable measured hold from 161.5 ms up
to, but not including, 178.5 ms selects 10f. At the upper boundary it rounds
up to 11f. The triangular probability window becomes a fixed 17 ms timing
window: inside it you select 10f; outside it you select another level.

This does not create new dry-land jump strengths or increase the game's FPS.
It changes which existing strength your input selects. Native direction
handling, material scaling and movement after takeoff remain in use. The
native release-update increment is retained, so a displayed frame count is
not simply the final charge timer divided by 1/60 second.

Takeoff still happens on a game update. SFC improves charge measurement; it
does not remove all input latency. Near a rounding boundary, small variations
in device reporting or sampling can still change the result.

Very short, reliably captured eligible taps produce a minimum jump rather
than disappearing between updates. Minimum and automatic-maximum jumps have
their own limits; the centered-window example above describes an ordinary
intermediate charge level.

[h2]Why I do not consider it a cheating tool[/h2]
I made SFC because I want the challenge to be choosing and executing the right
input, rather than having that input interpreted differently depending on
its alignment with an unseen update clock.

Vanilla's sampling can work both against you and in your favor. It can turn
a nearly correct hold into the wrong jump, but it can also rescue a hold
outside the nearest timing window. SFC removes both effects.

For example, if you need 10f but hold for 180 ms, vanilla can still give you
10f about 41% of the time in the ideal model. SFC selects 11f for a measured
180 ms hold. It does not know that you wanted 10f and does not choose the
result that helps you reach the platform.

That is why I see it as an input-consistency and quality-of-life mod, not
an auto-play tool. [b]That does not mean it has no competitive advantage.[/b]
A precise player can become substantially more consistent, and maps built
around repeated 1f windows can become much easier. Removing favorable and
unfavorable uncertainty does not guarantee that they cancel out for every
player or every map.

Vanilla timing skill is real. Players may also learn useful rhythmic or
audio cues. SFC changes the input rules, including how useful adaptation to
the original sampling behavior is. It does not make achievements under those
original rules any less impressive.

[b]Please disclose SFC use in speedruns, difficult-map clears and record
submissions, and follow the relevant community or category rules.[/b]
My design philosophy is not a ruling on what a leaderboard must allow.
An SFC-assisted clear should not be presented as an unmodified vanilla clear.

[h2]Settings and measurements[/h2]

[b]Enabled[/b] applies correction. With it off, the mod still measures input
but leaves native jumping unchanged.
[b]Show SFC[/b] displays timing beside the frame count in Last Jump Value / Jump%.

The overlay shows [code]SFC: 200 ms[/code], [code]SFC: Buffered[/code], or
[code]SFC: Not supported[/code] when reliable timing is unavailable.
Before a measurement exists, it shows a dash. With correction disabled,
[code](would 15f)[/code] appears only when the predicted frame count differs.
Automatic maximum jumps show press-to-takeoff time with [code](max)[/code].

Jump% is optional. Its SFC timing display is not an always-on enabled-status
badge: measurements also work with correction off, and the display can be
hidden. A timing number alone does not establish which rules a run used.

With Quarter-step Charge off, landing buffers retain native power. With it on,
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

A 1 ms polling target does not guarantee 1 ms hardware precision. Device report
rates, drivers and Windows scheduling still matter. Unreliable evidence leaves
the native jump intact. Release Jump after connecting or reconnecting a device
before starting a measured hold.

Casual Jumping's Vanilla and Casual modes can use SFC. Casual+ and Ball King's
ball jumps use their own controllers.

[h2]For level authors[/h2]
Without a map opt-in, enabled correction uses the game's normal
modified-player-behaviour flag. Measurement-only mode does not add that flag.

Add [code]AllowSubframeCharge[/code] to permit correction on your map without
SFC adding its own modified-run restriction. This does not clear restrictions
from other mods or override a community's record-submission rules.
