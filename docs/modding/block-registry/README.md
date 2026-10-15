# Jump King block-colour registry

This directory is the durable shared registry for **all our mods**, not a
Mega Gameplay Expansion-only palette. `catalog.json` is the reviewed snapshot from
2026-09-05: 34 installed Workshop DLLs, 24 concrete factories (including the
embedded Ball King module), plus native wind/teleport/fake pixels. It covers **5,252
distinct opaque RGB codes**, not just individual named constants.

Each `rgb_ranges` row is `[R, G, first_B, last_B]`, inclusive. Entries record
Workshop owner, assembly, factory and accepted count. DLL SHA-256 fingerprints
allow detecting stale evidence after an update. Unsubscribed mods and future
versions are not covered. Transparent pixels are not available allocations.

## Existing overlaps (not introduced by us)

| RGB | Owners |
| --- | --- |
| 128, 0, 0 | HighGravityBlockMod / Expansion Blocks |
| 16, 16, 16 | Expansion Blocks / Ghost of the Immortal Babe Blocks |
| 128, 128, 0 | Expansion Blocks / Ghost of the Immortal Babe Blocks |
| 144, 144, 0 | Expansion Blocks / Ghost of the Immortal Babe Blocks |
| 255, 190, 190 | Expansion Blocks / Ghost of the Immortal Babe Blocks |

Do not silently choose a winner: Jump King's factory registration order can
affect these existing combinations. New Mega Gameplay Expansion reservations avoid all
of them and all encoded ranges (for example JumpKingPlus warp destinations).

## JK Runtime screen reservations

Runtime reserves 30 opaque nonblocking screen pixels: `RGB(173,G,211)` for
G=80..109. Each mechanic receives On / Off / Local in that order. See the
[complete palette and activation contract](../../../mods/jk-runtime/docs/map-mechanics.md#marker-palette).
Screen pixels and XML Screens declarations are alternatives for the same
On/Off/Local permission. Use either one; XML needs no duplicate pixel. If both
describe the same screen/mechanic, they must agree. The controlling map tag stays
separate. These permissions do not create a mechanic's solid terrain or local zone.
Ball King's three existing restriction colours also produce metadata now; its
Sticky material remains solid. The historical DLL fingerprints below describe
factory colour ownership, not the current solidity of those colours.

## Mega Gameplay Expansion reservations

| RGB | Hex | Meaning |
| --- | --- | --- |
| 173, 47, 211 | #AD2FD3 | Warp Jump solid surface; arms the next departure |
| 173, 48, 211 | #AD30D3 | Warp Jump screen marker; affects jumps and falls on that screen |
| 173, 49, 211 | #AD31D3 | Warp Jump local nonblocking zone; activates on player overlap |
| 173, 50, 211 | #AD32D3 | No Walk Off solid surface |
| 173, 51, 211 | #AD33D3 | No Walk Off nonblocking screen marker |
| 173, 52, 211 | #AD34D3 | No Walk Off local nonblocking zone |
| 173, 53, 211 | #AD35D3 | Air Dash solid surface; grants one dash after jumping |
| 173, 54, 211 | #AD36D3 | Air Dash nonblocking screen marker |
| 173, 55, 211 | #AD37D3 | Air Dash local nonblocking activation zone |

These are exact collision pixels, not artwork colours. Do not interpolate or
antialias them. Reserve each future mode here before implementing its factory.
Ball King's four colours remain owned by Ball King and are included in the
snapshot, not reused by Mega Gameplay Expansion.

Every Mega Gameplay Expansion mechanic requires a complete Solid / Zone / Screen
triplet. Reserve all three colours here and in `catalog.json` before registering
the mechanic; do not replace an existing screen colour with a zone. Solid creates
terrain, Zone creates local nonblocking geometry, Screen creates metadata only.

## Reproduce / query

`powershell.exe -NoProfile -File scripts/audit-block-registry.ps1` compiles an
offline scanner and writes `build/_work/research/block-registry/installed.json`. It calls
factory constructors and `CanMakeBlock` for **every one of the 16,777,216 opaque
RGB values**, never mod entrypoints or `GetBlock`. Factory failure aborts the
audit instead of declaring the colours free. Inspect newly downloaded factory
constructors/predicates before running this tool against unfamiliar code.

`python scripts/block_registry.py --rgb 173 47 211` queries the durable snapshot.
Running without arguments verifies all reservations. Refresh/review the durable
snapshot after installing more block packs. Native special pixels must be added
separately, as LevelManager handles them outside IBlockFactory.

Native metadata also reserves `RGB(R,0,255)` screen teleports and
`RGB(R,255,0)` wind (R=0..255). Factories take precedence over this metadata;
the registry conservatively reserves their union.

`--refresh-labels` imports literal colour-constant names from the reviewed
decompilation under `build/_work/research/block-registry/decompiled` and reformats numeric
arrays. These labels aid lookup; the complete factory predicate scan, not name
extraction, defines occupied colours. Encoded families do not need one label per
destination/parameter value to remain reserved.

The scanner also inspects embedded first-party module DLLs. Assemblies with no
IBlockFactory remain in the fingerprint inventory; a Workshop item title alone
is not evidence that its DLL currently registers blocks.