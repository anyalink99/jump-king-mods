# Workshop cover sources

In the private authoring workspace, run `../build-preview.ps1` to extract the
native bounce poses and rebuild the cover. The public checkout includes the
finished assets; extraction and composition tools stay private.
It needs the installed game, .NET Framework's C# compiler, Python and Pillow.
The exporter uses an offscreen graphics device; it doesn't start gameplay or load
mods and saves.

Both characters use `Regular.SpriteKey.jump_bounce` from `Content/king/*.xnb`.
The left king wears Cap, Tunic and YellowShoes. The right wears CrownOwl, CapeOwl
and GiantBoots. Composition preserves native pixels, mirrors the right king and
enlarges both by exactly 3×. Item order matches the game's cape/boots/shirt/hat layers.

`workshop-preview-art-native.png` is the 256×256 illustration with a flat background
and an empty upper 30%. `workshop-preview-title.png` is the separate title layer,
using the game's Litter Lover font. `workshop-preview-master.png` is the finished
1024×1024 enlargement; `../workshop-preview.png` is the indexed 256×256 delivery.
The composer verifies native colors, uncovered character pixels, the header area
and the 34,000-byte file budget. No generated illustration is used in this cover.
