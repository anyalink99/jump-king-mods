# Changelog

## 2.5.0 - shared player presentation

- Register Hammer as a Runtime attachment and declare Jetpack equipment and world-effect layer roles.
- Requires JK Runtime 1.35 or newer 1.x.

## 2.4.0 — map-controlled activation

- Use JK Runtime 1.34 screen/zone permission for Jetpack, Hammer and Rewinder.
- Support temporary map-equipped Jetpack and Hammer without changing owned counts or saved equipment; retain owned-only mode.
- Persist controlled modules Off on map entry, enforce Rewinder permission without granting charges, and release equipment at safe entity boundaries.

## 2.3.0 — Runtime particle engine

- Use JK Runtime 1.33's bounded pool, shared collision index and ordered sprite
  commands for Jetpack fire, including neighbouring screens and live foreign blocks.
- Preserve emission, lifetime, turbulence, bounce response, palette and thrust physics.

## 2.1.2 — Map-authorized Hammer

- Honor the exact `AllowHammer` level tag through the shared Runtime body pipeline,
  preserving native run eligibility on maps that authorize the equipment.
- Keep untagged use marked from equip, re-read permission across world loads, and
  preserve existing and foreign modifier history on unequip.
- Verify native modifier counters, tag matching, persistent equipment transitions
  and removal while a merchant modal has suspended the player.

## 2.1.1 — Hammer modal resume

- Preserve Hammer physics when merchant or other modal pages temporarily suspend
  player components; restore the sprite immediately on resume without crashing.
- Release visual ownership explicitly on unequip or controller disposal, including
  disposal during a modal suspension.
- Regress repeated modal suspension/resume and native player drawing on a real
  graphics device, plus unequip while suspended.

## 2.1.0 — Hammer equipment

- Move the complete Hammer King implementation, sound assets, source recordings,
  audio tool and regression fixtures into More Items.
- Sell Hammer for three Silver Coins; register an idempotent `Add Hammer` debug
  action and persistent Inventory Equip/Unequip behavior.
- Put force adjustment in Debug Actions. New 100% equals former 120%; the
  renamed 125% endpoint retains the former maximum without retuning physics.
- Require Runtime 1.14's live debug values, preserve other item settings and
  retain legacy standalone settings when the obsolete DLL is retired.

## 2.0.0 — JK Runtime migration (unpublished)

- Attribute transient Jetpack usage markers to More Items in Runtime's end-screen history.
- Hard dependency on JK Runtime 1.x, using its common generated package SDK.
- Remove old first-party reflection/optional-runtime compatibility paths.
- Use shared lifecycle, settings and gameplay/state contracts; preserve feature behavior.
- Install only as part of the coordinated runtime release; manual combined acceptance is pending.

## 1.0.0 — More Items

- Consolidated Jetpack, Rewinder, the item system and Bargainburg trading into one
  gameplay-content mod that depends on UIApi+.
- Moved Jetpack activation from a mod toggle to the ordinary Inventory Equip
  flow and added ownership persistence.
- Added a Jetpack merchant offer and an `Add Jetpack` registered debug action.
- Added the item-module registry, per-item compact settings cards and independent
  `Enable Rewinders` / `Enable Jetpack` switches.
- Kept module switches at the settings root, hid disabled modules from the item
  grid and added the default-on `Render rewind` option.
- Made Jetpack use UIApi+'s dedicated native Equip checkbox contract and kept
  its visual/audio settings editable while unequipped.
- Reused the actual Jetpack body sprite for its merchant offer.
- Preserved the existing Casual+, Ball King, physics, sprite and level-tag
  compatibility contracts.
- Replaced the separate Consumables namespace/API with the unified
  `MoreItemsApi`, module-owned runtime lifecycle and one settings store.
- Removed unreleased UIApi+ inventory migrations and marked the old standalone
  Jetpack mod obsolete.

## 1.0.1

- Made Casual Jumping and Ball King detection independent of DLL load order.
- Kept Casual+ thrust recoverable after a ceiling impact.
- Prevented Jetpack flight sprites from replacing the Ball King sprite.
- Fixed controller ordering when all three gameplay mods are enabled.

## 1.0.0

- Added momentum-preserving mid-air thrust with a smooth acceleration ramp and
  speed cap.
- Added a pose-aware jetpack, animated flame, embedded loop sound and optional
  world-space fire trail.
- Added dependent visibility, fire-trail and volume settings.
- Added ceiling-impact shutdown and a Vanilla ceiling lock until landing.
- Added the `AllowJetpack` level tag.
- Added compatibility with Casual Jumping without requiring it.
