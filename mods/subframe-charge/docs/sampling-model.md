# Frame sampling and success probability

This model explains ordinary, non-buffered intermediate jumps. It separates
input timing from the native charge timer and from the chance of completing
an entire jump. Position, direction and the environment still matter.

## Assumptions

Let updates occur exactly every `T = 17 ms`. Let `d` be the duration for which
Jump is down at the input source, and let the starting phase be uniform over
one update period, independently of `d`. Ignore device reporting error,
scheduling jitter, minimum-jump handling and the maximum-charge cap.

The number of held-input samples is equivalent to

`N = floor((d + U) / T)`, where `U` is uniform on `[0, T)`.

For an ordinary native charge, this matches the input-frame label used here.
The native release update also advances the charge timer. SFC preserves that
increment; the final timer is not simply `N / 60`.

For `d = kT + r`, with `0 <= r < T`, the only outcomes are:

- `k` frames with probability `1 - r/T`;
- `k + 1` frames with probability `r/T`.

Consequently, `d = 170 ms` gives exactly ten held samples under the model.
There is no theoretical 50% ceiling. An input event exactly coinciding with a
sample needs a consistent event-order convention; such phases have zero
probability in the continuous model.

## One accepted charge level

For a target level `n`, the conditional success function is

`K_n(d) = P(N = n | d) = max(0, 1 - |d - nT| / T)`.

This is a triangle centered at `nT`, with height one and total width `2T`.
For `n = 10`:

| Hold | Vanilla 10f | Other vanilla outcome | SFC selection |
| --- | ---: | --- | ---: |
| 169 ms | 94.12% | 9f: 5.88% | 10f |
| 170 ms | 100% | None | 10f |
| 171 ms | 94.12% | 11f: 5.88% | 10f |
| 178.5 ms | 50% | 11f: 50% | 11f |
| 179 ms | 47.06% | 11f: 52.94% | 11f |
| 180 ms | 41.18% | 11f: 58.82% | 11f |
| 186 ms | 5.88% | 11f: 94.12% | 11f |

The 179 ms percentages in the quoted community discussion were reversed.
The triangle itself was correct.

SFC selects `floor(d/T + 0.5)`, subject to minimum and maximum limits, using
the measured duration. Its ideal 10f interval is `[161.5, 178.5) ms`.
Halfway values round upward. The ideal success function is a rectangle.

If `m` consecutive levels are acceptable, add their individual conditional
probabilities. For `m >= 2`, vanilla gives a trapezoid: a `(m - 1)T` plateau
with a ramp of width `T` on either side. SFC gives a rectangle of width `mT`.
These statements concern charge selection, not arbitrary platform geometry.

## Human variation and convolution

Let `f(d)` be the player's probability density of hold durations. Overall
success is the weighted integral

`P(success) = integral f(d) K_n(d) dd`.

For uniform holds over a range of width `W <= 2T` centered on `nT`, vanilla
success is `1 - W/(4T)`. SFC success is `min(1, T/W)`.
Thus a 17 ms uniform range gives 75% versus 100%; a 34 ms uniform range gives
50% versus 50%. Neither example estimates a particular player's accuracy.
A bell-shaped distribution does not specify an answer without its center
and spread.

`K_n` is a conditional probability function, not a probability density over
duration: its area is `T`, not one. However, convolution is not restricted to
probability densities. If a player's error density is `g` and their intended
hold is `a`, success as a function of aim is

`P(a) = integral g(e) K_n(a + e) de`.

This can be written as a convolution of the reversed error density with
`K_n`; for symmetric `g`, it is simply `g * K_n`. Describing the calculation
as an overlap or correlation is also appropriate. Saying that convolution
cannot be used because the triangle is not a PDF is incorrect.

An equivalent route is to convolve the duration density with the uniform
phase density to obtain the continuous distribution of `D + U`, then
integrate that density over each interval `[nT, (n + 1)T)` to get the discrete
frame probabilities. Convolution alone is not yet the discrete output.

If a player uses cues that correlate their timing with the frame phase, the
independent-uniform assumption no longer applies. Use the joint distribution
instead; this model cannot establish limits on human timing skill.

## What the installed code establishes

The audited JumpKing.exe SHA-256 is
`476f2033b8b614ec97b04311799b2b78239397a2fe8018c77bb45a1f946ffc88`.
`Game1` constructs `TargetElapsedTime` through .NET Framework
`TimeSpan.FromSeconds(0.01666666753590107)`, which yields 17 ms.
`JumpGame.Update` advances gameplay using `1f / 60f`. These are distinct clocks.
The nominal real-time update rate is therefore `1000/17`, about 58.82 Hz.

A target interval is not a guarantee of evenly spaced input samples.
[MonoGame's fixed-step loop documentation](https://docs.monogame.net/articles/getting_to_know/whatis/game_loop/)
describes catch-up updates when the loop falls behind. Device reports and
thread scheduling also separate a physical hold from a measured hold. The
ideal 170 ms result must not be advertised as a hardware guarantee.

The relevant local contracts are `JumpState.MyRun`, Jump%'s
`JumpChargeCalc.PostfixJumpStateMyRun`, and SFC's `ChargeQuantizer`.
`QuantizeRelease` rounds input frames before applying the material multiplier
and includes the native release increment. True buffers remain native;
automatic maximum jumps and short-tap recovery are separate cases. See
[Timing and input](timing.md) for integration details.

Removing both lucky and unlucky outcomes does not prove competitive
neutrality. It can greatly help a well-centered player and hurt a hold that
previously succeeded by favorable phase alignment. Whether the changed input
rules are allowed is a community/category decision, not a probability result.
