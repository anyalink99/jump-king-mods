# Changelog

## 1.0.4 - 2026-11-07

- Remove the V-sync control from Graphics. Leave the game's display preference unchanged.

## 1.0.3 - 2026-11-07

- Always remember the free window's normal position alongside its size, without
  another setting. Restore it on startup and re-enable, clamped to an available
  monitor when the desktop layout changes.

## 1.0.2 - 2026-11-07

- Remove Linear sampling. Graphics offers Sharp and Nearest.

## 1.0.1 - 2026-11-07

- Fix Free scaling silently staying off after startup. Initialize against the
  existing graphics device instead of waiting for a hook installed inside the
  already-running native content initialization.
- Restore the saved free window during startup and keep all settings in Graphics.
  Remove the duplicate main/pause mod settings.

## 1.0.0 - 2026-11-07

- Add Free scaling to the native Graphics menu, with Sharp, Nearest and Linear filters.
- Disable native window-mode and integer-scale controls while free sizing owns the window.
- Save custom client dimensions separately and restore the previous native window on disable.
- Use JK Runtime's final-sampling service, shared with Smooth Camera.
- Add shared Smooth Rendering with high-rate movement/menu polling and native
  Jump timing, plus the game's existing V-sync preference.
