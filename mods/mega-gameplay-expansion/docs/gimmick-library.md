# Gimmick library

Open **Mega Gameplay Expansion -> Gimmick library** from the main or pause
settings. The starting menu exposes Active & reset, Restore original map state, Pinned, Maps & regions,
Installed mods, Behaviour / Wind, Search, Saved searches and Refresh. Browsing
maps or mods does not inherit or overwrite the saved search filters. Reset can
disable MGE overrides and its three built-in modes, retaining configurations
and pins. Third-party toggles are separate persistent **Mod settings**; neither
bulk reset nor Restore writes them. Individual edits still use the owner's
controls and are not undone by Restore. See the [reset recovery note](gimmick-controls.md#library-menu)
for settings overwritten by the old 0.9.1/0.9.2 bulk reset.
Load a level to inspect player state and apply overrides. Navigation
uses normal menu controls, mouse and controller. The text editor supports native
text input, paste, caret/selection and deletion. Source map/region filters are
independent of the target scope. **Apply draft** commits edits; pins use the last
applied configuration. See the [interface and placement guide](gimmick-controls.md)
for combined filters, colour palette, saved searches, target unions and Wind.

Warp Jump, No Walk Off and Air Dash appear in the catalogue and are pinned by
default. Existing booleans, identifiers, bindings and map triggers retain their
meaning. Pin/unpin never activates an effect. Pins and override configurations
persist in the existing settings XML; an explicitly empty pin list stays empty.
Unavailable pins remain visible. Removing a mod does not erase its preferences.
Opening the library does not apply gameplay changes. Search preferences are saved separately.

## What discovery actually means

There are no adapters selected by a third-party mod's name, namespace or ID.

| Entry | Generic source | Control |
| --- | --- | --- |
| Built-in | The three existing MGE settings | Their original global setting |
| Setting | Installed mod menu methods returning native `IToggle` | The original permission check, toggle callback and persistence |
| Block | Registered `IBlockFactory` palettes and bounded construction recipes, real factory observations and already loaded colliders | Detached copies retaining the exact CLR type and scalar parameters |
| State | Bool/enum fields and writable static properties in loaded mod assemblies, behavior/component objects and nested objects (depth 2) | A lease holding a chosen value until release |
| Index set/table | Observed `HashSet<int>` or `Dictionary<int,bool/enum>` | Selected screen-minus-one indices; the user must verify these really represent screen indices |
| Wind | Native LevelScreen and wind-body contracts | Direction, relative strength, entry timing and target scope |
| Native screen state | Bool/enum instance fields on native `LevelScreen` | Values held on selected screens, including native wind enabled |

State discovery skips dormant types with static constructors unless a live
factory/behavior/component proves the type has already been initialized.
Opening a factory entry attempts to construct its material immediately, including
on the vanilla campaign. There is no source-map visit requirement for supported
construction recipes. Templates are not serialized or transplanted across DLL
versions/processes; saved encoded colours are rediscovered from their identities.

The construction interpreter follows the selected `GetBlock` branch with its RGB
and a small rectangular sample. It supplies a neutral Workshop-level record when
the native campaign has no Workshop `Level`; it never replaces the real content
manager's level. Factory static/instance stores and collection changes stay in
the interpreter. Only explicit native value/owned-collection operations are
invoked directly. Construction is bounded by instruction count, depth and array
size. Unsupported instructions, dependencies and object graphs fail with details.
Static initializers are interpreted before allocating a new type; CLR allocation
may initialize that type normally. This is a restricted construction mechanism,
not a security sandbox for arbitrary mod code. Provider gameplay methods still
execute normally once a material is enabled.

Factory/color identities are distinct even when two mods claim the same RGB.
Installed uncompressed Windows XNB5 atlases are indexed on a file-only worker;
factory ownership queries and game objects stay on the game thread. Main/DLC
content and Workshop maps are included. Indexing is lazy and explicitly
refreshable. Corrupt/unsupported atlases display an error. Color matches are
candidate provenance, not proof that a specific factory won the original load.
Unknown colors remain visible. Map tags are read but do not become executable
controls automatically. Encoded colors currently remain separate entries.

## Block application

| Mode | Result |
| --- | --- |
| Auto | New-rule default: inferred surface or empty-space placement; ambiguous effects require a choice |
| Surface | Same-bounds ordinary terrain replacement, retaining slopes and existing materials |
| Fill empty space | Retain geometry and add nonblocking volumes over exact unoccupied pixels |
| Replace nonblocking zones | Replace confirmed nonblocking volumes only |
| Ordinary solid | Replace exact native `BoxBlock` instances |
| All blocking terrain | Replace blocks that report blocking native collision |
| Existing blocks + zones | Replace existing colliders, including nonblocking volumes; leave air empty |
| Fill including air | Replace selected screens with a full-screen volume of the material |
| Nonblocking overlay | Retain original terrain and add full-screen nonblocking material volumes |

Rectangular `IBlock` implementations with scalar instance fields and one
consistent rectangle can be copied, including blocks that do not inherit
`BoxBlock`. Repeated copies of the same rectangle in base/derived fields are
updated together.
Native slopes retain orientation when copied as slopes. Converting a slope to
a rectangular material requires **Convert slopes**. Reference-owning blocks,
custom geometry and metadata-only factory results are refused. A screen marker
is metadata, not an invisible copyable collider; inspect the provider's state.

Existing provider handlers are retained. When missing, the library scans managed
IL for explicit native `RegisterBlockBehaviour` pairs, constructs that handler
and injects available native player/body/collision-query/input dependencies.
Only the selected handler is registered; no `OnLevelStart` callback is replayed.
Library-owned handlers are removed on disable/unload, including rollback after
failed multi-rule activation. Ambiguous handlers, extra external publication,
unavailable constructor parameters and external constructor writes are refused.
Group controllers, assets and map-tag configuration are not reconstructed. A
successful collider construction alone does not prove every provider-side
dependency or Harmony patch is active.
Block overrides change collision/effects, **not baked background/foreground
art**. There is no attempt to reconstruct arbitrary mod artwork.

Preparation builds from the original arrays, then checks conflicts, player
intersection, array ownership and size before committing. One terrain
replacement may affect a screen; multiple nonblocking overlays can coexist.
Full-screen solid fill is refused where it would enclose the player. Choose a
different region or a nonblocking material. An ongoing Warp transition must
finish before reconfiguration. Disable/release/unload restores original arrays
only while the session still owns them; concurrent foreign replacements cause
a refusal rather than silently overwriting another mod's geometry.

## Advanced states and release

Reflected members owned by an `IBlockBehaviour` are **read-only
collision diagnostics**. This applies to the handler's fields/properties, nested
objects and index collections, including handler objects reached through static
references. MGE identifies the native interface, without provider names or
per-mod adapters. A contact flag can depend on a collision object and its material
parameters; forcing the flag alone can crash the provider. These entries show
`Diagnostics` / `Read-only`, offer no Apply or Force controls, and link to
**Browse this mod's materials**. Choose the actual block and use **Auto** placement.
Conveyor materials replace ordinary surfaces while preserving their bounds.

Direct activation, pinned shortcuts and legacy saved rules enforce the same
restriction before acquiring state leases. If an old saved configuration contains
such a rule, startup refuses that configuration and reports the reason; remove
the saved override in its diagnostic card or use Restore original map state.
Other mod settings are untouched. Other reflected states remain experimental:
an available field/property is not proof of an independent, safe enable API.

These are raw controls, not inferred semantic switches. A mod can have several
independent gates, transition targets and input flags. **Details and full state
path** exposes the actual member. Bool can be held true or false; enums expose
their declared values. Turning the override off releases it; it does not mean
"force false". Choose False and enable when forcing a state off is intended.

Fields are written before the native body tick. Auto-property backing fields
also receive a generic getter postfix while held. This handles intercepted
reads even after a provider resets the backing field; calls already inlined by
the CLR and direct field reads cannot be intercepted. Other fields may be
overwritten later in the frame. State machines may need more than one field,
and arbitrary transition invariants cannot be derived reliably from reflection.
There is no blanket Sprint/Ball compatibility claim or bespoke fallback.

Scalar/collection values are restored on scope exit or release only when their
current value still matches this override. A replaced collection remains owned
by its original provider. Saved state rules record the provider module MVID and
value type; a changed binary requires explicit re-enabling. Refreshing paths is
refused while state leases are held. State changes start on the next player tick,
so a paused menu remains paused.

Actual generic override use marks the run modified. Original allow-tag behavior
of the three built-ins remains intact. Runtime snapshots refuse configurations
changed since capture. Generic overrides are not recorded as deterministic
Replay events; do not treat a matching on-disk map digest as replay equivalence.

**Release all block/state overrides** restores library-owned changes and clears
their saved rules. Original mod toggles and the three built-in settings continue
to use their own switches. No map files, mod DLLs, or player save files are edited
to implement geometry/state overrides; original foreign setting callbacks retain
their own persistence behavior.

## Lifecycle and verification

`BeforeLevelLoad` installs dormant discovery hooks using the sole already-loaded
Harmony engine. A prefix on public `LevelManager.LoadScreens` refreshes factory
hooks before pixel loading because native registration can be inlined. It only
observes factory results; expensive loaded-world catalogue work runs in Runtime
`OnWorldReady`, with named startup measurements, including the installed factory
palette needed for saved overrides. `BeforeAttempt` prepares selected saved
factory recipes, handler registration metadata, state paths, dormant getter
patches and selected geometry plans, including empty-space masks. Only the selected live state targets bind on the first player tick.
Without enabled overrides that tick performs no catalogue discovery, geometry
application or foreign setting calls. Full live state discovery happens on an
explicit library visit; dormant pin construction defers setting reads until the
menu is actually drawn. Runtime owns each session
and snapshot registration. Discovery hooks remain passive between sessions.

Run `scripts/check-mods.ps1 -Mod mega-gameplay-expansion -Integration`.
Tests compile a separate, otherwise unknown provider assembly and exercise real
Harmony observation, enum/bool getter holds, nested raw states, index collections,
shape policy, player-overlap refusal, original-array restoration, preferences,
column-major XNB parsing and overlapping region order. Native menu tests verify
default/empty/missing pins and render native browser, filters, palette, draft, wind and preview views under
`build/mega-gameplay-expansion/_INTERNAL/gimmick-ui/`. Existing native physics,
Warp, No Walk Off, Dash, audio and installed-map suites remain required.
These fixtures are not a full live playthrough of every installed mod.

The separate installed-provider audit runs with:

```powershell
& build/mega-gameplay-expansion/_INTERNAL/MegaGameplayExpansionTests.exe 'C:/Program Files (x86)/Steam/steamapps/common/Jump King' ConstructionAudit
```

It builds factory samples in a vanilla fixture without visiting source maps or
calling mod startup. The report is `_INTERNAL/installed-construction.txt`.
Counts cover palette candidates from successfully constructed factories; factory
construction failures are listed separately. Application/release checks verify
geometry and registration, not every mechanic's behavior. A dedicated real
Expansion Blocks gravity check also verifies collision-triggered gravity and
horizontal modifiers and their release. The unknown-provider regression checks
RGB parameters, isolated stores, refused file calls, real gravity, missing-handler
attachment, publication refusal and transactional cleanup.
