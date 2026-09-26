# Air Dash

The component order is body -> Warp transition -> native input -> Dash observer
-> native behavior tree. This lets Dash consume the newly refreshed native jump
token before JumpState can use it. Only a shared activation edge is consumed. During
the dash the input component and behavior tree remain enabled; the next edge
can therefore become an ordinary held buffer. A separate Dash binding never
consumes the independently held Jump buffer. There is no hardcoded Space key,
high-rate sampler or replacement charge formula.

`DashBindings` explicitly registers a chord-aware Controls+ row and stores
physical chords per native device save identifier. Missing profiles dynamically
follow native Jump; resetting a row removes that override. Keyboard/mouse and
controller button namespaces are separate. `DashInput` polls only custom-bound
devices and uses cached native press state for default profiles. Each device has
its own edge history, independent of ControllerManager.GetMain(). It observes
holds while grounded or unavailable, resets on pause/reconnect/restore/rebind,
and expands Controls+ virtual Jump chords before checking shared activation.
A simultaneous separate custom Dash wins over a default Jump on another device,
leaving that Jump buffered. Driver-read errors suppress the custom action and
log once until input recovers; they do not consume native input.

The body hook runs at BeforeWind, after SFC's pre-wind policy. It returns early
only during active dash ticks. The native collision cache still runs. Dash sets
a horizontal 6 px/tick impulse and records it as LastVelocity. Each movement
tick sweeps at most six one-pixel steps, applying native screen caps and side
teleports and checking native collision plus block-specific X checks. An impact
calls the actual ResolveXCollisionBehaviour and bump sound. Flat walls use
PlayerValues.BOUNCE; slopes use native projection. The normal body pipeline
resumes next tick. Dry and underwater dash distance are both 80 pixels.
At 6 px/tick this takes 13 full steps and one 2-pixel step: 14 ticks in total.
Direction is chosen once at activation: nonzero current dpad X wins, then
horizontal velocity, then facing. Opposite input can reverse the dash; changing
direction during an already active dash does not bend its path.

The availability observer remembers the last supported Solid contact and latches
it only for a jump, including a tiny jump whose vertical velocity has already
turned down by the first airborne update. Zone and Screen permissions are
queried at activation. All sources use one spent flag. Landing refreshes it;
entering another zone in the same flight does not. Map-authored permission wins
over the global checkbox for run attribution.

The tunable constants are AirDashController.Distance, Speed and ExitSpeed. Distance is
independent of speed, with the last step clamped if those values stop dividing
evenly. Free completion sets (direction * 3, 0), half the dash speed, after the
final displacement. LastVelocity still records that tick's 6 px/tick dash impulse.
Native gravity and material/wind effects resume next tick; the exit reduction
is applied only once, with no ongoing post-dash deceleration or speed cap. Collision keeps the
native response instead. Cancelling an unfinished Dash restores captured velocity; handing off
to another movement controller leaves that controller's velocity untouched.
Cancellation leaves the dash spent for that flight and does not consume an
unrelated buffered press.

The state participant stores remaining distance, direction, saved velocity,
ground contact, Solid grant and spent/global flags. Presentation tails are not
gameplay state: restoring an active dash restarts its short visual trail. The
wrapper draws the current native sprite over outfit-aware echoes and a pixel
wake; it tracks new native poses in LateUpdate and removes only its own wrapper.
Warp capture can unwrap this owned effect without treating it as a foreign skin.

The 300 ms audio edit is an embedded resource, loaded once per level through
native JKSound as SFX. The level scope owns its voice and NativePause lease.
Actual first-tick movement triggers one attack; observing input alone does not.
Restoring a partial dash stops the old sound without replaying its attack.
Cancellation and level teardown stop it. Native preferences control mute and
master volume; no separate mixer or file lookup runs during a dash. A rapid
retrigger replaces a still-busy native voice from cached WAV bytes, avoiding
MonoGame's asynchronous Stop/Play race without blocking a gameplay tick.

The focused tests cover exact free-flight displacement, water, both directions,
ascending/falling momentum, native input token order, one dash per flight, thin
walls and half-impulse bounce, scope, cancellation and snapshots. Eight native
continuation ticks after completion check position and velocity parity in air
and water. Restoring a partial dash also checks the exact end position and
outgoing impulse. The GPU preview
uses the installed king texture and the actual effect renderer:

```powershell
.\mods\mega-gameplay-expansion\render-preview.ps1 -Effect AirDash
```

These checks are not a claim of compatibility with arbitrary patched collision
methods or custom movement controllers. Speed and in-game readability still
need a playtest.
