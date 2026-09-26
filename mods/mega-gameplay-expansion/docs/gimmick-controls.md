# Gimmick library: search, placement and wind

Open **Mega Gameplay Expansion -> Gimmick library** in pause settings. The same
browser is available from the main menu; applying an override requires a loaded
map. The three original MGE mechanics remain pinned by default.

## Library menu

The starting screen provides nine visible actions and routes:

| Route | Purpose |
| --- | --- |
| Active MGE effects & reset | Inspect MGE overrides and its three built-in modes, including unavailable saved overrides; turn off one or all. |
| Restore original map state | Directly release only MGE-applied overrides; retain global mod settings, configurations and pins. |
| Pinned gimmicks | Open configured shortcuts or remove unavailable pins. |
| Browse maps & regions | Choose an installed map, then all its gimmicks or a numbered multi-screen region. |
| Browse installed mods | Choose a provider and see its entries. |
| Browse by behaviour / Wind | Open surfaces, media, Environment / Wind, player mechanics or advanced/unknown entries. |
| Search & colour filters | Resume the saved search with all the 0.9 text, palette and combined-filter tools. |
| Saved searches | Load, save or remove named queries. |
| Refresh catalogue & maps | Refresh discovery and the installed-map index. |

Map, provider and category browsing use their own fresh query. An old search
term or colour filter cannot hide their entries. Refining these views does not
overwrite the saved search. Back restores the previous page's selected row and
scroll position. Search results also expose **Menu** (mouse or Right) to return
directly to the library menu.

**Active MGE effects & reset** contains only MGE overrides and its three
built-in modes. Open an entry to turn it off immediately or edit its configuration.
**Turn off all MGE effects** releases overrides and disables Warp Jump, No Walk
Off and Air Dash. **Release overrides only** retains those three global switches.
Both retain names, placement, target scopes and pins. Map-authored triggers remain
independent.

Third-party menu toggles belong to **Mod settings**, available through installed
mod browsing and search. They are not evidence of a gameplay mechanic or an
MGE-applied effect, and never enter the active-effects list or bulk reset.
**Save to owning mod** explicitly edits the provider's persistent preferences;
Restore does not undo such individual setting edits. Pinned mod settings are
shortcuts to the same persistent controls.

If you used bulk reset in 0.9.1 or 0.9.2, check your other mods' settings. Those
versions invoked enabled third-party setting callbacks. Those callbacks could save `false`
in another mod's XML. Restarting or Restore cannot recover the prior values:
these versions kept no undo record. Re-enable affected controls in their owning
mods or recover the specific values from a known backup; preserve bindings,
save slots and unrelated options. The recovery cannot infer overwritten values.

## Find an effect

The search field matches all entered words against name, provider, identity and
behaviour family. Click the field or press Left. Text input supports native
characters, clipboard paste, selection, caret movement, Backspace and Delete.
Enter confirms; Escape restores the previous text. Characters absent from the
native font display as `?` in the editor but remain intact in the query.

**Filters** (or Secondary) combines providers, source maps, authored regions,
behaviour, geometry, availability, usage and exact colour. Choices within a facet
combine with OR; facets combine with AND. Region identities include their source
map, so region 1 of one map does not match region 1 of another. Region numbers
follow author order and may span several screens or overlap. Source filters do
not change where an override will apply.

Use the paged colour palette, `#RRGGBB`, `R,G,B` or `rgb(R,G,B)`. These are
collision-atlas colours, not artwork colours. Unknown colours are searchable
records with no executable toggle. Native wind marker colours are source
candidates too; a foreign factory can change their interpretation. Colour
occurrence is evidence of a source, not proof of which handler won map loading.

Filters show the result count. Remove a facet using a chip above the results or
edit it in Filters. Library menu -> Saved searches saves, loads or deletes named queries.
Text and filters persist; selection and scroll survive detail-page round trips
within the open browser. Sorting supports name/relevance, provider and readiness.
Unavailable filter selections remain visible in the active filter count and can
be cleared; they are not silently broadened.

Readiness describes discovery: **Needs preparation** means opening the entry can
attempt bounded construction; **Ready** means a template/control is present,
not that every external dependency is verified. **Unavailable** exposes a
reason. Classification of an unseen material stays unknown until preparation.
Typing, hovering and drawing do not call foreign getters or construct providers.

## Edit, apply and pin

Opening an entry creates a draft. Enabled, placement, value, slope consent, wind
controls and target selection change only that draft. **Apply draft** validates
and commits the complete configuration. **Discard draft changes** reloads its
saved values. Returning to the browser discards unapplied edits.

