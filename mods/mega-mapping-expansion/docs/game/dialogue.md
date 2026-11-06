# Change NPC speech and react to dialogue

[Handbook](../README.md) / [Native game systems](README.md)

This page starts from a native NPC that already works in your map. MME attaches to it by its exact settings `name`. Declaring an `npc` component doesn't create the NPC or supply its sprites and conversation files.

For a first change, follow **Change native typing speed** below: edit the existing native field, build and approach the NPC again. You should hear/see slower typing with `talking_speed=0.08`. For an individual voice, copy the supplied `mme-assets/chime.wav` and use **Give one NPC a voice**, replacing `hermit` with your NPC's exact name.

After testing voice and typing separately, use line events for portrait, light or camera changes. The final sections cover scene captions and conditional quote/visual controls.

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
    <Component id="pose" kind="transform">
      <Value name="position" value="240,70" />
    </Component>
    <Component id="glow" kind="light">
      <Value name="transform" value="pose" />
      <Value name="intensity" value="0" />
    </Component>
  </Components>
  <Effects>
    <Effect id="greeting" duration="0.5" fadeOut="0.4">
      <Set target="glow/intensity" value="0.1" />
    </Effect>
  </Effects>
  <Rules>
    <Rule id="greet" event="speaker.started">
      <Action op="effect" target="greeting" />
    </Rule>
  </Rules>
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
    <Component id="pose" kind="transform">
      <Value name="position" value="240,100" />
    </Component>
    <Component id="caption" kind="text">
      <Value name="transform" value="pose" />
      <Value name="value" value="The gate is open." />
      <Value name="visibleCharacters" value="0" />
    </Component>
    <Component id="tick" kind="audio">
      <Value name="path" value="mme-assets/chime.wav" />
      <Value name="volume" value="0.12" />
    </Component>
  </Components>
  <Graph>
    <Node id="time" op="input" source="time.gameplay" />
    <Node id="rate" op="constant" value="20" />
    <Node id="elapsedCount" op="multiply" inputs="time;rate" units="characters" />
    <Node id="limit" op="constant" value="16" units="characters" />
    <Node id="count" op="minimum" inputs="elapsedCount;limit" />
  </Graph>
  <Bindings>
    <Bind id="reveal" source="count" target="caption/visibleCharacters" />
  </Bindings>
  <Rules>
    <Rule id="tick" event="caption.character" cooldown="0.04">
      <Action op="playAudio" target="tick" />
    </Rule>
  </Rules>
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

## Choose a quote from scene conditions

Native `.oldman` and merchant definitions still spawn the character and control
its normal quote list. The `npc` adapter can override the next conversation:
set `quoteOverride=true` and bind Text to `quote`. Separate lines with `&#10;`.
A graph `select` can choose the text from an item, saved variable or any other
Boolean condition. With `quoteOverride=false`, the native selector runs normally.

This complete construction file expects a native NPC named `watcher` already
present in the map's normal NPC files:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Variables>
    <Variable id="finished" kind="Boolean" value="false" />
  </Variables>
  <Components>
    <Component id="watcher" kind="npc">
      <Value name="name" value="watcher" />
      <Value name="quoteOverride" value="true" />
    </Component>
  </Components>
  <Graph>
    <Node id="done" op="input" source="variable:finished" />
    <Node id="before" op="constant" kind="Text" value="The gate is still closed." />
    <Node id="after" op="constant" kind="Text" value="You found it.&#10;Go on through." />
    <Node id="quote" op="select" inputs="done;after;before" />
  </Graph>
  <Bindings>
    <Bind id="quote" source="quote" target="watcher/quote" />
  </Bindings>
</MappingSettings>
```

Set `finished` from your own trigger or C# consumer. The selected quote is copied
when native `FetchQuote` starts the conversation. Changing a binding while the
NPC speaks affects the next conversation; it doesn't swap a half-spoken line.
Native line advancement, typing, bubble formatting and the existing voice/line
observations still run. Scene quotes don't consume the native quote list.
Use [templates and language selection](../scenes/localized-text.md) to reuse text.

## Change how an NPC is drawn

These bindable fields belong to the same `npc` component:

| Field | Default | Effect |
|---|---|---|
| `offset` | `0,0` | Draw offset in pixels, rounded to whole pixels |
| `tint` | white | Multiplies the native sprite color |
| `opacity` | `1` | Sprite opacity from 0 to 1 |
| `visible` | `true` | Hides only the sprite when false |
| `textVisible` | `true` | Hides the speech bubble when false |

The offset moves the drawing and bubble together. It doesn't move the native
NPC's interaction area. Sprite, position and color are borrowed for the draw
pass and restored even if drawing throws. These settings don't disable dialogue,
quests or merchant logic; use the game's definitions for those behaviors.
