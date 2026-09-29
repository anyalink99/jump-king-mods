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

Open every Settings page with keyboard, controller and mouse. Test drag cancel,
binding capture, Apply versus Cancel, presets and a personal map profile. Compare
the animated illustration to actual jumps and falls. Enable diagnostics while
crossing authored zones. Test both tags, a forced range with Enable off, a native
neighbor, a one-way portal between forced ranges and returning from a strict map.

The edge filter is a visual heuristic, not a proof of route clearance. Automated
fixtures don't establish compatibility with arbitrary third-party draw hooks
or replace a complete playthrough. Builds and tests don't install the mod.
