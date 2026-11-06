# Place Quest enemies in a map

Requires Metroidvania 0.4.0 and JK Runtime 2.4.0 or newer. Enable Mode before
starting the attempt. The current pack contains `fslave-crawler`; version 0.3.0
also supports that type. Additional types must exist in the installed content pack.
Multiplayer combat isn't implemented in this version.

Create `props/metroidvania/encounters.xml` under the map's content root:

```xml
<Metroidvania Version="2">
  <Enemy Id="courtyard-crawler" Type="fslave-crawler"
         Screen="1" X="220" Y="308" Patrol="48" />
</Metroidvania>
```

Coordinates are the top-left of the definition's movement body in a 480 by 360
screen. The crawler body is 32 by 22 pixels. Screens start at 1. In this example
the supporting floor is at Y=330.
Leave enough flat space for the body and patrol. The imported animated hurt and
attack boxes can extend outside that movement body.

| Attribute | Accepted value |
| --- | --- |
| `Id` | Unique nonblank ID, at most 64 characters; keep it when moving a spawn. |
| `Type` | A stable enemy ID in the installed catalogue, currently `fslave-crawler`. Unknown types are rejected. |
| `Screen` | Existing screen, starting at 1. |
| `X`, `Y` | Body must fit the screen: X up to 480 minus its width, Y up to 360 minus its height. For the crawler: X 0–448, Y 0–338. |
| `Patrol` | 16 to 180 pixels left/right from X, clipped to the screen's sides. |

Names and attributes are case-sensitive. Use version 2 and typed `<Enemy>` spawns.
The old `<Crawler>` prototype and format 1 have been removed. IDs must be unique,
with at most 16 spawns in total. Unknown elements/attributes, malformed
data, unsupported placement and duplicate IDs reject the attempt's combat setup.

The crawler walks on supported flat terrain. Its lunge follows the imported
forward movement but stops before a wall or unsupported edge. It doesn't jump
between platforms, navigate slopes or drop onto a lower screen. Enemies far from
the king sleep. Nothing spawns automatically if the file is absent.

Authored enemies respawn on each new attempt. Victories currently use the same
repeatable progression rewards as training. There are no unique boss receipts,
trigger groups, waves, respawn options or door callbacks yet. Those are planned
in the [development roadmap](roadmap.md).

Test a missed lunge, a successful hit, a guard/parry, interruption and defeat at
each authored placement. Try the nearest wall and edge as well as the intended
approach. Valid packaging doesn't prove that the encounter or route is playable.
