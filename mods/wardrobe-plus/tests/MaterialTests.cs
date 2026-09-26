using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void MaterialReferences(string gameDir)
    {
        string workshop = Path.GetFullPath(Path.Combine(gameDir, "..", "..", "workshop", "content", "1061090"));
        var settings = NativeAppearance.Settings();
        string[] paths = { Path.Combine(gameDir, "Content", "king", settings.skins.First(s => s.item == Items.GiantBoots).texture),
            Path.Combine(workshop, "3162670641", "GoldenBoots"),
            Path.Combine(gameDir, "Content", "king", settings.skins.First(s => s.item == Items.Tunic).texture),
            Path.Combine(workshop, "3162938650", "cap") };
        if (paths.Any(p => !File.Exists(p + ".xnb"))) return;
        var report = new List<string>();
        using (var content = new Microsoft.Xna.Framework.Content.ContentManager(Game1.instance.Services))
        using (var bitmap = new System.Drawing.Bitmap(960, 160))
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.Clear(System.Drawing.Color.FromArgb(24, 28, 35));
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for (int i = 0; i < 6; i++)
            {
                int index = i < 3 ? (i == 1 ? 1 : 0) : (i == 4 ? 3 : 2);
                var texture = content.Load<Texture2D>(paths[index]);
                var sprite = NativeAppearance.Frames(new KingSprites(texture).regular)[0];
                var pixels = FramePixels(sprite);
                if (i == 2 || i == 5)
                    for (int p = 0; p < pixels.Length; p++) pixels[p] = MaterialBaker.Shade(i == 2 ? MaterialKind.Gold : MaterialKind.RedVelvet,
                        pixels[p], p % sprite.source.Width, p / sprite.source.Width, false, (x,y) => Color.Transparent);
                using (var small = new System.Drawing.Bitmap(sprite.source.Width, sprite.source.Height))
                {
                    for (int y = 0; y < small.Height; y++) for (int x = 0; x < small.Width; x++)
                    { var c = pixels[y * small.Width + x]; small.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)); }
                    var opaque = Enumerable.Range(0, pixels.Length).Where(p => pixels[p].A != 0).ToArray();
                    int left = opaque.Min(p => p % small.Width), right = opaque.Max(p => p % small.Width);
                    int top = opaque.Min(p => p / small.Width), bottom = opaque.Max(p => p / small.Width);
                    var bounds = new System.Drawing.Rectangle(left, top, right - left + 1, bottom - top + 1);
                    graphics.DrawImage(small, new System.Drawing.Rectangle(i * 160 + (160 - bounds.Width * 6) / 2, 55, bounds.Width * 6, bounds.Height * 6), bounds, System.Drawing.GraphicsUnit.Pixel);
                }
                graphics.DrawString(new[] { "Original boots", "GoldenBoots ref", "Gold material", "Original tunic", "Red Tunic ref", "Velvet material" }[i],
                    System.Drawing.SystemFonts.MessageBoxFont, System.Drawing.Brushes.White, i * 160 + 4, 8);
                report.Add(paths[index] + (i == 2 || i == 5 ? " [material]" : ""));
                report.AddRange(pixels.Where(c => c.A > 0).GroupBy(c => c).OrderByDescending(g => g.Count()).Take(16)
                    .Select(g => g.Key.R + "," + g.Key.G + "," + g.Key.B + " count=" + g.Count()));
            }
            bitmap.Save(Path.Combine(output, "installed-material-references.png"));
        }
        File.WriteAllLines(Path.Combine(output, "installed-material-palettes.txt"), report);
    }

    private static void MaterialScenePreview(string gameDir, GraphicsDevice device, SpriteBatch batch, RenderTarget2D target)
    {
        using (var content = new Microsoft.Xna.Framework.Content.ContentManager(Game1.instance.Services))
        using (var appearance = PreparedAppearance.Build(new Outfit { Material = MaterialKind.Diamond }, Controller.Catalog, false))
        {
            var background = content.Load<Texture2D>(Path.Combine(gameDir, "Content/screens/background/bg100"));
            var midground = content.Load<Texture2D>(Path.Combine(gameDir, "Content/screens/midground/100"));
            var body = NativeAppearance.Frames(appearance.Base.regular)[0];
            var watch = new Stopwatch();
            for (int frame = 0; frame < 24; frame++)
            {
                watch.Start();
                device.SetRenderTarget(target); device.Clear(Color.Black);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                batch.Draw(background, new Vector2(-frame, 0), Color.White);
                batch.Draw(midground, new Vector2(-frame, 0), Color.White);
                if (body is CrystalSprite) ((CrystalSprite)body).DrawScaled(new Vector2(220, 248), 4, SpriteEffects.None);
                else batch.Draw(body.texture, new Vector2(220,248) - body.source.Size.ToVector2() * body.center * 4,
                    body.source, Color.White, 0, Vector2.Zero, 4, SpriteEffects.None, 0);
                // Native size beside the zoomed sample, on the same moving map.
                body.Draw(new Vector2(350, 245), SpriteEffects.FlipHorizontally);
                JKRuntime.UI.UiTheme.TextLine(CrystalRenderer.ExperimentalEnabled ? "EXPERIMENTAL REFRACTION" : "GLASS / TRANSPARENCY", new Vector2(22, 16), Color.White, true);
                batch.End(); device.SetRenderTarget(null);
                watch.Stop();
                using (var stream = File.Create(Path.Combine(output, "crystal-scene-" + frame.ToString("D2") + ".png"))) target.SaveAsPng(stream, 480, 360);
            }
            File.AppendAllText(Path.Combine(output, "crystal-live-timing.txt"), "\n24 map frames, full native body at 4x plus 1x, excluding PNG readback: " + watch.ElapsedMilliseconds + " ms");
        }
    }
    private static void MaterialTests()
    {
        Check(MaterialBaker.Name(MaterialKind.Diamond) == "Glass" && MaterialBaker.Name(MaterialKind.RedVelvet) == "Magenta",
            "Legacy material IDs display as Glass and Magenta");
        var outfit = new Outfit { Material = MaterialKind.Gold };
        outfit.SetMaterial(4, MaterialKind.Diamond); outfit.SetMaterial(8, MaterialKind.Original);
        Check(outfit.MaterialFor(9) == MaterialKind.Gold && outfit.MaterialFor(4) == MaterialKind.Diamond
            && outfit.MaterialFor(8) == MaterialKind.Original, "Individual materials override the outfit, including original artwork");
        var copy = outfit.Copy(); copy.SetMaterial(4, null);
        Check(copy.MaterialFor(4) == MaterialKind.Gold && outfit.MaterialFor(4) == MaterialKind.Diamond, "Material inheritance and draft copies are independent");
        var store = new Store(Path.Combine(output, "materials"));
        store.Save(new WardrobeData { Current = outfit, Presets = new List<Outfit> { outfit.Copy() }, Undo = outfit.Copy() });
        var loaded = store.Load();
        Check(loaded.Version == 3 && loaded.Current.MaterialFor(4) == MaterialKind.Diamond
            && loaded.Presets[0].MaterialFor(8) == MaterialKind.Original && loaded.Undo.Material == MaterialKind.Gold,
            "Current, saved outfits and undo preserve materials in schema 3");
        Check(store.Import(store.Export(outfit)).MaterialFor(4) == MaterialKind.Diamond, "Recipes preserve materials");
        string legacy = Path.Combine(output, "legacy-materials.xml");
        File.WriteAllText(legacy, "<WardrobeData><Version>1</Version><Current><Id>old</Id><Name>Legacy</Name></Current></WardrobeData>");
        var old = Store.Read<WardrobeData>(legacy); Store.Validate(old);
        Check(old.Current.MaterialFor(4) == MaterialKind.Original, "Legacy settings without material fields retain their original appearance");
        var invalid = new WardrobeData(); invalid.Current.Materials.Add(new ItemMaterial { Kind = (MaterialKind)999 });
        Reject(() => Store.Validate(invalid), "Unknown material kinds are rejected");
        invalid.Current.Materials.Clear(); invalid.Current.Materials.Add(new ItemMaterial { Item = 4 }); invalid.Current.Materials.Add(new ItemMaterial { Item = 4 });
        Reject(() => Store.Validate(invalid), "Duplicate material overrides are rejected");
        foreach (MaterialKind kind in Enum.GetValues(typeof(MaterialKind)))
        {
            bool alpha = true;
            for (int a = 0; a < 256; a++)
            {
                Color input = new Color(a, a / 2, a / 3, a);
                Color changed = MaterialBaker.Shade(kind, input, 6, 12, false, (x, y) => Color.Blue);
                bool glass = kind == MaterialKind.Diamond && !CrystalRenderer.ExperimentalEnabled;
                alpha &= (glass ? changed.A <= a && (a < 3 || changed.A < a) : changed.A == a)
                    && changed.R <= changed.A && changed.G <= changed.A && changed.B <= changed.A;
                if (kind == MaterialKind.Original) alpha &= changed == input;
            }
            Check(alpha, MaterialBaker.Name(kind) + " preserves valid premultiplied alpha at every source opacity");
        }
        var taps = new List<Point>();
        Color red = MaterialBaker.ShadeRefractive(MaterialKind.Diamond, Color.Gray, 5, 4, false, (x, y) => { taps.Add(new Point(x, y)); return Color.Red; });
        Color blue = MaterialBaker.ShadeRefractive(MaterialKind.Diamond, Color.Gray, 5, 4, false, (x, y) => Color.Blue);
        Check(red != blue && taps.Any(p => p != Point.Zero) && taps.Distinct().Count() >= 3,
            "Parked refraction still samples displaced lower layers with separate chromatic taps");
        if (!CrystalRenderer.ExperimentalEnabled)
        {
            taps.Clear();
            Color glass = MaterialBaker.Shade(MaterialKind.Diamond, Color.Gray, 5, 4, false, (x,y) => { taps.Add(new Point(x,y)); return Color.Red; });
            Check(taps.Count == 0 && glass.A > 0 && glass.A < 255, "Glass is translucent without sampling lower layers");
            bool clearInterior = true, brightRim = true;
            for (int y = 0; y < 36; y++) for (int x = 0; x < 28; x++)
            {
                Color interior = MaterialBaker.Shade(MaterialKind.Diamond, Color.Gray, x, y, false, (dx,dy) => Color.Transparent);
                Color rim = MaterialBaker.Shade(MaterialKind.Diamond, Color.Gray, x, y, true, (dx,dy) => Color.Transparent);
                clearInterior &= interior.A > 0 && interior.A <= 51;
                brightRim &= rim.A >= 200 && rim.R > 150 && rim.G > 150 && rim.B > 150;
            }
            Check(clearInterior && brightRim, "Glass interior including facet seams transmits at least 80 percent while preserving a bright silhouette rim");
        }
        Check(MaterialBaker.Over(new Color(128, 0, 0, 128), Color.Blue) == new Color(128, 0, 127, 255), "Lower layers composite with premultiplied coverage");
        Color neutralRed = MaterialBaker.Shade(MaterialKind.Diamond, new Color(255, 0, 0), 5, 4, false, (x, y) => Color.Transparent);
        Color neutralGreen = MaterialBaker.Shade(MaterialKind.Diamond, new Color(0, 76, 0), 5, 4, false, (x, y) => Color.Transparent);
        Check(Math.Abs(neutralRed.R - neutralGreen.R) <= 1 && Math.Abs(neutralRed.G - neutralGreen.G) <= 1 && Math.Abs(neutralRed.B - neutralGreen.B) <= 1,
            "Diamond uses source luminance, not source hue");
        Color gold = MaterialBaker.Shade(MaterialKind.Gold, new Color(89, 73, 76), 5, 4, false, (x,y) => Color.Transparent);
        Check(Math.Abs(gold.R - 223) < 5 && Math.Abs(gold.G - 154) < 5 && Math.Abs(gold.B - 76) < 5, "Gold midtone matches installed GoldenBoots reference");
        Color velvet = MaterialBaker.Shade(MaterialKind.RedVelvet, new Color(45,161,64), 5, 4, false, (x,y) => Color.Transparent);
        Check(Math.Abs(velvet.R - 222) < 5 && Math.Abs(velvet.G - 13) < 5 && Math.Abs(velvet.B - 114) < 5, "Velvet main tone matches installed red Tunic reference");
    }

    private static Color[] FramePixels(Sprite sprite)
    {
        var all = new Color[sprite.texture.Width * sprite.texture.Height]; sprite.texture.GetData(all);
        var frame = new Color[sprite.source.Width * sprite.source.Height];
        FitBaker.CopyFrame(all, sprite.texture.Width, sprite.source, frame, sprite.source.Width, Point.Zero);
        return frame;
    }

    private static void GlassGraphicsTests(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target)
    {
        using (var appearance = PreparedAppearance.Build(new Outfit { Material = MaterialKind.Diamond }, Controller.Catalog, false))
        using (var background = new Texture2D(device, 480, 360))
        {
            var sprite = NativeAppearance.Frames(appearance.Base.regular)[0];
            Check(!appearance.HasRefraction && appearance.Base.m_groups.All(g => NativeAppearance.Frames(g).Values.All(s => !(s is CrystalSprite))),
                "Glass publishes ordinary sprites without refraction resources or equipment refresh");
            var source = FramePixels(sprite);
            Check(source.Any(c => c.A > 0 && c.A < 255), "Glass atlas contains actual transparency");
            int captures = CrystalRenderer.Captures, quads = CrystalRenderer.ShadedQuads;
            bool matches = true;
            for (int frame = 0; frame < 2; frame++)
            {
                var scene = new Color[480 * 360];
                for (int i = 0; i < scene.Length; i++) scene[i] = new Color((i * 17 + frame * 47) % 255, (i / 480 * 29) % 255, (i * 7) % 255);
                background.SetData(scene);
                device.SetRenderTarget(target); device.Clear(Color.Black);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                batch.Draw(background, Vector2.Zero, Color.White);
                var anchor = new Vector2(240,220); sprite.Draw(anchor);
                batch.End(); device.SetRenderTarget(null);
                var actual = new Color[scene.Length]; target.GetData(actual);
                var top = (anchor - sprite.source.Size.ToVector2() * sprite.center).ToPoint();
                for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++)
                {
                    int sx = x - top.X, sy = y - top.Y, index = y * 480 + x;
                    Color expected = scene[index];
                    if (sx >= 0 && sx < sprite.source.Width && sy >= 0 && sy < sprite.source.Height)
                        expected = MaterialBaker.Over(source[sy * sprite.source.Width + sx], expected);
                    matches &= Math.Abs(actual[index].R - expected.R) <= 1 && Math.Abs(actual[index].G - expected.G) <= 1
                        && Math.Abs(actual[index].B - expected.B) <= 1 && actual[index].A == expected.A;
                }
            }
            Check(matches, "Glass blends the changing background at original coordinates without distortion");
            Check(captures == CrystalRenderer.Captures && quads == CrystalRenderer.ShadedQuads, "Glass drawing never captures the scene or invokes the refraction shader");
        }
    }

    private static void LiveCrystalTests(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target)
    {
        Check(CrystalRenderer.Supported, "Installed MonoGame exposes the saved batch-state contract for crystal rendering");
        using (var background = new Texture2D(device, 480, 360))
        using (var mask = new Texture2D(device, 12, 12))
        {
            var scene = new Color[480 * 360];
            for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++)
                scene[y * 480 + x] = new Color((x * 11) % 240, (y * 13) % 240, (x * 7 + y * 3) % 240);
            background.SetData(scene);
            var maskData = new Color[144]; var cells = new List<CrystalPixel>();
            for (int y = 1; y < 11; y++) for (int x = 1; x < 11; x++)
            {
                if (x == 5 && y == 5) continue;
                maskData[y * 12 + x] = Color.Gray;
                cells.Add(CrystalPixel.Create(Color.Gray, x, y, x == 1 || y == 1 || x == 10 || y == 10));
            }
            mask.SetData(maskData);
            using (var sprite = new CrystalSprite(Sprite.CreateSprite(mask), cells.ToArray()))
            {
            int captures = CrystalRenderer.Captures, quads = CrystalRenderer.ShadedQuads;
            Func<int, SpriteEffects, Color[]> render = (offset, flip) => {
                device.SetRenderTarget(target); device.Clear(Color.Black);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                batch.Draw(background, new Vector2(offset, 0), Color.White);
                sprite.Draw(new Vector2(120, 120), flip);
                batch.Draw(Game1.instance.contentManager.Pixel.texture, new Rectangle(300, 250, 3, 3), Color.Magenta);
                batch.End(); device.SetRenderTarget(null);
                var result = new Color[480 * 360]; target.GetData(result); return result;
            };
            Color[] first = render(0, SpriteEffects.None), second = render(3, SpriteEffects.None);
            Check(CrystalRenderer.Captures >= captures + 2, "Crystal uses live GPU scene captures, not only the baked fallback");
            Check(CrystalRenderer.ShadedQuads == quads + 2, "Each crystal sprite shades in one quad regardless of covered pixel count");
            device.SetRenderTarget(target);
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            captures = CrystalRenderer.Captures;
            sprite.Draw(new Vector2(-100,-100));
            sprite.SetColor(Color.Transparent); sprite.Draw(new Vector2(120,120)); sprite.SetColor(Color.White);
            batch.End(); device.SetRenderTarget(null);
            Check(CrystalRenderer.Captures == captures, "Offscreen and invisible crystals skip scene capture");
            bool exterior = true;
            for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++)
                if (!new Rectangle(121,121,10,10).Contains(x,y) && !new Rectangle(300,250,3,3).Contains(x,y)) exterior &= first[y * 480 + x] == scene[y * 480 + x];
            Check(exterior && first[125 * 480 + 125] == scene[125 * 480 + 125], "Discard target restoration preserves every pixel outside the mask, including holes");
            Check(first[250 * 480 + 300] == Color.Magenta, "Drawing continues correctly in the caller's deferred batch after crystal rendering");
            CrystalPixel chosen = cells.First(p => p.X == 6 && p.Y == 4);
            Color actual = first[124 * 480 + 126];
            Color r = scene[(124 + chosen.Dy) * 480 + 126 + chosen.Dx - 1];
            Color g = scene[(124 + chosen.Dy) * 480 + 126 + chosen.Dx];
            Color b = scene[(124 + chosen.Dy) * 480 + 126 + chosen.Dx + 1];
            float t = chosen.Transmission;
            Check(Math.Abs(actual.R - (r.R * t + chosen.Surface.R * (1-t))) <= 3
                && Math.Abs(actual.G - (g.G * t + chosen.Surface.G * (1-t))) <= 3
                && Math.Abs(actual.B - (b.B * t + chosen.Surface.B * (1-t))) <= 3,
                "Crystal GPU pixels contain the displaced scene RGB samples and neutral facet reflection");
            Check(actual != second[124 * 480 + 126], "Moving the background immediately changes the transmitted scene");
            var mirrored = render(0, SpriteEffects.FlipHorizontally);
            Check(mirrored[124 * 480 + 125] != first[124 * 480 + 125], "Mirrored crystal changes facet orientation and sample direction");
            using (var clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true })
            {
                device.SetRenderTarget(target); device.Clear(Color.DarkGreen);
                var viewport = new Viewport(20, 30, 400, 300); var clip = new Rectangle(60,70,20,20);
                device.Viewport = viewport; device.ScissorRectangle = clip;
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clipped, null, Matrix.CreateScale(2));
                sprite.Draw(new Vector2(20,20));
                Check(device.Viewport.Equals(viewport) && device.ScissorRectangle == clip, "Crystal restores offset viewport and scissor with a scaled batch");
                batch.End(); device.SetRenderTarget(null);
                var result = new Color[480 * 360]; target.GetData(result);
                Check(result[0] == Color.DarkGreen && result[90 * 480 + 90] == Color.DarkGreen, "Crystal clipping preserves pixels beyond the caller's viewport and scissor");
            }
            // Measure a representative full native crystal silhouette with no readback in the loop.
            var watch = Stopwatch.StartNew();
            for (int frame = 0; frame < 60; frame++)
            {
                device.SetRenderTarget(target); device.Clear(Color.Black);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                batch.Draw(background, Vector2.Zero, Color.White); sprite.Draw(new Vector2(120,120)); batch.End(); device.SetRenderTarget(null);
            }
            watch.Stop();
            File.WriteAllText(Path.Combine(output, "crystal-live-timing.txt"), "CPU submission, 60 live crystal frames (99 covered pixels): " + watch.ElapsedMilliseconds + " ms");
            }
        }
    }

    private static void MaterialGraphicsTests(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target)
    {
        if (CrystalRenderer.ExperimentalEnabled) LiveCrystalTests(device, batch, target);
        else GlassGraphicsTests(device, batch, target);
        var saved = Controller.Data.Current.Copy();
        var kinds = new[] { MaterialKind.Original, MaterialKind.Gold, MaterialKind.Diamond, MaterialKind.RedVelvet };
        var appearances = new List<PreparedAppearance>();
        var timer = Stopwatch.StartNew();
        try
        {
            foreach (var kind in kinds)
            {
                var outfit = saved.Copy(); outfit.Material = kind;
                appearances.Add(PreparedAppearance.Build(outfit, Controller.Catalog, false));
            }
            timer.Stop();
            File.WriteAllText(Path.Combine(output, "material-timing.txt"), "Four complete appearances: " + timer.ElapsedMilliseconds + " ms\nOwned texture bytes: "
                + appearances.Sum(p => p.Owned.Sum(t => (long)t.Width * t.Height * 4)));
            bool liveCoverage = true;
            foreach (var group in appearances[2].Base.m_groups)
                foreach (var sprite in NativeAppearance.Frames(group).Values)
                    foreach (var flip in new[] { SpriteEffects.None, SpriteEffects.FlipHorizontally })
                    {
                        Color[][] pixels = { new Color[480 * 360], new Color[480 * 360] };
                        try {
                            for (int pass = 0; pass < 2; pass++)
                            {
                                CrystalRenderer.Suspended = pass == 0;
                                device.SetRenderTarget(target); device.Clear(Color.Transparent);
                                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                                sprite.Draw(new Vector2(240,220), flip); batch.End(); device.SetRenderTarget(null); target.GetData(pixels[pass]);
                            }
                        } finally { CrystalRenderer.Suspended = false; }
                        liveCoverage &= pixels[0].Select(c => c.A).SequenceEqual(pixels[1].Select(c => c.A));
                    }
            Check(liveCoverage, "Live crystal preserves coverage for every native movement and ending pose in both directions");
            for (int i = 1; i < appearances.Count; i++)
            {
                bool matches = true; int changedPixels = 0;
                for (int group = 0; group < appearances[0].Base.m_groups.Count; group++)
                    foreach (var pair in NativeAppearance.Frames(appearances[0].Base.m_groups[group]))
                    {
                        var material = NativeAppearance.Frames(appearances[i].Base.m_groups[group])[pair.Key];
                        var before = FramePixels(pair.Value); var after = FramePixels(material);
                        matches &= pair.Value.center == material.center && (kinds[i] == MaterialKind.Diamond && !CrystalRenderer.ExperimentalEnabled
                            ? before.Select(c => c.A != 0).SequenceEqual(after.Select(c => c.A != 0))
                            : before.Select(c => c.A).SequenceEqual(after.Select(c => c.A)));
                        changedPixels += before.Zip(after, (a, b) => a != b ? 1 : 0).Sum();
                    }
                Check(matches && changedPixels > 100, kinds[i] + " changes all native poses without changing silhouettes or anchors");
            }
            device.SetRenderTarget(target); device.Clear(new Color(18, 23, 29));
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            for (int i = 0; i < kinds.Length; i++)
            {
                JKRuntime.UI.UiTheme.TextLine(MaterialBaker.Name(kinds[i]), new Vector2(i * 120 + 5, 3), Color.White, true);
                var layered = NativeAppearance.Layer(appearances[i], appearances[i].MaterialEquipment);
                for (int pose = 0; pose < 3; pose++)
                {
                    foreach (Sprite sprite in NativeAppearance.Layers(NativeAppearance.Frames(layered.regular)[pose]))
                        batch.Draw(sprite.texture, new Vector2(i * 120 + 60, 105 + pose * 108) - sprite.source.Size.ToVector2() * sprite.center * 3,
                            sprite.source, Color.White, 0, Vector2.Zero, 3, pose == 2 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
                }
            }
            batch.End(); device.SetRenderTarget(null);
            using (var stream = File.Create(Path.Combine(output, "materials.png"))) target.SaveAsPng(stream, 480, 360);

            var diamond = saved.Copy(); diamond.SetMaterial((int)Items.Cap, MaterialKind.Diamond);
            using (var normal = PreparedAppearance.Build(diamond, Controller.Catalog, false, false))
            {
                diamond.SetFit(new FitAdjustment { BaseId = normal.Resolved[NativeAppearance.BaseItem].Id,
                    SourceId = normal.Resolved[(int)Items.Cap].Id, Item = (int)Items.Cap, X = 7, Y = 5 });
                using (var shifted = PreparedAppearance.Build(diamond, Controller.Catalog, false, false))
                {
                    bool same = FramePixels(NativeAppearance.Frames(normal.Items[Items.Cap].regular)[0]).SequenceEqual(
                        FramePixels(NativeAppearance.Frames(shifted.Items[Items.Cap].regular)[0]));
                    Check(same != CrystalRenderer.ExperimentalEnabled, "Glass appearance is independent of lower layers; experimental refraction follows fitting");
                    using (var baked = PreparedAppearance.Build(diamond, Controller.Catalog, false))
                    {
                        Func<Sprite, Vector2, SpriteEffects, Color[]> render = (sprite, anchor, effect) => {
                            device.SetRenderTarget(target); device.Clear(Color.Transparent);
                            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                            sprite.Draw(anchor, effect); batch.End(); device.SetRenderTarget(null);
                            var pixels = new Color[480 * 360]; target.GetData(pixels); return pixels;
                        };
                        foreach (SpriteEffects flip in new[] { SpriteEffects.None, SpriteEffects.FlipHorizontally })
                            Check(render(NativeAppearance.Frames(shifted.Items[Items.Cap].regular)[0], new Vector2(flip == SpriteEffects.None ? 247 : 233, 205), flip)
                                .SequenceEqual(render(NativeAppearance.Frames(baked.Items[Items.Cap].regular)[0], new Vector2(240, 200), flip)),
                                "Material preview and fitted publication agree facing " + flip);
                    }
                }
            }
            Controller.Apply(diamond); Controller.Pump();
            var oldActive = Controller.Active;
            NativeAppearance.Manager.GetMethod("DisableSkin", Flags).Invoke(null, new object[] { Items.Shoes });
            Controller.Pump();
            Check(CrystalRenderer.ExperimentalEnabled
                ? !ReferenceEquals(Controller.Active, oldActive) && !Controller.Active.MaterialEquipment.Contains((int)Items.Shoes)
                : ReferenceEquals(Controller.Active, oldActive), "Equipment removal only rebakes experimental refractive materials");
            oldActive = Controller.Active;
            Controller.Pump();
            Check(ReferenceEquals(oldActive, Controller.Active), "Unchanged equipment does not rebake materials every update");
            NativeAppearance.Manager.GetMethod("EnableSkin", Flags).Invoke(null, new object[] { Items.Shoes }); Controller.Pump();
            Check(CrystalRenderer.ExperimentalEnabled ? Controller.Active.MaterialEquipment.Contains((int)Items.Shoes)
                : ReferenceEquals(Controller.Active, oldActive), "Equipment addition avoids unnecessary Glass rebaking");
            Controller.Apply(saved); Controller.Pump();

            var page = new WardrobePage(); page.OnOpen(); page.Update(new JKRuntime.UI.UiInput(), .016f);
            typeof(WardrobePage).GetMethod("Sources", Flags).Invoke(page, new object[] { (int)Items.Cap });
            SelectRow(page, r => RowLabel(r).StartsWith("Material:"));
            SelectRow(page, r => RowLabel(r) == "Glass");
            var draft = (Outfit)typeof(WardrobePage).GetField("draft", Flags).GetValue(page);
            Check(draft.MaterialFor((int)Items.Cap) == MaterialKind.Diamond && Controller.Data.Current.MaterialFor((int)Items.Cap) == MaterialKind.Diamond,
                "Item material picker publishes its material immediately");
            device.SetRenderTarget(target); device.Clear(Color.Black);
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            typeof(JKRuntime.UI.UiPointer).GetMethod("BeginDraw", Flags).Invoke(null, null);
            page.Draw(); typeof(JKRuntime.UI.UiPointer).GetMethod("EndDraw", Flags).Invoke(null, null);
            batch.End(); device.SetRenderTarget(null);
            using (var stream = File.Create(Path.Combine(output, "material-picker.png"))) target.SaveAsPng(stream, 480, 360);
            SelectRow(page, r => RowLabel(r) == "Use outfit material");
            Check(Controller.Data.Current.MaterialFor((int)Items.Cap) == MaterialKind.Original, "Picker restores material inheritance immediately");
            page.OnClose();
        }
        finally { foreach (var appearance in appearances) appearance.Dispose(); }
    }
}
