using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus
{
    // Keep texture-readable fallback atlases alongside live crystal sprites.
    internal static class MaterialBaker
    {
        private sealed class Cell { internal int Group, Frame, X, Y; internal Sprite Sprite; }
        internal static string Name(MaterialKind kind)
        { return kind == MaterialKind.Diamond ? "Glass" : kind == MaterialKind.RedVelvet ? "Magenta" : kind == MaterialKind.Original ? "Original texture" : kind.ToString(); }

        internal static void Apply(PreparedAppearance prepared, Outfit outfit, IList<int> equipped, bool preview)
        {
            var pixels = new Dictionary<Texture2D, Color[]>();
            var parts = new Dictionary<int, KingSprites> { { NativeAppearance.BaseItem, prepared.Base } };
            foreach (var pair in prepared.Items) parts.Add((int)pair.Key, pair.Value);
            var ordered = new[] { NativeAppearance.BaseItem }.Concat(prepared.Settings.skins
                .OrderBy(s => NativeAppearance.LayerOrder(s.layers[0])).Select(s => (int)s.item));
            foreach (int item in ordered)
            {
                if (preview && item != NativeAppearance.BaseItem && !equipped.Contains(item)) continue;
                MaterialKind kind = outfit.MaterialFor(item);
                if (kind == MaterialKind.Original) continue;
                var below = new List<int>();
                if (item != NativeAppearance.BaseItem)
                {
                    below.Add(NativeAppearance.BaseItem);
                    var skin = prepared.Settings.skins.First(s => (int)s.item == item);
                    below.AddRange(equipped.Where(id => id != item && prepared.Settings.skins.Any(s => (int)s.item == id
                        && NativeAppearance.LayerOrder(s.layers[0]) < NativeAppearance.LayerOrder(skin.layers[0])
                        && !s.layers.Intersect(skin.layers).Any())));
                }
                Bake(parts[item], kind, (group, frame, x, y) => {
                    var fit = outfit.Fit(prepared.Resolved[NativeAppearance.BaseItem].Id, prepared.Resolved[item].Id, item, group, frame);
                    Color result = Color.Transparent;
                    foreach (int lower in below)
                    {
                        var sprite = NativeAppearance.Frames(parts[lower].m_groups[group])[frame];
                        var lowerFit = outfit.Fit(prepared.Resolved[NativeAppearance.BaseItem].Id, prepared.Resolved[lower].Id, lower, group, frame);
                        int sx = (int)Math.Round(x + fit.X - lowerFit.X + sprite.source.Width * sprite.center.X);
                        int sy = (int)Math.Round(y + fit.Y - lowerFit.Y + sprite.source.Height * sprite.center.Y);
                        result = Over(Sample(sprite, pixels, sx, sy), result);
                    }
                    return result;
                }, pixels, prepared.Owned);
            }
        }

        private static Color Sample(Sprite sprite, Dictionary<Texture2D, Color[]> cache, int x, int y)
        {
            if (x < 0 || y < 0 || x >= sprite.source.Width || y >= sprite.source.Height) return Color.Transparent;
            Color[] data;
            if (!cache.TryGetValue(sprite.texture, out data))
            { data = new Color[sprite.texture.Width * sprite.texture.Height]; sprite.texture.GetData(data); cache.Add(sprite.texture, data); }
            return data[(sprite.source.Y + y) * sprite.texture.Width + sprite.source.X + x];
        }

        private static void Bake(KingSprites sprites, MaterialKind kind, Func<int, int, float, float, Color> below,
            Dictionary<Texture2D, Color[]> cache, List<Texture2D> owned)
        {
            const int width = 1024;
            var cells = new List<Cell>(); int x = 0, y = 0, row = 0;
            for (int group = 0; group < sprites.m_groups.Count; group++)
                foreach (var pair in NativeAppearance.Frames(sprites.m_groups[group]).OrderBy(p => p.Key))
                {
                    if (pair.Value.source.Width > width) throw new InvalidOperationException("Material frame exceeds texture budget");
                    if (x + pair.Value.source.Width > width) { x = 0; y += row; row = 0; }
                    cells.Add(new Cell { Group = group, Frame = pair.Key, X = x, Y = y, Sprite = pair.Value });
                    x += pair.Value.source.Width; row = Math.Max(row, pair.Value.source.Height);
                }
            if (y + row > 8192) throw new InvalidOperationException("Material atlas exceeds texture budget");
            var data = new Color[width * (y + row)];
            var cosmicData = kind == MaterialKind.Cosmic ? new Color[data.Length] : null;
            foreach (var cell in cells)
            {
                var sprite = cell.Sprite;
                for (int py = 0; py < sprite.source.Height; py++)
                    for (int px = 0; px < sprite.source.Width; px++)
                    {
                        Color input = Sample(sprite, cache, px, py);
                        if (input.A == 0) continue;
                        bool edge = Sample(sprite, cache, px - 1, py).A == 0 || Sample(sprite, cache, px + 1, py).A == 0
                            || Sample(sprite, cache, px, py - 1).A == 0 || Sample(sprite, cache, px, py + 1).A == 0;
                        float localX = px - sprite.source.Width * sprite.center.X, localY = py - sprite.source.Height * sprite.center.Y;
                        data[(cell.Y + py) * width + cell.X + px] = Shade(kind, input, px, py, edge,
                            (dx, dy) => below(cell.Group, cell.Frame, localX + dx, localY + dy));
                        if (cosmicData != null) cosmicData[(cell.Y + py) * width + cell.X + px] = new Color(edge ? 255 : 0,
                            (int)Math.Round((input.R * .2126 + input.G * .7152 + input.B * .0722) * 255 / input.A), 0, input.A);
                    }
            }
            var texture = new Texture2D(cells[0].Sprite.texture.GraphicsDevice, width, y + row, false, SurfaceFormat.Color);
            owned.Add(texture); texture.SetData(data); cache.Add(texture, data);
            Texture2D cosmicMask = null;
            if (cosmicData != null)
            { cosmicMask = new Texture2D(texture.GraphicsDevice, width, y + row); owned.Add(cosmicMask); cosmicMask.SetData(cosmicData); }
            CrystalMaps maps = kind == MaterialKind.Diamond && CrystalRenderer.ExperimentalEnabled ? new CrystalMaps(texture.GraphicsDevice, width, y + row, owned) : null;
            var surfaces = maps == null ? null : new Color[data.Length];
            var facets = maps == null ? null : new Color[data.Length];
            foreach (var cell in cells)
            {
                var result = Sprite.CreateSpriteWithCenter(texture,
                    new Rectangle(cell.X, cell.Y, cell.Sprite.source.Width, cell.Sprite.source.Height), cell.Sprite.center);
                if (maps != null)
                {
                    var crystal = new List<CrystalPixel>();
                    for (int py = 0; py < cell.Sprite.source.Height; py++)
                        for (int px = 0; px < cell.Sprite.source.Width; px++)
                        {
                            var input = Sample(cell.Sprite, cache, px, py);
                            if (input.A == 0) continue;
                            bool edge = Sample(cell.Sprite, cache, px - 1, py).A == 0 || Sample(cell.Sprite, cache, px + 1, py).A == 0
                                || Sample(cell.Sprite, cache, px, py - 1).A == 0 || Sample(cell.Sprite, cache, px, py + 1).A == 0;
                            var pixel = CrystalPixel.Create(input, px, py, edge); crystal.Add(pixel);
                            int index = (cell.Y + py) * width + cell.X + px;
                            CrystalMaps.Pack(pixel, out surfaces[index], out facets[index]);
                        }
                    result = new CrystalSprite(result, crystal.ToArray(), maps, result.source);
                }
                if (cosmicMask != null) result = new CosmicSprite(result, cosmicMask, result.source, Point.Zero);
                NativeAppearance.Frames(sprites.m_groups[cell.Group])[cell.Frame] = result;
            }
            if (maps != null) { maps.Surface.SetData(surfaces); maps.Facets.SetData(facets); }
        }

        internal static Color Over(Color top, Color bottom)
        {
            int inverse = 255 - top.A;
            return new Color(Math.Min(255, top.R + (bottom.R * inverse + 127) / 255),
                Math.Min(255, top.G + (bottom.G * inverse + 127) / 255),
                Math.Min(255, top.B + (bottom.B * inverse + 127) / 255),
                Math.Min(255, top.A + (bottom.A * inverse + 127) / 255));
        }
        internal static Color Shade(MaterialKind kind, Color input, int x, int y, bool edge, Func<int, int, Color> below)
        {
            if (kind == MaterialKind.Cosmic && input.A != 0)
            {
                // Static fallback for consumers that flatten textures without virtual Draw.
                Color color = Color.White;
                if (!edge)
                {
                    double dx = x % 31 - 16, dy = (y % 37 - 19) * 1.8;
                    double radius = Math.Sqrt(dx * dx + dy * dy);
                    double arm = Math.Pow(.5 + .5 * Math.Cos(Math.Atan2(dy,dx) * 2 + radius * .8), 4);
                    float glow = (float)(Math.Exp(-radius * .2) * (.1 + arm * .8));
                    color = new Color(.003f + glow * .4f, .006f + glow * .25f, .014f + glow);
                    if ((x * 137 + y * 79 + x * y * 17) % 109 < 2) color = new Color(186,217,255);
                }
                return new Color((color.R * input.A + 127) / 255, (color.G * input.A + 127) / 255, (color.B * input.A + 127) / 255, input.A);
            }
            if (kind != MaterialKind.Diamond || CrystalRenderer.ExperimentalEnabled || input.A == 0)
                return ShadeRefractive(kind, input, x, y, edge, below);
            // Ordinary premultiplied alpha lets the real lower layers show through
            // at their original coordinates. No scene capture or baked substrate.
            var glass = CrystalPixel.Create(input, x, y, edge);
            // Keep the silhouette rim bright while making every interior facet,
            // including its seams, a faint reflection over the visible background.
            float opacity = (1f - glass.Transmission) * (edge ? 1f : .2f);
            int alpha = (int)Math.Round(input.A * opacity);
            return new Color((glass.Surface.R * alpha + 127) / 255, (glass.Surface.G * alpha + 127) / 255,
                (glass.Surface.B * alpha + 127) / 255, alpha);
        }
        // Retained for the opt-in experimental refraction build and its fixtures.
        internal static Color ShadeRefractive(MaterialKind kind, Color input, int x, int y, bool edge, Func<int, int, Color> below)
        {
            if (kind == MaterialKind.Original || input.A == 0) return input;
            // XNB sprites use premultiplied alpha. Work in straight RGB and restore
            // the original coverage exactly, including antialiased Workshop sprites.
            float light = (input.R * .2126f + input.G * .7152f + input.B * .0722f) / input.A;
            Color color;
            if (kind == MaterialKind.Gold)
            {
                // Measured from installed GoldenBoots against native shoes_iron:
                // the middle values are bright amber/yellow, not brown bronze.
                color = Palette(light, GoldStops, Gold);
            }
            else if (kind == MaterialKind.RedVelvet)
            {
                // Installed red Tunic is saturated raspberry: (222,13,114),
                // shaded with (138,8,71). Preserve folds instead of adding stripes.
                float nap = light < .08f ? 0 : (((x * 13 + y * 7) % 7) - 3) * .0015f;
                color = Palette(light + nap, VelvetStops, Velvet);
            }
            else
            {
                int cx = x / 7, cy = y / 9, fx = x % 7, fy = y % 9;
                bool triangle = fx * 9 > fy * 7;
                int dx = ((cx * 3 + cy) % 5) - 2 + (triangle ? 1 : -1);
                int dy = ((cy * 3 + cx) % 5) - 2;
                Color transmitted = below(dx, dy), red = below(dx + 1, dy), blue = below(dx - 1, dy);
                // Texture-reading consumers use this static fallback. Retain only
                // source luminance when no lower layer exists; live sprites sample the scene.
                Color substrate = new Color(light, light, light);
                Color refraction = new Color(red.R, transmitted.G, blue.B);
                refraction = Color.Lerp(substrate, refraction, transmitted.A / 255f);
                float facet = ((cx + cy * 2) % 4) * .11f + (triangle ? .17f : 0);
                Color crystal = Color.Lerp(new Color(86, 91, 103), new Color(247, 252, 255), MathHelper.Clamp(.25f + light * .5f + facet, 0, 1));
                color = Color.Lerp(refraction, crystal, .68f);
                bool seam = Math.Abs(fx * 9 - fy * 7) < 6;
                if (seam) color = Color.Lerp(color, new Color(231, 251, 255), .48f);
                if (edge) color = Color.Lerp(color, new Color(229, 241, 255), .7f);
                if ((x * 17 + y * 31) % 113 == 0) color = new Color(249, 255, 255);
            }
            return new Color((color.R * input.A + 127) / 255, (color.G * input.A + 127) / 255,
                (color.B * input.A + 127) / 255, (int)input.A);
        }
        private static readonly float[] GoldStops = { 0f, .135f, .30f, .55f, .88f, 1f }, VelvetStops = { 0f, .12f, .3468f, .5072f, .78f, 1f };
        private static readonly Color[] Gold = { new Color(60,20,0), new Color(133,78,41), new Color(223,154,76), new Color(255,255,134), new Color(255,255,217), Color.White };
        private static readonly Color[] Velvet = { Color.Black, new Color(46,1,24), new Color(138,8,71), new Color(222,13,114), new Color(244,46,134), new Color(255,129,181) };
        private static Color Palette(float light, float[] stops, Color[] colors)
        {
            for (int i = 1; i < stops.Length; i++)
                if (light <= stops[i]) return Color.Lerp(colors[i - 1], colors[i], MathHelper.Clamp((light - stops[i - 1]) / (stops[i] - stops[i - 1]), 0, 1));
            return colors[colors.Length - 1];
        }
    }
}
