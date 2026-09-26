# UI release history

Archived UI release notes from the earlier documentation layout. New Runtime and
UI changes are recorded together in [the Runtime changelog](../../CHANGELOG.md).
The entries below preserve the behavior and version numbers of those releases.

## 1.27.1 - native inventory spacing and item colors

- Merge mod and cursed items into one column, leaving three unlabeled groups.
- Size columns to their contents and use the native inventory format for frame
  padding, anchoring, row spacing and Back placement. Remove duplicate cursor
  insets and the extra footer reservation.
- Preserve registered inventory item colors, with pixel comparisons against
  original row rendering. Skip inventory layout refresh on idle input ticks.

## 1.27.0 - compact inventory

- Add Compact Inventory to Runtime Settings, on by default, with a persistent
  opt-out to the original list. Keep native ownership conditions and item menus.
- Arrange registered mod items, cursed equipment, cosmetics and miscellaneous
  items in four unlabeled columns. Wrap names and show native equipped marks.
- Add column navigation, independent overflow scrolling and matching mouse hit
  areas. An open item menu retains input ownership; unknown custom row types
  retain native rendering and navigation.
- Add installed-game graphics/input regressions, settings round trips and the
  frozen 1.26 API surface. `UIApi.Supports("compact-inventory")` exposes support.

## 1.26.3 - segmented grid separators and native cursor spacing

- Split horizontal grid separators per column and add vertical segments between
  adjacent entries, limited to their content height. Keep gaps between segments
  and avoid cell outlines.
- Match native cursor geometry: 16-pixel text inset and a five-pixel selected
  shift. Reserve both when wrapping titles and metadata.
- Move Optimizations and Diagnostic mode into JK Runtime Settings in both main
  and pause menus, preserving their saved values and behavior.
- Validate separators and cursor geometry against actual native-font pixels.

## 1.26.2 - menu lifecycle, viewport safety and adaptive grid rows

- Clear interrupted menu decorators on exit/reentry, so Settings and vanilla
  Workshop Next require a fresh confirmation. Preserve genuinely active children
  and close interrupted embedded page lifetimes exactly once.
- Keep native settings and Workshop windows inside the game viewport. Oversized
  lists scroll to the selected row; mouse regions follow the visible rows,
  including native Workshop popup selectors.
- Size each three-column grid row by its tallest wrapped entry. Pack complete
  rows into the available height, add thin horizontal separators and reproduce
  the native five-pixel selected-text shift without changing wrapping.
- Add actual native lifecycle and nested-click regressions, pixel shift checks,
  viewport/scroll hit tests and headless adaptive layout/resource-lifetime tests.

## 1.26.1 — original menu behavior and compact Workshop layout

- Remove global selection/back audio hooks. Keep Move and Back silent, use
  native selectA for accepted actions/settings edits, and leave unavailable
  shared controls silent. Existing consumer binaries inherit this correction.
- Use three columns and up to twelve text entries per page, with the native
  selection arrow, no cell frames/highlights and no duplicate title footer.
- Recover complete Workshop author strings, retain status metadata and native
  completion icons, and wrap names using cached pixel-font layouts.
- Allow focused Runtime-only release installation without rebuilding or
  replacing unrelated installed feature mods.

## 1.26.0 — shared native feedback

- Add semantic UiSounds, ui-feedback-v1 discovery and separate normal/secondary/
  disabled colors. Shared controls own feedback; custom pages can use the service.
- Add navigation/back cues (removed in 1.26.1) and Runtime page feedback.
- Use wide, wrapped Workshop cells with native frames and the native cursor.
- Later Runtime UI changes are recorded in [the Runtime changelog](../../CHANGELOG.md).

## 1.16.0 — focused binding pages

- Add public UiBindingsPage and binding-pages-v1 capability for an explicit list
  of registered IDs. Keep constructors idle and use existing binding storage.
- Share primary/secondary slot editing with Controls+; support clear/default,
  mouse navigation, one/two-button capture and device/focus cancellation.
- Migrate Smooth Camera, MGE and Ball King to this common component.
- Preserve previous API signatures, binding IDs and device profiles.

## 2.1.0

- Exposed physical binding resolution for mods that need to sample Controls+
  chord bindings outside Jump King's frame-timed input path.

## 2.0.0

- Made Controls+ a permanent core UIApi+ facility instead of a toggleable
  built-in addon.
- Moved all concrete Consumables, Rewinder and Bargainburg gameplay into the
  dependent More Items mod.
- Added a generic Inventory/Equip registration contract so item mods can use
  Jump King's native Inventory hierarchy without copying its private menu
  integration.
- Added a dedicated equipment-state contract that renders Jump King's standard
  Equip checkbox instead of treating equipment as a generic action.
- Exposed the compact 2:1 card grid as a reusable public menu contract.
- Kept `ModsDebugActions`, merchants and currencies generic: source mods own
  and register every concrete action, offer and provider.

## 1.9.1

- Made Cancel and Escape return from compact Workshop grids to exactly one
  parent menu layer instead of closing the surrounding settings or pause menu.
- Deferred the parent result until the back button is released and guarded a
  returning card child from receiving the same press again.

## 1.9.0

- Added generic timed feedback results for root pause-menu actions and direct
  vanilla `TextButton` integrations.
- Kept feedback state inside the owning button and restored its normal label
  automatically after the requested duration.

## 1.8.0

- Added high-level, priority-ordered root menu page and action factories.
- Added page-owned Cancel policy for nested modal navigation and replaced
  temporary binding rewrites with reversible per-device modal input gates.
- Made consumable persistence fail closed with in-memory rollback instead of
  replacing an unreadable inventory and later overwriting it.
- Released empty chord wrappers after their last owner, validated vanilla
  binding persistence at startup and cached automatic binding discovery.
- Added a short disambiguation window so a single-button action cannot fire
  before a registered two-button chord using the same first button.

## 1.7.0

- Added stable, scoped root pause-menu command registration before the vanilla
  localized `Save & Exit` row.
- Kept pause-menu placement generic and independent of any consuming mod.
- Documented the complete root-menu registration lifecycle and capability.

## 1.6.2

- Anchored injected title-screen rows to the original vanilla frame bottom
  instead of the oversized title-screen anchor region.

## 1.6.1

- Anchored extended root menus to their vanilla lower edge so registered rows
  expand the frame upward instead of beyond the screen.

## 1.6.0

- Added stable root main-menu item registration before the vanilla Extras
  entry.
- Added a public return-to-main-menu operation for in-world playback pages.
- Made root menu injection idempotent and part of the existing menu-factory
  lifecycle.

## 1.5.0

- Added an exclusive secondary action for modal pages through the player's
  Boots binding.
- Suspended all enabled player components during modal pages so third-party
  movement and visual controllers cannot leak into an overlay.
- Added a public main-menu continuation operation for embedded pages that need
  to enter the currently loaded world.

## 1.4.0

- Added public embedded menu pages for independent full-screen mod interfaces.
- Added transparent modal pages for world overlays that retain exclusive input.

## 1.3.0

- Added default compact Workshop grids for levels, skin collections, individual
  skins and mods, with short 2:1 cards and four-direction navigation.
- Added opt-in Controls+ bindings for native mod toggles.
- Added a browser for pinning mod settings directly below Resume.
- Added nested modal-page stacking with exclusive top-layer input.
- Sized every compact Workshop window to its contents and removed author names
  from cards.
- Moved Pinned Settings beside Settings and Controls+ in the mod menu.
- Split Workshop-grid, pinned-setting and vanilla-menu integration into
  isolated components with idempotent factory refreshes.
- Added explicit, scoped display labels for discovered mod settings.
