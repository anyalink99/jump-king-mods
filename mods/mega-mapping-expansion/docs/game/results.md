# Intro pages and final statistics

[Handbook](../index.md) / [Native game systems](index.md)

Use `intro` components to replace the opening legend with a sequence of pages.
Use `result` components to append pages to the game's final statistics screen.
Both are available in MME 1.2.0. They live in `mapping_settings.xml` and accept
ordinary graph bindings; no extra controller or compilation step is needed.

## Add a counter to the final screen

This complete scene counts jumps in the current attempt. Jump King shows its
usual statistics first. Confirming that page opens CHALLENGE; another confirmation
continues the game's completion sequence. This isn't text drawn in the Babe room.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Variables><Variable id="jumps" kind="Integer" value="0" /></Variables>
  <Components>
    <Component id="jump-result" kind="result">
      <Value name="page" value="CHALLENGE" />
      <Value name="label" value="Jumps" />
    </Component>
  </Components>
  <Graph>
    <Node id="count" op="input" source="variable:jumps" />
    <Node id="caption" op="format" inputs="count" />
  </Graph>
  <Bindings><Bind id="count" source="caption" target="jump-result/value" /></Bindings>
  <Rules><Rule id="jump" event="jump"><Action op="increment" target="jumps" value="1" /></Rule></Rules>
</MappingSettings>
```

For a room-specific counter, add a rule condition comparing `player.screen` to
that room's one-based number. For totals across attempts, use a `Save` variable
and give the scene a stable `saveId`; see [state and saves](../scenes/state.md).
The results adapter reads values, not gameplay: it doesn't create jumps, change
native achievements or overwrite the game's saved statistics.

Each `result` component is one row. Rows with the same `page` form a page:

| Field | Default | Meaning |
|---|---|---|
| `page` | `RESULTS` | Page title and grouping key; structural |
| `role` | `any` | `any`, `main`, `nbp` or `owl`; structural |
| `order` | `0` | Integer from -1024 to 1024; structural |
| `label` | empty | Bindable text before the value |
| `value` | empty | Bindable text, often a `format` or `template` output |
| `color` / `background` | white / black | Bindable page colors, taken from the first enabled row in display order |
| `enabled` | `true` | Bindable Boolean; false removes this row |

Rows sort by `order`, then component ID. Pages sort by their lowest enabled row's
order, then title. All rows in a page must use the same ending role. The Owl
ending uses `owl`; Main Babe uses `main`; New Babe Plus uses `nbp`. A page with
no enabled rows is omitted. To hide a whole page, bind each row's `enabled` to
the same condition.

A row normally reads `label: value`. With an empty label it shows only the value;
with an empty value it shows only the label. This allows a plain final sentence.
The native menu font and centered native layout are retained. Limit the scene
to eight pages, eight rows per page and 64 rows total. Titles and labels accept
up to 80 characters; values accept 160. Each must be one line. The actual font
must fit every line within 440 pixels. An oversized page is skipped with a
message in `MegaMappingExpansion.log`; shorten the text rather than shrinking
the whole game frame.

Values are frozen for the results session. The snapshot survives attempt-scene
teardown, so counters remain visible after the ending disposes gameplay objects.
Loading another world releases the pages and their hooks. Restarting the same
map starts a fresh session. Disabling MME suppresses the additional pages.

## Replace the opening legend with several pages

The game still owns intro skipping and the transition into play. MME replaces
only the text sequence. This complete example has two pages:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="opening" kind="intro">
      <Value name="value" value="They say timing is everything." />
      <Value name="stay" value="2.5" />
    </Component>
    <Component id="answer" kind="intro">
      <Value name="order" value="1" />
      <Value name="value" value="Here, they mean it." />
      <Value name="color" value="#65FFCC" />
      <Value name="stay" value="3" />
    </Component>
  </Components>
</MappingSettings>
```

| Field | Default | Meaning |
|---|---|---|
| `order` | `0` | Structural integer from -1024 to 1024; ties sort by ID |
| `value` | empty | Bindable text; `&#10;` inserts a line break |
| `font` | `menu` | Structural native font: `menu`, `small`, `style`, `location`, `gargoyle` |
| `color` | white | Bindable text color |
| `fadeIn` / `fadeOut` | `0.7` | Bindable seconds, 0 to 60 |
| `stay` | `2.5` | Bindable seconds, 0 to 300 |
| `enabled` | `true` | Bindable Boolean |

At most 16 pages are allowed. Each page is centered and must fit within 440 by
300 pixels in its selected font. Split long text yourself with line breaks or
another page. Text is resolved when the native intro tree is built, before the
player is attached. Initial variables, loaded saved variables and language
selection work here; live player observations aren't available yet. Changing
a binding during play changes the next constructed intro, not a page already
on screen.

The game's fast intro path caps each fade at 0.5 seconds and the stay at one
second. Debug startup can skip the intro entirely. With no enabled `intro`
components, the ordinary legend remains; `view/intro` is still the simpler
option for replacing that one string. Intro components take precedence when
present. See [localized text and templates](../scenes/localized-text.md) for
sharing captions between intros, NPCs and scene text.
