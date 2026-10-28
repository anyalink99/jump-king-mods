# Use Mapping inspector to check a scene

[Handbook](../README.md) / [Reference](README.md)

Load a map containing `mapping_settings.xml`. Open the game's mod settings menu, then **Mapping inspector**. If it says to load a construction scene first, check the loaded map folder and the first MME load error.

## Follow a value through the scene

Use [lesson 4](../getting-started/lessons.md#4-show-a-caption-in-an-area):

1. Stand inside the area, then open the inspector.
2. Choose **Typed observations and quality**. Find `zone.touching`; it should be true after contact has been sampled.
3. Go back and choose **Components and effective properties**, then `captionPose`, then `visible`. It should also be true.
4. Leave the area and repeat. Both values should become false. The inspector/menu can pause the scene, so close it to move, then reopen/refresh.

Use the displayed **Refresh** command to update the list. Read the evidence quality too: Unavailable means there is no usable sample. It doesn't mean zero.

## Pages

| Page | What to check |
|---|---|
| Components and effective properties | Actual value, kind, units, bindability and owner |
| Typed observations and quality | Player/adapter/sensor values and sample quality/sequence |
| Scene variables | Declared value, kind and Attempt/Save lifetime |
| Effect library | Authored effects and their clock/duration/repeat policy |
| Active effects and owners | Which effect is active, who owns it and remaining time |
| Control trees | Current state/faults of waiting or running sequences |
| Diagnostics | Bounded scene errors; also read the log for preparation failures |

An effective value can differ from its XML baseline because a binding, effect or preview contributes to it. Higher-priority contributions apply later. Check the owner before changing an otherwise correct baseline.

## Preview a property

1. Start a local test run with `-debug` in the game's launch arguments.
2. Open a bindable property under **Components and effective properties**.
3. Use the provided number/vector/Boolean/color or choice controls to try a value.
4. Choose **Clear inspector preview** afterward to remove the inspector's contributions.

Preview has high priority and can hide an authored binding. It doesn't save a new baseline into your source. Structural fields need XML edits and reload; geometry/native files need a restart. Editing is unavailable while another session owns world state.

## Keep an experiment

**Export preview as an effect recipe** writes `props/mega-mapping-expansion/preview/inspector-effect.xml` under the loaded map. It contains a MappingFragment with an effect and a scene.ready rule.

Copy the fragment into your editable source, review it, and include it deliberately using [Include](../getting-started/files.md#split-a-scene-into-files). Choose IDs/priority suitable for your scene. A later editor build can overwrite the playable copy, so don't leave your only copy there.

## Reload source

Save/copy changed XML and assets to the playable folder, then choose **Reload scene source**. Candidate validation/preparation happens first. A failed reload leaves the active scene intact. [Loading and testing](../getting-started/worldsmith.md#reload-presentation-without-restarting) explains which changes need a restart instead.
