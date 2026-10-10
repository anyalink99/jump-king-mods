# Changelog

## 0.2.1 - 2026-10-05

- Preserve empty native asset directories in the complete map's Workshop package,
  fixing the regular Jump King startup crash at `props/textures/old_man/lines`.
- Check the installed game's content loaders after restoring the package from
  files alone, so local empty directories can't hide missing download content.

## 0.2.0 - shared player presentation

- Generate cube and ship textures through Runtime outfit capture and register the active form in the shared visual stack.
- Requires JK Runtime 1.35 or newer 1.x.
