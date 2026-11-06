# Choose a language and insert values into text

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

Use a Text pattern such as `Jumps: {0}` to put a value into a caption. [Lesson 5](../getting-started/lessons.md#5-count-jumps-in-this-attempt) is the smallest working example. To translate it, choose a pattern by the game language before inserting values.

Put the complete example below in your test map, [build and load](../getting-started/worldsmith.md), and jump. Then switch the game language to French and reload/restart as needed: the caption should use the French pattern. Other cultures use the explicit English fallback. The French phrases are deliberate localization fixtures.

The active `language.culture` is the exact game culture name, such as `fr-FR`; matching is case-sensitive. It is available during intro preparation and updates during play. MME does not automatically match a parent language, and the selected font must contain your characters.

## Choose a language and insert values

This complete example displays a counter in French for `fr-FR` and English for
other cultures. The captions are deliberate localization fixtures.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Variables>
    <Variable id="jumps" kind="Integer" value="0" />
  </Variables>
  <Components>
    <Component id="pose" kind="transform">
      <Value name="position" value="240,80" />
    </Component>
    <Component id="caption" kind="text">
      <Value name="transform" value="pose" />
    </Component>
  </Components>
  <Graph>
    <Node id="culture" op="input" source="language.culture" />
    <Node id="french" op="constant" kind="Text" value="fr-FR" />
    <Node id="is-french" op="equal" inputs="culture;french" />
    <Node id="en" op="constant" kind="Text" value="Jumps: {0}" />
    <Node id="fr" op="constant" kind="Text" value="Sauts : {0}" />
    <Node id="phrase" op="select" inputs="is-french;fr;en" />
    <Node id="count" op="input" source="variable:jumps" />
    <Node id="caption" op="template" inputs="phrase;count" />
  </Graph>
  <Bindings>
    <Bind id="caption" source="caption" target="caption/value" />
  </Bindings>
  <Rules>
    <Rule id="jump" event="jump">
      <Action op="increment" target="jumps" value="1" />
    </Rule>
  </Rules>
</MappingSettings>
```

`format` converts one typed value to Text. `template` takes a Text pattern first,
then up to 16 arguments. `{0}` inserts the first argument, `{1}` the second;
`{0:0.0000}` formats a numeric argument to four decimal places. Numeric output
uses invariant culture, so a dot remains a dot regardless of the game's language.
Use `{{` and `}}` for literal braces. Numeric alignment and precision must stay
below 1000, and a placeholder may contain at most 48 characters. A bad pattern or missing argument produces
a graph diagnostic and unavailable output, rather than writing invented text.
An unavailable input also makes the template unavailable; use an explicit
fallback with `available` and `select` when an observation may be absent.

For a conditional NPC response, select between complete Text phrases and bind
the result to `npc/quote`; enable `quoteOverride` only while the scene should
choose the conversation. See [dialogue](../game/dialogue.md). No old flag or
string-table format is loaded by the current engine.
