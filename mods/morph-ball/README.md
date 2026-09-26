# Ball King

[Technical documentation](docs/index.md).

Ball King lets the king curl into a rolling ball, cling to solid surfaces and
travel along floors, slopes, walls and ceilings. Version **2.2.0** requires
[JK Runtime 1.30 or newer 1.x](../jk-runtime/README.md).

The ball is built at runtime from Jump King's active layered sprite. Base
reskins, equipped clothing and Workshop clothing remain visible without
requiring compatibility atlases.

**Binds** in the main and pause mod settings opens the shared Runtime editor
for **Morph** only: primary/secondary slots, two-button chords, Clear and Default.
It shares the existing `auto.MorphBall.Morph` Controls+ provider and per-device
Runtime profiles; legacy Morph keys and explicitly cleared profiles remain valid.

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

Ball King registers four opaque solid collision colours before a level loads:

- `RGB(160, 64, 255)` — **Restricted screen**. The block itself collides like
  an ordinary solid block. Its presence anywhere on a screen gives Ball King
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

All four colours remain ordinary blocking geometry when Ball King controls are
disabled, provided the mod is installed while the custom level is loaded.

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
