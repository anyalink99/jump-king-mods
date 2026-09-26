# Wardrobe+ changelog

## 1.4.3 — faster Cosmic drift

- Triple galaxy and star-layer drift speed, preserving the existing twinkle
  rhythm, map anchoring and seamless screen transitions.

## 1.4.2 — clearer Glass

- Reduce Glass interior opacity fivefold, including facet seams, while retaining
  the bright silhouette rim. Lower layers remain visible without refraction.

## 1.4.1 — keep the wardrobe open during edits

- Stop re-registering menus after equipment, appearance and preference changes.
  Deferred Workshop refresh used to replace the running wardrobe page.
- Make startup menu registration idempotent. Verify Equip/Unequip, materials and
  preferences through a real embedded page and Runtime's deferred menu callbacks.

## 1.4.0 — live wardrobe and complete outfits

- Remove Apply: skin, material and fitting edits publish immediately. Back keeps
  changes; bounded Undo/Redo includes equipment and groups repeated fitting steps.
- Equip/unequip owned items through native slot rules. Preview the worn outfit,
  preserve list selection, filter equipped items and search skins/saved outfits.
- Save complete presets and recipes in schema 3. Legacy appearance-only presets
  keep equipment unchanged; empty equipment snapshots explicitly remove clothing.
- Use JK Runtime 1.17 shared keyboard/gamepad text entry for names and searches.
- Queue actions without dropping rapid changes, coalesce fitting writes, reuse
  prepared preview/material textures and preserve state on failed publication.

## 1.3.0 — Cosmic

- Add Cosmic for the body, clothing and outfit default: white contour, black
  interior, spiral galaxies and two animated star layers. Remove source hue and
  nearly all source detail while preserving silhouettes and source alpha.
- Anchor the field to map coordinates. Preserve its direction when mirroring
  and stitch adjacent screen viewports continuously with one time per frame.
- Render each visible part in one masked quad, without capturing the scene or
  generating textures each frame. Preload shared resources at world readiness.
- Preserve fitting, presets, recipes and texture-readable static fallbacks.
  Glass remains non-refractive; the previous refraction experiment stays parked.

## 1.2.3 — transparent Glass and Magenta

- Replace Diamond's active refraction with ordinary alpha-blended Glass. Keep
  neutral facets and bright edges; remove scene capture and optical-map overhead.
- Rename Red velvet to Magenta without changing its palette or fabric shading.
- Keep existing material IDs so settings, presets, undo and recipes remain valid.
- Park the refraction implementation, shader and GPU fixtures behind the
  developer-only `-ExperimentalRefraction` build switch. Normal builds do not
  require a shader compiler or embed the refraction shader.

## 1.2.2 — faster live diamond rendering

- Replace thousands of per-pixel draws with one shader quad per visible crystal
  sprite. Prepare shared optical maps when baking the appearance.
- Keep live map/lower-layer refraction, dispersion, neutral facets, fitting and
  clipping. Skip scene capture for invisible and offscreen crystals.
- Embed the compiled shader in the existing DLL; no extra runtime dependency.
- Add an optional GPU-completed benchmark against the previous installed package,
  with alternating run order, warm-up and percentile reporting.

## 1.2.1 — reference palettes and live crystal refraction

- Match gold to the installed GoldenBoots ramp and velvet to the red Tunic's
  raspberry palette; preserve source folds instead of overlaying broad stripes.
- Remove source hue from diamond. Refract the current rendered map and lower
  character layers on the GPU, with dispersion and neutral facet reflections.
- Retain fitted/mirrored native sprites and texture-readable fallbacks for
  flattened appearance consumers. Preserve targets, clipping and batch state.
- Reuse capture resources, prepare them at world readiness when needed and
  release them with the world scope. No gameplay pixel readback.

## 1.2.0 — outfit materials

- Add Gold, Diamond and Red velvet to body and clothing, with an outfit default
  and independent per-item overrides. Preserve source coverage and shading.
- Refract lower character layers through diamond facets, including fitted
  positions, chromatic dispersion and refresh after native equipment changes.
- Include materials in the existing saved outfits, undo and recipes. Read legacy
  data; write schema 2 so older builds cannot silently discard material choices.
- Keep ordinary sprite layers for native rendering and appearance consumers.
  Refraction uses current character layers, not the world background.

## 1.1.1 — menu access only

- Remove the opening hotkey and its binding registration. Retain Workshop and mod
  settings access, saved appearances and legacy chord data.

## 1.1.0 — simpler outfit editing

- Put collection, items and their selected appearances on one main screen with
  a preview and persistent Apply, Outfits and More commands.
- Open skins directly from each item and preview unequipped items automatically.
- Replace Inherit with From collection or Map appearance; move explicit default
  choices, randomizer locks and pose-specific positioning into item options.
- Start all-pose positioning immediately with Move; retain detailed fitting tools.
- Apply enables the selected outfit automatically. Restore normal appearance
  replaces the separate Enabled setting and preserves saved looks.
- Group saved-outfit actions together; put recipe, map, preview and diagnostic
  tools behind More. Remove Draft and Try-on terminology from the regular flow.
- Preserve existing settings, presets, recipes and fitting data.
