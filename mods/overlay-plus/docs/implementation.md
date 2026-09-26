# Implementation record

## Contract

- Standalone Overlay+ package under mods/overlay-plus; preserve unrelated edits.
- Nonmodal editor only during an active map, paused preview, complete window
  pointer coverage, return-to-visible, undo/redo, profiles and reusable widgets.
- Timer, Area splits, configurable inputs, Unicode text, panels, images, stats,
  jump charge and supported external providers/adapters.
- Strict native Area routes: Main Babe 10, NB+ 9, GoTB 8 in the installed data.
  Preserve authored order (GoTB starts at screen 157, then moves to 102).
- PB, segment bests and history partitioned by world revision, route and clock.
  Practice runs never replace records; stable modified rules have separate PBs.
- Atomic durable files, backup recovery, no mutations of native gameplay save data; timer preferences use native settings.
- Validate installed native hooks, focused tests, GPU rendering and package.
- Install after checks, with game closed and backups; do not publish or commit.

## Native evidence

Installed game inspected with ILSpy. Game1.Draw ends its native SpriteBatch,
sets the backbuffer, calls DrawRenderTarget, then base.Draw. A postfix on
DrawRenderTarget composes Overlay+ directly at window resolution after either
native or Smooth Camera scene presentation, then draws the native foreground UI.
Overlay+ widgets never render into a 480x360 target.

A checked JumpGame.Draw transpiler inserts the UI boundary at the common branch
following world entities/foreground, before the first state UI (m_nexile_logo).
At that boundary an owned transparent 480x360 target receives the native state
and IForeground UI draws, including pause menus, dimming and UIApi+ pages. Native
methods run once in their original order; no UI callbacks or input registration
are replayed. The original scene target is never rebound during the frame, since
its DiscardContents usage could erase the completed world. Game1's existing
EndBatch and SetRenderTarget(null) finish capture. The layer is cleared every
frame and owned by the world preparation scope. Output composition restores
foreground UI even when an Overlay+ draw throws.
GameLoop.DrawIngameOverlayItems owns the native timer; a checked transpiler
redirects its single TextHelper.DrawString call, preserving the actual native
string/font and other Harmony hooks. PauseManager.SetPause/PauseUpdate/Draw
provide editor pause and menu isolation. LocationSettings.locations contains
all 27 base Areas including non-monotonic GoTB ordering and overlapping ranges.

## Enable lifetime

`ModuleEnabled` defaults to true and is exposed through a shared Runtime
`Setting<bool>` in the native main/pause mod menus. It is independent of the
legacy HUD visibility field `Enabled`. Preference commits first drain pending
layout writes, then save atomically at the explicit menu action. A failed commit
restores the previous in-memory switch; a failed activation tears down partial
resources and rolls back the preference.

World and active-session resource scopes can be disposed early by disabling.
Their enclosing Runtime lifetimes still enforce world/attempt teardown. No
resources are constructed while disabled: no fonts/images, map hashing/history
reads, input/window hooks, gameplay subscription or provider patches. Re-enable
at a loaded-map menu action prepares new resources and reads the attempt afresh;
ordinary startup continues to use the named Runtime preparation stages.

## Resource and data ownership

OnWorldReady owns fonts, image textures, the foreground target, SpriteBatch,
dormant hooks and adapter
discovery. BeforeAttempt hashes map files and reads the selected Area archive.
Named preparation stages are `overlay-plus.render-resources`,
`overlay-plus.area-route` and `overlay-plus.run-history`. Activation attaches
input/event observers; it does not patch player mechanics. Expensive record
comparisons refresh on archive or comparison changes, not every render frame.

Edit history uses deep XML snapshots. Run checkpoints copy every mutable route,
run, flag, elapsed time and split on the game thread without XML serialization.
The coalescing queue uses JK Runtime's `BackgroundWorkQueue` to encode detached snapshots and write them with
atomic replacement and retained backups; world
teardown flushes outstanding writes with a 30-second timeout. Runtime owns
worker lifetime and teardown ordering. Native and optional adapter hooks use
separate `OwnedPatches` leases; removing one never unpatches its sibling callbacks.
Ghost selection calls Replays' transactional `SetGhost` before refreshing playback. The game
thread retains graphics ownership throughout.

ControllerManager.Update already polls all native devices before game behavior
and physics. A checked PadInstance.GetPadState transpiler observes its existing
IPad.GetPressedButtons call, returns the exact native result and retains a copy.
Editor input runs after ControllerManager.Update; drawing consumes that tick's
controller snapshot. Keyboard/mouse can refresh at draw time. Layouts without
input widgets skip widget sampling; disconnected devices leave no stale buttons.
External provider callbacks only run for a visible claimed source.

## Native appearance

The editor borrows `gui.FrameSprites` and native Menu/Small/Location/
Story/Gargoyle SpriteFonts. The Unicode atlas uses only the installed game's
LanaPixel font. No browser, external window, OS settings dialog or system font
is part of the editor. A scoped `UiPointer.AcquireWindowCursor()` lease shares
UIApi+'s native Win32 cursor across the entire game client area. No cursor sprite
is drawn by Overlay+. WM_CHAR is observed on the existing game HWND for text.

Implementation, behavioral/GPU fixtures, provider API, documentation, packaging
and focused transactional installation are present. See [validation](validation.md) for the
distinction between automated checks and live playtest coverage.
