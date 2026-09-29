# Wardrobe+ changelog

## 2.2.0 - shared player presentation

- Publish outfit revisions through Runtime and provide scoped animation contexts for captures. Keep independent replay actors and retain the outfit resources they use. Existing discovery bridges remain available.
- Requires JK Runtime 1.35 or newer 1.x.

## 2.1.0 — Runtime particle engine

- Use JK Runtime 1.33's pooled particle storage, explicit update clock, shared
  prepared world geometry and ordered sprite commands for authored effects.
- Keep water motion, manifest rules, alpha/additive layers and independent
  preview/replay clocks in Wardrobe+. Existing skin packages require no edits.

## 2.0.11 — spilled water performance

- Index nearby block bounds in 16-pixel cells for water droplets. Point queries
  inspect local candidates instead of scanning every nearby block at each substep.
- Reuse collision scratch storage and discard world references and occupancy
  after each update. Moving blocks, foreign blocking state and world replacement
  remain visible; particle count, motion and lifetime are unchanged.
- Resolve particle colors and sprite resources when emitted, and skip empty
  attachment passes. Rendering no longer parses colors or looks up textures for
  every droplet on every draw.

## 2.0.10 — reward materials throughout ending scenes

- Apply item materials to reward artwork embedded in the base ending atlas,
  even when the separate equipment layer is empty or the reward is not yet worn.
- Follow Crown through delivery and wearing, CrownNBP from Babe's head through
  handoff to the King, and CapeOwl while carried by the owl. Preserve the NBP
  Babe's replacement crown, its delivery, and all unrelated actor pixels.
- Keep body and reward materials independent within one sprite, including live
  Cosmic with a Glass crown. Native reward timing and equipment stay unchanged.

## 2.0.9 — item ownership in ending poses

- Apply separate ownership masks to all native item slots instead of reusing the
  body mask. Ending boots, crowns and capes receive materials across their own
  visible silhouettes; NPC-only frames stay protected.
- Reconstruct hidden item contours only where native pose pixels provide exact
  evidence. Keep body and item masks in a shared, validated lookup independent
  of the selected collection or item source.
- Add item-mask review sheets and graphics checks for protected pixels, live
  Cosmic rendering, collection mixing and restoring original materials.

## 2.0.8 — water collisions and sprite ownership

- Spilled water collides with native and compatible custom blocks, spreads on
  impact, falls from edges, settles in basins and evaporates within a few seconds.
  Effects opt in through `collision: "water"`; previews and replay actors remain independent.
- Outfit and item materials now apply to authored animation frames. Original
  texture restores a collection's own shader without changing its animations.
- Protect other characters and props with an explicit two-class atlas mask.
  Occluding pixels contribute to the hidden King contour but retain their original
  artwork. Separate objects and ending accessories do not enter the body model.
- Rebuild Ashen, Eclipse and Vessel fallback atlases with the shared ownership mask.

## 2.0.7 — Vessel King

- Add a separate glass body collection with inertial water, impact waves,
  splat drainage, ballistic droplets, a fading puddle and gradual recovery.
- Authored shaders can opt into bounded actor-owned liquid simulation; preview
  and replay actors have independent state, and rendering never advances it.

## 2.0.6 — native walking cadence

- Eclipse King selects walking frames from the native sprite instead of cycling
  all poses at 80 ms. Native holds, smear frames and replay poses remain intact.
- Clips can opt into `nativeFrames` to map authored frames to native pose keys.

## 2.0.5 — Eclipse King

- Add Eclipse King as a separate body collection with dark armor, gold trim,
  sequential visor ignition, reflected light, takeoff flash and flight cooling.
- Expose actor-owned jump and landing ages, takeoff charge and charging state to
  authored shaders. Pose transitions preserve these clocks; restore clears them.
- Validate minimum skin versions against the current module version.

## 2.0.4 — flight pose timing

- Ashen King switches directly from ascent to descent with the native pose,
  without an extra apex animation holding the ascent pose after descent begins.
- Only explicitly authored body apex clips delay the fall animation. Apex
  events still fire without a clip; looping clips exit before wrapping.

## 2.0.3 — native audio timing

- Dispatch takeoff, landing and splat audio at the native playback call, keeping
  native audio ahead of added accents, particles and cosmetic event observers.
- Preserve native sound exceptions and avoid replaying native audio after an
  authored effect fails.
- Ashen King now keeps native impact and takeoff audio on every surface,
  including heavy boots; its charge sound remains independently configurable.

## 2.0.2 — native local package browsing

- Register local cosmetic packages in the native Workshop Collections and Skins
  lists, with titles, previews and selection controls, including after refresh.
- Preserve stable local IDs and external Workshop entries without assigning
  fake Steam IDs to local packages.

## 2.0.1 — Ashen King collection

- Package Ashen King as a native body collection; migrate prior standalone
  choices, favorites and fitting references while preserving other outfit choices.
- Keep the standing pose on landing, use opaque pose changes and remove
  package-added screen shake. Native equipment feedback remains available.
- Check complete animation loops at four draws per simulation tick, including
  the native player draw path with Smooth Camera.

## 2.0.0 — advanced cosmetic packages

- Load declarative Skin/Set manifests with dependencies and native fallbacks.
- Add state clips, frame events, attachment hierarchies/inertia, equipment anchors,
  masked materials, native/custom particles, owned audio and character shake.
- Select animation, particles, surface audio, equipment audio and shake separately;
  preserve snow eligibility, foreign callbacks and heavy-boots gameplay.
- Add author preview, resource reload, diagnostics, schema 4 outfit persistence,
  validation/packaging tools and the original-model Ashen King reskin example.
- Expose cosmetic events and independent actors for Replays 2.3 playback/ghosts.

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
