# Native map integration

[Handbook](../index.md) / [Integration](index.md)

Keep spawn, Babe, screen artwork, props, dialogue, audio and metadata in the game's formats. MME adds construction, optional pixel collision/layout, screen links and ending bridges. A construction entry doesn't make an incomplete native map playable.

## What stays native

Ordinary background/midground/foreground layers are still the best place for static scene art. Native prop definitions still create native props. Hidden walls retain their grouped integer rectangles, fade state and sfx_info. Native dialogue and event music retain their loaders. MME can observe and capture those systems without taking ownership of their entities.

If a native prop fails to spawn, inspect its native XML and required resources. Do not put construction Components into prop_settings.xml. The [file table](../getting-started/files.md#file-layout) distinguishes native and MME files.

## Audio

A sound action uses a loaded native event_music dictionary key, without a path or extension. For a clip packaged as `audio/music/event_music/chime.xnb`, the key is normally `chime`; confirm it exists in the built map's native audio resources. The compiler checks the action's shape; preparation checks the loaded key. Merely placing a raw audio file in an arbitrary MME asset folder doesn't register a native sound.

A native hidden wall with sfx_info already plays its own audio. Listen to `hiddenwallenter:<texture_name>` for counters/effects without adding a second sound action. If you're deliberately replacing that audio, remove the native sound setting and own the replacement policy explicitly. The `sound` action is a native one-shot. For an independently controlled voice, use an [`audio` component](../game/audio.md): it accepts volume, fades, loop and playing bindings and follows native sound preferences.

## Native settings and adapters

The [native map catalog](../game/native-map.md) explains settings for NPC typing, quote selection, hidden-wall sounds, ambient audio, inventory and endings. Keep those settings in the ordinary game files. MME adapters add the missing connections: per-NPC voices and line events, inventory observations, exact wall selection, owned audio, camera requests and custom victory conditions. See [worked examples](../game/index.md).

## Native observations and actions

Player positions and velocity are detached typed observations, not access to mutable BodyComp. Contacts carry captured position/normal/velocity evidence. Ending actor selectors draw game-owned actors without updating them. This keeps scene logic independent of private entity lifetime.

Use [native wall events](../scenes/contact-feedback.md#preserve-native-hidden-walls) for faithful wall triggers, [surface sensors](../scenes/contact-feedback.md#select-actual-surfaces) for mod-block contacts, and [ending bridges](endings.md) for exact cutscene cues. A region near a warp doesn't establish that a landing occurred.

## Loading and toggles

Resources and native contracts prepare before player activation. Player behavior and capability publication attach only at normal activation. Mod authors should use JK Runtime's OnWorldReady and BeforeAttempt preparation scopes rather than adding private startup hooks for scene work.

The MME checkbox holds scene clocks and logic. Collision, slope aliases and native side links remain installed; they are map geometry/navigation, not optional decoration. Native hidden walls remain native too. If the scene is essential to a completion handshake, express the dependency in [map policy](../maps/map-policies.md) and test the disabled case deliberately.

World unload releases MME's owned hooks/resources and restores its owned route changes without overwriting a newer owner's changes. Reload is a presentation operation, not a replacement for rebuilding collision or native prop definitions.
