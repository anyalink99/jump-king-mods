# Vessel King

A hollow glass King with turquoise water inside the native silhouette. Water
lags acceleration, oscillates after jumping and landing, and settles with
damping. Its surface follows world direction even when the King turns around.
The interior mask accounts for each pose's area, keeping crouches from simply
stretching the waterline. Walking and recovery follow native pose keys.

Splat drains the vessel into colliding water droplets. They spread along blocks,
fall from edges, settle against walls and fade over roughly 3.4–4.6 seconds.
Native slopes and compatible custom blocking geometry use the game's collision
queries. Spills scale with the remaining
water; the vessel stays empty while flattened and refills over about three seconds
after getting up. Water is cosmetic: it doesn't change blocks, friction, movement
or saves. Audio and camera shake remain native.

Select **Vessel King** in Collections. Advanced water requires Wardrobe+ 2.0.8.
The native fallback atlas shows a glass King with still water. Shared ending
frames keep NPCs, birds and awarded accessories; explicit occlusion masks
supply continuous vessel geometry behind other characters. Choose Original
texture to keep the water shader; a chosen Gold/Glass/Magenta/Cosmic material
replaces its surface while preserving animation and particle settings.

Build from the repository root:

```powershell
mods/wardrobe-plus/build.ps1
mods/wardrobe-plus/build-example.ps1 -Example vessel-king
```

Original King artwork: Nexile / Jump King. The generator derives glass contours
and volume masks from the installed native atlas; it shares the bundled Ashen
King XNB reader. No native audio is redistributed and nothing is uploaded.
