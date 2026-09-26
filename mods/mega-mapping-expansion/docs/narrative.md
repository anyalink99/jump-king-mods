# Text, native actors and results

For timed/conditional reactions to dialogue, use [scene behavior trees](behavior-trees.md#react-to-a-native-npc).
They share these actors' dialogue events and flags without replacing native dialogue control flow.

[Handbook](authoring.md) · [Objects](objects.md) · [Native workflows](native-workflows.md) · [Ending trees](endings.md)

MME extends the existing map instead of replacing its NPC/gameplay framework.
Old Man placement, sprites, trigger boxes, native quotes, sounds, speech bubbles
and Merchant purchases remain native. Binding a native actor adds an MME ID for
effects, attachments, selected reflections and dialogue events.

## A complete text and counter scene

```xml
<MegaMapping version="1">
  <Flags><Flag id="visits" type="integer" value="0" scope="run" /></Flags>
  <Strings>
    <String id="legend" value="Beyond the last light, the climb begins." />
    <String id="counter" value="Visits: {flag:visits}" />
    <String id="results" value="THE LAST LIGHT" />
    <String id="visits-label" value="Places visited" />
  </Strings>
  <Intro><Page string="legend" stay="3" fadeIn="1" fadeOut="1" color="#E5D7B3" /></Intro>
  <Texts><Text id="hud" string="counter" screen="1" x="16" y="16" width="300" font="small" space="screen" /></Texts>
  <Regions><Region id="gate" screen="1" x="100" y="200" width="80" height="100" /></Regions>
  <Rules><Rule id="count" event="enter:gate" increment="visits" amount="1" cooldown="0.5" /></Rules>
  <Results><Page title="results" color="#E5D7B3" background="#101722"><Row string="visits-label" counter="visits" /></Page></Results>
</MegaMapping>
```

This decorates a playable native map; it does not create collision or an ending
trigger. The usual Babe/ending sequence still finishes the run. A separate
ready-to-compile copy ships as `narrative/props/mega-mapping-expansion/scene.xml`.

## Strings and localization

`Strings/String` has a unique `id` and either `value` (language-independent) or
explicit `Translation` children. Translation fields are `culture` (a .NET culture
name such as `en-US`) and `value`. Exact active `LanguageJK.language.Culture` is
used; if the game leaves it unset, its .NET UI culture is used. No parent-language
search or missing-translation substitution takes place. A missing translation is
a preparation error, not an invisible/English string. Reload the scene or restart
after changing language. Universal `value` strings are deliberate, not fallback.

```xml
<MegaMapping version="1">
  <Strings><String id="welcome">
    <Translation culture="en-US" value="Welcome back." />
    <Translation culture="ru-RU" value="С возвращением." />
  </String></Strings>
  <Texts><Text id="greeting" string="welcome" x="20" y="20" width="440" /></Texts>
</MegaMapping>
```

Declare every language you intend to support. The chosen native font must contain
every glyph; missing glyphs are errors. XML escapes `&amp;`, `&lt;` and `&quot;`
work normally. Use `&#10;` for a line break inside an attribute. Text is literal,
not markup or executable code. `{flag:ID}` inserts a declared flag/counter's current
value. Other braces remain ordinary text. Limit: 2048 strings, 4096 characters each.

## Text placement

`Texts/Text` fields: `id`, `string`, `screen=1`, `x=0`, `y=0`, `width=440`,
`font=menu`, `align=left`, `color=#FFFFFF`, `opacity=1`, `visible=true`,
`space=world`, `layer=foreground`, `z=0`.

Fonts: menu, small, style, location, gargoyle. Alignment: left/center/right.
World text participates in the background/world/foreground queues and lighting.
Screen text is drawn after scene lighting with native overlays and independently
of whether the timer is disabled; full-frame mirroring/tint still applies.
Screen-space text uses scene declaration order; layer/z apply only to world text.
Width is 8..480 px; X/Y are ±4096; screen is one-based. Wrapping is measured with
the actual font; long words split only when necessary. Limit: 512 texts,
128 wrapped lines per text. It does not auto-shrink or clip to a UI panel.

Effects can change visible, opacity, color, x and y. String/font/width/layer/space
are structural and require reload. Cached layouts are rebuilt only when their
resolved text changes. A scene without text creates no text draw commands.

## Counters, conditions and saves

Flag `type=string` preserves literal string state; `type=integer` requires an
explicit signed 32-bit initial `value`. Rule `increment` names an integer flag and
`amount` is a nonzero signed integer. It cannot be combined with setFlag on the
same rule. Overflow is reported before that rule's effect/flag mutations and
disables the failing rule until reload. Conditions compare exact strings, not
expressions; use canonical integer spellings (`0`, `1`, `-1`). No arbitrary scripts.

`scope=run` resets on restart; `scope=save` uses the existing map-owned save flags
and requires a stable `Options saveId`. Runtime snapshots restore counters and
once-rule state together. Native statistics are not rewritten by map counters.
See [behavior persistence](behaviors.md) before choosing scopes for collectibles.

## Native actor binding

First create the NPC using Jump King's ordinary `props/textures/old_man/lines`
or Merchant data. MME does not generate a substitute NPC or alternate shop system.
Use the exact native `name`. This scene assumes the map already loads an Old Man
named `keeper` and a Merchant named `shopkeeper`:

```xml
<MegaMapping version="1">
  <Flags><Flag id="help" value="true" /></Flags>
  <Strings><String id="advice" value="Follow the light.&#10;The old road still holds." /></Strings>
  <NativeActors>
    <Actor id="guide" kind="oldman" name="keeper" opacity="1" tint="#FFFFFF">
      <Quote string="advice" requiresFlag="help" equals="true" />
    </Actor>
    <Actor id="shop" kind="merchant" name="shopkeeper" />
  </NativeActors>
  <Lights><Light id="guide-lamp" attach="guide" offsetX="8" offsetY="-16" radius="55" intensity="0.3" /></Lights>
  <Rules><Rule id="advice-read" event="dialogueend:guide" setFlag="help" value="false" /></Rules>
</MegaMapping>
```

Offline validation checks schema/references. Preparation checks loaded native
names; activation checks actual native entity instances. Missing/duplicate bindings
are errors. Maximum 128 bindings, at most one per kind/name.

Actor fields: id, name, kind=oldman/merchant (default oldman), visible=true,
textVisible=true, opacity=1, tint=#FFFFFF, offsetX=0, offsetY=0 (±4096 px).
All visual fields are effect-editable. Visible controls the sprite; textVisible
separately controls the bubble. Neither disables dialogue, collision or trading.
Offsets are visual only: the actual native trigger box is unchanged. Sprite and
bubble offsets are restored after drawing, including exceptions. Native animation
continues. Actors receive ordinary scene compositing; no always-on outline is added.

MME snapshots restore its flags/effects, not the native NPC's internal dialogue or
shop state. Native saves and Runtime's own native-state support remain responsible
for those systems. Reloading a scene is not a dialogue rewind.

`attach="guide"` uses the actor's visual position and home screen. Selected water
or puddle reflections accept `reflectionObjects="player;guide"` and draw the live
sprite without speech bubbles. Whole-world reflections continue to use the composed
world. The native oldman_layer/text_layer settings own the actor's native layers.

For Old Man, the first matching extra Quote is offered at the native quote-fetch
point, before native queued quotes. Each Quote needs string, requiresFlag and
equals; at most 64. While its condition stays true it can repeat at the normal
native conversation cadence. Clear a flag on dialogueend for one-time advice.
Native text speed, pause, interaction and busy timing are preserved. Merchant
extra Quotes are rejected to avoid replacing purchase dialogue.

Events: `dialoguebegin:ID` after a quote was fetched, `linebegin:ID` when a native
line resets for playback, `dialogueend:ID` after the last line of the quote. They
include the native home screen, and cover native as well as extra quotes. Leaving
range or interrupting the run is not a synthetic dialogueend. Flag rules run in
the normal scene dispatch, not reentrantly inside NPC code.

## Intro pages

Use `Options introText` for a single replacement of "The legend says…". For
multiple pages use `Intro/Page`; both at once are an error. Each Page references
a String and has stay=3 (0..60 seconds), fadeIn=1/fadeOut=1 (0..10), color=#FFFFFF.
The total duration must be positive. Maximum 16 pages; text wraps at 440 px and
must fit 300 px high. Pages use the native centered menu font and FadeText entities.
Fast intro mode caps stay at 1 and each fade at 0.5 seconds; native debug skips
still skip the intro. Intro strings cannot depend on flags because gameplay has
not started. Preparation precedes native intro-tree construction on restarts too.

## Statistics and ending presentation

Custom ending behavior trees and custom statistics are different features.
[Ending trees](endings.md) customize the native ending animation sequence.
`Results/Page` adds measured, colored pages after the original statistics screen.
Native win statistics and completion/achievement bookkeeping are left intact.

Page fields: title (String ID), color=#FFFFFF, background=#000000. Row fields:
string, optional integer counter, optional requiresFlag/equals. Counter rows append
`: value`; strings can also interpolate flags. Up to 8 pages, 10 rows each; 440 px
wide and 300 px high in the native menu font. Flags are captured before scene
teardown; pages do not depend on a disposed live scene. Each page needs a new
native confirm/input press with the normal delay; holding a key does not sweep
through all pages. New preparation clears the previous run's pages.

This does not expose an arbitrary menu layout language or reorder the original
native statistics. Author additional pages rather than inventing fake values for
time/falls/jumps. Failing layout/translation validation is explicit, not a fallback
to a different narrative. Budget/layout checks use real fonts at preparation.
