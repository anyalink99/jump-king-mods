# Native game systems

[Handbook](../index.md)

Keep using the files you already use for a normal Jump King map. MME adds
connections between those systems and the scene. It doesn't replace the native
NPC quote selector, inventory or ending controller.

Start with [what the game already does](native-map.md). Then choose a task:

- [NPC dialogue and text](dialogue.md): native typing speed, individual voices,
  line events and progressively revealed scene text.
- [Sound and ambience](audio.md): native screen ambience, sound zones and owned
  voices with fades.
- [Camera shots](camera.md): show a screen or point, hold it and return.
- [Items, hidden walls and victory](interactions.md): observe actual native
  interactions and change the condition that starts an ending.
- [Remote scene changes](../scenes/remote-scenes.md): connect a local trigger to
  something elsewhere in the map.

All examples use ordinary map files plus `mapping_settings.xml`. A text editor
and the supplied compiler are sufficient. The optional editor extension is a
convenience, not part of the runtime contract.
