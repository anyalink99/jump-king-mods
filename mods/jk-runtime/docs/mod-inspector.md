# Mod Inspector

Open **JK Runtime -> ModsDebugActions -> Mod Inspector** from main or pause
settings. Load a map to inspect live blocks or apply overrides. The feature is
experimental; an available reflected member is not a promise that forcing it is safe.

**Loaded world & block factories** shows current-screen objects, actual CLR
types, bounds, observed factory/RGB origins and stored slope lines. Unknown
origins are reported as unknown. The list shows at most 256 blocks per screen;
the material catalogue groups types and colours separately.

Browse maps, providers or behaviour, filter exact colours, save searches and create named
configurations. Opening a card never constructs a material. **Prepare material
sample**, Preview and Apply are explicit operations. Reset releases only
inspector-owned overrides; settings edited through a mod's own callback remain
that mod's settings, including Warp Jump, No Walk Off and Air Dash.

Update Runtime and MGE together when replacing the old library. Older MGE
releases still install their original factory observer.

## Settings

Preferences live beside Runtime in `JKRuntime.Inspector.xml`. The inspector
starts with empty configurations and searches. It doesn't read MGE settings.
Named material and wind configurations are available under **Saved configurations**;
queries are under **Saved searches**.

`JKRuntime.Inspection`'s public preference, rule, application-mode and search
types describe the inspector's XML schema. They aren't a provider registration API.

## What discovery actually means

Material and state discovery use native contracts. Loader provenance has a reviewed More Block Sizes adapter; unknown loaders retain live geometry with unknown provenance.

| Entry | Generic source | Control |
| --- | --- | --- |
| Setting | Installed mod menu methods returning native `IToggle` | The original permission check, toggle callback and persistence |
| Block | Registered `IBlockFactory` palettes and bounded construction recipes, real factory observations and already loaded colliders | Detached copies retaining the exact CLR type and scalar parameters |
| State | Bool/enum fields and writable static properties in loaded mod assemblies, behavior/component objects and nested objects (depth 2) | A lease holding a chosen value until release |
| Index set/table | Observed `HashSet<int>` or `Dictionary<int,bool/enum>` | Selected screen-minus-one indices; the user must verify these really represent screen indices |
| Wind | Native LevelScreen and wind-body contracts | Direction, relative strength, entry timing and target scope |
| Native screen state | Bool/enum instance fields on native `LevelScreen` | Values held on selected screens, including native wind enabled |

State discovery skips dormant types with static constructors unless a live
factory/behavior/component proves the type has already been initialized.
Opening a factory entry is read-only. **Prepare material sample**, Preview and
Apply can explicitly construct its material, including on the vanilla campaign. There is no source-map visit requirement for supported
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
Native slope copies preserve the stored collision lines, including foreign corrections, without running constructors. Converting a slope to
a rectangular material requires **Convert slopes**. Reference-owning blocks,
custom geometry and metadata-only factory results are refused. A screen marker
is metadata, not an invisible copyable collider; inspect the provider's state.

Existing provider handlers are retained. When missing, the library scans managed
IL for explicit native `RegisterBlockBehaviour` pairs, constructs that handler
and injects available native player/body/collision-query/input dependencies.
Only the selected handler is registered; no `OnLevelStart` callback is replayed.
Inspector-owned handlers are removed on disable/unload, including rollback after
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
different region or a nonblocking material. An active movement owner can refuse reconfiguration. Disable/release/unload restores original arrays
only while the session still owns them; concurrent foreign replacements cause
a refusal rather than silently overwriting another mod's geometry.

## Advanced states and release

Reflected members owned by an `IBlockBehaviour` are **read-only
collision diagnostics**. This applies to the handler's fields/properties, nested
objects and index collections, including handler objects reached through static
references. Runtime identifies the native interface, without provider names or
per-mod adapters. A contact flag can depend on a collision object and its material
parameters; forcing the flag alone can crash the provider. These entries show
`Diagnostics` / `Read-only`, offer no Apply or Force controls, and link to
**Browse this mod's materials**. Choose the actual block and use **Auto** placement.
Conveyor materials replace ordinary surfaces while preserving their bounds.

Direct activation and saved rules enforce the same
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
Sprint/Ball compatibility requires a supported controller-state contract.

Scalar/collection values are restored on scope exit or release only when their
current value still matches this override. A replaced collection remains owned
by its original provider. Saved state rules record the provider module MVID and
value type; a changed binary requires explicit re-enabling. Refreshing paths is
refused while state leases are held. State changes start on the next player tick,
so a paused menu remains paused.

Actual generic override use marks the run modified. MGE's own mechanics retain
their original allow-tag behavior. Runtime snapshots refuse configurations
changed since capture. Generic overrides are not recorded as deterministic
Replay events; do not treat a matching on-disk map digest as replay equivalence.

**Restore original map state** releases inspector-owned changes and disables
saved overrides while retaining their configurations. Other mods' settings,
including MGE's mechanic switches, remain under their original controls. No map files, mod DLLs, or player save files are edited
to implement geometry/state overrides; original foreign setting callbacks retain
their own persistence behavior.


See [controls](inspector-controls.md), [lifecycle](inspector-lifecycle.md) and
[compatibility](compatibility.md#block-observation-and-ceiling-slopes).
