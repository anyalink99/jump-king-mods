# Screen connection examples

These are additions to existing native maps, not complete levels. Copy one example's files to a test map, keep the map's actual collision/layout and create safe openings/arrivals before trying it.

- [Three-screen pool](three-screen/README.md): the smallest random-route example, with fixed seed and explicit returns.
- `random/`: screen 5 selects 10,15,20,25 on crossing and returns through screen 3; needs those screens in a map with at least 30 screens.
- `schemes/`: screen 3 selects a whole route scheme; also needs the named screens in a map with at least 30 screens.

MME 1.3.0 requires Runtime 2.2.1+. One enabled native link serves both edges. Add a second link or close the other edge with collision. [The full routing guide](../../docs/maps/teleports.md) explains modes, held choices, triggers and saved state.
