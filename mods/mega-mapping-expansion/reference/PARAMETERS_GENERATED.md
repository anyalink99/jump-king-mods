# Construction reference

Generated from the component registry and embedded recipes. Structural fields require resource preparation; other fields accept typed graph bindings and owned effect contributions.

## audio

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| path | Text |  |  | no | Structural |  |
| bus | Text | SFX |  | no | Structural | SFX, Ambience, Music |
| playing | Boolean | false |  | yes | None |  |
| loop | Boolean | false |  | yes | None |  |
| volume | Number | 1 |  | yes | None |  |
| fadeIn | Number | 0 | seconds | yes | None |  |
| fadeOut | Number | 0 | seconds | yes | None |  |

## camera

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| screen | Integer | 1 |  | no | Structural |  |
| position | Vector2 | 240,180 | px | yes | None |  |
| active | Boolean | false |  | yes | None |  |
| transition | Text | cut |  | no | Structural | cut, smooth |
| duration | Number | 1 | seconds | yes | None |  |
| returnDuration | Number | 0.5 | seconds | yes | None |  |
| priority | Integer | 0 |  | no | Structural |  |
| fallback | Text | cut |  | no | Structural | cut, error |

## capture

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| selection | Text |  |  | no | Structural |  |
| stage | Text | world |  | no | Structural | background, world, foreground |
| enabled | Boolean | true |  | yes | None |  |

## deformation

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| field | Text |  |  | no | Structural |  |
| pin | Vector2 | 0,0 | px | yes | None |  |
| extent | Vector2 | 1,1 | px | yes | None |  |
| strength | Number | 1 |  | yes | None |  |
| falloff | Number | 2 |  | yes | None |  |
| axis | Text | both |  | no | Structural | x, y, both |
| space | Text | local |  | no | Structural | local, world |
| next | Text |  |  | no | Structural |  |

## emitter

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| transform | Text |  |  | no | Structural |  |
| asset | Text |  |  | no | Structural |  |
| material | Text |  |  | no | Structural |  |
| rate | Number | 10 | particles/s | yes | None |  |
| lifetime | Number | 1 | seconds | yes | None |  |
| velocity | Vector2 | 0,0 | px | yes | None |  |
| acceleration | Vector2 | 0,0 | px | yes | None |  |
| spread | Vector2 | 0,0 | px | yes | None |  |
| area | Vector2 | 0,0 | px | yes | None |  |
| size | Number | 1 |  | yes | None |  |
| seed | Integer | 0 |  | no | Structural |  |
| capacity | Integer | 256 |  | no | Structural |  |
| shape | Text | point |  | no | Structural | point, box, line, disc |
| clock | Text | gameplay |  | no | Structural | gameplay, presentation |
| layer | Text | world |  | no | Structural | background, world, surface, foreground, composite |
| z | Number | 0 |  | yes | Membership |  |
| enabled | Boolean | true |  | yes | None |  |
| captureOnly | Boolean | false |  | yes | Membership |  |

## ending

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| role | Text | main |  | no | Structural | main, nbp, owl |
| mode | Text | replace |  | no | Structural | replace, and, or |
| condition | Boolean | false |  | yes | None |  |
| screen | Integer | 0 |  | no | Structural |  |
| position | Vector2 | 0,0 | px | yes | None |  |
| size | Vector2 | 0,0 | px | yes | None |  |
| requireGrounded | Boolean | true |  | yes | None |  |

## field

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| function | Text | sine |  | no | Structural | sine, noise, curve, spring |
| amplitude | Vector2 | 1,1 | px | yes | None |  |
| frequency | Vector2 | 1,1 | cycles/px | yes | None |  |
| origin | Vector2 | 0,0 | px | yes | None |  |
| phase | Number | 0 | turns | yes | None |  |
| decay | Number | 0 |  | yes | None |  |
| curve | Text |  |  | no | Structural |  |
| seed | Integer | 0 |  | no | Structural |  |
| segments | Integer | 64 |  | no | Structural |  |
| length | Number | 480 | px | yes | None |  |
| direction | Vector2 | 1,0 | ratio | yes | None |  |
| stiffness | Number | 32 |  | yes | None |  |
| propagation | Number | 120 |  | yes | None |  |
| damping | Number | 4 |  | yes | None |  |
| limit | Number | 64 | px | yes | None |  |
| impulseRadius | Number | 0.04 |  | yes | None |  |
| clock | Text | gameplay |  | no | Structural | gameplay, presentation |

## hiddenWall

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| texture | Text |  |  | no | Structural |  |
| position | Vector2 | 0,0 | px | no | Structural |  |
| matchPosition | Boolean | false |  | no | Structural |  |

## item

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| name | Text |  |  | no | Structural |  |

