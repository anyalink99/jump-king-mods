# Use the game's sound, NPCs, items and ending

[Handbook](../README.md) / [Native game systems](README.md)

Keep the native objects that already work in your map. MME connects their real events and settings to your scene.

| Task | Start here | Native setup needed |
|---|---|---|
| Change NPC typing speed or conversation settings | [Native settings](native-map.md), [dialogue](dialogue.md) | A working native NPC |
| Give one NPC a voice or change its quote/appearance | [Dialogue](dialogue.md) | NPC name, sprites, lines; sound for a custom voice |
| Play a short sound on a jump | [Lesson 6](../getting-started/lessons.md#6-play-a-sound-on-a-jump) | No extra native sound dictionary |
| Loop ambience inside an area | [Audio](audio.md) | A WAV/XNB loop |
| React to a pickup or equipped item | [Items](interactions.md#items) | Native item spawning/inventory |
| Observe exact Hidden Wall entry | [Hidden walls](interactions.md#hidden-walls) | Native wall definition and texture |
| Show a distant screen | [Camera](camera.md) | At least the referenced screens; optional Smooth Camera |
| Change the win condition | [Victory](interactions.md#victory-conditions) | A working native ending |
| Add intro text or final statistics | [Intro/results](results.md) | Normal intro/completion path |

For ending actor choreography use [native ending trees](../integration/endings.md). For a caption independent of NPC dialogue use [scene text](../scenes/narrative.md). [Observations](../observations/README.md) and [events](../reference/events.md) list the values these systems expose.
