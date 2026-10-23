# Graph values and every operation

[Handbook](../README.md) / [Reference](README.md)

For a first connection, follow [the area-caption lesson](../getting-started/lessons.md#4-show-a-caption-in-an-area). Graphs calculate values continuously; [rules](../scenes/behaviors.md) run actions when events occur.

## Node fields and sources

| Attribute | Meaning |
|---|---|
| `id` | Unique graph-node name, referenced by inputs, bindings, effects and rule conditions |
| `op` | Operation from the table below |
| `inputs` | Semicolon-separated graph node IDs, in the operation's input order |
| `source` | For input: observation, variable, component property or exported node signal |
| `kind`, `value` | Typed constant, or initial memory where required by delay/integrate; defaults Number and 0 |
| `units` | Output unit label where the operation declares one; e.g. px, seconds, F, turns, frame |
| `clock` | Gameplay or Presentation for time-dependent nodes |
| `curve` | Ordered position:value keys for curve |
| `quality` | Minimum observation quality for input; default Estimated; Exact rejects weaker evidence |

## Read a value

An input node's `source` identifies an observation or a declared value:

| Source | Meaning |
|---|---|
| `player.position`, `player.center`, `player.feet` | Local-screen position, Vector2 in pixels |
| `player.velocity` | Vector2 in pixels per second |
| `player.screen` | Current screen, Integer |
| `player.present`, `player.grounded`, `player.facingLeft` | Boolean observations |
| `charge.active`, `charge.heldFrames` | Charge state and duration evidence |
| `zone.touching` | Boolean sensor membership |
| `variable:jumps` | A declared variable |
| `lamp/intensity` | A component's last committed property |
| `node:follow.offset` | An exported recipe node |
| `time.gameplay`, `time.presentation` | Scene clocks, in seconds |
| `paused`, `scene.completed`, `scene.phase` | Pause/completion state and playing/ending/stopped phase |
| `actor:ending.king.position`, `actor:ending.king.present` | Native cutscene actor observations |

IDs are case-sensitive. `inputs="a;b"` lists graph node IDs. An input source isn't another node's ID: use `pass` for a direct dependency, or an exported `node:` port across a recipe boundary.

Signals carry a typed value, quality and sequence number. Quality is `Unavailable`, `Estimated`, `Sampled` or `Exact`. Missing evidence isn't a measured zero. `available` produces a Boolean for a guard; `quality` produces its quality code as Integer. An input can require `quality="Exact"`, making weaker evidence unavailable. See [charge](../observations/charge.md) for a practical use.

## Calculate a value

Constants default to Number. Other kinds are `Integer`, `Boolean`, `Vector2`, `Color` and `Text`. Typical literals are `12`, `true`, `20,40`, `#88BBDD` and ordinary text. Use `number` or `integer` for explicit numeric conversion. `integer` floors; `round` rounds halves away from zero and still returns Number.

The order below is the order in `inputs`:

| Operation | Inputs and result |
|---|---|
| `constant`, `input` | No graph inputs; use `value` or `source` |
| `pass` | One value, unchanged |
| `add`, `subtract`, `multiply`, `divide` | A, B; calculate A op B |
| `minimum`, `maximum`, `power` | A, B; choose a bound or calculate A to the power B |
| `abs`, `floor`, `round` | One Number |
| `clamp` | Value, minimum, maximum |
| `remap` | Value, input minimum, input maximum, output minimum, output maximum |
| `wrap` | Value, positive period; result in [0, period) |
| `curve` | One Number; interpolate `curve="0:0;0.5:1;1:0"` |
| `vector` | X Number, Y Number; result Vector2 |
| `x`, `y`, `length`, `normalize`, `angle` | One Vector2; angle is degrees |
| `dot` | Two vectors |
| `sin`, `cos` | Number in turns; 1 is a full cycle |
| `less`, `greater`, `equal` | A, B; Boolean result. Less/greater are strict |
| `and`, `or`, `not` | Two Booleans, or one for not |
| `select` | Condition, true value, false value |
| `smooth` | Target Number/Vector2, response; larger response approaches faster |
| `spring` | Target Number/Vector2, stiffness, damping |
| `integrate` | Rate Number/Vector2; accumulate rate × seconds |
| `delay` | One value; return the preceding advancing sample |
| `hysteresis` | Number, lower threshold, upper threshold; Boolean result |
| `number`, `integer`, `format` | One value; convert to Number, Integer or Text |
| `template` | Text pattern followed by up to 16 typed arguments; see [text templates](../scenes/localized-text.md) |
| `available`, `quality` | One value; inspect its evidence |

`remap` doesn't clamp. Follow it with `clamp` when values can exceed the range. Curves hold their endpoint values outside the key range. Hysteresis turns on at the upper threshold and stays on until the value reaches the lower threshold. A zero-length vector normalizes to zero.

Numeric ports carry units such as `px`, `seconds`, `F`, `turns` and `frame`. Use comparable quantities in comparisons; addition rejects conflicting nonempty units. A remap or multiply can declare output units when converting quantities. `clamp` preserves input units. Unary numeric nodes such as floor/abs/round use their declared output units; specify those when retaining or converting a quantity. Declare frame units where a value becomes an animation index.

Stateful nodes (`smooth`, `spring`, `integrate`, `delay`, `hysteresis`) select `Gameplay` or `Presentation` time. Zero-time refreshes don't integrate again. `delay` uses `kind` and `value` for its initial sample and is the only graph cycle break. Property inputs read the previous committed property; direct node dependencies are better for same-evaluation relationships.

## Type rules that commonly cause errors

Number and Integer are different kinds. Convert an Integer counter with `number` before arithmetic operations that require Number. `less` and `greater` take Numbers; `equal` accepts any pair of the same kind. Arithmetic accepts Number or Vector2, not arbitrary Integer/Text/Color values. `multiply` and `divide` accept a Number scale on the right of a vector.

`select` requires a Boolean and two values of the same kind. `clamp` accepts a Number/Vector2 value and Number bounds. `smooth` and `spring` accept a Number/Vector2 target and Number response parameters. `integrate` must declare a matching kind/value when accumulating Vector2 instead of Number. `delay` must also declare the kind and initial value of its input.

`number` retains its source units. To turn a Number into frame units for an atlas, use an explicit calculation/conversion node that declares `units="frame"`, such as a remap or unary floor with those units. Don't rely on a Number/Integer conversion to silently relabel a quantity.

## Missing observations

An actor absent from the scene has an unavailable position, not a measured position of 0,0. Use `available` to test a signal, then `select` between the real result and a known fallback. `quality` exposes an Integer code: Unavailable=0, Estimated=1, Sampled=2, Exact=3. These are evidence levels, not accuracy guarantees for unrelated quantities.

`select` can choose its known branch when the other branch is unavailable. Boolean `and`/`or` do not provide that fallback: an unavailable input makes their result unavailable. A failed calculation, such as division by zero, reports a diagnostic and makes that branch unavailable.

## Bindings and competing changes

A Bind uses `id`, graph-node `source`, component/property `target`, optional `mode` and optional integer `priority`. Modes are set, add or multiply. Add/multiply apply only to Number/Vector2 properties. The source kind must match the target; nonempty unit labels must agree.

The authored Value is the baseline. Bindings and effects contribute in stable priority/ID order; higher priority applies later. When an effect ends, the latest underlying binding becomes visible. Use [Mapping inspector](inspector.md) to see the effective value and owner.

Component properties read as graph sources hold their last committed value. Use an explicit graph-node dependency for calculations that must use the current evaluation. Graph node declaration order isn't execution order; dependencies determine it. `delay` is intentional previous-sample memory and the supported feedback-cycle break.

For complete uses, see [scrolling and spring examples](../scenes/patterns.md), [charge instruments](../observations/charge.md) and [text templates](../scenes/localized-text.md).