## light

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| transform | Text |  |  | no | Structural |  |
| color | Color | 1,1,1,1 |  | yes | Material |  |
| intensity | Number | 1 |  | yes | None |  |
| radius | Number | 64 | px | yes | None |  |
| cone | Number | 360 | degrees | yes | None |  |
| softness | Number | 0.5 |  | yes | None |  |
| enabled | Boolean | true |  | yes | None |  |
| occluders | Text |  |  | no | Structural |  |
| scatter | Number | 0 |  | yes | None |  |
| layer | Text | world |  | no | Structural | background, world, foreground |
| z | Number | 0 |  | yes | Membership |  |
| captureOnly | Boolean | false |  | yes | Membership |  |

## material

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| color | Color | 1,1,1,1 |  | yes | Material |  |
| opacity | Number | 1 |  | yes | None |  |
| emission | Number | 0 |  | yes | None |  |
| sampling | Text | point |  | no | Structural | point, linear |
| blend | Text | alpha |  | no | Structural | alpha, add, multiply |
| address | Text | clamp |  | no | Structural | clamp, wrap |
| channel | Text | rgba |  | no | Structural | rgba, alpha |
| mask | Text |  |  | no | Structural |  |
| maskSpace | Text | local |  | no | Structural | local, screen |
| capture | Text |  |  | no | Structural |  |
| uvOffset | Vector2 | 0,0 | turns | yes | None |  |
| uvScale | Vector2 | 1,1 | ratio | yes | None |  |
| ambient | Number | 1 |  | yes | None |  |
| diffuse | Number | 0 |  | yes | None |  |
| normalLight | Vector2 | 0,0 | ratio | yes | None |  |

## mesh

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| transform | Text |  |  | no | Structural |  |
| asset | Text |  |  | no | Structural |  |
| material | Text |  |  | no | Structural |  |
| geometry | Text | grid |  | no | Structural | grid, polygon, disc |
| size | Vector2 | 64,64 | px | yes | Geometry |  |
| outline | Text |  |  | no | Structural |  |
| columns | Integer | 16 |  | no | Structural |  |
| rows | Integer | 16 |  | no | Structural |  |
| projection | Text | planar |  | no | Structural | planar, spherical, cylindrical, perspective |
| projectionTilt | Vector2 | 0,0 | degrees | yes | Geometry |  |
| deformation | Text |  |  | no | Structural |  |
| visible | Boolean | true |  | yes | None |  |
| layer | Text | world |  | no | Structural | background, world, surface, foreground, composite |
| z | Number | 0 |  | yes | Membership |  |
| captureOnly | Boolean | false |  | yes | Membership |  |

## npc

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| name | Text |  |  | no | Structural |  |
| voice | Text |  |  | no | Structural |  |
| muted | Boolean | false |  | yes | None |  |
| linePause | Number | -1 | seconds | yes | None |  |

## receiver

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| transform | Text |  |  | no | Structural |  |
| material | Text |  |  | no | Structural |  |
| outline | Text |  |  | no | Structural |  |
| mode | Text | shadow |  | no | Structural | shadow, reflection, occlusion |
| projection | Vector2 | 0,0.35 | ratio | yes | None |  |
| opacity | Number | 0.5 |  | yes | None |  |
| layer | Text | surface |  | no | Structural | background, world, surface, foreground, composite |
| z | Number | 0 |  | yes | Membership |  |
| captureOnly | Boolean | false |  | yes | Membership |  |

## sensor

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| transform | Text |  |  | no | Structural |  |
| shape | Text | box |  | no | Structural | box, disc, polygon, surface |
| size | Vector2 | 8,8 | px | yes | None |  |
| outline | Text |  |  | no | Structural |  |
| selection | Text |  |  | no | Structural |  |
| actor | Text | player |  | no | Structural |  |
| phase | Text | contact |  | no | Structural | contact, scene |
| hysteresis | Number | 0 |  | yes | None |  |
| dwell | Number | 0 | seconds | yes | None |  |
| minimumSpeed | Number | 0 | px/s | yes | None |  |
| initial | Text | baseline |  | no | Structural | baseline, fire |
| enabled | Boolean | true |  | yes | None |  |

## sprite

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| transform | Text |  |  | no | Structural |  |
| asset | Text |  |  | no | Structural |  |
| material | Text |  |  | no | Structural |  |
| origin | Vector2 | 0,0 | px | yes | None |  |
| frame | Number | 0 | frame | yes | None |  |
| frameTransition | Number | 0 | seconds | yes | None |  |
| transition | Text | none |  | no | Structural | none, slide, crossfade |
| playback | Text | loop |  | no | Structural | manual, loop, once, pingpong |
| clock | Text | gameplay |  | no | Structural | gameplay, presentation |
| layer | Text | world |  | no | Structural | background, world, surface, foreground, composite, hud |
| z | Number | 0 |  | yes | Membership |  |
| visible | Boolean | true |  | yes | None |  |
| captureOnly | Boolean | false |  | yes | Membership |  |

## stamp

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| asset | Text |  |  | no | Structural |  |
| material | Text |  |  | no | Structural |  |
| mask | Text |  |  | no | Structural |  |
| duration | Number | 1 | seconds | yes | None |  |
| capacity | Integer | 8 |  | no | Structural |  |
| clock | Text | gameplay |  | no | Structural | gameplay, presentation |
| layer | Text | world |  | no | Structural | background, world, surface, foreground, composite |
| z | Number | 0 |  | yes | Membership |  |
| captureOnly | Boolean | false |  | yes | Membership |  |

