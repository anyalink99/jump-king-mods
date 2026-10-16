# Signals, graphs and effects

[Handbook](../index.md) / [Scene authoring](index.md)

A **signal** describes the current state: the King's position, a sensor's membership, or a counter. An **event** says something happened: a jump began, a sensor was entered, or a variable changed. Read signals in graphs; listen to events with rules or trees. These parts work the same way for instruments, lights, deformation and cutscenes.

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
| `integrate` | Rate Number/Vector2; accumulate rate Г— seconds |
| `delay` | One value; return the preceding advancing sample |
| `hysteresis` | Number, lower threshold, upper threshold; Boolean result |
| `number`, `integer`, `format` | One value; convert to Number, Integer or Text |
| `template` | Text pattern followed by up to 16 typed arguments; see [text templates](localized-text.md) |
| `available`, `quality` | One value; inspect its evidence |

`remap` doesn't clamp. Follow it with `clamp` when values can exceed the range. Curves hold their endpoint values outside the key range. Hysteresis turns on at the upper threshold and stays on until the value reaches the lower threshold. A zero-length vector normalizes to zero.

Numeric ports carry units such as `px`, `seconds`, `F`, `turns` and `frame`. Use comparable quantities in comparisons; addition rejects conflicting nonempty units. A remap or multiply can declare output units when converting quantities. `clamp` preserves input units. Unary numeric nodes such as floor/abs/round use their declared output units; specify those when retaining or converting a quantity. Declare frame units where a value becomes an animation index.

Stateful nodes (`smooth`, `spring`, `integrate`, `delay`, `hysteresis`) select `Gameplay` or `Presentation` time. Zero-time refreshes don't integrate again. `delay` uses `kind` and `value` for its initial sample and is the only graph cycle break. Property inputs read the previous committed property; direct node dependencies are better for same-evaluation relationships.

## Bind a result

A binding names a node `source` and a `component/property` target. Mode defaults to `set`; `add` and `multiply` work where the type supports them. Composition starts from the authored baseline and applies bindings and effects in stable priority/ID order. Higher priorities apply later. When an effect ends, the latest binding value becomes visible, rather than an old value captured at activation.

Structural fields can't be bound. Asset references, capture selection, topology and sampling need preparation: edit source and reload. The [field reference](../../reference/PARAMETERS_GENERATED.md) marks every bindable field.

## Count jumps and flash a light

This complete scene displays a saved jump count and a short flash. It needs no external art:

```xml mapping_settings.xml
<MappingSettings version="1" saveId="handbook-jump-counter">
  <Variables><Variable id="jumps" kind="Integer" value="0" scope="Save" /></Variables>
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,60" /></Component>
    <Component id="label" kind="text"><Value name="transform" value="pose" /></Component>
    <Component id="lamp" kind="light">
      <Value name="transform" value="pose" />
      <Value name="radius" value="48" /><Value name="intensity" value="0" />
    </Component>
  </Components>
  <Graph>
    <Node id="count" op="input" source="variable:jumps" />
    <Node id="caption" op="format" inputs="count" />
  </Graph>
  <Bindings><Bind id="caption" source="caption" target="label/value" /></Bindings>
  <Effects>
    <Effect id="flash" duration="0.6" fadeOut="0.5" clock="Presentation" repeat="refresh">
      <Set target="lamp/intensity" value="0.08" />
    </Effect>
  </Effects>
  <Rules>
    <Rule id="countJump" event="jump">
      <Action op="increment" target="jumps" value="1" />
      <Action op="effect" target="flash" />
    </Rule>
  </Rules>
</MappingSettings>
```

Jump, inspect `variable:jumps`, then restart. Save scope retains the total; Attempt scope resets each attempt. Keep `saveId` stable when renaming a map. It identifies saved scene values within the native save context. A native reset invalidates older data. [Architecture](../reference/architecture.md#state) explains snapshots and shared-state ownership.

## Rules and actions

A rule listens to an exact event. Its optional `condition` names a Boolean graph node. `once=true` permits one execution per attempt. `cooldown` uses the rule's Gameplay or Presentation clock. Conditions are checked at dispatch; a failed condition doesn't defer the event.

Rules for an event execute in stable ID order. Variable actions refresh graph dependencies before later rules test them. A rule/tree action that changes a variable emits `variable:<id>`; assigning the same value doesn't. Use that event to keep counting logic separate from visual feedback. Loading/restoring values and direct API writes don't invent that cause-bearing event.

| Action | Target and value |
|---|---|
| `set` | Variable; literal `value` or same-kind graph `source` |
| `increment` | Number/Integer variable; same-kind delta |
| `effect` | Prepared effect ID |
| `cancel` | Effect ID belonging to this executing owner |
| `emit` | Custom event ID; retains cause actor, screen and payload |
| `stamp` | Stamp component; optional Vector2 point, otherwise event position |
| `burst` | Emitter component; optional Number value |
| `sound` | Loaded native event_music key, without path/extension |
| `playAudio` | Prepared `audio` component ID; plays its owned voice without overlapping itself |
| `impulse` | Spring field; Number force, at event position or field origin |

Every action needs a target. Guard unavailable graph sources before actions that require them. The `sound` action is a native one-shot. For volume, fades and controlled loops, bind an [`audio` component](../game/audio.md); use `playAudio` for a prepared event voice.

## Effects and ownership

An effect has duration, fadeIn/fadeOut, clock, priority, group and repeat policy. Each Set uses a literal or graph source and set/add/multiply mode. Numeric/vector contributions blend with the envelope; Boolean/Text switch discretely. Animate opacity for a visible fade.

Duration zero holds until released and can't fade out. Fade-in plus fade-out must fit a finite duration. For the same owner, `refresh` restarts the envelope, `ignore` keeps the existing activation, `replace` removes it before creating another, and `stack` allows multiple contributions. A group replaces competing effects from that owner.

Cancellation is narrow: an exit rule cannot cancel a held effect started by an unrelated enter rule. Tree action nodes also have distinct owners. Use a sensor binding for вЂњwhile touching,вЂќ a finite effect for a pulse, tree completion/clear for a sequence, or an API lease for caller-controlled lifetime. Removing one owner never erases somebody else's effect.

## Events and timing

Native event IDs include `chargestarted`, `chargeended`, `jump`, `landed`, `teleported`, `screenchanged` and `pausechanged`. `scene.ready` fires once at first activation. Endings emit `scene.ending.enter` and `.exit`. Sensors emit `<id>.enter`, `.exit` and, for blocking movement evidence, `.impact`.

Events contain ID, source, actor, screen, time, sequence and typed payload. They are queued observations; they don't retroactively alter collision. A contact event can retain a struck point even after teleporting. [Sensors](contact-feedback.md) explains phase selection and the differences from native hidden walls.

## Pause and completion

Gameplay time stops on native pause, Runtime UI and completion. Presentation time can continue during an ending or after completion, but **also stops on pause by default**. A view may explicitly set `animateWhilePaused=true`; that permits Presentation animation on the selected screen while logic dispatch remains held. The scene checkbox holds both clocks regardless of this option.

Clock spelling is `Gameplay`/`Presentation` on graph nodes, rules, effects and trees; `gameplay`/`presentation` on drawing components. [Endings](../integration/endings.md) shows how to keep a cutscene sequence running without keeping ordinary gameplay alive.