Pins operate the last applied configuration. Pinning does not apply a draft or
enable its effect. **Create another configuration** creates a named disabled
copy of a block or Wind rule. Assign its target, then Apply. Each copy has its
own pin; **Delete this saved copy** releases it and removes its pin. Raw states
and third-party menu toggles are not duplicated because they can share one
underlying state owner.

Targets can be the entire current map, current screen, multiple authored regions,
or explicit numbers/ranges such as `1,3-7,12`. Selected regions form a union of
screen numbers. Deselecting all targets means no screens, not the entire map.
Enabled rules without targets in the loaded map are refused. Scopes persist as
screen numbers and are interpreted against the next loaded map, not bound to a
particular Workshop map.

## Placement

New configurations use **Auto**. Previously saved modes retain their meaning.

| Choice | Behaviour |
| --- | --- |
| Auto | A confirmed blocking rectangle uses Surface; an inferred nonblocking medium uses Fill empty space. Unknown/contact/shape-changing effects require an explicit choice. |
| Ordinary surface, preserve shape | Replace ordinary native rectangular terrain with same-bounds, exact-type material copies. Keep slopes and existing material blocks intact. |
| Fill empty space | Retain existing objects and add nonblocking copies over their unoccupied pixels, following native slope collision. Existing nonblocking zones remain and may overlap the new effect. |
| Replace nonblocking zones | Replace confirmed nonblocking volumes while retaining blocking/unknown geometry. |
| Ordinary solid (legacy) | Replace exact native BoxBlock objects; retains the old saved mode. |
| All blocking terrain | Advanced replacement of blocking objects, including other materials. Slope-to-rectangle conversion needs explicit consent. |
| All existing blocks + zones | Replace existing colliders; originally empty space stays empty. |
| Entire space, including air | Replace selected screen arrays with full-screen material volumes. |
| Overlay whole screens | Add nonblocking full-screen volumes while retaining all existing objects. |

Surface mode does not grant arbitrary foreign materials a slope implementation.
Slopes remain unchanged. Geometry preview shows rectangle bounds and labels this
limitation; its blue empty-space mask follows actual slope collision. Unknown or
conditional source collision causes exact empty-space fill to refuse the request.
Masks are snapshots of the selected geometry; moving geometry has no universal
update contract and is not supported. No background rescans repair a stale mask.

Preview builds a temporary plan without installing it. Apply additionally checks
handler dependencies, player intersection, competing replacements, array ownership
and the per-screen object limit. Only one terrain replacement and one Wind rule
may target a screen. Multiple nonblocking layers may overlap; their original
handlers determine interaction. This does not recolour baked map artwork.

## Wind

Search **Wind**, or filter Behaviour -> Environment. Native Wind exists even on
windless vanilla maps. Select Alternating, Left, Right or Disabled, and strength
from 0x to 8x using the slider, wheel or arrows. 1x is ordinary native wind; 0x
disables wind rather than requesting the engine's default-intensity sentinel.

Immediate timing arms native wind on the next gameplay tick, including midair.
Native entry timing waits for the game's grounding/screen-entry condition.
Alternating uses the existing clock. Snow and NoWind still suppress native force.
The entry reports native gate status at inspection; third-party patches may add
their own conditions. Disabling an override restores authored flag, strength,
direction and its owned latch. Overlapping Wind and raw native wind-field rules
are refused so they cannot compete for the same screen fields.

Native Wind is integration with the game contract. Foreign wind controllers,
numeric tag schemas and arbitrary startup logic are not replaced with native
Wind or handled through per-mod adapters. Unsupported controls keep diagnostics.

## Preparation and verification

Runtime `BeforeAttempt` compiles selected saved geometry under
`mega-gameplay.prepare-geometry-plan`, alongside existing recipe/handler/state
preparation. Activation verifies and consumes that plan. Explicit paused edits
may compile a new plan; Draw, idle ticks and unselected entries never do so.

Regression coverage includes exact 172800-pixel floor/slope filling, concrete
provider type and object identity, draft isolation, XML compatibility, disjoint
scopes, wind ownership/restoration, search facets, text edits and native UI
captures. Startup tests verify 120 idle ticks without mask construction and
selected masks compiled before attachment with no rebuild at handoff. Native
physics, installed-map, audio and wind-continuation integration tests remain in
the build. These fixtures do not replace a live playthrough with every mod.

The library does not provide a collision-atlas eyedropper, fuzzy colour matching
or semantic contracts for arbitrary foreign screen effects. Unsupported entries
remain visible with diagnostics; see [discovery and control](gimmick-library.md).
