# No Walk Off

## Native movement

The audited JumpKing.exe SHA-256 is
`476f2033b8b614ec97b04311799b2b78239397a2fe8018c77bb45a1f946ffc88`.

`Walk.MyRun` sets ordinary horizontal speed to ±1.5 pixels per update.
`IceBlockBehaviour.IsPlayerOnBlock` instead reports grounded state whenever
Snake Ring is enabled. Walk then approaches its target speed by twice the ice
friction (0.2), and the block behaviour applies 0.1 friction per update.
This produces fractional positions and explains the use of short ring-assisted
inputs for fine positioning. No Walk Off does not equip the ring or emulate
its movement elsewhere. The guard no longer has an ice/Snake Ring exclusion.
`WalkMotionGuard` recognizes their inertia through the actual velocity change
after the material pass, before Walk writes the next command.

## Ordering and support

Owned JK Runtime body phases bracket native X movement and collision.
The first records grounded position, activation and directional input. The
second examines the actual displacement after native speed modifiers and wall
resolution, before Y movement can remove support.

The shared motion observer also samples before wind and after material behaviour.
It checks the previous Walk command (the body runs before this frame's Walk),
velocity entering X movement, actual X displacement and post-material momentum.
JK Runtime supplies the actual requested step and its arithmetic coverage.
Unexpected velocity, offsets, friction or late displacement
invalidate protection without checking a wind, ice or conveyor block name.
An ordinary complete cycle re-arms eligibility; controls, support and map scope
must still permit a stop. Existing edge latches release on external motion.

No callbacks are replayed with zero input: foreign `ModifyXVelocity` methods
may mutate state. Runtime analyzes supported arithmetic and copies independent
operands from the single real invocation. Its neutral calculation preserves the
actual modifier order without re-running getters or writes. Water, sand,
JumpKingPlus low gravity/legacy speed, Expansion Blocks SuperLowGravity and
unfamiliar scaling handlers use the same mechanism. MGE contains no speed
coefficients or low-gravity type/flag checks.

Only a fresh `ControlledOnly` sample is accepted. Unsupported code, changed
patch graphs, unprepared handlers and incomplete observations yield. This is
local arithmetic coverage, not a proof of an arbitrary mod's alternate-world
behavior. Foreign code outside the observed update order still needs compatibility
testing. A later callback cannot be predicted from an earlier observation.
See Runtime's [coverage and ownership contract](../../jk-runtime/docs/motion-observation.md).

The world owns prepared observers; the attempt owns each player subscription.
Preparation is measured as `mega-gameplay.motion-analysis` before activation,
or runs on explicit enable/library actions. A disabled, unauthored feature does
not scan handler types or install observation hooks. Restarts reuse the prepared
world service, never a previous player's motion state.

A one-pixel strip beneath the hitbox queries actual blocking intersections and
the native collision result's `SlopeType`. A slope-only contact is not standing
support: native Y resolution marks it airborne. The game's own flat/slope
precedence preserves the last flat pixel where both surfaces meet.
The sweep checks each crossed collision column, not only the destination, so a
fast tick cannot skip an unsupported gap. It keeps the last supported integer
position. After stopping, fractional outward displacement is also clamped to
that position, preventing subpixel jitter underwater.

JumpKingPlus top-facing one-way blocks are nonblocking in `Intersects`; their
registered behaviour supplies the native Y collision instead. The support query
recognizes that contract only at the block's top edge, with no body overlap with
another one-way block. Bottom/side-facing blocks, embedded bodies and lower
platforms do not provide standing support. This read-only query does not invoke
foreign collision callbacks, change collision flags or make other nonblocking
volumes solid. Unregistered or replacement behaviours are not assumed compatible.

No synthetic wall is created: there is no bounce or bump sound. Y velocity,
charge, input bindings and native collision flags remain untouched. A jump
with upward velocity bypasses the guard. Native slope contacts and teleport
handoffs are left to their own controllers.

## Input and lifetime

The stop stores its direction and hitbox. Releasing that direction arms one
outward press; pressing again permits departure. Opposite input, relocation,
airborne movement and scope exit reset the latch. Both directions held at once
are not a release of either direction.

Solid activation requires foot contact; Zone activation uses body overlap;
Screen uses the shared marker registry. Authority is checked again at the
last supported position when a sweep reaches an edge. Global settings never
disable authored activation.

Version 2 snapshots contain the edge latch, departure permission, prior command
and movement eligibility. A restore discards
in-progress phase samples. Unload removes owned body phases and disables the
reusable post-body component without touching other mods' components.

Global-use attribution is queued during the stop and committed by that
post-body component in the same entity update. Registering a temporary marker
inside BodyComp would invalidate its linked-list enumerator. Map-authored use
and `AllowMegaGameplayExpansion` skip the marker; a completed contribution is
not erased by restoring a movement snapshot.

## Verification

The focused build executes native Entity updates with native Walk, input and
BodyComp physics. Fixtures cover both edges, descending slope seams, water, all map scopes,
continued holds, release/repress, retreat, takeoff, restore and teardown.
Additional tests cover joined and lower terrain, swept gaps and global mode.
Ice/Snake Ring and native-wind walk-off parity, stateful conveyor-like offsets,
held/neutral input and external-force activation at a latched edge are tested.
Observers do not add calls to the stateful callback. Combined Warp departures/landings, map permissions, opposing held
directions, old settings and disabled-mode walking/falling are covered too.
Headless fixtures omit particle drawing and bump audio, not collision logic.
Manual checks should include the intended controller, charge on an icy edge,
slopes and teleports.

Fast checks include a separate unknown-provider DLL with changing scaling and
stateful getter/callback counters, composed with water in both directions.
Integration additionally loads the installed JumpKingPlus DLL when available.
It checks 64 combinations of solid/one-way support, low gravity, legacy speed,
water and walking direction, with native approach parity and release/repress.
It also covers provider registration, all one-way orientations, embedded/lower
contacts, upward passage, low-gravity entry/exit and external forces introduced
at a latched edge. Installed SuperLowGravity requires no new arithmetic adapter;
the real ConveyorBlockMod handler checks active/inactive carry and exactly one
history update. Absence of the optional JumpKingPlus provider is reported as a skip.

## Declared foreign support

Runtime 1.32 material declarations let unfamiliar providers expose a bounded,
read-only state capture and pure support predicate. No Walk Off queries at most
64 foot-contact candidates per probe position after native flat/slope precedence
and the reviewed JumpKingPlus adapter. It passes actual velocity and body/surface
overlap; unknown or failed declarations do not grant support. Removing a scoped
registration immediately removes coverage. It never replays additional collision
callbacks. See Runtime's [interop contract](../../jk-runtime/docs/interop.md).

The separate unknown-provider DLL verifies both one-way edges, release/repress,
changing support state and registration cleanup. JumpKingPlus retains its reviewed
ordered-contact/initial-overlap adapter and uses Runtime's shared top-face rule.
