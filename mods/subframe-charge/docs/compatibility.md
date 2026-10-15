# Input devices and other movement mods

Keyboard, mouse buttons, Xbox/XInput and native DirectInput controls are
supported through Runtime. Steam Input's virtual Xbox devices use XInput.
Current primary/secondary bindings and Controls+ chords are respected.

A 1 ms polling target doesn't guarantee 1 ms hardware accuracy. Device report
rates, drivers, focus loss and scheduling affect available evidence.
Unsupported measurements leave the native jump intact.

Subframe Charge includes a native 1 ms preset. The third-party
Millisecond Charge 0.1.0 addon targets the older quarter-step implementation;
its private patch is no longer applicable. Use **Enable Substep Charge > Charge
Step > 1 ms** instead.

Water keeps intermediate half-strength steps. The native ice continuation
and direction buffer remain available. Casual Jumping's Vanilla and Casual
modes can use SFC; Casual+ and Ball King's ball jumps use their own controllers.
Runtime suspends the charge policy for any controller reserving native
jumping, then restores it without changing SFC's enabled setting.
SFC yields to an undeclared replacement JumpState instead of treating that
intentional ownership as a missing-node crash. Malformed native graphs still fail.

For ConveyorBlockMod (Workshop 3330536917), Runtime adapts
the conveyor's exact-type jump lookup on belt exit while
preserving belt momentum and native reset conditions. SFC discards
cancelled charge evidence when a block resets the active jump. Updating SFC alone
doesn't supply the Runtime adapter.

Player-enabled ordinary correction marks the run modified unless the map has
`AllowSubframeCharge`. Map-authored correction uses authored attribution.
Measurement-only mode doesn't add that mark.
Switching off both settings also removes the hidden measurement
node, trajectory probe and high-rate sampler; **Show SFC** can explicitly restore
measurement while correction remains off.
