# Narrative scene

Copy `props/mega-mapping-expansion` into an existing playable native map. This
sample has no collision data or NPC assets and is not a standalone playable map.
It adds one intro page, a screen-one HUD counter, a trigger rectangle and a result
page after native statistics. Walk through X=100..180/Y=200..300 to increment the
counter. It resets each run; snapshots restore it. Finish the map normally to see
the result page. No custom ending trigger is installed.

Read `NARRATIVE.md` in the parent authoring kit for all fields, native NPC binding,
localization and save-scope variants. The mod build compiles this fixture.
