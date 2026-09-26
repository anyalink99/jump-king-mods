# Babe of Ascension: fractional-charge wall contact

Investigated on 2026-09-21 after a report of real rebounds with Enabled on and
sliding with Enabled off. The screenshot and logged launch Y identify screen 59
(zero-based 58), the right wall above the icy platform.

## Geometry and reproduction

Installed Workshop map: `3160240585`. Its `level.xnb` SHA-256 was
`232DCC14FE90939802310164C71611913658FA40DC58EB48DA5B3452AF4A9149`.
The collision atlas has red native slope tiles at x=432, local y=160..304,
above the ice support at local y=312. Loading it with the game's base factory
produces a chain of 8x8 TopLeft slope blocks, not a flat vertical box wall.

An isolated executable loaded the installed atlas through native LevelTexture
and LevelManager.LoadBlocksInterval, then advanced the installed BodyComp
pipeline. Starting body size was 18x26, world Y=-20594, with native horizontal
takeoff speed. Wind, audio, particles, screen bookkeeping, capping and teleport
stages were omitted for this interior, non-windy trajectory. Collision, slope
resolution, ice, gravity and GuardtowerSoulBugFixBehaviour remained native.
No live game body, save, map or installed mod was changed.

For 25 starting X values from 410 through 416 at .25 intervals, all 35 integer
charge levels (875 pairs) gave the same first post-tick leftward-velocity tick
for native accumulated timers and whole-step SFC timers. This compares wall
outcomes, not bit-identical floating-point trajectories or every initial state.

At X=414, 30 of 69 quarter-step samples from 18 through 35 hold frames acquired
leftward velocity. Examples (hold frames exclude the native release increment):

| Hold frames | Result in this fixture |
| --- | --- |
| 27.5 | Leftward motion on zero-based flight tick 57 |
| 28 | No leftward motion before landing |
| 28.25 | No leftward motion before landing |
| 28.5 | Leftward motion on zero-based flight tick 59 |

Stage tracing identifies native ResolveXCollisionBehaviour as the point that
changes X velocity from positive to negative. Removing the Guardtower fix in
a diagnostic-only comparison does not eliminate the effect. Fractional launch
velocities change subsequent contact with the slope chain; the native collision
code itself can then send the king away from the wall.

The user's log includes fractional releases on this screen (including 25.5 and
21.75 hold frames) before later tests with whole-step charge and Enabled off.
The later on-disk settings had both Enabled and QuarterStepCharge off.

## Interpretation and limits

### Follow-up: native rebounds and the actual departure mechanism

A second isolated executable (`SlopeFollowup.cs`) references only JumpKing and
MonoGame: it neither references nor loads SFC, Runtime, or Harmony. Its copied
JumpKing.exe matches the installed executable, SHA-256
`476F2033B8B614EC97B04311799B2B78239397A2FE8018C77BB45A1F946FFC88`.
It initializes launch velocity with the native dry-jump formula, advances the
same native collision stages, and follows each flight until landing or 180 ticks.
This is a post-launch body fixture, not a replay of the complete live input tree.

At the non-overlapping initial position X=414, native whole-frame holds 15..22
and 32 all depart at least four pixels from the wall before landing: 9 of the
35 native powers tested. Thus the rebound is not exclusive to fractional charge.
The wider 875-start/power sweep is an exploratory sample, not an estimate of
in-game frequency; some X values greater than 414 initially overlap the wall.

| Hold frames | Charge construction | Leftward velocity tick | Landing X | Departure from X=414 |
| --- | --- | --- | --- | --- |
| 18 | Native accumulated timer | 47 | 403.661 | 10.339 px |
| 25 | Native accumulated timer | None | 414.621 | None |
| 25.5 | Fractional timer | 53 | 398.948 | 15.052 px |
| 28 | Native accumulated timer | None | 414.000 | None |
| 28.5 | Fractional timer | 59 | 398.164 | 15.836 px |
| 32 | Native accumulated timer | 46 | 375.678 | 38.322 px |

The 28 / 28.5 comparison reveals the following sequence:

1. Repeated ascending contacts project velocity along the TopLeft slope tangent.
   Native integer hitboxes and X-position snapping resolve the body to X=414.
2. After tick 41, hold 28 has residual X velocity about 0.00000765 pixels/tick.
   At X=414 this is too small to change the stored single-precision position.
   The body descends beside the wall without another horizontal collision.
3. Hold 28.5 instead retains about 0.0607201 pixels/tick toward the wall.
   There is no X or Y collision on ticks 42..57. This is adjacent free flight,
   rather than an uninterrupted series of contacts.
4. On tick 58, X reaches 415.0324 and the integer hitbox overlaps slope tiles
   at local Y=192, 200 and 208. Incoming velocity is (0.0607201, 4.310709).
   Native ResolveX projects it to approximately (-2.124995, 2.124995).
   For this slope the tangential X component is `(vx - vy) / 2`.
5. The native Guardtower fix reverses X once to +1.062497 and marks the body
   knocked. On tick 59 there is another slope collision; projection gives
   X=-0.6598201. The guard does not reverse it again because the body is already
   knocked. The resulting leftward motion persists through the remaining flight.

Two diagnostic-only interventions confirm the residual-velocity mechanism:
setting X velocity to zero after tick 41 prevents departure in the 28.5 case;
setting it to +0.0607201 at the same tick creates departure in the native 28
case (landing X=398.649). These are counterfactual experiments, not a proposed
production fix. They change physics and are never installed.

This updates the initial assessment: describing the observation as a proven
Quarter-step compatibility bug was premature. Native slope projection explicitly
permits this response, and ordinary native powers can reach it too. Whether the
game or map authors intended this particular wall outcome cannot be inferred
from the implementation alone. Fractional powers add more trajectories that
encounter the same native behavior.

Automatically clamping small X velocities, treating the slope chain as a flat
wall, or suppressing repeated rebounds would change native gameplay. A generic
"continuous contact" exemption is also insufficient here: the demonstrated
flight has sixteen collision-free ticks before recontact. No such adjustment
is justified as a transparent SFC correction by the evidence collected so far.

### Scope

Quarter-step charge introduces trajectories unavailable at the corresponding
native integer charge levels. Preserving native collision physics does not
guarantee that those extra trajectories retain the same wall-slide outcomes.
Disabling only Quarter-step Charge retains ordinary SFC timing correction and
the independently configured input/presentation features. Suppressing these
rebounds while retaining fractional strengths would be an additional collision
gameplay policy and is not applied by this investigation.

This reproduction uses installed native collision code and the actual atlas,
without loading the user's foreign Harmony hooks. It establishes a native
fractional-trajectory cause; it does not rule out other input or mod interactions.

Local research artifacts (not packaged):
`build/subframe-charge/_INTERNAL/SlopeProbe.cs`,
`build/slope-probe.log`, `build/slope-probe-quarter.log`,
`build/slope-probe-guard.log`, `build/slope-probe-stage.log`,
`build/subframe-charge/_INTERNAL/SlopeFollowup.cs`, `build/slope-followup.csv`,
and `build/slope-trace-*.csv`.
