# Put scene files in the map and package them

[Handbook](../README.md) / [Getting started](README.md)

Keep one editable source map and build a separate playable copy. For the lessons, place `mapping_settings.xml` beside `level_settings.xml` in source. Add its resources at the same relative paths, then [build and launch](worldsmith.md).

```text
My MME Test/
  level_settings.xml
  mapping_settings.xml
  mme-assets/
    panel.png
  fragments/
    light.xml                 <- only if the entry includes it
  screens/
  props/
  ...other native resources
```

The entry and every included fragment must reach the playable copy. MME reads XML on map load; it creates/refreshes its disposable cache automatically. You don't ship that cache or run a separate scene compiler.

## File layout

| Path relative to playable map root | Owner and purpose |
|---|---|
| mapping_settings.xml | MME construction entry and optional version 2 layout |
| fragments/*.xml | Suggested location for scene fragments and map recipes |
| mme-assets/* | Suggested artwork folder; other safe relative paths work |
| screens/mega-mapping-expansion/transitions.xml | Side links, random pools and dynamic routing |
| screens/mega-mapping-expansion/collision-shapes.xml | Mod/native block slope aliases |
| ending/custom_<role>.xml | Native ending tree replacements |
| jk-runtime/policy.xml | Required modules and map restrictions |
| level_settings.xml, level.xnb, screens/* | Native playable map settings, collision and art |
| props/propN.xml, props/hidden_walls/*, audio/* | Native props, walls and audio |

Construction doesn't belong in native props.xml or prop_settings.xml. Those go through the game's prop parser and have a different schema. An error from PropManager.SpawnProps may indicate that one of those native definitions is broken even when the MME entry is valid.

The entry root is MappingSettings version=1. Sections are MapLayout, Assets, Components, Graph, Bindings, Variables, Effects, Rules, Trees, Exports, Recipes and Instances. IDs connect them. Use decimal points, finite numbers, safe relative paths and the exact case shown in examples.

## Split a scene into files

Includes are relative to the map root, not the fragment's directory. They merge sections before recipe expansion. Keep IDs unique across the merged scene. This complete two-file example separates a light from the entry:

<!-- mme-example:start -->
```xml mapping_settings.xml
<MappingSettings version="1">
  <Include src="fragments/light.xml" />
  <Rules>
    <Rule id="pulse" event="jump">
      <Action op="effect" target="pulse" />
    </Rule>
  </Rules>
</MappingSettings>
```
```xml fragments/light.xml
<MappingFragment version="1">
  <Components>
    <Component id="pose" kind="transform">
      <Value name="position" value="240,120" />
    </Component>
    <Component id="lamp" kind="light">
      <Value name="transform" value="pose" />
      <Value name="intensity" value="0" />
    </Component>
  </Components>
  <Effects>
    <Effect id="pulse" duration="0.5" fadeOut="0.5">
      <Set target="lamp/intensity" value="0.08" />
    </Effect>
  </Effects>
</MappingFragment>
```
<!-- mme-example:end -->

Fragments use MappingFragment version=1, not a second MappingSettings root. Include cycles and excessive depth fail. Paths cannot escape the map. DTDs and external XML entities are disabled. Unknown fields and duplicated sections fail rather than being silently ignored.

For a larger scene, split by responsibility: shared assets, moving decoration, contact reactions, instruments and cutscene cues. Use recipes for repeated composition. A fragment organizes one scene; a recipe gives several instances their own namespace and arguments.

## Edit and load

1. Edit `mapping_settings.xml`, its included fragments or your map's recipes in a text editor.
2. If you keep a separate source folder, build the native map with your usual tools and copy the updated XML and referenced resources into the playable folder.
3. Load or restart the map in Jump King. MME reads the current XML during attempt preparation and reports invalid fields, references or resources before activating the scene.

No separate MME compiler or editor extension is required. For presentation-only edits, you can also use Mapping inspector's reload while running with `-debug` in a local, unsynchronized session. Layout, collision, native props and screen-link changes require restarting the map. Saving an XML file doesn't alter an already running scene by itself.

MME checks construction types, references, graph/recipe/transform/capture cycles and resource paths. Native preparation checks loaded sound keys, game contracts and graphics resources. These checks don't prove route completion or cutscene choreography. See [troubleshooting](../reference/troubleshooting.md) for how to find the first error.

## Automatic local cache

MME stores its disposable scene cache under:

```text
%LOCALAPPDATA%\JumpKing\MegaMappingExpansion\SceneCache
```

This directory belongs to the current Windows user, outside the game, Workshop and map folders. You don't create it or include it in an upload. It contains expanded scene descriptions, not artwork, sounds or saved progress.

Every load reads the entry and included XML. MME checks their merged content and its own implementation identity before reusing a cached description. Changing a fragment, recipe or setting, or updating MME, automatically replaces the affected entry. Moving the map to a different directory creates a separate entry. Image and sound resources still load from the map; the cache doesn't substitute old artwork for edited files.

A missing, outdated, truncated or corrupt cache is rebuilt from the current XML. If the directory can't be read or written, the scene still loads from XML without a persistent cache. Invalid source remains an error: MME never hides it by loading an older scene. Removing `mapping_settings.xml` removes the scene even if a cache remains.

You can delete the cache directory while the game is closed; it will be recreated as needed. MME limits its entries to 128 files and 64 MiB when writing new entries, removing older entries first. Each description is limited to 16 MiB for caching; a larger valid scene can still load without a cache. These are storage limits, not scene resource budgets.

Older map packages may contain `props/mega-mapping-expansion/scene.mmgfx`. MME ignores that file. You can remove it from your source and future packages, but it doesn't block XML edits if left in place. Keep the entry and every included XML file: an old cache alone isn't a scene source.

Texture compilation can replace an image with a matching uncompressed XNB. Leave the authored image path in Assets; MME resolves the compiled counterpart when the source image is absent. This packaging step doesn't change the source fingerprint. Shared-world identity also includes decoded texture pixels, so a visually changed asset isn't assumed to be the same world.

## Package and test

Include the native playable map, entry/fragments, referenced assets and any collision/route/ending files it uses. Declare MME and Runtime dependencies, plus the mods supplying custom blocks. Don't bundle another active copy of MME inside the map.

Validate the final package, not only a source folder. Open it in the installed game and check the first scene, screen transitions, pause/resume, restart, teleport and completion paths it uses. Test assets after Worldsmith compilation too. Source validation alone can't prove an XNB or native prop was packaged correctly.

## Convert an older scene

MME 1.0 doesn't have legacy scene readers, specialized planet/gaze/weather entities, flags, old region formats or old cache readers. There is no automatic conversion of arbitrary source.

1. Preserve the old source and screenshots as reference.
2. Keep native spawn, Babe, collision, props, dialogue and audio in their native formats.
3. Create MappingSettings version=1. Declare assets, transforms, materials and drawing components.
4. Replace specialized objects with recipes or their expanded common parts.
5. Replace flags with typed Attempt/Save variables; replace relationships with graph bindings.
6. Choose actual surface sensors, general regions or native hidden-wall events according to the original trigger semantics.
7. Move pixel layout metadata into MapLayout version=2. Keep side-link data in its supported format.
8. Load the XML scene and test behavior, especially trigger boundaries, phase ordering and clocks.

Saved-data conversion belongs to a deliberate map maintenance tool, with native reset epochs and existing new saves respected. Don't add a runtime compatibility reader just to retain one map's old counter totals.

### Native narrative conversion

MME 1.2 restores the native narrative adapters through construction components:

| Older source | Current source |
|---|---|
| `Intro/Page` | One `intro` component per page, with order, value, fades and stay |
| `Results/Page/Row` | One `result` component per row, grouped by page title |
| Row counter | Integer/Number variable, graph `format`, binding to `result/value` |
| Conditional row or quote | Graph Boolean bound to `enabled` or `quoteOverride`; Text bound to `quote` |
| Native actor offset/tint/visibility | Fields on the `npc` adapter |
| String dictionary / flag substitution | Named Text constants, `language.culture`, `select` and `template` |

See [intro/results](../game/results.md), [NPC dialogue](../game/dialogue.md) and
[localized text](../scenes/localized-text.md) for complete examples. The engine
still doesn't read the old XML. Preserve source phrases and translate their
connections, rather than copying generated caches into the new package.
