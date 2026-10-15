# Inspect and troubleshoot

[Handbook](../index.md) / [Reference and diagnostics](index.md)

Start with the first reported error and follow the data from its source to its output. Changing art coordinates at random can hide the cause without fixing it. Keep the failing package/source so the problem can be reproduced.

## Read the inspector

Mapping inspector shows components, effective properties and owners, observations with quality/sequence, variables, effect definitions/active leases, tree states and bounded diagnostics. Refresh when examining a changing value. A native pause/UI may hold the scene while you're inspecting it.

In a -debug local run, preview bindable properties and activate authored effects. Structural fields require source edits and reload. Clear preview removes inspector contributions. Export writes `props/mega-mapping-expansion/preview/inspector-effect.xml`; review and include that MappingFragment in source if you want to keep it. It doesn't rewrite source or turn a preview into a new baseline automatically.

Scene reload validates/prepares the candidate before switching. A failed candidate keeps the active scene. Geometry/layout changes need a restart; reload is blocked while another session owns shared world state. Clear previews before judging an authored binding so an inspector owner isn't masking it.

## Scene won't load

Read MegaMappingExpansion.log and Runtime diagnostics. Confirm the playable root contains mapping_settings.xml, referenced fragments/assets and the expected layout. Don't confuse a source checkout with the folder the game actually opened.

Load that exact folder in Jump King and read the first MME error. Fix the reported unknown field, duplicate ID, missing reference or resource error, then restart the map. MME refreshes stale or corrupt local caches automatically; no compiler or cache rebuild step is needed. Removed formats aren't accepted as current construction. An error from native PropManager usually points to native prop definitions/resources, which need their own schema.

## Art is missing or misplaced

Inspect transform screen and final position, parent, scale, sprite origin, layer, z, visible/enabled, material opacity and captureOnly. A sprite anchors its origin; a mesh centers geometry. Screen 0 repeats local scenery, not one giant world-space image.

Check the exact asset ID/path and atlas grid. With compiled maps, verify the matching uncompressed XNB is packaged. If a capture is empty, confirm its selection, stage, actor availability and output ordering. A capture-only source won't draw outside a selected capture. Recursive captures are invalid.

For a mask, distinguish local from screen space and inspect its alpha. A colored but transparent mask won't reveal anything. A final screen-sized capture overlay normally belongs at `(0,0)`; moving it shifts the entire captured screen.

## A value doesn't drive its property

Inspect the input's quality first. Unavailable isn't zero. Then check graph IDs, kinds and units, conversion to Number/Integer, target bindability and effective owners. Another higher-priority contribution may be overriding the binding.

Guard division by zero and invalid ranges. Remap doesn't clamp. Properties read as graph inputs are last-committed values; use a direct node dependency for current-evaluation relationships. A delay is intentional previous-sample memory, not a universal fix for incorrect references.

## Contacts fire too early or not at all

A region tests body overlap, including boundary contact; it doesn't prove a landing. Native hidden walls use strict integer rectangles and grouped membership. Preserve native events for faithful native wall behavior. A visual mask doesn't narrow a sensor.

For surface sensors, check the exact type/ID/tag selection, screen and contact phase. Slope aliases preserve source identity and only real triangle contact. Nonblocking return/warp blocks don't emit blocking impacts; use enter where appropriate. Pre-teleport event position and relocated player position aren't interchangeable.

For landing-only feedback, test downward top crossing, sideways grazing and upward passage. For a faithful remake, include the exact successful trajectory in regression checks; it may deliberately touch a boundary without intersecting it.

## Animation or sequences freeze

Check clocks on every moving part: graph integrator, field, sprite, emitter, effect, rule cooldown and tree wait. Gameplay stops at completion; Presentation may continue through an active ending. Pause freezes both visual clocks by default. `view.animateWhilePaused=true` is an explicit visual opt-in and doesn't enable paused logic. The scene checkbox holds everything regardless.

For a tree, inspect its current condition and event spelling. waitEvent sees future events after arming, not old history. A tree finishing releases its effects; include a wait while it should own a held contribution. One rule cannot cancel another rule's effect.

Continuous particles/automatic playback reconstruct after restore. Bursts and stamps clear. Sounds aren't replayed. A restart is a new attempt; Save counters reload, Attempt gates reset. If only an old API lease looks active, inspect generation and rebind after scene replacement.

## Audio is missing or doubled

Confirm the sound key exists in native event_music and is packaged in the loaded map. A sound action takes a key, not a path. If a hidden wall already plays the clip, don't also play it from its event. Check repeated entry semantics before adding cooldowns or a voice ban; that changes behavior rather than diagnosing it.

## Native adapter problems

For `audio`, inspect the map-relative WAV/XNB path, bus, `playing`, volume and
`audible` signal. `sound` and `playAudio` take different targets: the former uses
a loaded native event key; the latter uses an audio component ID. A held
non-looping `playing=true` doesn't restart every frame. Check the player's sound
preferences too; positive local volume cannot override a muted bus.

For an NPC, match `name` to the native settings name, not its filename. Check
`present`, `talking` and `line` before debugging a rule. The native trigger box,
home screen and quote history still decide whether it speaks. A camera showing
the NPC doesn't approach it on behalf of the King. Use `talking_speed` in the
native settings for typing speed.

For a camera, inspect `active`, `selected`, priority and the fallback policy.
An exclusive replay target can suspend the request. A native ending withdraws
MME's shots. Without an available smooth provider, `fallback=cut` frames the whole
screen and `fallback=error` reports a diagnostic. Between disconnected vertical
ranges, Smooth Camera deliberately cuts instead of traversing unrelated rooms.

For victory, inspect `condition`, mode, screen, grounded state and the rectangle.
The native controller must still have its ordinary ending resources. An area
uses a fresh body hitbox; a Boolean supplied by another sensor retains that
sensor's sample timing. See [native interaction examples](../game/interactions.md).

## Large-map or mod-block problems

Verify actual count versus atlas capacity and source ordering. Atlas is column-major from top; strip starts at bottom. Match logical resolution to enlarged collision pixel groups. Check custom factory opt-in at finer resolutions and exact slope alias source colors.

One enabled native side link serves both exits. Add a second link or block an unwanted edge with collision. A valid destination doesn't guarantee clear arrival space. Restart after editing geometry/routes. See [collision](../maps/collision.md) and [screen links](../maps/teleports.md) for detailed format checks.

## Report a useful failure

Keep the map and mod versions, first error, exact event or property, relevant source fragment, and whether the problem requires restart, teleport, pause, restore or completion. Record expected and actual behavior. A package that compiles is useful evidence, but it doesn't prove its route or choreography was tested.
