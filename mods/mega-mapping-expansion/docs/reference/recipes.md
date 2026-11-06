# Every built-in recipe argument

[Handbook](../README.md) / [Reference](README.md)

Use [the recipe guide](../scenes/recipes.md) for complete scenes and defining your own preset. These are the built-in argument names; unknown arguments fail validation. Required means you must provide an Arg. An empty default is accepted, but supply artwork for a textured result.

Recipe arguments configure expansion before play. To animate a prepared instance, use its exported component/property/node connections. Internal names are implementation details. The source library is already embedded: don't Include it again under the same recipe IDs.

## atlas-display

| Argument | Kind | Default | Purpose |
|---|---|---|---|
| `asset` | Text | Required | Image/atlas Asset ID, not a path. |
| `source` | Text | `charge.heldFrames` | Observation or variable signal name; e.g. charge.heldFrames or variable:jumps. |
| `position` | Vector2 | `240,180` | Anchor in local screen pixels. |
| `origin` | Vector2 | `0,0` | Sprite anchor within its atlas cell in pixels. |
| `screen` | Number | `1` | One-based screen index; use zero only where the generated argument bound allows it. |
| `minimum` | Number | `1` | Source value mapped to the first frame, in the source quantity. |
| `maximum` | Number | `35` | Source value mapped to the last frame. |
| `frames` | Number | `35` | Number of displayed frames; match the supplied atlas. |
| `rounding` | Text | `floor` | Graph unary operation used after remap/clamp; use floor or round for discrete frame selection. Default floor selects lower bands. |

Exported ports: `<instance>.transform` (component), `<instance>.image` (component).

## atlas-digit

| Argument | Kind | Default | Purpose |
|---|---|---|---|
| `asset` | Text | Required | Image/atlas Asset ID, not a path. |
| `source` | Text | Required | Observation or variable signal name; e.g. charge.heldFrames or variable:jumps. |
| `position` | Vector2 | `240,180` | Anchor in local screen pixels. |
| `screen` | Number | `1` | One-based screen index; use zero only where the generated argument bound allows it. |
| `digit` | Number | `0` | Decimal place: 0 ones, 1 tens, 2 hundreds, through 9. Extraction uses the absolute value. |
| `roll` | Number | `0.24` | Seconds for the clipped sliding digit transition. |

Exported ports: `<instance>.transform` (component), `<instance>.image` (component).

## follow-offset

| Argument | Kind | Default | Purpose |
|---|---|---|---|
| `clock` | Text | `Gameplay` | Clock for this recipe's moving parts. planet/follow-offset/wind-panel take Gameplay or Presentation; precipitation takes gameplay or presentation. |
| `target` | Text | `player.center` | Position observation to follow, normally player.center. Must be Vector2 in local screen pixels. |
| `origin` | Vector2 | `240,180` | Resting point of the follower in local screen pixels. |
| `extent` | Vector2 | `3,2` | Maximum directional look offset per axis in pixels. |
| `response` | Number | `7` | Smoothing response; larger follows more quickly. |
| `nearRadius` | Number | `24` | Softening distance near the origin, in pixels. It is not an outer range cutoff. |
| `screen` | Number | `1` | One-based screen index; use zero only where the generated argument bound allows it. |

Exported ports: `<instance>.transform` (component), `<instance>.offset` (node).

## planet

| Argument | Kind | Default | Purpose |
|---|---|---|---|
| `clock` | Text | `Gameplay` | Clock for this recipe's moving parts. planet/follow-offset/wind-panel take Gameplay or Presentation; precipitation takes gameplay or presentation. |
| `surface` | Text | `` (empty) | Base surface image Asset ID. Empty uses an untextured surface. |
| `overlay` | Text | `` (empty) | Optional second image Asset ID; empty hides the overlay. |
| `position` | Vector2 | `240,180` | Anchor in local screen pixels. |
| `size` | Vector2 | `240,240` | Prepared mesh width/height in pixels. |
| `screen` | Number | `1` | One-based screen index; use zero only where the generated argument bound allows it. |
| `rate` | Number | `0.008333333333333333` | Motion rate: turns per second for planet/wind-panel, particles per second for precipitation. |
| `overlayRate` | Number | `0.0070921985815602835` | Overlay UV rotation in turns per second. |
| `sampling` | Text | `point` | point or linear; transform pixel policy still applies. |
| `tilt` | Vector2 | `0,0` | Spherical projection tilt angles in degrees. |

