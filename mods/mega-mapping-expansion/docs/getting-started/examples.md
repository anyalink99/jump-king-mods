# Worked examples

[Handbook](../index.md) / [Getting started](index.md)

Each link below opens a complete scene in its guide. Copy the example into a
playable map, add the assets named by that guide, and try it before combining
it with another example. Sample artwork is in the
[minimal map assets](../../examples/minimal/mapping_settings.xml).

Several files enclosed by an example marker belong to one example. Merge XML
sections when combining scenes, keep IDs unique, and follow
[the loading workflow](files.md#edit-and-load) before testing in the game.

The catalogue lists complete `mapping_settings.xml` examples. The
[collision](../maps/collision.md), [ending](../integration/endings.md) and
[map policy](../maps/map-policies.md) guides also contain native XML fragments
and configuration examples in their own formats.

## Getting started

- [Split a scene into files](files.md#split-a-scene-into-files)
- [First scene](first-scene.md#first-scene)
- [Make an area react](first-scene.md#make-an-area-react)

## Scene authoring

- [A complete timed sequence](../scenes/behavior-trees.md#a-complete-timed-sequence)
- [Coordinate two events](../scenes/behavior-trees.md#coordinate-two-events)
- [Count jumps and flash a light](../scenes/behaviors.md#count-jumps-and-flash-a-light)
- [Place a transient mark at contact](../scenes/contact-feedback.md#place-a-transient-mark-at-contact)
- [Preserve native hidden walls](../scenes/contact-feedback.md#preserve-native-hidden-walls)
- [Show and change a caption](../scenes/narrative.md#show-and-change-a-caption)
- [Native and bitmap fonts](../scenes/narrative.md#native-and-bitmap-fonts)
- [Mask one image or a whole group](../scenes/objects.md#mask-one-image-or-a-whole-group)
- [Reflection and shadow](../scenes/objects.md#reflection-and-shadow)
- [Bend artwork with fields](../scenes/objects.md#bend-artwork-with-fields)
- [Emit an upward stream](../scenes/objects.md#emit-an-upward-stream)
- [Scroll a material](../scenes/patterns.md#scroll-a-material)
- [Burst particles from an event](../scenes/patterns.md#burst-particles-from-an-event)
- [Disturb a spring field](../scenes/patterns.md#disturb-a-spring-field)
- [Nest a reusable composition](../scenes/patterns.md#nest-a-reusable-composition)
- [Use the library](../scenes/recipes.md#use-the-library)
- [Define your own preset](../scenes/recipes.md#define-your-own-preset)
- [Show a distant change](../scenes/remote-scenes.md#show-a-distant-change)
- [Play an atlas](../scenes/texture-animation.md#play-an-atlas)
- [Build rolling decimal digits](../scenes/texture-animation.md#build-rolling-decimal-digits)
- [A complete vector-backed sprite](../scenes/vectors.md#a-complete-vector-backed-sprite)

## Game observations

- [A charge-driven display](../observations/charge.md#a-charge-driven-display)
- [Judge a corrected launch](../observations/charge.md#judge-a-corrected-launch)
- [Example: react when falling starts](../observations/index.md#example-react-when-falling-starts)

## Native game systems

- [A sound zone](../game/audio.md#a-sound-zone)
- [A complete screen showcase](../game/camera.md#a-complete-screen-showcase)
- [Give one NPC a voice](../game/dialogue.md#give-one-npc-a-voice)
- [Reveal scene text](../game/dialogue.md#reveal-scene-text)
- [Items](../game/interactions.md#items)
- [Hidden walls](../game/interactions.md#hidden-walls)
- [Victory conditions](../game/interactions.md#victory-conditions)

## Geometry and navigation

- [Make a 480 by 360 collision atlas](../maps/collision.md#make-a-480-by-360-collision-atlas)
- [Other resolutions and strips](../maps/collision.md#other-resolutions-and-strips)
- [Choose native, atlas or strip](../maps/large-maps.md#choose-native-atlas-or-strip)
- [Complete settings and transitions](../maps/teleports.md#complete-settings-and-transitions)

## Integration

- [A complete effect handshake](../integration/endings.md#a-complete-effect-handshake)

## Intro, results and shared text

- [Final statistics and multi-page intros](../game/results.md): complete counter and opening sequence examples.
- [Localized captions](../scenes/localized-text.md): choose a language and insert typed values.
