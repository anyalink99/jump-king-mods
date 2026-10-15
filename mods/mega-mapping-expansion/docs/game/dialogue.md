# NPC dialogue and scene text

[Handbook](../index.md) / [Native game systems](index.md)

For a normal NPC, start with its native settings file. MME can observe that NPC
and add a voice or line pause. Use a `text` component for captions and labels
that aren't part of the native conversation system.

## Change native typing speed

Edit `props/textures/old_man/lines/<file>.xml`. In its existing `OldManSettings`,
set `talking_speed` to the interval in seconds between characters. For example:

```xml-fragment
<talking_speed>0.08</talking_speed>
```

This fragment belongs inside the existing settings, not in `mapping_settings.xml`.
It gives roughly twelve characters per second. The native default is `0.05`;
zero also means that default. Emission is capped at one character per native
update. Merchants use the same field inside their nested NPC settings.

`talk_delay` controls when an approached NPC begins talking. `trigger_box` controls
where the approach occurs. `bubble_format.width`, direction and anchors control
placement. Keep quote selection in the native `intro_quote`, `looping_lines`,
`screens`, `falls`, `items` and `flag_quotes` fields. MME doesn't consume a second
copy of these pools or maintain a competing dialogue save.

## Give one NPC a voice

This complete MME scene attaches to a native NPC whose settings contain
`<name>hermit</name>`. Keep that NPC's settings and sprites in their usual places.
Copy `mme-assets/chime.wav` from the supplied minimal example, or substitute your
own WAV/XNB sound. Compilation checks the scene and asset; native preparation
also checks that `hermit` exists in the loaded map.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="voice" kind="audio">
      <Value name="path" value="mme-assets/chime.wav" />
      <Value name="volume" value="0.25" />
    </Component>
    <Component id="speaker" kind="npc">
      <Value name="name" value="hermit" />
      <Value name="voice" value="voice" />
      <Value name="linePause" value="1.4" />
    </Component>
    <Component id="pose" kind="transform"><Value name="position" value="240,70" /></Component>
    <Component id="glow" kind="light">
      <Value name="transform" value="pose" /><Value name="intensity" value="0" />
    </Component>
  </Components>
  <Effects><Effect id="greeting" duration="0.5" fadeOut="0.4"><Set target="glow/intensity" value="0.1" /></Effect></Effects>
  <Rules><Rule id="greet" event="speaker.started"><Action op="effect" target="greeting" /></Rule></Rules>
</MappingSettings>
```

The adapter `name` matches the native settings name, not the XML filename.
The same adapter works with a merchant using that name. A second adapter for the
same NPC is rejected, since two owners could disagree about its voice and pause.

`voice` selects an `audio` component. Empty keeps the original sound. `muted=true`
silences character sound. `linePause=-1` keeps the native pause; nonnegative values
set seconds after a line. Both `muted` and `linePause` accept graph bindings and
effects. Names and voice resources are structural and need preparation.

The sound follows the game's original non-space character and screen checks. The
owned voice doesn't overlap itself; a long sample may cover several characters.
Use a short sample for a regular typing rhythm. Give dialogue its own voice when
it should be independent of a loop or event sound elsewhere in the scene.

## React to speech

For adapter `speaker`, read these signals:

| Signal | Meaning |
|---|---|
| `speaker.present` | The bound native entity is alive |
| `speaker.talking` | Its native SayLine node is running |
| `speaker.line` | Full current native line |
| `speaker.revealed` | Number of revealed UTF-16 code units |
| `speaker.position` | NPC position in its home screen |
| `speaker.screen` | One-based home screen |

Listen to `speaker.started` for a new native line, `speaker.character` for a
newly revealed character (`index` in its payload), `speaker.finished` when the
line node succeeds, and `speaker.stopped` when an observed running node stops
without that completion. These events describe lines, not the whole quote pool.

They can drive a portrait, room light, caption or a camera sequence. Use a rule
for an immediate reaction and a tree when the reaction must wait, branch or stop.
Showing the NPC's screen with a camera doesn't make it start a conversation:
native approach logic still uses the real player's screen and position.

Disabling the scene restores the native line pause and voice. Reload, attempt
teardown and unload release the adapter. Dialogue selection and its history
remain in the game's native save.

## Reveal scene text

`text/visibleCharacters` exposes a prefix while keeping the layout of the full
text. The baseline is `65535`, so existing text appears in full. The value is
floored and clamped to the text length. Both native and atlas fonts support it.
Wrapping and center/right alignment use the full line: letters don't slide as
the prefix grows. Pixel snapping remains enabled by default.

This example reveals twenty characters per second. It needs the sample chime
sound; remove the audio component and sound rule for silent text.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,100" /></Component>
    <Component id="caption" kind="text">
      <Value name="transform" value="pose" /><Value name="value" value="The gate is open." />
      <Value name="visibleCharacters" value="0" />
    </Component>
    <Component id="tick" kind="audio"><Value name="path" value="mme-assets/chime.wav" /><Value name="volume" value="0.12" /></Component>
  </Components>
  <Graph>
    <Node id="time" op="input" source="time.gameplay" />
    <Node id="rate" op="constant" value="20" />
    <Node id="elapsedCount" op="multiply" inputs="time;rate" units="characters" />
    <Node id="limit" op="constant" value="16" units="characters" />
    <Node id="count" op="minimum" inputs="elapsedCount;limit" />
  </Graph>
  <Bindings><Bind id="reveal" source="count" target="caption/visibleCharacters" /></Bindings>
  <Rules><Rule id="tick" event="caption.character" cooldown="0.04"><Action op="playAudio" target="tick" /></Rule></Rules>
</MappingSettings>
```

The graph is the timing mechanism. To start on an event, record `time.gameplay`
in a Number variable, subtract it from the current time and multiply by your
rate. To reveal immediately, set a large visible count through an effect.
For a pause in the sentence, use a curve instead of constant multiplication.
No new text-specific sequencing language is needed.

Text exposes `<id>.content`, `.revealed` and `.complete`. Its `.character` event
contains `from` and `to` counts, with one event per advancing update even if
several characters appeared. `.finished` marks the transition to complete.
Initial layout, replacing the whole string and state restoration establish a
baseline without replaying all character sounds. Events dispatch on a following
advancing scene tick. Counts use UTF-16 code units, matching the native dialogue
node; this isn't a grapheme-cluster shaping engine.
