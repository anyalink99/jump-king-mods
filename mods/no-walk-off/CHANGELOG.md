# No Walk Off changelog

## 1.0.1 - 2026-11-06

- Keep map-only installations disabled outside their controlled maps while preserving
  global preferences after outside use or an explicit enable. History survives
  restarts; existing enabled settings are preserved on upgrade.
- Require JK Runtime 2.7.0 / API 2.11.


## 1.0.0 — 2026-10-28

- Rebrand the existing Mega Gameplay Expansion Workshop item as No Walk Off.
- Keep edge protection as its only mechanic, enabled by default on fresh installs.
- Move Warp Jump and Air Dash to Powers without importing old settings.
- Preserve the existing platform, zone and screen colours. Map rules use
  `no-walk-off`; global run permission uses `AllowNoWalkOff`.
- Keep Runtime motion observation, Blocks2 compatibility and release/repress behavior.
