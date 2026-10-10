# Changelog

## 1.3.2

- Limit diagnostic journals to three 4 MiB generations, including oversized logs
  from older versions. Keep run checkpoints and recovery copies unchanged.

## 1.3.1

- Use Runtime's shared list viewport and scroll indicator. Wheel movement keeps the selected screen row, and hovering no longer recenters the list on the next update.
- Reserve a separate status row below tall detail rows and above commands.
- Require JK Runtime 1.44 or newer 1.x.
