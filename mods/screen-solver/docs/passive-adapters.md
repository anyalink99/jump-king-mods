# Passive integrations

Audited on 2026-09-06. These rules distinguish observed gameplay, presentation
and already-loaded geometry from changes that require a separate movement model.
They do not grant blanket approval to a Harmony owner.

## Accepted contracts

| Integration | Accepted patches | Why speculative ticks do not execute them |
| --- | --- | --- |
| JK Runtime 1.2.0 / 1.3.0 | ModifierRegistrationObserver Prefix/Postfix/Finalizer on the four public body registration/removal methods | Native construction uses internal registration. The model edits only its own lists and never calls public registration/removal. |
| Jump% | JumpChargeCalc.Run postfix on JumpState.MyRun | Native charge is modeled independently. The postfix only reads charge and writes the overlay's three counters. |
| Mute Jump SFX | Three HandleSounds transpilers and PlayBumpSFXBehaviour.ExecuteBehaviour transpiler | The modeled controller has no audio calls; its body pipeline excludes the bump sound endpoint. |
| More Block Sizes | LoadScreens prefix/postfix, seven-argument LoadBlocksInterval prefix, LevelScreen.DebugDraw prefix | Solve captures loaded rectangles, materials, wind and teleports. It neither reloads the level nor uses native debug drawing. |
| Forced Slopes | SlopeBlock(Rectangle, SlopeType) postfix | Snapshotting copies the actual rectangle, type and collision lines into an uninitialized native slope, avoiding constructor registration and geometry regeneration. |

Mute's exact BlockMuteJumpSfx type is nonblocking and only drives audio. Its
exact BehaviourMuteJumpSfx type returns unchanged movement/gravity inputs and no
extra collisions; its only state change tracks audio-zone occupancy. Neither is
included in branch geometry or behaviours, and speculative ticks do not modify
its live static flag. Unknown subclasses are not accepted.

Forced Slopes' SlopeBottomLeft subclass only corrects the native line array. It
can be reduced to a native SlopeBlock with a deep copy of that array. This also
preserves FixMySlopes changes made to ordinary native SlopeBlock objects after
construction. Recreating a slope from its enum would lose those changes and
append the clone to the mod's live BottomLeftSlopes list.

## Binary evidence

| Workshop item | DLL | SHA-256 |
| --- | --- | --- |
| 3158935297 | JumpKingLastJumpValue.dll | `e7fd77e35380e6552df67890063424f2f0963cdc008e9359e880fc42583c0538` |
| 3545167426 | MuteJumpSfxBlock.dll | `a90e615e3675192f2751484ad7be46ff6d6dfe6ee9ab5baf1e1545899f345026` |
| 3470750355 | MoreBlockSizes.dll | `2318f08edd9d7ea8b039d3271e53c4320ce20280a8897dbf19d4259d009619cf` |
| 3353090188 | ForcedSlopeBlocks.dll | `5af16339e2d0e20e49569727a78006fc58e20e156b722313f52ccb73e7844c2d` |

Foreign rules verify the actual assembly name, hash, patch method, target and
kind. Runtime rules require the loaded RuntimeApi version constant to be 1.2.0 or 1.3.0
and the hook to belong to the actual dependency assembly and observer class.
New builds must be audited before expanding the accepted contract.
Runtime 1.3 was checked on 2026-09-07: the observer source is unchanged; the new
geometry, inventory and presentation APIs do not run inside its registration hooks.

The previous broad exclusion of methods named Register*/Unregister* is removed.
Unknown registration patches fail. InventoryManager.HasItemEnabled is also
audited: AntiBlocks can change Snake Ring or other item effects through it.
Patches to the integrated foreign implementations themselves are not implicitly
trusted either.

## Tests and remaining limits

Tests load the actual DLLs in a separate headless process, install their patches
and Runtime's actual observer hooks, then run another 141,120 exact controller
comparisons with audio markers. Each model tick must leave Jump% counters and
the mute flag unchanged. Capture/cancel is exercised with the hooks installed.

40,848 collision probes compare snapshots against native and corrected slopes,
including non-grid dimensions and the fixed subclass. Cloning must not extend
the foreign static list or share the original line array. More Block Sizes'
load patches are installed during tests, but its texture-loading pipeline is
not executed; the evidence here covers the resulting loaded geometry, not map
asset loading.

Negative tests keep rejecting an unknown patch on RegisterBehaviour, a forged
postfix using Runtime's owner, an inventory-query patch and a patch to Jump%'s
own observer. Existing native, Vertical Wind and route replay tests still run.

This does not cover Expansion Blocks, UpsideDownCore, CustomWindSwitch,
Conveyor Blocks, Ghost of the Immortal Babe Blocks, AntiBlocks, Movement Control
Blocks, Sprinting or SwitchBlocks. An installed mod can register patches even
when its blocks are absent. Establishing that those registrations are inactive
requires inspecting their map state and update behaviour, not just looking for
their colours in the current screen.
