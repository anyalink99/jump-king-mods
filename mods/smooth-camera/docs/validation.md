# Smooth Camera validation

Run from the repository root with Jump King installed:

```powershell
.\scripts\check-mods.ps1 -Mod smooth-camera
.\scripts\check-mods.ps1 -Mod smooth-camera,mega-mapping-expansion,subframe-charge -Integration
```

Fast checks cover camera motion and installed-game contracts. Integration adds
isolated graphics fixtures. Selecting Mapping and Subframe Charge also supplies
their fresh implementations for compositor and scheduler checks.

## Coverage

- Camera convergence, apex and landing framing, sustained falls, map bounds,
  teleports, pauses and reset.
- Tap/hold look controls, Focus, simultaneous inputs, release thresholds and
  restoring the prior view.
- Native screen links, open-edge filtering, negative world coordinates,
  portal rebasing and adjacent-screen NPC rendering.
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

The edge filter is a visual heuristic, not a proof of route clearance. Automated
fixtures do not establish compatibility with arbitrary third-party draw hooks
or replace a complete playthrough. Builds and tests do not install the mod.
