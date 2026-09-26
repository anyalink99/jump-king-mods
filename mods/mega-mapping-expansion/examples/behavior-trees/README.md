# Behavior-tree overlay

Requires MME 0.6+ and JK Runtime 1.30+. This folder is a self-contained **scene
overlay**, not a playable map, replacement collision atlas or finished cinematic.

Merge `props/mega-mapping-expansion/scene.xml` into an existing map. Enter the
screen-one rectangle x=160..320, y=220..320: three short warm tints run. Leaving
cancels them. Re-entering starts a fresh execution. Move the region to suit your
map; it does not add collision.

The optional `ending/custom_main_ending.xml` replaces the Main Babe controller.
It resets a typed scene flag, emits a cue, waits for the scene's three-pulse
sequence to acknowledge it and ends normally. Do not overwrite an authored
ending without merging its choreography. This is a control-flow demonstration,
not coordination of all stock actor animations or rewards.

Validate/compile the whole folder with SceneCacheCompiler, not the ending file
alone: the handshake requires this scene and its `ending-ready` flag. The mod
build compiles this pair and exports it in the authoring kit.

In a debug run, the Mapping inspector lists tree states and all start/stop events.
While paused, emit `ending:cue` and single-step to inspect the acknowledgement.
Native actor dialogue reactions use the same mechanism with `dialogueend:ID`.
See the kit's BEHAVIOR_TREES.md for the complete vocabulary and lifecycle.
