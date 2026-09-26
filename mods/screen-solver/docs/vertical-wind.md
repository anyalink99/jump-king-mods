# Vertical Wind adapter

Audited on 2026-09-06 against the installed Workshop item 3437222016.

## Contract

- Assembly: `VerticalWindMod`, version `1.0.0.0`.
- MVID: `f538a3ec-e88c-4e66-9f6e-86bd7486956e`.
- SHA-256: `425a617b79fba61ba40d39e70f264c5ceb32e069c79330ed3f451276f07a976d`.
- Harmony owner: `McOuille.VerticalWindMod`.
- Target: `WindVelocityUpdateBehaviour.ExecuteBehaviour`.
- Patch: `VerticalWindMod.WindVelocityUpdateBehaviourExecuteBehaviourPatch.Transpiler`.

The mod patches wind during BeforeLevelLoad regardless of which map is loaded.
Its RGB (53, 113, 220) marker factory records screen indices and returns null, so
there is no block to find in the collision array. The manager clears its set on
level load and unload.

The transpiler checks `VerticalWindManager.Instance.HasWind(currentScreen)` at
the native velocity addition. Marked screens add the original wind force to Y
instead of X. Positive means down; negative means up. Activation, entry rules,
Snow suppression, NoWind collision tests and the waveform are unchanged.

## Isolation and coverage

Solve verifies the patch's target, kind, owner, declaring type, module identity
and file hash. It then copies the manager's private HashSet into a per-screen
mask. A null singleton means an empty set; reading it does not initialize it.
Invalid screen indices reject capture. NativePlayer owns another copy of the
mask, which remains fixed for the session.

The branch uses its own current screen before the X movement phase to select
the wind axis. Camera and teleport transitions therefore affect the next wind
application in the same order as the real game. Search never calls the foreign
manager or transpiler and never adds a background polling hook.

Unknown patches on this target, other patches sharing the same owner, and
patches to Vertical Wind's own methods are not covered. Patches to native wind
generation (such as CustomWindSwitch or SwitchBlocks) need separate adapters.
This adapter does not establish compatibility with a whole Workshop collection.

## Verification

The test build loads the installed, hash-checked DLL into its separate headless
process and applies the actual transpiler. It does not call the mod's lifecycle
or register factories in the running game.

Two 32,000-tick sweeps compare exact native and modeled positions, velocities and
camera indices. One uses an empty marker set; one marks screen 1, leaving its
teleport destinations unmarked. Cases cover both wind signs and clocks, wind
enabled/disabled, Snow, NoWind, four slopes and 480 combined teleports. Event
checks require nonzero horizontal, upward and downward samples.

Additional tests verify immutable marker snapshots, null-singleton reads,
invalid marker rejection, wrong owner/kind/method rejection, unknown patches on
an accepted target, extra patches sharing the accepted owner, patched manager
methods and multi-owner diagnostics. Native controller, body and route tests
remain part of the same build. Manual route execution is still unverified.
