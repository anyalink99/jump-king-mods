# Sound and ambience

[Handbook](../index.md) / [Native game systems](index.md)

Use native screen ambience for tracks that follow screen progression. Use a
Hidden Wall's native sound settings for an existing invisible entry/exit trigger.
An MME `audio` component supplies an independent voice when playback must follow
a graph, an area or an event.

## Native files first

The native `audio/background/data/values.xml` already supports several ambience
tracks per screen range, their volumes, fade-in/fade-out lengths and restart
policy. [Native map systems](native-map.md#ambience-and-event-sounds) explains the
boundary. MME doesn't mute or take over that table automatically.

The existing `sound` action plays a loaded native `event_music` key. Its target
is the key without a path or extension. It keeps the native sound's trigger and repeat behavior.
For an MME-owned voice, use `playAudio` with the audio component ID instead.

## A sound zone

Copy `mme-assets/chime.wav` from the minimal example. This complete scene loops
that sound while the King occupies a rectangle on screen 1 and fades it at the
edges. Replace the example sound with a seamless ambience loop for a real map.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="area" kind="transform"><Value name="position" value="100,180" /></Component>
    <Component id="zone" kind="sensor">
      <Value name="transform" value="area" /><Value name="size" value="140,120" />
      <Value name="phase" value="scene" /><Value name="initial" value="fire" />
    </Component>
    <Component id="air" kind="audio">
      <Value name="path" value="mme-assets/chime.wav" /><Value name="bus" value="Ambience" />
      <Value name="loop" value="true" /><Value name="volume" value="0.2" />
      <Value name="fadeIn" value="0.5" /><Value name="fadeOut" value="0.8" />
    </Component>
  </Components>
  <Graph><Node id="inside" op="input" source="zone.touching" /></Graph>
  <Bindings><Bind id="play" source="inside" target="air/playing" /></Bindings>
</MappingSettings>
```

`playing` is a desired state. A false-to-true edge starts the voice; false fades
it out and stops it. A non-looping sound that finishes naturally doesn't restart
every frame while `playing` remains true. Set it false and true again, or use
`playAudio`, for another playback.

Use two voices with complementary volume bindings for a crossfade. A sensor's
hysteresis can prevent repeated start/stop at a boundary. A condition can combine
membership with an item, variable or player state using ordinary graph nodes.

## Files and voice ownership

`path` is relative to the map and ends in `.wav` or `.xnb`. It must stay inside
the map. Native XNB sound assets work without conversion to a new MME format.
Initial decoding happens during preparation, and inactive prepared voices stay
warm for restarts. Changed content is rehashed; the pathname alone isn't a cache
identity. A reload candidate receives its own voice while the old scene is active.

Each voice uses the native `SFX`, `Ambience` or `Music` bus, including its master
volume and enabled preference. `volume` is a multiplier in `0..1`; it doesn't
overwrite the player's settings. `<id>.audible` reports that this voice is playing
with positive authored volume. A muted global bus can still make it inaudible.

`loop`, `playing`, `volume`, `fadeIn` and `fadeOut` are ordinary bindable
properties. File and bus are structural. One encoded file is limited to 64 MiB;
the warm cache allows 256 MiB of encoded data. Decoded audio also consumes memory,
so this is intended for short sounds and reasonable loops, not streaming music.

`playAudio` plays a single non-overlapping voice without changing its `playing`
binding. An already playing sample isn't layered over itself. Use separate audio
components when simultaneous sounds are intentional. Keep a dialogue voice
separate from a background loop if both must play independently.

## Pause, restore and teardown

The base game pauses ambience but doesn't pause an already playing Hidden Wall
event sound. With an active MME scene, native event voices referenced by Hidden
Walls or `sound` actions also pause with native and Runtime UI menus. MME borrows
the loaded sound instance; it doesn't replace the wall, change its rectangles,
replay the trigger or create another voice. Only sounds paused by this scene are
resumed. Stopped sounds stay stopped, and an unrelated owner's pause stays held.
Disabling or releasing the scene releases its borrowed pauses.

Native pause and Runtime UI pause both hold voices and fade progress. The scene's
visual `animateWhilePaused` option doesn't override audio pause. Resume continues
a paused sound rather than starting it from the beginning.

Disabling the scene stops its voices. Unload/reload releases attempt bindings;
world teardown releases prepared resources. Restoration stops transient sound.
A held non-looping `playing` state doesn't replay its attack just because a
snapshot was restored. A requested loop can restart from its beginning; sample
playback cursors aren't part of scene snapshots.

In a following world, continuous audio follows received scene properties.
Local dialogue/pickup observations don't dispatch a second authoritative rule.
Audio doesn't create its own networking or inventory authority.
