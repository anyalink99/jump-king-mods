# Mod Inspector controls

Open **JK Runtime -> ModsDebugActions -> Mod Inspector**. Loaded world and
registered factories are read-only diagnostic views. Catalogue routes include
saved configurations, maps/regions, providers, behaviour, search and saved searches.

**Active overrides & reset** lists inspector-owned effects, including unavailable
saved overrides. Turn off one or all while retaining configurations.
**Restore original map state** releases the same overrides. Neither action calls
other mods' setting callbacks. Map-authored triggers remain independent.

**Save to owning mod** explicitly edits a provider's persistent setting. Restore
doesn't undo that edit. This also applies to MGE's three mechanic switches.

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
edit it in Filters. Mod Inspector -> Saved searches saves, loads or deletes named queries.
Text and filters persist; selection and scroll survive detail-page round trips
within the open browser. Sorting supports name/relevance, provider and readiness.
Unavailable filter selections remain visible in the active filter count and can
be cleared; they are not silently broadened.

Readiness describes discovery: **Needs preparation** means **Prepare material sample** can
attempt bounded construction; **Ready** means a template/control is present,
not that every external dependency is verified. **Unavailable** exposes a
reason. Classification of an unseen material stays unknown until preparation.
Typing, hovering and drawing do not call foreign getters or construct providers.

## Edit and apply

Opening an entry creates a draft. Enabled, placement, value, slope consent, wind
controls and target selection change only that draft. **Apply draft** validates
and commits the complete configuration. **Discard draft changes** reloads its
saved values. Returning to the browser discards unapplied edits.

**Create another configuration** creates a named disabled copy of a block or
Wind rule. Assign its target, then Apply. Find these copies under **Saved
configurations**. **Delete this saved copy** releases and removes a copy.
Unavailable providers remain listed so their saved configurations can be deleted.
Raw states and third-party menu toggles aren't duplicated because they can share
one underlying state owner.

Targets can be the entire current map, current screen, multiple authored regions,
or explicit numbers/ranges such as `1,3-7,12`. Selected regions form a union of
screen numbers. Deselecting all targets means no screens, not the entire map.
Enabled rules without targets in the loaded map are refused. Scopes persist as
screen numbers and are interpreted against the next loaded map, not bound to a
particular Workshop map.

## Placement

New configurations use **Auto**.

| Choice | Behaviour |
| --- | --- |
| Auto | A confirmed blocking rectangle uses Surface; an inferred nonblocking medium uses Fill empty space. Unknown/contact/shape-changing effects require an explicit choice. |
| Ordinary surface, preserve shape | Replace ordinary native rectangular terrain with same-bounds, exact-type material copies. Keep slopes and existing material blocks intact. |
| Fill empty space | Retain existing objects and add nonblocking copies over their unoccupied pixels, following native slope collision. Existing nonblocking zones remain and may overlap the new effect. |
| Replace nonblocking zones | Replace confirmed nonblocking volumes while retaining blocking/unknown geometry. |
| Ordinary solid | Replace exact native BoxBlock objects. |
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

Selected saved recipes and state paths are prepared in `BeforeAttempt`. Geometry
compiles after native `OnLevelStart` callbacks have finished correcting the world.
Activation consumes the plan for the same arrays and settings; it refuses stale
plans. Explicit paused edits can compile a new plan. Drawing and idle ticks don't.

See [lifecycle and tests](inspector-lifecycle.md) and [discovery](mod-inspector.md).
