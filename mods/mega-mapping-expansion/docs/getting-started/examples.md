# Choose a complete example

[Handbook](../README.md) / [Getting started](README.md)

Use one example at a time in a copy of a working native map. A **complete scene** means a complete MME entry, not a whole native level. The table tells you what else it needs. [Loading steps](worldsmith.md) and [combining examples](combining.md) apply to every scene.

## Learn the basics

The [seven lesson folders](../../examples/lessons/README.md) each contain their own XML and required assets. Copy the contents into the map root beside `level_settings.xml`. [The lesson sequence](lessons.md) explains expected results and changes to try.

| Example | Needed beyond the native test map | Result |
|---|---|---|
| [First image](first-scene.md) | panel.png supplied in lesson 1 | One panel at 240,160 on screen 1 |
| [Grouping](../scenes/placement.md#move-several-images-together) | panel.png | Two images move around a shared parent |
| [Image plus counter](combining.md) | panel.png | Both features in one complete entry |
| [Atlas playback](../scenes/texture-animation.md#play-an-atlas) | digits.png | Pingpong animation |
| [Saved jump counter and flash](../scenes/behaviors.md#count-jumps-and-flash-a-light) | None | Count and light pulse on jump |
| [Timed sequence](../scenes/behavior-trees.md#a-complete-timed-sequence) | None | One-second light that restarts on jump |
| [Wait for a future event](../scenes/behavior-trees.md#coordinate-two-events) | None | Latch after the first jump |

## Drawing and reactive effects

For sample art/audio, use the files in `examples/minimal/mme-assets` or the relevant lesson. That combined sample is a feature demonstration; it isn't the first lesson.

| Example | Required assets | Result |
|---|---|---|
| [Tint and opacity](../scenes/materials.md#fade-or-recolor-a-sprite) | panel.png | Tinted translucent panel |
| [Masked group](../scenes/masks.md#mask-one-image-or-a-whole-group) | panel.png; replace the demonstration mask for a real cutout | Group drawing through alpha |
| [Reflection](../scenes/surfaces.md#reflection-and-shadow) | None; live King for actor capture | Reflection in a bottom-screen polygon |
| [Pinned panel](../scenes/deformation.md#bend-artwork-with-fields) | panel.png | Lower edge stays pinned while it bends |
| [Upward particles](../scenes/particles.md#emit-an-upward-stream) | mote.png | Rising stream |
| [Scroll material](../scenes/patterns.md#scroll-a-material) | surface.png | Texture moves inside a stationary image |
| [Burst](../scenes/patterns.md#burst-particles-from-an-event) | mote.png | Particles on a jump |
| [Spring ripple](../scenes/patterns.md#disturb-a-spring-field) | panel.png | Event disturbs a flexible surface |
| [Nested composition](../scenes/patterns.md#nest-a-reusable-composition) | mote.png | A reusable rising-particle composition |
| [Library recipes](../scenes/recipes.md#use-the-library) | surface.png, mote.png | Rotating surface and following accent |
| [Own recipe](../scenes/recipes.md#define-your-own-preset) | None | Two lamp instances |
| [Contact stamp](../scenes/contact-feedback.md#place-a-transient-mark-at-contact) | Named image/mask in the guide; specified block provider | Mark at captured surface contact |
| [Screen presentation](../scenes/views.md#try-a-screen-tint) | None | Tint and timer setting |

## Text and game observations

| Example | Prerequisite | Result |
|---|---|---|
| [Timed caption](../scenes/narrative.md#show-and-change-a-caption) | None | Caption after a jump |
| [Bitmap font](../scenes/narrative.md#native-and-bitmap-fonts) | digits.png | Number drawn with atlas glyphs |
| [Languages/templates](../scenes/localized-text.md#choose-a-language-and-insert-values) | Font with the required glyphs | French/English counter phrase |
| [Vector sprite](../scenes/vectors.md#a-complete-vector-backed-sprite) | Both XML files in the guide | Stepped diamond |
| [Rolling digits](../scenes/texture-animation.md#build-rolling-decimal-digits) | digits.png | Two-digit saved count |
| [Charge display](../observations/charge.md#a-charge-driven-display) | digits.png | Ten-cell charge meter |
| [Corrected-launch check](../observations/charge.md#judge-a-corrected-launch) | Exact native/corrected release evidence | Flash when the authored tolerance matches |
| [Falling-start reaction](../observations/README.md#example-react-when-falling-starts) | None | Flash at start of fall |

## Native systems and map structure

| Example | Native setup/resources needed | Result |
|---|---|---|
| [Sound zone](../game/audio.md#a-sound-zone) | chime.wav | Loop/fades in an area |
| [NPC voice](../game/dialogue.md#give-one-npc-a-voice) | Existing named NPC and chime.wav | Individual voice and line reaction |
| [Text reveal](../game/dialogue.md#reveal-scene-text) | chime.wav | Caption reveals over time |
| [Item marker](../game/interactions.md#items) | Native GoldRing/inventory | Marker while owned |
| [Hidden wall](../game/interactions.md#hidden-walls) | Native closeZone wall | Count real entry |
| [Native wall definition](../scenes/contact-feedback.md#preserve-native-hidden-walls) | Wall texture/audio named by the guide | Exact native grouped trigger |
| [Victory](../game/interactions.md#victory-conditions) | Ring and working native main ending on screen 2 | Require ring plus authored win area |
| [Camera](../game/camera.md#a-complete-screen-showcase) | At least two screens; optional Smooth Camera | Show screen 2, then return |
| [Remote reveal](../scenes/remote-scenes.md#show-a-distant-change) | Two screens and panel.png | Reveal distant panel and show it |
| [Intro/results](../game/results.md) | Normal intro/working completion path | Opening pages and final count |
| [Ending handshake](../integration/endings.md#a-complete-effect-handshake) | Working main ending resources; both example files | Cue waits for a scene effect |
| [Existing grid with level+](../maps/collision.md) | Your collision grid and actual screen count | Same rooms and 8-pixel block size through MME |
| [Finer platforms](../maps/finer-collision.md) | The same grid, enlarged with nearest-neighbour scaling | Smaller blocks, with old slope scale preserved where needed |
| [Optional one-screen sample](../../examples/maps/one-screen/README.md) | A separate disposable one-screen map | Ready floor/ledge collision |
| [Mixed resolutions](../reference/collision.md#other-resolutions-and-strips) | Matching level+ image/grid | Coarse and fine screens in one atlas |
| [Native/atlas layouts](../maps/large-maps.md) | Matching collision capacity and native art | Explicit authored screen count |
| [Two-screen links](../maps/teleports.md) | Two native screens, openings/arrivals | Edge connection and return |
| [Three-screen random route](../../examples/transitions/three-screen/README.md) | Three native screens, openings/arrivals | Alternating no-repeat destination pool |
| [Imported room](../maps/physics-frames.md) | Source room and matching collision/charge setup | Preserve reference-screen arithmetic |
| [Required mods](../maps/map-policies.md) | Runtime installed | Load-time policy checks |

For custom slopes, route schemes, API consumers and native ending node choices, use [the full feature checklist](../reference/features.md) and the linked references. Their contracts have prerequisites beyond a standalone decorative scene.
