# What each component setting does

[Handbook](../README.md) / [Reference](README.md)

Look up a setting here when a working example needs another control. For exact types, defaults, numeric bounds and accepted choices, use the [generated tables](../../reference/PARAMETERS_GENERATED.md) or [XML catalog](../../reference/scene-parameters.xml). These descriptions explain purpose; the catalog comes from the running build's descriptors.

Put settings inside a Component as `<Value name="position" value="240,160" />`. Omitted settings use their defaults. A field marked bindable can follow a graph or effect during play. A structural field needs a source edit and reload, or a map restart for native layout/resources. [XML syntax](xml.md) explains IDs and data types.

Component `id` is your case-sensitive name. `kind` selects one of the sections below. Optional `tags="groupA;groupB"` supplies semicolon-separated selection labels. Tags do not create parents; use transforms for a moving group.

## audio

[Worked guide](../game/audio.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#audio)

| Field | What it changes |
|---|---|
| `path` | Map-relative WAV or uncompressed SoundEffect XNB; an authored WAV path can resolve its compiled XNB counterpart. |
| `bus` | Native SFX, Ambience or Music bus; respects player preferences. |
| `playing` | Desired playback state. A rising edge starts it; false fades/stops it. A finished one-shot does not restart every frame. |
| `loop` | Repeat the voice while requested. |
| `volume` | Authored volume multiplier from zero to one, combined with native bus settings. |
| `fadeIn` | Seconds to fade after start. |
| `fadeOut` | Seconds to fade when requested playback stops. |

## camera

[Worked guide](../game/camera.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#camera)

| Field | What it changes |
|---|---|
| `screen` | Screen to show, from 1 to the map count. |
| `position` | Local screen point centered by Smooth Camera. Native cut fallback shows the whole requested screen. |
| `active` | Request this view while true. An effect or binding controls how long it stays requested. |
| `transition` | cut is immediate; smooth uses the optional Smooth Camera provider. |
| `duration` | Seconds of travel to the shot; separate from how long its request stays active. |
| `returnDuration` | Seconds to return to current King tracking after release. |
| `priority` | Priority among active camera requests. Higher priority wins; ties use stable IDs. |
| `fallback` | cut allows native fallback when Smooth Camera is absent; error rejects preparation instead. |

## capture

[Worked guide](../scenes/masks.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#capture)

| Field | What it changes |
|---|---|
| `selection` | Drawing IDs, tag:name, native layer names or actor selectors. Semicolons combine tokens. Empty captures the pre-UI world frame. |
| `stage` | Capture render stage: background, world or foreground. Selected scene drawing cannot come from a later stage. |
| `enabled` | Whether this component is active. Bind a Boolean to switch it during play. |

## deformation

[Worked guide](../scenes/deformation.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#deformation)

| Field | What it changes |
|---|---|
| `field` | Field component ID supplying displacement. |
| `pin` | Fixed point of the weighted profile in local geometry coordinates. |
| `extent` | Distance scales around the pin that control the deformation weight. |
| `strength` | Multiplier applied to field displacement. |
| `falloff` | How the deformation weight grows away from the pin. |
| `axis` | Apply displacement on x, y or both axes. |
| `space` | local samples relative geometry; world samples after placement, useful for a field shared across objects. |
| `next` | Another deformation ID to apply after this one. Chains must be acyclic and at most 16 steps. |

## emitter

[Worked guide](../scenes/particles.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#emitter)

| Field | What it changes |
|---|---|
| `transform` | ID of the transform that places this drawing or sensor. An empty reference uses the default pose. |
| `asset` | Asset ID for the image/atlas. Declare its path in Assets; this field takes the ID, not the path. |
| `material` | Material component ID for color, opacity, sampling and masks. An empty reference uses normal white/opaque drawing. |
| `rate` | Continuous births per second. Zero stops new births while existing particles finish. |
| `lifetime` | Seconds each particle remains alive. |
| `velocity` | Initial X/Y speed in pixels per second. Negative Y rises. |
| `acceleration` | X/Y velocity change per second. |
| `spread` | Random X/Y variation around the initial velocity. |
| `area` | Dimensions of the birth area around the transform, not the full travel area. |
| `size` | Particle drawing scale ratio. |
| `seed` | Repeatable particle-pattern seed. |
| `capacity` | Prepared particle slots. For a steady stream, start at rate times lifetime plus room for bursts. |
| `shape` | point, box, line or disc birth distribution. |
| `clock` | Clock used for time-dependent behavior: lowercase gameplay or presentation on components. See the clocks guide. |
| `layer` | Drawing stage. Choose from this component's accepted layers in the generated table; higher stages draw later. |
| `z` | Order within this layer. Higher numbers draw later; stable component IDs settle ties. |
| `enabled` | Whether this component is active. Bind a Boolean to switch it during play. |
| `captureOnly` | Keep this drawing available to selected captures while hiding its ordinary draw. Use it to avoid showing a captured group twice. |

## ending

[Worked guide](../game/interactions.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#ending)

| Field | What it changes |
|---|---|
| `role` | Native ending role: main, nbp or owl; one adapter per role. |
| `mode` | replace uses the authored check; and requires it and the native check; or accepts either. |
| `condition` | Boolean story/item/graph gate. Bind it explicitly; the default is false. |
| `screen` | Require this screen; 0 accepts any screen. |
| `position` | Top-left corner of the optional win rectangle in local screen pixels. |
| `size` | Win-rectangle width/height. 0,0 omits it; otherwise both dimensions must be at least one pixel. |
| `requireGrounded` | Require the native grounded state as well as the other authored conditions. |

## field

[Worked guide](../scenes/deformation.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#field)

| Field | What it changes |
|---|---|
| `function` | sine for regular waves, noise for deterministic variation, curve for authored keys, spring for traveling disturbances. |
| `amplitude` | X/Y displacement multipliers. |
| `frequency` | Cycles per pixel on X/Y for the field pattern. |
| `origin` | Field sampling origin in pixels; also the fallback position for an impulse without an event point. |
| `phase` | Cycle offset in turns. Bind an integrator to phase to move a sine/noise pattern. |
| `decay` | Attenuation with absolute local Y distance from origin: exp(-abs(y)*decay). Zero disables it. |
| `curve` | Vector2 field samples such as `0,0;1,0;0,1`, interpolated along the field pattern. Graph curves use a different position:value format. |
| `seed` | Deterministic noise seed. |
| `segments` | Number of cells used by a spring field; more cells cost more state and simulation. |
| `length` | Spring-field length in pixels. |
| `direction` | Spring line direction used to map a contact point onto its length. |
| `stiffness` | Spring restoring strength. |
| `propagation` | How strongly neighboring spring cells transmit displacement. |
| `damping` | How quickly spring motion loses energy. |
| `limit` | Maximum spring displacement in pixels. |
| `impulseRadius` | Radius of a spring impulse as a fraction of the field length. |
| `clock` | Clock used for time-dependent behavior: lowercase gameplay or presentation on components. See the clocks guide. |

## hiddenWall

[Worked guide](../game/interactions.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#hiddenwall)

| Field | What it changes |
|---|---|
| `texture` | Exact native texture_name used by an existing Hidden Wall. |
| `position` | Canonical world position of the wall artwork, not its hitbox center. On screen n, world Y is localY-(n-1)*360. |
| `matchPosition` | Require the position as well as texture to identify a wall. Necessary when several walls reuse a texture. |

## intro

[Worked guide](../game/results.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#intro)

| Field | What it changes |
|---|---|
| `order` | Display order. Lower values appear first; IDs settle ties. |
| `value` | Opening page text; use &#10; for a line break in an XML attribute. |
| `font` | Native font preset: menu, small, style, location or gargoyle. |
| `color` | Color multiplier, normally authored as #RRGGBB. White keeps the original colors; alpha/opacity control transparency. |
| `fadeIn` | Seconds to fade in; the native fast-intro path caps it at 0.5. |
| `stay` | Seconds to hold the page; the fast path caps it at one. |
| `fadeOut` | Seconds to fade out; the fast path caps it at 0.5. |
| `enabled` | Whether this component is active. Bind a Boolean to switch it during play. |

## item

[Worked guide](../game/interactions.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#item)

| Field | What it changes |
|---|---|
| `name` | Exact native Items enum name, such as GoldRing. Observes inventory; it does not create, equip or consume an item. |

## light

[Worked guide](../scenes/materials.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#light)

| Field | What it changes |
|---|---|
| `transform` | ID of the transform that places this drawing or sensor. An empty reference uses the default pose. |
| `color` | Color multiplier, normally authored as #RRGGBB. White keeps the original colors; alpha/opacity control transparency. |
| `intensity` | Light strength. Begin around 0.05 and inspect at game scale; zero skips the light. |
| `radius` | Reach in screen pixels. |
| `cone` | Angular spread in degrees; 360 is radial. |
| `softness` | Falloff shaping from zero to one. |
| `enabled` | Whether this component is active. Bind a Boolean to switch it during play. |
| `occluders` | Semicolon-separated component IDs or tag:name selections of receiver components in occlusion mode. |
| `scatter` | Additional spread/falloff control. Start at zero and compare before raising it. |
| `layer` | Drawing stage. Choose from this component's accepted layers in the generated table; higher stages draw later. |
| `z` | Order within this layer. Higher numbers draw later; stable component IDs settle ties. |
| `captureOnly` | Keep this drawing available to selected captures while hiding its ordinary draw. Use it to avoid showing a captured group twice. |

## material

[Worked guide](../scenes/materials.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#material)

| Field | What it changes |
|---|---|
| `color` | Color multiplier, normally authored as #RRGGBB. White keeps the original colors; alpha/opacity control transparency. |
| `opacity` | Opacity multiplier from zero (transparent) to one (fully visible). |
| `emission` | Additional emissive drawing strength. It does not create a light illuminating nearby objects. |
| `sampling` | point keeps hard pixels; linear interpolates when the drawing transform also opts out of pixelSnap. |
| `blend` | alpha draws normal transparency, add adds brightness, multiply darkens using the source. |
| `address` | clamp holds the texture edge; wrap repeats it for UV scrolling/tiling. |
| `channel` | rgba uses source colors; alpha selects its alpha silhouette, useful for tintable masks and captures. |
| `mask` | Asset ID whose alpha clips the drawing. Opaque reveals; transparent hides. |
| `maskSpace` | local moves the mask with the object; screen places it in screen-pixel coordinates. |
| `capture` | Capture component supplying an image instead of an ordinary texture. |
| `uvOffset` | Texture offset in turns: 1,0 moves by one full texture width. |
| `uvScale` | Texture repetition/scale ratio on each axis. |
| `ambient` | Ambient shading contribution for projected surfaces. |
| `diffuse` | Directional shading contribution for projected surfaces. |
| `normalLight` | Two-axis direction used by projected-surface shading. |

## mesh

[Worked guide](../scenes/deformation.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#mesh)

| Field | What it changes |
|---|---|
| `transform` | ID of the transform that places this drawing or sensor. An empty reference uses the default pose. |
| `asset` | Asset ID for the image/atlas. Declare its path in Assets; this field takes the ID, not the path. |
| `material` | Material component ID for color, opacity, sampling and masks. An empty reference uses normal white/opaque drawing. |
| `geometry` | grid for a rectangular flexible image, polygon for a local outline, disc for round projection. |
| `size` | Width and height in pixels for grid/disc geometry; the transform anchors its center. |
| `outline` | Polygon points in local pixels, written x,y;x,y;... in boundary order. |
| `columns` | Horizontal subdivisions prepared for grid/disc deformation. More cells cost more vertices. |
| `rows` | Vertical subdivisions prepared for grid/disc deformation. |
| `projection` | planar, spherical, cylindrical or perspective texture projection. |
| `projectionTilt` | Projection tilt angles in degrees. |
| `deformation` | Deformation component ID. Empty leaves the mesh undeformed. |
| `visible` | Whether this object is visible. A transform also hides its children when false. |
| `layer` | Drawing stage. Choose from this component's accepted layers in the generated table; higher stages draw later. |
| `z` | Order within this layer. Higher numbers draw later; stable component IDs settle ties. |
| `captureOnly` | Keep this drawing available to selected captures while hiding its ordinary draw. Use it to avoid showing a captured group twice. |

## npc

[Worked guide](../game/dialogue.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#npc)

| Field | What it changes |
|---|---|
| `name` | Exact native OldManSettings name, not its XML filename. The component attaches to an existing NPC. |
| `voice` | Audio component ID for character sound. Empty keeps the original native voice. |
| `muted` | Silence character sound for this NPC. |
| `linePause` | Seconds after a line; -1 keeps the native 2.2-second pause. |
| `quoteOverride` | Let the scene select the line using quote while true. |
| `quote` | Text used when quoteOverride is true. |
| `offset` | Visual X/Y offset of the native NPC sprite. |
| `tint` | Native NPC sprite color multiplier. |
| `opacity` | Opacity multiplier from zero (transparent) to one (fully visible). |
| `visible` | Show/hide the native NPC sprite without removing the conversation entity. |
| `textVisible` | Show/hide its speech bubble/text. |

## receiver

[Worked guide](../scenes/surfaces.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#receiver)

| Field | What it changes |
|---|---|
| `transform` | ID of the transform that places this drawing or sensor. An empty reference uses the default pose. |
| `material` | Material component ID for color, opacity, sampling and masks. An empty reference uses normal white/opaque drawing. |
| `outline` | Ordered local polygon boundary x,y;x,y;... around the transform. |
| `mode` | reflection projects RGBA; shadow projects captured alpha; occlusion prepares an invisible contour for selected lights. |
| `projection` | X projection shear and Y compression ratio. A zero Y projection produces no projected drawing. |
| `opacity` | Opacity multiplier from zero (transparent) to one (fully visible). |
| `layer` | Drawing stage. Choose from this component's accepted layers in the generated table; higher stages draw later. |
| `z` | Order within this layer. Higher numbers draw later; stable component IDs settle ties. |
| `captureOnly` | Keep this drawing available to selected captures while hiding its ordinary draw. Use it to avoid showing a captured group twice. |

## result

[Worked guide](../game/results.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#result)

| Field | What it changes |
|---|---|
| `page` | Page title/group key. All rows with the same title form one page. |
| `role` | any, main, nbp or owl. Rows on one page must share the role. |
| `order` | Display order. Lower values appear first; IDs settle ties. |
| `label` | Optional text before the value. |
| `value` | Displayed value, usually a bound format/template result. |
| `color` | Page text color, read from the first enabled row in display order. |
| `background` | Page background color, read from the first enabled row in display order. |
| `enabled` | Whether this component is active. Bind a Boolean to switch it during play. |

## sensor

[Worked guide](../scenes/contact-feedback.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#sensor)

| Field | What it changes |
|---|---|
| `transform` | ID of the transform that places this drawing or sensor. An empty reference uses the default pose. |
| `shape` | box/disc/polygon is a region; surface selects actual observed collision surfaces. |
| `size` | Full width/height of box/disc around its transform center. |
| `outline` | Local polygon boundary points x,y;x,y;... for polygon shape. |
| `selection` | For surface shape, exact surface IDs, type:Full.Type.Name or tag:name separated by semicolons. Empty accepts all observed surfaces. |
| `actor` | Observed actor ID. player is the native gameplay feed; * accepts all actors actually supplied to the sensor system, not an automatic multiplayer search. |
| `phase` | contact samples movement evidence before input; scene samples after the native update. Use contact for actual surfaces or pre-return contact. |
| `hysteresis` | Extra exit-boundary padding in pixels for an existing region membership; reduces boundary flicker. |
| `dwell` | Seconds of qualifying contact before entry is accepted. |
| `minimumSpeed` | Minimum contact speed in pixels per second for surface sensors. |
| `initial` | baseline records initial overlap without enter; fire allows initial overlap to emit enter. |
| `enabled` | Whether this component is active. Bind a Boolean to switch it during play. |

## sprite

[Worked guide](../scenes/texture-animation.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#sprite)

| Field | What it changes |
|---|---|
| `transform` | ID of the transform that places this drawing or sensor. An empty reference uses the default pose. |
| `asset` | Asset ID for the image/atlas. Declare its path in Assets; this field takes the ID, not the path. |
| `material` | Material component ID for color, opacity, sampling and masks. An empty reference uses normal white/opaque drawing. |
| `origin` | Image point placed at the transform anchor. 0,0 is the top-left corner; half the image dimensions centers it. |
| `frame` | Zero-based atlas cell for manual playback or a bound frame selection. |
| `frameTransition` | Seconds spent sliding or crossfading to a changed frame. |
| `transition` | none changes frames directly; slide clips the change to the cell; crossfade blends the old and new frames. |
| `playback` | loop repeats, once stops at the last frame, pingpong reverses, manual uses frame. Asset fps controls automatic speed. |
| `clock` | Clock used for time-dependent behavior: lowercase gameplay or presentation on components. See the clocks guide. |
| `layer` | Drawing stage. Choose from this component's accepted layers in the generated table; higher stages draw later. |
| `z` | Order within this layer. Higher numbers draw later; stable component IDs settle ties. |
| `visible` | Whether this object is visible. A transform also hides its children when false. |
| `captureOnly` | Keep this drawing available to selected captures while hiding its ordinary draw. Use it to avoid showing a captured group twice. |

## stamp

[Worked guide](../scenes/contact-feedback.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#stamp)

| Field | What it changes |
|---|---|
| `asset` | Asset ID for the image/atlas. Declare its path in Assets; this field takes the ID, not the path. |
| `material` | Material component ID for color, opacity, sampling and masks. An empty reference uses normal white/opaque drawing. |
| `mask` | Alpha-mask asset in screen coordinates, often a collision-derived contact mask. |
| `duration` | Seconds a mark stays alive. Atlas frames advance over that lifetime. |
| `capacity` | Maximum live marks. A new mark removes the oldest when the pool is full. |
| `clock` | Clock used for time-dependent behavior: lowercase gameplay or presentation on components. See the clocks guide. |
| `layer` | Drawing stage. Choose from this component's accepted layers in the generated table; higher stages draw later. |
| `z` | Order within this layer. Higher numbers draw later; stable component IDs settle ties. |
| `captureOnly` | Keep this drawing available to selected captures while hiding its ordinary draw. Use it to avoid showing a captured group twice. |

## text

[Worked guide](../scenes/narrative.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#text)

| Field | What it changes |
|---|---|
| `transform` | ID of the transform that places this drawing or sensor. An empty reference uses the default pose. |
| `material` | Material component ID for color, opacity, sampling and masks. An empty reference uses normal white/opaque drawing. |
| `font` | Native menu, small, style, location or gargoyle preset; otherwise an Asset ID for a bitmap atlas. |
| `characters` | Exact cell-to-character order for a bitmap font. Native fonts ignore this table. |
| `spacing` | Extra pixels between bitmap-font cells. |
| `value` | Text to draw. Bind format/template output for changing values. |
| `visibleCharacters` | How much of the string is revealed, counted in UTF-16 code units; drives text observation events. |
| `width` | Wrapping width in pixels. |
| `align` | left, center or right around the text anchor, applied independently to each wrapped line. |
| `layer` | Drawing stage. Choose from this component's accepted layers in the generated table; higher stages draw later. |
| `z` | Order within this layer. Higher numbers draw later; stable component IDs settle ties. |
| `captureOnly` | Keep this drawing available to selected captures while hiding its ordinary draw. Use it to avoid showing a captured group twice. |

## transform

[Worked guide](../scenes/placement.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#transform)

| Field | What it changes |
|---|---|
| `position` | Anchor in local screen pixels: X right, Y down. Child transforms use offsets from their parent. |
| `scale` | Width/height ratios. 1,1 keeps size; 2,2 doubles it; negative axes mirror it. |
| `rotation` | Clockwise visual rotation in degrees around the transform anchor. |
| `parent` | Another transform ID. Move/rotate/scale the parent to move the group. Cycles are invalid. |
| `screen` | 1 draws on screen 1; 0 repeats at the same local coordinates on every visible screen. |
| `visible` | Whether this object is visible. A transform also hides its children when false. |
| `pixelSnap` | Round final geometry to logical pixels and force point sampling. Disable explicitly for smooth/subpixel drawing. |

## view

[Worked guide](../scenes/views.md) | [Exact fields](../../reference/PARAMETERS_GENERATED.md#view)

| Field | What it changes |
|---|---|
| `screen` | 0 is the global view; a screen-specific view takes precedence with its own field values. |
| `ambient` | Ambient scene color. |
| `tint` | Screen tint color, mixed using tintAmount. |
| `tintAmount` | Tint mix from zero (none) to one (full). |
| `exposure` | Screen brightness multiplier. |
| `mirror` | none, horizontal, vertical or both. Visual mirroring does not change collision. |
| `timer` | inherit, show or hide the native timer. |
| `intro` | Simple native opening text. Use escaped <newline> markers for line breaks, or intro components for pages. |
| `animateWhilePaused` | Allow Presentation-clock visuals through pause. Rules, trees, camera and audio still pause. |
