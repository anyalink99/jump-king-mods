# Build and test Wardrobe+

Run `scripts/check-mods.ps1 -Mod wardrobe-plus` from the repository root.
Packages built with the current SDK need Runtime 2.0+; the published 2.2.1
release retains its Runtime 1.44 minimum.
The release shell is `build/wardrobe-plus/UPLOAD_TO_WORKSHOP/WardrobePlus.dll`.
Builds don't install or upload anything. No game assemblies, Runtime DLL or
separate implementation DLL should be included in the Workshop package.

Normal builds compile and embed `assets/Cosmic.fx` and the two Cosmic PNG layers.
To exercise the parked refraction code,
run `mods/wardrobe-plus/build.ps1 -Graphics -ExperimentalRefraction`. This defines
`WARDROBE_REFRACTION`, restores the old behavior for the Glass slot, and runs its
retained GPU fixtures. This experimental build replaces the local build output;
rebuild without the switch before installing or publishing a normal package.
There is no in-game refraction setting.

`build-effect.ps1` uses the compatible MonoGame 3.7.1 effect compiler. Only
experimental builds additionally compile and embed `assets/Crystal.fx`.
On first use it downloads the pinned official SDK archive, verifies its SHA-256,
and extracts it into the internal build cache with 7-Zip; it doesn't install
the SDK. Alternatively, pass `-EffectCompiler` to `build.ps1` with an existing
3.7.1 `2MGFX.exe`. The SDK and compiler are build dependencies only.

For native GPU/preview tests, use
`scripts/check-mods.ps1 -Mod wardrobe-plus -Integration`
or `mods/wardrobe-plus/build.ps1 -Graphics`. Graphics tests use the
installed game assemblies and atlases in an isolated fixture; they never launch
the game or write its saves. Images are retained under
`build/wardrobe-plus/_INTERNAL/tests/<run-id>`.

Run `mods/wardrobe-plus/verify-compatibility.ps1` to freshly build Ball King,
Replays, Mega Mapping Expansion and Smooth Camera, then exercise their real
appearance consumers and camera hook against fitted sprites. Direct `build.ps1 -Compatibility` uses
existing consumer builds and is intended for iteration after that initial run.
Every build also checks native shell discovery before Runtime loading and
idempotent module registration after Runtime loading.

## Material rendering

Gold follows the bright amber, lemon and cream ramp of GoldenBoots. Magenta
uses the saturated raspberry highlights and deep shadows of the installed red
Tunic reference, retaining the source folds with subtle fabric nap.
Glass discards the source hue and keeps its luminance for the shape. Its
nearly transparent interior facets and bright edges blend normally over the map and
lower character layers, with no displacement, dispersion or scene capture.
It uses ordinary sprites in the game, preview and compatible appearance consumers.

Cosmic turns the silhouette into a window onto a black star field, with a crisp
white contour, colored spiral galaxies, drifting star layers and subtle twinkling.
The source hue is discarded and only a faint trace of luminance remains. The
field is anchored to map coordinates, not the character or its facing direction.
Adjacent screen viewports share continuous coordinates and one animation time
per game draw, including camera translation. The preview uses its own screen space.
One masked shader quad draws each visible part; there is no framebuffer capture,
readback, per-pixel draw loop or per-frame texture generation.
Two shared 512x512 layers use 2 MiB of GPU texture memory, plus each appearance's
mask atlas. Native and replay sprite draws animate; texture-flattening consumers
such as Ball King and Mega Mapping use a static cosmic fallback. Custom effect,
sorted, depth/stencil or rotated-transform passes also keep that fallback.

An authored native-atlas mask protects NPCs, birds and props, including characters
sharing a frame with the King. Its separate-object and body-occlusion classes keep
foreign pixels unchanged while reconstructing body contours behind overlaps.
Each item slot has its own ending masks, so boots, crowns and capes outside the
body silhouette receive the selected material. Reward crowns and the owl cape
painted into the base atlas use their own item material throughout delivery,
handoff and wearing, including before the native reward is equipped. The NBP
Babe's replacement crown remains unchanged. Masks apply after collection and
individual source selection, before fitting; switching collections doesn't
change mask ownership. Crowns and awarded capes remain separate from body reskins. Masks describe the
native anatomy; extensively redrawn third-party ending poses may need new masks.
