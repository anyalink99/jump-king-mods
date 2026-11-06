# Ashen King

A reskin of the original Jump King model. The generator keeps its exact
silhouette, helmet, body proportions and native poses, recolors armor to ash and
charcoal, and animates ember highlights. During charge the original helmet slit
heats from dark through red to incandescent gold, driven by actual charge.
The visor shader keeps all silhouette alpha and affects only the slit. Original character artwork: Nexile.
Native ending atlases keep their original geometry and alpha as well.
Ascent switches directly to descent with the native pose, without an apex hold.

Run `mods/wardrobe-plus/build-example.ps1` from the repository root with Python
and Pillow installed. It uses the installed game's `Content/king/base.xnb`,
exports native layout, creates editable sources, builds a native Set package (a body collection)
and invokes the installed Worldsmith's offline validator. No upload is performed.

The generated `source/wardrobe/skin.json` is the complete declarative example.
The `embers` profile can be selected independently on any outfit. It demonstrates
takeoff/landing embers, an air trail and charge audio. Takeoff, landing and splat
keep native surface and heavy-boot audio. It also demonstrates
snow preservation and water steam. Landing uses the standing pose, with instant
pixel-art pose changes and no added screen shake. Audio is synthesized from source;
no native audio is redistributed. The package requires Wardrobe+ 2.0 for advanced
features; the collection remains a native body reskin without it.
Choose it from Collections in Wardrobe+.

`build_assets.py` needs `--native GAME_BASE_XNB --layout LAYOUT_JSON --output NEW_SOURCE`.
Existing output directories are rejected to keep prior artwork and packages.
