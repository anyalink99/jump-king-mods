# Ball King

Ball King lets the King curl into a ball, roll across floors and slopes, and
cling to walls and ceilings. Version **2.4.1** requires
[JK Runtime 1.36 or newer 1.x](../jk-runtime/README.md).
The shared visual bridge also needs one loaded Harmony 2 engine, such as the
copy supplied by Subframe Charge or Mega Mapping Expansion.

[Technical guides](docs/index.md) · [Map setup](docs/map-authoring.md)

The mod builds the ball from the King's current sprite. Base reskins, equipped
clothing and Workshop outfits stay visible without separate compatibility art.

Open **Binds** in the main or pause settings to change **Morph**. The shared
Runtime editor supports primary and secondary slots, two-button chords, Clear
and Default. It keeps the existing `auto.MorphBall.Morph` Controls+ provider and
per-device Runtime profiles. Existing Morph keys and deliberately cleared
profiles still work.

## Controls

- Press the configurable `Morph` binding to curl up or unfold.
- Use left and right to roll along the current surface.
- Press jump while attached for a variable-height platformer jump.
- Press jump shortly after rolling off a surface to use the same jump in the
  air without consuming the optional double jump.
- Release jump before pressing it again to activate a compatible Jetpack.

`Sticky` enables rolling across slopes, walls, ceilings and corners. Turn it
off for a ground-only ball; regular slopes are roll-only and carry the ball
downhill rather than acting as jump or bounce launchers. `Double jump` adds one
aerial jump, including the regular jump sound and particles, before the next
surface contact.

Curling and unfolding use matching forward and reversed 8-bit sound effects.
The ball never enters Jump King's splat state. Falls of at least 100 pixels
bounce once; falls from the vanilla splat height start a two-bounce sequence.

## Level tag

Ball King marks the run as modified and disables achievements by default.
Level authors can add `AllowBallKing` to permit it without marking the run as
modified.

## Custom-map pixels

The legacy Ball King colours include three opaque nonblocking screen markers and
one solid material. The common On/Off/Local palette is listed under
[Map-controlled activation](#map-controlled-activation).

- `RGB(160, 64, 255)` — **Restricted screen**. The pixel creates no collider and enables Ball King on its screen. It gives Ball King
  surface/coyote jumps a coherent 75%-height profile. When Double jump mode is
  enabled, both the surface jump and aerial jump instead use a 35%-height
  profile. All profiles have proportionally shorter takeoff and hold phases.
  The rule also disables the global Sticky setting for that screen.
- `RGB(255, 96, 64)` — **Restricted screen, no Double jump**. Applies the same
  jump and Sticky restrictions and disables Double jump on that screen.
- `RGB(64, 128, 255)` — **Restricted screen, forced Double jump**. Applies the
  same jump and Sticky restrictions and enables Double jump on that screen,
  regardless of the player's setting.
- `RGB(255, 64, 160)` — **Sticky surface**. A morphed ball treats only this
  material as sticky even when Sticky is disabled or the screen is restricted.
  Connected pixels form solid platforms; diagonal supported cells are inferred
  as native slopes from the same colour.

Only the Sticky material remains blocking geometry when Ball King is disabled.
The three screen markers never create terrain.

While paused on a restricted screen, affected Ball King settings show
`Restricted` instead of a misleading editable value.

## Compatibility

Ball King works independently. Casual Jumping yields control while the king is
a ball, and Jetpack remains available while airborne. The integration is
independent of DLL load order.

Subframe Charge's enabled and measurement-only modes are supported in either
initialization order. SFC's charge quantizer does not apply to the ball's
separate platformer jump system.

The form role is the typed `JKRuntime.Gameplay.IPlayerForm` contract, published
as `player.form:1:0`. Mods with custom
non-rectangular solid colliders can provide their exact world-space polygon via
`BallKingGeometryApi.Register`; ordinary rectangular blocks and native slopes
need no adapter. Runtime ordering, state ownership and extension contracts are
documented in [`docs/architecture.md`](docs/architecture.md).

Runtime 1.3 also exposes the existing ball contour as `ball-king.contour` version
1. Its bottom-left slope correction never changes the native blocks used by an
ordinary King. Third-party geometry registrations continue to work as before.

## Installation

The outfit compositor preserves individual sprite anchors, including padded
frames from Wardrobe+ position fitting. Updating Ball King is necessary when
using those fitted appearances with the ball form.

Place `MorphBall.dll` in:

```text
Jump King/Content/JKMods/
```

Restart the game after installing or updating the mod. Graphics and audio are
embedded in the DLL.

## Building

```powershell
.\build.ps1
.\install.ps1
```

The release DLL is written to:

```text
build/morph-ball/UPLOAD_TO_WORKSHOP/MorphBall.dll
```

The Workshop shell remains `MorphBall.dll`, with its runtime implementation
embedded. See the architecture notes for the current extension APIs.

## Map-controlled activation

Add the relevant exact tag to `Tags` in `level_settings.xml`:

- `JKRuntime.MapControlled:ball-king.form`

| Mechanic | ID | On RGB | Off RGB | Local RGB |
| --- | --- | --- | --- | --- |
| Ball form | `ball-king.form` | `(173,80,211)` | `(173,81,211)` | `(173,82,211)` |

Colours are opaque nonblocking screen metadata. On enables the screen; Off denies
activation; Local permits only local triggers or XML zones. On a tagged map,
unmarked screens are off and manual enabling is locked. Entry saves global enable
Off; it stays off after leaving until the player enables it again.

**Screen markers and XML Screens declarations are alternatives for the same
On/Off/Local rule: use either one, without duplicating it in the other.** XML also
supports ranges, zones and supported parameters. If both are used for the same
screen/mechanic, they must agree; XML does not override a marker. The map tag
remains separate and is not added automatically by either method.

Optional screen/zone declarations belong in `jk-runtime/mechanics.xml` at the
map root. Screens start at 1; coordinates use 480 x 360 screen-local pixels.
Pixel and XML declarations must agree.

[Map authoring: tags, exact colours, complete XML and Ball King rules](docs/map-authoring.md)
contains the full instructions within this mod.
