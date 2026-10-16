# Changelog

## 1.4.0 - 2026-10-13

- Open a completed run's final screen from Run history > Actions, including
  offline runs. Back keeps the selected run and action.
- Save native result text separately from signed reports. Older runs use their
  recorded counters without inventing missing attempt or item details.
- Clear the previous result at attempt start and match native completion
  counters before drawing a seal, so a different result can't inherit it.

## 1.3.4 - 2026-10-12

- Include MME's new authoring files in map evidence and match Replays 2.7.2's
  world revision hash. Keep revisions unchanged for legacy maps.

## 1.3.3 - 2026-10-12

- Include alternate collision and sizes in map evidence, and match Replays 2.7.1's
  revision digest including Mapping layout metadata.
- Build with JK Runtime 2.0.2 / API 2.2.

## 1.3.2

- Limit diagnostic journals to three 4 MiB generations, including oversized logs
  from older versions. Keep run checkpoints and recovery copies unchanged.

## 1.3.1

- Use Runtime's shared list viewport and scroll indicator. Wheel movement keeps the selected screen row, and hovering no longer recenters the list on the next update.
- Reserve a separate status row below tall detail rows and above commands.
- Require JK Runtime 1.44 or newer 1.x.
