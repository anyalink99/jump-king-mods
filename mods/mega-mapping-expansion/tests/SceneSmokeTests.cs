using System;
using System.IO;
namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void ActualExample(string root)
        {
            SceneFile scene = SceneValidation.Load(root, 169);
            Require(scene != null, "Night Garden scene loads");
            Require(scene.VectorAssets.Length >= 13 && !Array.Exists(scene.Props, delegate(PropData prop) { return prop.Screen == 1; }),
                "Night Garden's first room retains its descriptive art");
            TextureData ocean=Array.Find(scene.Textures,delegate(TextureData texture){return texture.Id=="storm-ocean-baked";});
            Require(ocean!=null && ocean.Pages.Length>0 && ocean.Frames>ocean.Columns*ocean.Rows,
                "the lighthouse's deliberately baked ocean spans multiple atlas pages");
            Require(Array.Exists(scene.Props,delegate(PropData prop){return prop.Texture==ocean.Id && prop.Screen==7 && prop.Layer=="background";}),
                "baked ocean remains behind native rocks and gameplay");
            Require(scene.Nodes.Length >= 38 && Array.Exists(scene.Lights, delegate(LightData light) { return light.Id == "lantern-light"; })
                && Array.Exists(scene.Lights, delegate(LightData light) { return light.Id == "moon-fill"; }),
                "Night Garden retains its lantern and moon light sources");
            Require(scene.Waters.Length == 1 && scene.Fogs.Length == 3,
                "Night Garden perspective reflection and layered fog");
            Require(scene.Waters[0].CompositeReflection && scene.Waters[0].ReflectionOpacity > 0.6f,
                "Night Garden reflects the fully composed frame");
            Require(scene.Waters[0].Interactive && scene.Waters[0].WaveSegments >= 96,
                "Night Garden water has player-driven surface physics");
            PropData farCanopy = Array.Find(scene.Nodes, delegate(PropData node) { return node.Id == "oak-left-crown-1"; });
            PropData cloud = Array.Find(scene.Nodes, delegate(PropData node) { return node.Id == "cloud-1a"; });
            PropData nearFrame = Array.Find(scene.Nodes, delegate(PropData node) { return node.Id == "land-frame-1"; });
            Require(farCanopy != null && cloud != null && nearFrame != null && farCanopy.Z > cloud.Z && nearFrame.Z > farCanopy.Z,
                "clouds cross cyan atmosphere but are alpha-masked by blue tree silhouettes");
            VectorAssetData matte = Array.Find(scene.VectorAssets,
                delegate(VectorAssetData asset) { return asset.Id == "hand-land"; });
            PropData occluder = Array.Find(scene.Nodes, delegate(PropData node) { return node.Id == "land-frame-1"; });
            Require(matte != null && matte.PixelSnap && string.IsNullOrWhiteSpace(matte.ClipPath)
                && occluder != null && occluder.Z > cloud.Z && occluder.Depth == 1f,
                "hand-built layers occlude clouds with object geometry, without scene masks");
            PropData moth = Array.Find(scene.Nodes, delegate(PropData node) { return node.Id == "moth-1"; });
            Require(moth != null && moth.PathInterpolation == "spline" && moth.OrientToPath
                && moth.FlutterHz >= 8f && moth.FlutterBodyStart > 0,
                "moth uses smooth tangent-following flight and independent articulated wing flutter");
            Require(Array.Exists(scene.Nodes, delegate(PropData node) { return node.Asset == "painted-reeds" && node.FlexSlices >= 20; }),
                "high-detail reeds have independently deformable slices");
            Require(Array.Exists(scene.Nodes, delegate(PropData node) { return node.OccludesReflection; }),
                "foreground geometry occludes the compositor reflection");
            Require(scene.Emitters.Length == 3 && scene.Emitters[0].Count >= 16,
                "Night Garden uses reusable depth-aware particle emitters");
            Require(scene.Options.ExpectedScreens >= 4 && Array.Exists(scene.Nodes,
                delegate(PropData node) { return node.Id == "goal-body-2" && node.Screen == scene.Options.ExpectedScreens; }),
                "Babe terrace remains on the separate final screen");
            Require(scene.Options.Timer == "hidden" && !string.IsNullOrWhiteSpace(scene.Options.IntroText),
                "Night Garden presentation options");
            Require(scene.Options.AdvancedLighting && scene.Options.PlayerRimOpacity >= 0.6f,
                "Night Garden enables deferred light-map and player rim passes");
            VectorAssetData tree = Array.Find(scene.VectorAssets, delegate(VectorAssetData asset) { return asset.Id == "oak-left-crown"; });
            Require(tree != null && tree.PixelSnap && tree.Source.EndsWith("oak-left-crown.xml")
                && string.IsNullOrWhiteSpace(tree.ClipPath), "forest stand has independent pixel geometry, not a picture cutout");
            using (var stream = new MemoryStream(VectorGraphics.RenderPngBytes(tree)))
            using (var bitmap = new System.Drawing.Bitmap(stream))
            {
                int occupied = 0, translucent = 0;
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
                {
                    int a = bitmap.GetPixel(x,y).A;
                    if (a != 0) occupied++;
                    if (a != 0 && a != 255) translucent++;
                }
                Require(occupied > 4000 && occupied < 25000 && translucent == 0,
                "pixel foliage has a detailed opaque silhouette: background clouds cannot leak through antialiased seams");
            }
            Require(farCanopy.Wind && !farCanopy.Flex && farCanopy.WindHeight == 159f
                && farCanopy.OriginX == 107f && farCanopy.OriginY == 183f,
                "crown uses a rooted 2D wind mesh rather than horizontal strips");
            VectorAssetData mothAsset = Array.Find(scene.VectorAssets, delegate(VectorAssetData asset) { return asset.Id == "painted-moth"; });
            using (var stream = new MemoryStream(VectorGraphics.RenderPngBytes(mothAsset)))
            using (var bitmap = new System.Drawing.Bitmap(stream))
            {
                int occupied = 0, partial = 0;
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
                { int a = bitmap.GetPixel(x,y).A; if (a > 0) occupied++; if (a > 0 && a < 255) partial++; }
                Require(occupied > 100 && partial == 0 && moth.Opacity == 1f, "moth wings and body are opaque, without a translucent tracing veil");
            }
            string falseRoot = Path.Combine(root, "content-without-scene");
            string resolved = SceneValidation.ResolveLevelRoot(falseRoot, new[] { "JumpKing.exe", "-debug", root });
            Require(Path.GetFullPath(resolved) == Path.GetFullPath(root), "debug command-line level root discovery");
        }

        private static void BackgroundGrounding(string root, string proofDirectory)
        {
            SceneFile scene = SceneValidation.Load(root, 169);
            VectorAssetData frame = Array.Find(scene.VectorAssets, delegate(VectorAssetData a) { return a.Id == "hand-frame"; });
            using (var stream = new MemoryStream(VectorGraphics.RenderPngBytes(frame)))
            using (var bitmap = new System.Drawing.Bitmap(stream))
            {
                // Gates at the authored limb/viewport crossings. Both sides of
                // each limb must cross the top; an in-frame end cap fails these.
                bool crosses = true;
                foreach (int center in new[] {121, 335})
                    for (int dx = -2; dx <= 2; dx++) crosses &= bitmap.GetPixel(center + dx, 0).A == 255;
                Require(crosses, "both framing limbs cross the top edge with their full width, without visible cut ends");
            }
            PropData tree = Array.Find(scene.Nodes, delegate(PropData p) { return p.Id == "oak-left-crown-1"; });
            PropData bank = Array.Find(scene.Nodes, delegate(PropData p) { return p.Id == "understory-1"; });
            VectorAssetData trunkAsset = Array.Find(scene.VectorAssets, delegate(VectorAssetData a) { return a.Id == tree.Asset; });
            VectorAssetData bankAsset = Array.Find(scene.VectorAssets, delegate(VectorAssetData a) { return a.Id == bank.Asset; });
            using (var trunkStream = new MemoryStream(VectorGraphics.RenderPngBytes(trunkAsset)))
            using (var bankStream = new MemoryStream(VectorGraphics.RenderPngBytes(bankAsset)))
            using (var trunk = new System.Drawing.Bitmap(trunkStream))
            using (var soil = new System.Drawing.Bitmap(bankStream))
            {
                using (var material = new System.Drawing.Bitmap(Path.Combine(proofDirectory,"understory-material.png")))
                {
                    bool same = material.Size == soil.Size;
                    for (int py=0;same && py<soil.Height;py++) for(int px=0;same && px<soil.Width;px++)
                        same = material.GetPixel(px,py).ToArgb() == soil.GetPixel(px,py).ToArgb();
                    Require(same,"compound material paths preserve every rendered RGBA pixel");
                }
                int x = (int)tree.OriginX, y = (int)tree.OriginY;
                bool continuous = true, meetsGround = false;
                for (; y < soil.Height; y++)
                {
                    if (soil.GetPixel(x,y).A == 255) { meetsGround = true; break; }
                    continuous &= trunk.GetPixel(x,y).A == 255;
                }
                Require(continuous && meetsGround, "oak stem reaches opaque soil without a sky gap beneath its wind pivot");
                Require(bank.Layer == tree.Layer && bank.Z > tree.Z && bank.Depth == tree.Depth,
                    "root-cover vegetation is in front of the rooted tree and shares its fixed camera plane");
            }
        }

        private static void SolidStoneAndLamp(string root, string proofDirectory)
        {
            SceneFile scene = SceneValidation.Load(root, 169);
            VectorAssetData floor = Array.Find(scene.VectorAssets, delegate(VectorAssetData a) { return a.Id == "rock-floor"; });
            byte[] floorPng = VectorGraphics.RenderPngBytes(floor);
            File.WriteAllBytes(Path.Combine(proofDirectory, "rock-floor-opaque.png"), floorPng);
            foreach (string id in new[] { "painted-platforms", "painted-platform-front" })
            {
                VectorAssetData asset = Array.Find(scene.VectorAssets, delegate(VectorAssetData a) { return a.Id == id; });
                byte[] original = VectorGraphics.RenderPngBytes(asset);
                using (var beforeStream = new MemoryStream(original))
                using (var floorStream = new MemoryStream(floorPng))
                using (var before = new System.Drawing.Bitmap(beforeStream))
                using (var floorBitmap = new System.Drawing.Bitmap(floorStream))
                using (var after = new System.Drawing.Bitmap(before.Width, before.Height))
                {
                    bool upperUnchanged = true, floorSolid = true, floorColor = true, floorConfined = true;
                    int partialAbove = 0;
                    for (int y = 0; y < after.Height; y++) for (int x = 0; x < after.Width; x++)
                    {
                        var a = before.GetPixel(x,y); var f = floorBitmap.GetPixel(x,y);
                        // Binary-alpha source-over: transparent floor pixels cannot
                        // alter the original. Avoid GDI clone premultiplication noise.
                        var b = f.A == 0 ? a : f;
                        after.SetPixel(x,y,b);
                        if (y < 329)
                        {
                            if (a.A > 0 && a.A < 255) partialAbove++;
                            upperUnchanged &= a.ToArgb() == b.ToArgb();
                            floorConfined &= floorBitmap.GetPixel(x,y).A == 0;
                        }
                        if (y >= 330 && id == "painted-platform-front" && floorBitmap.GetPixel(x,y).A == 255)
                            floorColor &= a.R == b.R && a.G == b.G && a.B == b.B;
                        if (y >= 340) floorSolid &= b.A == 255;
                    }
                    Require(asset.AlphaCutoff == 0f && partialAbove > 500 && upperUnchanged && floorConfined,
                        id + " keeps the original platform RGBA above the separate bottom strip");
                    Require(floorSolid && floorColor, id + " has opaque bottom stones with unchanged RGB material detail");
                    after.Save(Path.Combine(proofDirectory, id + "-floor-only.png"));
                }
            }
            PropData lamp = Array.Find(scene.Nodes, delegate(PropData p) { return p.Id == "lamp-1"; });
            PropData glow = Array.Find(scene.Nodes, delegate(PropData p) { return p.Id == "lamp-glow-1"; });
            Require(lamp.Motion == "none" && (lamp.Tracks == null || lamp.Tracks.Length == 0)
                && glow.Motion == "none" && glow.X == lamp.X && glow.Y == lamp.Y
                && glow.Scale == lamp.Scale && glow.OriginX == lamp.OriginX && glow.OriginY == lamp.OriginY,
                "the lamp post and glass stay fixed together without swaying or translating");
            Require(glow.Tracks != null && glow.Tracks.Length > 0
                && Array.TrueForAll(glow.Tracks, delegate(TrackData t) { return t.Property == "brightness"; }),
                "only glass material brightness is animated, never opacity or fixture geometry");
            TrackData pulse = glow.Tracks[0];
            Require(SceneAnimation.TrackValue(pulse, 0f, glow.Id) == 1f
                && SceneAnimation.TrackValue(pulse, .45f, glow.Id) == 1.5f,
                "the former full-bright peak is the new minimum and the new peak emits fifty percent more RGB");
            Require(SceneAnimation.TrackValue(pulse, .45f, glow.Id) - SceneAnimation.TrackValue(pulse, 0f, glow.Id) >= .3f
                && Math.Abs(SceneAnimation.TrackValue(pulse, 0f, glow.Id) - SceneAnimation.TrackValue(pulse, 1f, glow.Id)) < .00001f,
                "original luminous glass material has visible dynamic range and a seamless breathing cycle");
            var tint = SceneHost.MultiplyBrightness(new Microsoft.Xna.Framework.Color(200,100,50,220), .62f);
            Require(tint.R == 124 && tint.G == 62 && tint.B == 31 && tint.A == 220,
                "brightness changes RGB without introducing transparency");
            Require(SceneHost.BrightnessPassGain(1f,0) == 1f && SceneHost.BrightnessPassGain(1f,1) == 0f
                && SceneHost.BrightnessPassGain(1.5f,0) == 1f && SceneHost.BrightnessPassGain(1.5f,1) == .5f,
                "the additive pass carries gain above one instead of clipping it in vertex tint");
            using (var emission = SceneHost.CreateEmissionBlend())
                Require(emission.ColorSourceBlend == Microsoft.Xna.Framework.Graphics.Blend.One
                    && emission.ColorDestinationBlend == Microsoft.Xna.Framework.Graphics.Blend.One
                    && emission.AlphaSourceBlend == Microsoft.Xna.Framework.Graphics.Blend.Zero
                    && emission.AlphaDestinationBlend == Microsoft.Xna.Framework.Graphics.Blend.One,
                    "emission adds premultiplied RGB without changing destination coverage");
            var glowAsset = Array.Find(scene.VectorAssets, delegate(VectorAssetData a) { return a.Id == glow.Asset; });
            var fixtureAsset = Array.Find(scene.VectorAssets, delegate(VectorAssetData a) { return a.Id == lamp.Asset; });
            using (var fixtureStream = new MemoryStream(VectorGraphics.RenderPngBytes(fixtureAsset)))
            using (var fixture = new System.Drawing.Bitmap(fixtureStream))
            {
                Require(fixture.GetPixel(126,80).A == 0,
                    "permanently bright glass is removed from the static fixture underneath the animated material");
            }
            byte[] glassPng = VectorGraphics.RenderPngBytes(glowAsset);
            File.WriteAllBytes(Path.Combine(proofDirectory, "lamp-glass-glow.png"), glassPng);
            using (var stream = new MemoryStream(glassPng))
            using (var bitmap = new System.Drawing.Bitmap(stream))
            {
                int occupied = 0; bool confined = true;
                double luminanceDelta = 0;
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x,y);
                    if (pixel.A == 0) continue;
                    occupied++;
                    // The GPU adds premultiplied radiance, then saturates the
                    // target. Clipping straight RGB before alpha is not equivalent.
                    double coverage = pixel.A / 255.0;
                    double r = pixel.R * coverage, g = pixel.G * coverage, b = pixel.B * coverage;
                    luminanceDelta += .2126 * (Math.Min(255, r * 1.5) - r)
                        + .7152 * (Math.Min(255, g * 1.5) - g)
                        + .0722 * (Math.Min(255, b * 1.5) - b);
                    confined &= x >= 107 && x <= 144 && y >= 61 && y <= 102;
                }
                Require(confined && occupied > 200, "glimmer stays inside the glass rather than washing over the post");
                Require(luminanceDelta / occupied >= 18,
                    "rendered glass changes luminance perceptibly, not merely an inconsequential overlay opacity");
            }
        }

    }
}
