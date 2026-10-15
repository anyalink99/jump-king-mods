# Screen transitions

Copy one example's files into an existing map with at least 30 screens. Keep
your map's own layout settings and collision. These examples configure routes;
paint openings and safe arrival areas in the map's collision image.

- `random/`: screen 5 selects one of screens 10, 15, 20 and 25 on each crossing,
  excluding its previous destination. Each trial returns to screen 3.
- `schemes/`: entering screen 3 chooses a complete route scheme. Screen 5 leads
  to a starting room, then a trial; both trial exits return to screen 3.

A screen with only one enabled link uses it from both edges. Close unwanted
exits with collision. Add a second link if the two edges need separate routes.

Players need MME 0.11.0+ and JK Runtime 2.1.0+. Read the
[screen teleports guide](../../docs/maps/teleports.md) for modes, seeds, triggers,
save behavior and validation limits.
