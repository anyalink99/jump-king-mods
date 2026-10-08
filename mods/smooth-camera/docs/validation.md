# Smooth Camera validation

Run from the repository root with Jump King installed:

```powershell
.\scripts\check-mods.ps1 -Mod smooth-camera
.\scripts\check-mods.ps1 -Mod smooth-camera,mega-mapping-expansion,subframe-charge -Integration
```

Fast checks cover camera motion and installed-game contracts. Integration adds
isolated graphics fixtures. Selecting Mapping and Subframe Charge also supplies
their fresh implementations for compositor and scheduler checks.

For validation against an already installed Runtime, the direct build also
accepts `-RuntimeAssembly <path-to-JKRuntime.dll>`. It compiles the camera and its
SDK package builder against that same assembly, then verifies package discovery
against it. This keeps the declared API requirement aligned with the tested SDK.

To check an installed Expansion Blocks build, pass its DLL to the direct build:

```powershell
.\mods\smooth-camera\build.ps1 -Graphics -ExpansionAssembly '<path-to-JumpKing-Expansion-Blocks.dll>'
```

This uses its real MultiWarp blocks and teleport behavior in isolated fixtures.
Expansion Blocks isn't bundled or required for ordinary camera builds. The
2026-10-07 compatibility check used assembly version 1.0.0.0, SHA-256
`D2CA8D04D7B444FD7F03B92277F2596F75162F5201A3E3EA1E9ACBCA8F47FEA1`.

## Coverage

- Legacy settings, isolated drafts and bindings, atomic persistence, protected
  invalid values, per-map profiles and author recommendation precedence.
- Forced screens with Enable off, persistent disabling state, zone priorities,
  half-open rectangle edges, malformed XML and bounded screen/portal coverage.
- Configurable focus, Window/Direct/Screen modes, directional response,
  look-ahead debounce, optional idle recenter and custom gesture thresholds.
- Forced-map dormant preparation and restart cleanup, real native rendering
  while disabled, zone exit and high-refresh release.
- Settings and numeric editor captures with native fonts; Cancel/Keep semantics.
- Camera convergence, apex and landing framing, sustained falls, map bounds,
  teleports, pauses and reset.
- Tap/hold look controls, Focus, simultaneous inputs, release thresholds and
  restoring the prior view.
- XML manual-control restrictions, screen/zone precedence, blocked latches,
  unfinished gestures and release before rearming after a restriction changes.
- Native screen links, open-edge filtering, negative world coordinates,
  portal rebasing and adjacent-screen NPC rendering.
- Debug dragging on both axes, outside clicks, side crossings, stationary cursor
  stability, Focus fallback and foreground loss with release before rearming.
- Ten-screen debug drags upward and downward, immediate physical screen updates
  and landing after release through native collision resolution.
- With Mapping selected, actual MME side-link application and native crossings to
  screens 257 and 289, with camera rebasing and retained one-way departure views.
- Optional installed MultiWarp checks: height-dependent destinations, offset
  encoding, conflicting bands, native-link precedence, real side teleports,
  continued debug dragging and rendered scenery continuity. MultiWarp's 479.8px
  body shift retains native sprite rounding; the camera doesn't alter physics.
- World preparation, dormant intro hooks, repeated same-world restarts,
  disable/re-enable, cancelled starts and world exit.
- Native update counts and deltas, partial-time conservation, catch-up and
  scheduler ownership transfers with Subframe Charge in both patch orders.
- Actual Harmony hooks for camera queries and world transforms, nested draw
  scopes, thread isolation, exceptions and cleanup.
- Native screen layers, weather, scrolling backgrounds, foreground occlusion,
  fixed UI and fractional movement at output resolution.
- Mapping lighting and reflections across screens, one final tint/mirror pass,
  disabled effects and resumption with retained resources.
- Render-target, viewport and sprite-batch restoration, resource disposal and
  native discovery of the packaged DLL.

Captures remain under `build/smooth-camera/_INTERNAL/graphics/`. Motion and draw
submission probes measure isolated fixtures, not live FPS or physical latency.

## In-game checks

Use the [README checklist](../README.md) with the intended map and mod set. Inspect
screen seams, portal transitions, fast falls, look controls, focus changes and
pause/restart. Repeat with Mapping and Subframe Charge enabled and disabled.

Open every Settings page with keyboard, controller and mouse. Test drag cancel,
binding capture, Apply versus Cancel, presets and a personal map profile. Compare
the animated illustration to actual jumps and falls. Enable diagnostics while
crossing authored zones. Test both tags, a forced range with Enable off, a native
neighbor, a one-way portal between forced ranges and returning from a strict map.
Enter areas with `allow-focus=false` and `allow-look=false` while tapping,
holding and latching each view. Check that denied views clear, enabled views
still work and a held button needs release before it works after leaving.
Repeat with a personal profile and Map recommendations off.

The edge filter is a visual heuristic, not a proof of route clearance. Automated
fixtures don't establish compatibility with arbitrary third-party draw hooks
or replace a complete playthrough. Builds and tests don't install the mod.
