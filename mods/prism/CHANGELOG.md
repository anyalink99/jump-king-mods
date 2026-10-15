# Changelog

## 0.2.0 - 2026-10-05

- Add a UIApi+ theme browser and per-theme settings with immediate updates.
  Preserve existing preferences when migrating to individual theme profiles.
- Enable Disable props by default. Hide native props, NPCs, Hidden Walls and
  MME scene drawing while leaving entity updates and gameplay events intact.
- Show actual wind direction and strength through moving stars and streaks.
  Clip wind streaks out of no-wind zones.
- Restart the music and visual timeline on every new attempt. Add live controls
  for music volume, platform glow and wind visibility.
- Draw water separately from solid geometry so submerged slopes keep their
  silhouettes. Correct southwest slope artwork without modifying collision.

## 0.1.0 - 2026-10-05

- Add the Event Horizon visual theme, collision-derived glass platforms,
  material accents, nebula, black hole, changing palettes and clipped reflections.
- Drive pulses and trails from a locally imported osu! chart and audio sample clock.
- Add immediate enable, reflections, gentle motion and original-art controls.
