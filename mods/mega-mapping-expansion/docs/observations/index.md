# Observe the game

[Handbook](../index.md)

Choose an observation by what happened in the game, then decide what should react.
The same observation can drive a sound, animation, counter, camera or ending.
Charge is one member of this system; there is no separate instrument runtime.

A **signal** is a current value. A **rule** or **tree** listens to an **event**.
Read a signal with a graph `input`; bind the result to a property. Use an event
when an action should happen once. [Graphs and effects](../scenes/behaviors.md)
explains the common wiring.

## Player and motion

| Signal | Type and meaning |
|---|---|
| `player.position`, `player.center`, `player.feet` | Vector2, pixels in the player's current screen |
| `player.screen` | Integer, one-based simulation screen |
| `player.velocity` | Vector2, pixels per second |
| `player.speed` | Number, velocity magnitude in pixels per second |
| `player.present`, `player.grounded`, `player.facingLeft` | Boolean native observations |
| `player.moving`, `player.stationary` | Velocity magnitude above/below the small numerical tolerance |
| `player.rising`, `player.falling`, `player.airborne` | Airborne direction and lack of ground support |
| `player.walking` | Native walk action succeeded while grounded, outside the splat state |
| `player.splat` | Native fail state is running |

Walking describes the native action. It is independent of movement: ice can carry
a King who has stopped walking, and a wall can prevent a walking King from moving.
The new Boolean states emit `<signal>.enter` and `<signal>.exit` on a change.
The first sample establishes a baseline and doesn't invent an entry event.
`player.stationary` describes velocity, not whether a menu has paused the game.

Existing native events include `jump`, `landed`, `supportlost`, `teleported`,
`screenchanged`, `chargestarted`, `chargeended` and `pausechanged`. Their payloads
include sampled position and velocity; position uses the event's screen. A
teleport is reported at the native commit rather than guessed from a long move.

## Contacts and regions

`player.impact` comes from an accepted blocking collision query. Its source
identifies the surface. Payloads include `surface` (native type), `position`,
`normal` and incoming `velocity`. The position can be estimated; a contact normal
can be unavailable when the resolver cannot establish one. Read the sample's
quality instead of treating missing evidence as a measured zero.

For a selected group of surfaces or a spatial region, use a `sensor`. It exposes
membership and contact values and emits entry, exit and impact events. Regions
and physical impacts have different meanings: crossing a nonblocking trigger
doesn't imply an impact. [Sensors and contact feedback](../scenes/contact-feedback.md)
includes rectangles, slopes and native hidden walls.

## Actions, interactions and lifecycle

| Family | Values and events | Guide |
|---|---|---|
| Charge | `charge.active`, timer, held/released frames and timing quality | [Charge timing](charge.md) |
| NPC | `<id>.present`, `.talking`, `.line`, `.revealed`, `.position`, `.screen`; `.started`, `.character`, `.finished`, `.stopped` | [Dialogue](../game/dialogue.md) |
| Inventory | `<id>.count`, `.owned`, `.equipped`; `.changed`, `.pickedUp` | [Items](../game/interactions.md#items) |
| Hidden wall | `<id>.touching`; `.enter`, `.exit` | [Hidden walls](../game/interactions.md#hidden-walls) |
| Text | `<id>.content`, `.revealed`, `.complete`; `.character`, `.finished` | [Scene text](../game/dialogue.md#reveal-scene-text) |
| Audio | `<id>.audible` reports the owned voice's playing state and local volume | [Audio](../game/audio.md) |
| Camera | `<id>.selected` reports request ownership | [Camera](../game/camera.md) |
| Lifecycle | `paused`, `scene.completed`, `scene.phase`, `scene.ready`, ending enter/exit | [State](../scenes/state.md), [endings](../integration/endings.md) |
| Time | `time.gameplay`, `time.presentation`, in seconds | [Animation](../scenes/texture-animation.md) |

Adapter IDs are authored component IDs. An `npc` component called `hermit` exposes
`hermit.talking`; it doesn't create a global signal named after every NPC in the
map. Resources and native contracts are prepared before activation. NPC references
are bound at activation and refreshed when the native dialogue node is encountered;
the adapter doesn't scan every entity on every frame.

## Timing, quality and screens

Observations carry `Unavailable`, `Estimated`, `Sampled` or `Exact` quality and a
sequence. No native actor bound yet means unavailable, not an actor at `(0,0)`.
Graphs can require a minimum quality or test `available`. Most live state is
sampled. Corrected released charge evidence and native identity are exact within
their stated contract.

Player contact sampling runs before input, after the body's movement. Ordinary
scene sampling runs after the native update. Rules dispatch on the next advancing
scene tick; a zero-time refresh doesn't replay actions. NPC and text events retain
the screen of their source, which need not be the player's current screen.

Screen numbers in authoring are one-based. Player samples are local to their
reported screen. Native wall selectors use canonical world coordinates because a
texture may be reused across screens. Camera authoring uses a screen plus local
coordinates. These conventions are explicit in the relevant guide.

Pausing stops gameplay logic. An opt-in visual pause policy doesn't authorize
sound or camera movement to continue. Restoring state restores observation
baselines; it doesn't turn every currently true condition into a new entry.
In shared worlds, the authoritative scene owns reactions. Inventory and pickup
observations refer to that authority's native inventory; they are not a merged
inventory of all players. See [shared state](../scenes/state.md).

## Example: react when falling starts

This complete scene flashes a small light once at the start of a fall. It needs
no external assets. Change the event to `player.walking.enter` or `landed` to
reuse the same effect with another observation.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,100" /></Component>
    <Component id="light" kind="light"><Value name="transform" value="pose" /><Value name="intensity" value="0" /></Component>
  </Components>
  <Effects><Effect id="flash" duration="0.4" fadeOut="0.3"><Set target="light/intensity" value="0.12" /></Effect></Effects>
  <Rules><Rule id="fall" event="player.falling.enter"><Action op="effect" target="flash" /></Rule></Rules>
</MappingSettings>
```
