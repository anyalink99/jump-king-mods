# Eclipse King

A captive sun behind the original King's T-shaped visor. Obsidian-black armor,
ivory accents and a restrained solar glow keep the native silhouette and
instant pose changes. Charge lights the stem before the horizontal opening,
reflects onto nearby helmet edges and produces short rays at full charge.
Takeoff flashes briefly; the glow cools across ascent and descent without
restarting at the apex. Landing briefly dims the visor. Native surface particles,
audio and equipment feedback remain available; this package adds no sound or shake.

Select **Eclipse King** in the native Collections list or Wardrobe+ Collections.
Advanced effects require Wardrobe+ 2.0.6; the native body reskin works without it.
Walking draws the game's selected walk pose directly, preserving its holds,
smear timing, stop/start behavior and recorded replay poses.
Ashen King remains a separate collection.

Each pose uses an exact mask of the native black visor opening, including the
tilted look-up and bounce frames. White and blue bevels are excluded. The build
checks all 13 native poses and writes a coverage contact sheet beside the package.

Build from the repository root:

```powershell
mods/wardrobe-plus/build.ps1
mods/wardrobe-plus/build-example.ps1 -Example eclipse-king
```

The default finish is `ivory`. For an alternative, add `-Finish graphite`,
`platinum`, `white-gold`, `bronze` or `garnet`; `current` keeps the earlier
black-and-gold proposal. Alternatives keep the same package ID and remain in separate staging
directories until a finish is chosen, rather than installed as duplicate skins.

The generator reads the installed game's atlas and shares its XNB reader with
the bundled Ashen King example. Generated source and packages go in a new build
directory. The offline Worldsmith validator checks the native Set; nothing is
uploaded. Original King artwork: Nexile / Jump King. Palette, masks, particle
texture and shader are reproducible from these sources. No game audio is bundled.
