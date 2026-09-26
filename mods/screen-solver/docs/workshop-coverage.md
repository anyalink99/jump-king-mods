# Installed-mod coverage

The previous capture guard treated every unknown gameplay patch as active. That
blocked a normal map merely because block libraries were subscribed. Removing
the guard would have hidden a different problem: some inactive patches cache
the current BodyComp in static fields whenever collision code runs. Calling
those methods for a speculative body would contaminate the running game.

## Isolation

`NativeResolution` implements the audited native X/Y resolution, gravity and
guardtower correction without invoking their patched entry points. Branch-local
block behaviours still supply native water, sand and ice responses. Walking
uses the native 1.5 value; charge uses the branch's water multiplier, never a
foreign getter that queries the real EntityManager player.

`WorkshopCoverage` checks binary hashes, original method, patch kind and owner.
For inactive-only integrations it additionally checks every loaded collider and
the library's retained state. Known library methods themselves remain in the
patch audit, so an extra patch to an adapter implementation is not hidden.
Native collision queries which remain patched are covered by exact-parity tests
under the combined installed patch set.

| Integration | Accepted state |
| --- | --- |
| Switch Blocks | No authored mechanics or retained material/conveyor flags |
| Expansion / Ghost blocks | Native terrain; registered custom behaviours inactive |
| Anti Blocks | Neither anti-splat nor anti-snake latched |
| Movement Control | Invert, forced-neutral and no-breaking inactive |
| Conveyor | No current or three-frame retained conveyor contact |
| UpsideDownCore | Native orientation, no reverse gravity |
| Sprinting | Sprint input not held; solver does not press it |
| Custom Wind Switch | Active per-screen profiles copied by value |
| JumpKingPlus | No custom terrain; thin-snow animation leaf omitted from tree comparison |
| High Gravity / Sample Low Gravity | Registered behaviours inactive on native terrain |
| UpsideDown Blocks | No custom terrain, native core orientation |

Libraries without Harmony patches are checked too: registration alone can add
block behaviours or alter the controller tree. Checkpoint and Trap Sand install
their behaviours only when their map markers are present; those active blocks
remain outside this provider's coverage.

The exact binaries and targets are recorded in `WorkshopCoverage.Contracts` and
checked again in `build.ps1`. This is an audited set, not a universal promise for
future releases of those libraries.

## Player composition

`PlayerAdapters` pins the MVIDs of the installed modules, including memory-loaded
JK Runtime packages. It normalizes only the audited SFC jump node to the native
tree shape. The order of all native body phases must still match exactly.

SFC gets a separate, serialized hold counter and buffered-charge flag. On an
ordinary release the model follows the installed quantizer's float conversions
and subtract/add sequence. Water scales native charge after input rounding;
buffers and maximum charge use native accumulation. Inputs in this model start
on simulated updates, so the route states the intended SFC hold in milliseconds.
It does not simulate the physical device, the polling worker or late delivery.

SFC measurement components, replay capture and racing ghosts are observers.
Save States and Manager are manual tools outside the solver's input vocabulary.
Rewinder must not be rewinding. Mega's registered phases are accepted only with
both global options off and no authored screen effects; custom solid/zone blocks
still fail geometry coverage. Their live methods are never executed in search.

More Items' `BargainburgMerchantGuard` exists throughout the main game, not just
near the merchant. Its only update effect is enabling/disabling the merchant's
dialogue tree. Solve ignores that exact audited type without executing its
update or destruction callback. Manual dispensers, jetpack sprite presentation
and usage attribution are likewise outside the route's movement/input model.
Automatic pickups remain unsupported because contact can change inventory and
invoke external subscribers. The regression suite enumerates every installed
More Items Entity/Component subclass, so an unreviewed new type fails the test.

## Evidence and limits

Tests load the actual Workshop DLLs, apply their actual patches and compare
native controller/world updates against speculative updates. Tests watch foreign
static caches, Jump% measurements and audio flags for unwanted writes. A capture
fixture reproduces the installed SFC replacement and the Mega/SFC/Save States/
Manager body composition without starting real device polling or graphics.
Synthetic multi-jump routes are replayed twice with native and SFC timing.

These checks establish the listed combinations. They do not establish support
for active switch puzzles, custom gravity, ball form, jetpack or warp, and they
are not a manual in-game execution of the displayed route. Wind annotations
still refer to captured world time, not a freshly synchronized launch on close.