Exported ports: `<instance>.transform` (component), `<instance>.rate` (node).

## precipitation

| Argument | Kind | Default | Purpose |
|---|---|---|---|
| `clock` | Text | `gameplay` | Clock for this recipe's moving parts. planet/follow-offset/wind-panel take Gameplay or Presentation; precipitation takes gameplay or presentation. |
| `asset` | Text | `` (empty) | Image/atlas Asset ID, not a path. |
| `screen` | Integer | `1` | One-based screen index; use zero only where the generated argument bound allows it. |
| `position` | Vector2 | `240,-16` | Anchor in local screen pixels. |
| `area` | Vector2 | `600,0` | Particle birth width/height around its position. |
| `velocity` | Vector2 | `-12,180` | Initial particle X/Y speed in pixels per second. |
| `spread` | Vector2 | `16,40` | Random X/Y variation of initial velocity. |
| `rate` | Number | `80` | Motion rate: turns per second for planet/wind-panel, particles per second for precipitation. |
| `lifetime` | Number | `3` | Particle lifetime in seconds. |
| `seed` | Integer | `1` | Repeatable random pattern seed. |
| `size` | Number | `1` | Particle drawing scale ratio. |

Exported ports: `<instance>.emitter` (component).

## surface-reflection

| Argument | Kind | Default | Purpose |
|---|---|---|---|
| `screen` | Integer | `1` | One-based screen index; use zero only where the generated argument bound allows it. |
| `position` | Vector2 | `0,280` | Anchor in local screen pixels. |
| `outline` | Text | `0,0;480,0;480,80;0,80` | Local polygon x,y;x,y;... around the reflection anchor. |
| `selection` | Text | `actor:player` | Drawing/actor selection for the capture. |
| `projection` | Vector2 | `0,0.6` | Reflection X shear and Y compression ratio. |
| `opacity` | Number | `0.4` | Receiver opacity from zero to one. |
| `tint` | Color | `#80B8D8` | Material tint multiplying the captured reflection. |

Exported ports: `<instance>.transform` (component), `<instance>.material` (component).

## wind-panel

| Argument | Kind | Default | Purpose |
|---|---|---|---|
| `clock` | Text | `Gameplay` | Clock for this recipe's moving parts. planet/follow-offset/wind-panel take Gameplay or Presentation; precipitation takes gameplay or presentation. |
| `asset` | Text | `` (empty) | Image/atlas Asset ID, not a path. |
| `position` | Vector2 | `240,180` | Anchor in local screen pixels. |
| `size` | Vector2 | `64,96` | Prepared mesh width/height in pixels. |
| `screen` | Integer | `1` | One-based screen index; use zero only where the generated argument bound allows it. |
| `amplitude` | Vector2 | `4,0` | Wind displacement per axis in pixels. |
| `rate` | Number | `0.3` | Motion rate: turns per second for planet/wind-panel, particles per second for precipitation. |
| `layer` | Text | `world` | Drawing layer for the panel. |

Exported ports: `<instance>.transform` (component), `<instance>.field` (component).

## Use an export

For an instance named `eye` made from follow-offset, a sprite can use `eye.transform`. Read its offset with a graph input source `node:eye.offset`. For an atlas instance named `counter`, target `counter.image/visible` or `counter.transform/position` in a binding/effect.

Only ports listed above are public exports. A precipitation recipe exports `emitter`, not a transform port. To change an unexposed internal control, copy the recipe under a new ID, add a suitable export, and use the modified definition.
