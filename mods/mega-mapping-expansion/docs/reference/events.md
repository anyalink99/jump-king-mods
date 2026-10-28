# Exact event names and their payloads

[Handbook](../README.md) / [Reference](README.md)

Put an event name in a Rule's `event`, a Tree's `event`/`stopEvent`, or a waitEvent node's `target`. Case matters: `jump`, `zone.enter` and `ring.pickedUp` are different spellings. An event runs a response once; a signal supplies a current value.

## Native player events

`chargestarted`, `chargeended`, `jump`, `landed`, `supportlost`, `teleported`, `screenchanged` and `pausechanged` follow the corresponding native Runtime gameplay event. They carry `position` (Vector2 local to the event's screen, px) and `velocity` (Vector2, px/s), sampled at that event. Restore-start/completed/failed events are deliberately not replayed as scene actions.

`player.impact` comes from an accepted blocking collision query. Its source is the surface ID. Payload: `position` (Vector2, px), `normal` (Vector2, direction), `velocity` (Vector2, px/s), `surface` (Text native type). Position can be Estimated; normal can be Unavailable. Use [contact sensors](../scenes/contact-feedback.md) for selected surfaces.

For each Boolean state `player.moving`, `player.stationary`, `player.rising`, `player.falling`, `player.airborne`, `player.walking` and `player.splat`, `.enter` means it became true and `.exit` means it became false. These events have no additional payload. The first sample establishes a baseline.

## Events from your components

In this table, replace `<id>` with the component ID you authored.

| Component/event | Extra payload |
|---|---|
| Sensor `<id>.enter`, `.exit`, `.impact` | position Vector2, normal Vector2, velocity Vector2, each with its own evidence quality; source is contact identity |
| NPC `<id>.started` | None; current line is available through its signals |
| NPC `<id>.character` | index Integer, revealed UTF-16 count |
| NPC `<id>.finished`, `.stopped` | None; successful finish differs from interruption |
| Item `<id>.pickedUp` | position Vector2 at the real native pickup |
| Item `<id>.changed` | previous Integer, count Integer |
| Hidden Wall `<id>.enter`, `.exit` | nativeId Text; follows exact native grouped-wall contact |
| Text `<id>.character` | from Integer, to Integer, revealed UTF-16 range |
| Text `<id>.finished` | None; text has become fully revealed |

For a sensor named `trial`, the entry event is **`trial.enter`**. The old `enter:trial` spelling isn't emitted by construction sensors. The component must exist; writing an event name doesn't create a sensor, NPC or pickup.

## Scene and custom events

`scene.ready` fires once on first scene activation. `scene.ending.enter` and `scene.ending.exit` mark the native ending lifecycle. These have no extra payload. `scene.phase` is a separate current Text signal: playing, ending or stopped.

`ending.main.begin`, `ending.nbp.begin` and `ending.owl.begin` fire at accepted victory, before player teardown. `ending.arrival` supplies the final feet in viewport pixels. The matching `.finish` fires after the controller/credits, including credits skipping, before picture/statistics. See [ending presentation](../integration/endings.md).

`variable:<id>` is emitted when a rule/tree action changes a declared variable. Payload: `value` in that variable's kind. Assigning the same value, loading a save, restoring a snapshot or writing through the API doesn't invent this cause-bearing event.

`emit` actions and native `EmitSceneEvent` ending nodes can send your own IDs. Choose a name such as `story.gate-open`. An emit action keeps the cause's actor/screen/payload. A bridge event supplies its native bridge context. C# consumers can supply detached typed payloads through the API.

Legacy-compatible `hiddenwallenter:<texture_name>` and `hiddenwallexit:<texture_name>` follow native wall entry/exit with no extra payload. Prefer a hiddenWall adapter when several walls share a texture and need separate identities.

## What XML can read from an event

Events carry Id, Source, Actor, Screen, Time, Sequence and typed payload samples. The built-in stamp and impulse actions use the cause's captured position. `emit` forwards the payload. C# subscribers can inspect it through [the scene API](../integration/scene-api.md).

There is no general graph input called `event.position` or `payload:<name>`. A graph input reads an observation/variable/property/exported signal. For current sensor contact values use `<id>.position`, `.normal`, `.velocity` and `.speed`; use the native action's event position when pre-teleport placement matters.

## Timing

Events queue until an advancing scene tick dispatches them. A zero-time refresh doesn't replay them. A failed rule condition doesn't defer the event. `waitEvent` only sees future events after it is armed; store a Boolean if a later sequence must remember something that already happened.

Pause holds logic dispatch. Restores resume recorded state without replaying historical sound/actions. A following shared world consumes authoritative scene state instead of executing a second local copy of the reactions.

## Moving-platform events

`platform:ID.arrived`, `platform:ID.board` and `platform:ID.leave` describe waypoint arrivals and rider changes. Payload `point` is a zero-based waypoint index for arrivals and `-1` for boarding/leaving. See [moving platforms](../maps/moving-platforms.md).