## text

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| transform | Text |  |  | no | Structural |  |
| material | Text |  |  | no | Structural |  |
| font | Text | menu |  | no | Structural |  |
| characters | Text | 0123456789+- .:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz |  | no | Structural |  |
| spacing | Number | 0 |  | yes | None |  |
| value | Text |  |  | yes | None |  |
| visibleCharacters | Number | 65535 | characters | yes | None |  |
| width | Number | 440 |  | yes | None |  |
| align | Text | center |  | no | Structural | left, center, right |
| layer | Text | world |  | no | Structural | background, world, surface, foreground, composite, hud |
| z | Number | 0 |  | yes | Membership |  |
| captureOnly | Boolean | false |  | yes | Membership |  |

## transform

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| position | Vector2 | 0,0 | px | yes | Pose |  |
| scale | Vector2 | 1,1 | ratio | yes | Pose |  |
| rotation | Number | 0 | degrees | yes | Pose |  |
| parent | Text |  |  | no | Structural |  |
| screen | Integer | 1 | screen | yes | Membership |  |
| visible | Boolean | true |  | yes | None |  |
| pixelSnap | Boolean | true |  | yes | None |  |

## view

| Property | Type | Default | Units | Bindable | Impact | Choices |
|---|---|---|---|---|---|---|
| screen | Integer | 0 |  | no | Structural |  |
| ambient | Color | 1,1,1,1 |  | yes | Material |  |
| tint | Color | 1,1,1,1 |  | yes | Material |  |
| tintAmount | Number | 0 |  | yes | None |  |
| exposure | Number | 1 |  | yes | None |  |
| mirror | Text | none |  | no | Structural | none, horizontal, vertical, both |
| timer | Text | inherit |  | no | Structural | inherit, show, hide |
| intro | Text |  |  | no | Structural |  |
| animateWhilePaused | Boolean | false |  | yes | None |  |

## Graph operations

`constant` `input` `pass` `add` `subtract` `multiply` `divide` `vector` `x` `y` `length` `normalize` `dot` `angle` `sin` `cos` `abs` `floor` `round` `minimum` `maximum` `clamp` `remap` `wrap` `curve` `select` `less` `greater` `equal` `and` `or` `not` `smooth` `spring` `integrate` `delay` `number` `integer` `format` `power` `hysteresis` `available` `quality`

## Recipes

Recipes expand to ordinary components, graph nodes and bindings. They have no dedicated runtime objects.

### atlas-display

- `asset` (Text): required
- `source` (Text): default `charge.heldFrames`
- `position` (Vector2): default `240,180`
- `origin` (Vector2): default `0,0`
- `screen` (Number): default `1`
- `minimum` (Number): default `1`
- `maximum` (Number): default `35`
- `frames` (Number): default `35`
- `rounding` (Text): default `floor`

### atlas-digit

- `asset` (Text): required
- `source` (Text): required
- `position` (Vector2): default `240,180`
- `screen` (Number): default `1`
- `digit` (Number): default `0`
- `roll` (Number): default `0.24`

### follow-offset

- `clock` (Text): default `Gameplay`
- `target` (Text): default `player.center`
- `origin` (Vector2): default `240,180`
- `extent` (Vector2): default `3,2`
- `response` (Number): default `7`
- `nearRadius` (Number): default `24`
- `screen` (Number): default `1`

### planet

- `clock` (Text): default `Gameplay`
- `surface` (Text): default ``
- `overlay` (Text): default ``
- `position` (Vector2): default `240,180`
- `size` (Vector2): default `240,240`
- `screen` (Number): default `1`
- `rate` (Number): default `0.008333333333333333`
- `overlayRate` (Number): default `0.0070921985815602835`
- `sampling` (Text): default `point`
- `tilt` (Vector2): default `0,0`

### precipitation

- `clock` (Text): default `gameplay`
- `asset` (Text): default ``
- `screen` (Integer): default `1`
- `position` (Vector2): default `240,-16`
- `area` (Vector2): default `600,0`
- `velocity` (Vector2): default `-12,180`
- `spread` (Vector2): default `16,40`
- `rate` (Number): default `80`
- `lifetime` (Number): default `3`
- `seed` (Integer): default `1`
- `size` (Number): default `1`

### surface-reflection

- `screen` (Integer): default `1`
- `position` (Vector2): default `0,280`
- `outline` (Text): default `0,0;480,0;480,80;0,80`
- `selection` (Text): default `actor:player`
- `projection` (Vector2): default `0,0.6`
- `opacity` (Number): default `0.4`
- `tint` (Color): default `#80B8D8`

### wind-panel

- `clock` (Text): default `Gameplay`
- `asset` (Text): default ``
- `position` (Vector2): default `240,180`
- `size` (Vector2): default `64,96`
- `screen` (Integer): default `1`
- `amplitude` (Vector2): default `4,0`
- `rate` (Number): default `0.3`
- `layer` (Text): default `world`

