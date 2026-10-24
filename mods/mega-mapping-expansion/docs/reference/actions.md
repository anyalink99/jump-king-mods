# Rules, actions and temporary effects

[Handbook](../README.md) / [Reference](README.md)

Use [the jump counter](../getting-started/lessons.md#5-count-jumps-in-this-attempt) to learn a rule. Use [the jump flash](../scenes/behaviors.md#count-jumps-and-flash-a-light) to learn an effect. This page lists the exact controls.

## Rule fields

| Attribute | Meaning |
|---|---|
| `id` | Unique rule name |
| `event` | Exact event ID, including case; see [events](events.md) |
| `condition` | Optional Boolean graph-node ID; must be available and true at dispatch |
| `once` | Default false; true permits one successful execution per attempt |
| `cooldown` | Default zero; seconds between executions, up to 86400 |
| `clock` | Gameplay by default, or Presentation |

Each Rule contains Action elements. Every action needs `op` and `target`. `value` is a literal; `source`, when supported, names a graph node with the required kind. An unavailable source faults the action/rule, so guard it with a condition first.

Rules for an event execute in stable ID order. Variable actions refresh graph dependencies before later rules test them. A rule/tree action that changes a variable emits `variable:<id>`; assigning the same value doesn't. Use that event to keep counting logic separate from visual feedback. Loading/restoring values and direct API writes don't invent that cause-bearing event.


## All actions

| op | target | value or source |
|---|---|---|
| `set` | Variable ID | Required literal or graph value of that variable's kind |
| `increment` | Integer/Number variable ID | Required delta of the same kind |
| `effect` | Declared Effect ID | No value needed |
| `cancel` | Declared Effect ID | Cancels that effect only for this executing owner |
| `emit` | Your event ID | Keeps the triggering actor, screen and payload; doesn't create a new payload from value |
| `stamp` | Stamp component ID | Optional Vector2 point; otherwise the event's captured position |
| `burst` | Emitter component ID | Optional Number of particles; omitted emits 16, bounded by capacity |
| `sound` | Loaded native event_music key | No path or extension; preparation checks the key exists |
| `playAudio` | Audio component ID | Plays its prepared voice without layering it over itself |
| `impulse` | Spring field component ID | Required Number force; event position is used, otherwise field origin |

These are scene actions, not arbitrary property setters. To change a drawing's property, use a binding or an effect Set. `set` changes a variable. For an independent sound with volume/fade control, use `playAudio` or audio/playing rather than registering a raw file as a native sound key.

## Effect fields

| Attribute | Meaning |
|---|---|
| `id` | Prepared effect name used by actions or the API |
| `duration` | Seconds; default 0 holds until released; maximum 86400 |
| `fadeIn`, `fadeOut` | Envelope times; their sum must fit a finite duration; held effects cannot have fadeOut |
| `clock` | Gameplay by default, or Presentation |
| `priority` | Contribution priority; higher applies later |
| `group` | Optional group for competing effects from the same owner |
| `repeat` | refresh (default), ignore, replace or stack |

Each Set inside an effect has a component/property `target`, a literal `value` or graph-node `source`, and optional `mode=set|add|multiply`. Targets must be bindable and kinds must match. Number/Vector2 contributions blend with the envelope; Boolean/Text switch discretely. Fade a material's opacity when the result should visibly fade.

## Effect lifetime and ownership

`refresh` restarts an existing activation's envelope for this owner. `ignore` leaves it running. `replace` removes it before activating again. `stack` permits several, within the active-effect budget. A group replaces competing effects from the same owner.

A rule and a tree action each have their own owner. One enter rule cannot start a held effect that an unrelated exit rule then cancels. Use a continuous binding for while-inside behavior. A tree's completion, clear, restart or cancellation releases its owned effects; variable assignments remain until changed/reset/restored.

Use [trees](../scenes/behavior-trees.md) for a sequence. Use [the C# API](../integration/scene-api.md) for a caller-controlled effect lease. Old leases cannot cancel newly restored effects after the scene generation changes.

## Moving-platform actions

`platformStart`, `platformStop`, `platformReverse` and `platformReset` take a platform or group ID as `target`, without `value` or `source`. See [platform setup and events](../maps/moving-platforms.md).
