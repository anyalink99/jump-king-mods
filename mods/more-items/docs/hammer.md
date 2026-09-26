# Hammer physics and checks

Hammer is equipment in [More Items](../README.md#items-and-controls). Equip it to
replace walking and charged jumps with mouse-driven climbing. Controls, purchase,
force settings and migration from the standalone package are in the README.

## Movement

The mouse target controls angle and extension around the grip. The pole slides
through it, so the head can push below the body and the tail can contact terrain.
Head and shaft use swept collision; resting contact supplies grip, larger loads
slide with friction, and pulling away releases the surface. Snow grips more
strongly than dry terrain; ice is slippery.

Native body motion, landing callbacks, splat recovery and material sounds remain
part of the controller. Mouse capture releases on pause or focus loss; resuming
discards the first delta. Runtime snapshots include Hammer state, while external
teleports clear stale anchors. A fresh level recreates the hammer near the king.

The controller uses Jump King's native collision queries. Custom geometry needs
to support the small-box queries those contacts use. No new collision colors are
required. Avoid combining Hammer with competing movement replacements; Runtime's
mechanic inventory does not automatically lock arbitrary foreign controllers.

This is an original kinematic controller inspired by Getting Over It. It does
not reproduce that game's joint solver, and handling parity is not established.

## Build and checks

```powershell
.\scripts\check-mods.ps1 -Mod more-items
.\scripts\check-mods.ps1 -Mod more-items -Integration
```

The package is `build/more-items/UPLOAD_TO_WORKSHOP/MoreItems.dll`. There is no
separate HammerKing.dll to install. The `hammer-king` check name forwards to
More Items for compatibility with older commands.

Focused checks cover free-air motion, contact/release, head and shaft sweeps,
friction, idle support, small adjustments, sustained launches, input resumption,
native launch/landing, splat recovery, poses and controller restoration. Material
fixtures compare snow, ice and dry terrain. Audio checks cover impact selection,
contact gating and embedded PCM.

Integration adds native XAudio decoding, SFX volume, independent voices and
cleanup, plus MonoGame sprite rendering and real inventory equip/unequip. The
production wrapper is checked for outfit visibility over the hammer. Output
stays under `build/more-items/_INTERNAL/`.

In-game checks should cover physical mouse feel, reach limits, contact release,
small planted adjustments, outfit overlap, pause/resume and equipment changes.
Fixture measurements alone do not establish how the controller feels to play.
