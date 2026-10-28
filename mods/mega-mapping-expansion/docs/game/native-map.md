# Find the settings the game already provides

[Handbook](../README.md) / [Native game systems](README.md)

Before adding a scene component, find the native file that already owns the behavior. This is especially useful for ordinary NPC speech speed, screen ambience and Hidden Wall sound.

Use the table below to pick a task, edit the existing native file, [build and test](../getting-started/worldsmith.md), then add an MME adapter only for the extra connection you need. Keep a working native definition as your starting point so required arrays and resources remain present.

The native behavior described here is checked by MME's installed-game adapter tests. A different game build needs those contract checks; an old tested version is not a general compatibility guarantee.

## Choose the smallest change

| Task | Native authoring | MME extension |
|---|---|---|
| Static scenery | Screen backgrounds, midground and foreground; ordinary props | Moving/reactive components and composition |
| NPC speech speed | `OldManSettings.talking_speed` | No extra speed field is needed |
| Conversation area | `OldManSettings.trigger_box` | React to speech events or combine scene conditions |
| NPC font and bubble | `font`, `text_layer`, `bubble_format`, `oldman_layer` | Scene text has separate layout/reveal controls |
| Quote selection | Intro, looping, screen progress, fall, item and story-flag pools | Observe native selection and react without replacing its history |
| NPC character sound | One shared native Talking sound | An `npc` adapter can use an owned `audio` voice or mute it |
| Pause after a line | Fixed native 2.2 seconds | `npc/linePause`, with `-1` retaining the native value |
| Screen ambience | Several tracks, volume and per-track fade settings | Area/event-driven playback and volume |
| A sound at an invisible boundary | Hidden Wall with transparent art and `sfx_info` | Observe the same wall; add scene reactions |
| Map location names | Native location ranges and unlock rules | Scene labels for other information |
| Inventory and world pickups | Native item spawning, pickup and saved inventory | Read count/equipped state and actual pickup events |
| Main victory | Native ending screen, horizontal threshold and grounded check | Replace, extend or complement that condition |
| Ending choreography | Native controller and actors | Existing ending XML bridges plus ordinary scene effects |
| Screen connections | Normal screen order and native side links | MME layout and dynamic link schemes |
| Camera tracking | Native screen framing | Scripted cuts; Smooth Camera provides timed movement |

MME's `npc`, `item`, `hiddenWall` and `ending` components are adapters to existing
native objects. Declaring one doesn't spawn a duplicate object. Native resources
remain in their ordinary directories. An unknown native name fails preparation
with the adapter ID rather than quietly creating an empty replacement.

## Native dialogue files

The loader reads NPC settings from `props/textures/old_man/lines/*.xml` and
their sprites from `props/textures/old_man/<name>/`. Merchants use the native
`merchant/` settings directory and contain an `OldManSettings` value too.

The settings already include `name`, `position`, `home_screen`, `sprite_cells`,
`random_count`, `talk_animation`, `busy_animation`, `font`, `text_layer`,
`oldman_layer`, `trigger_box`, `bubble_format`, `talk_delay`, `talking_speed`,
`intro_quote`, `looping_lines`, `screens`, `falls`, `items`, `flag_quotes` and
`achievements`. Preserve the arrays and required sprite files from a working NPC
when making a new one.

`talking_speed` is seconds between characters. `0.05` is the default, and `0`
also chooses that default. It doesn't mean instant speech. The native node emits
at most one character per update, so a very small interval cannot produce an
arbitrary number of characters per frame.

`talk_delay` is the approach delay. The native tree checks contact, waits half a
second, checks contact again, then waits any remaining configured delay. It is
different from the pause after a completed line and the native busy period.

The bubble has a width, direction and anchor. A conversation trigger can be a
rectangle relative to the NPC (`width`, `height`, `center_x`, `center_y`) or an
explicit `screen_rect`. A nonzero `screen_rect.Width` selects the latter. Changing
the trigger doesn't change the artwork. See [dialogue](dialogue.md) for edits and
an adapter example.

## Ambience and event sounds

`audio/background/data/values.xml` uses `AmbienceSaveValues`. Its `sections`
describe screen ranges and arrays of tracks; each track has a name and volume.
`special_info` adds each track's sound bus, restart policy and fade-in/fade-out
lengths. Keep this format for ordinary music/ambience tied to screen progression.
Don't rebuild the same screen table as hundreds of MME rules.

Native event sounds are loaded into `event_music`. Hidden Walls can name these
sounds, and MME's `sound` action can play the same keys. This is a shared native
one-shot interface. Use an `audio` component when a scene needs its own stoppable
voice, a loop, a fade, or a volume binding.

## Invisible sound triggers with Hidden Walls

A native wall combines `texture_name`, a position, one or more `hitboxes`, and
`sfx_info`. The texture can be transparent while the rectangles remain active.
`use_screen_coords` controls whether rectangles are relative to the screen or
the wall artwork. `enable` and `disable` select the native entry/exit sounds;
`PlayEveryTime` controls repetition.

The rectangles form one native wall. Moving from one rectangle to another while
still inside their union doesn't constitute another entry. Native contact uses
integer `Rectangle.Intersects`: touching an edge without overlapping isn't entry.
MME observes the original native test. Replacing it with an approximately drawn
sensor can change which jumps trigger speech, even when the picture looks equal.

Use the full [native hidden-wall example](../scenes/contact-feedback.md) and
[identity rules](interactions.md#hidden-walls). The native wall itself plays its
sound; don't also play it from the MME entry rule unless you want two voices.

## Items, titles and endings

Native inventory records item counts and enabled equipment separately. A scene
should use the appropriate observation: owning boots isn't the same as wearing
them. A pickup event describes an actual world pickup; inventory changes can
also come from other native operations.

Native location titles already have `start`, `end`, `unlock` and `name` fields.
Use those for location announcements. A custom MME text label is useful when the
information is different, such as a counter or a changing instruction.

The main ending's native test uses the configured ending screen, the King's
horizontal center relative to its ending start position, and grounded state.
It doesn't test overlap with Babe's sprite. Keep actor positions, victory
conditions and cutscene choreography as separate decisions. MME's ending adapter
changes the condition; the game still owns selection, cutscene execution and
completion. See [victory conditions](interactions.md#victory-conditions).

## Files stay authoritative

Set native options in their native files. Put only MME's extra behavior in
`mapping_settings.xml`. Arbitrary extra elements in a native settings struct may
be ignored by the game's serializer; they don't become supported extensions just
because the XML is well formed. MME validates its own fields and references.

Native systems also have their own timing. Showing a remote screen doesn't
advance a remote NPC's conversation. A rule changing a distant scene variable
doesn't pick up an item. Use [observations](../observations/README.md) to connect
the real event to the desired reaction.
