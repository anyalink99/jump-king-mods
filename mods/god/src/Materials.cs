using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace God
{
    internal sealed class Material
    {
        internal int Id;
        internal Rectangle Bounds;
        internal Region Region;
        internal BlockTemplate Template;
        internal int Kind = 1;
        internal bool Untextured;
        internal bool Composite, FlipX, FlipY;
        internal Material Sample;
        internal Material FoundationSource;
        internal int Foundation;
        internal int SlopeCell;
        internal bool TiledSlope;
        private HashSet<int> sourcePixels;
        internal bool Wall { get { return Region.Wall; } }
        private readonly Dictionary<int, List<int>> samples = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, List<int>> matches = new Dictionary<int, List<int>>();
        private int[] original;
        private bool samplesReady;
        private static readonly int[] dx = { 0, 1, 0, -1 }, dy = { -1, 0, 1, 0 };
        private static readonly int[] cx = { -1, 1, 1, -1 }, cy = { -1, -1, 1, 1 };
        internal Material(int id, Region region, int[] cells)
        {
            Id = id; Bounds = region.Bounds; Region = region; RebuildSamples(cells);
        }
        internal void RebuildSamples(int[] cells)
        {
            original = cells; samples.Clear(); matches.Clear(); sourcePixels = null; samplesReady = false;
        }
        private void PrepareSamples()
        {
            if (samplesReady) return;
            var cells = original; sourcePixels = new HashSet<int>();
            foreach (int i in Region.Pixels)
            {
                if (cells[i] == 0 || (!Composite && cells[i] != Id)) continue;
                sourcePixels.Add(i);
                int feature = Feature(cells, i % Terrain.Width, i / Terrain.Width); List<int> values;
                if (!samples.TryGetValue(feature, out values)) samples.Add(feature, values = new List<int>());
                values.Add(i);
            }
            // bounded, evenly spaced candidates keep contour matching cheap
            foreach (int key in samples.Keys.ToArray())
            {
                var values = samples[key];
                if (values.Count > 256) samples[key] = Enumerable.Range(0, 256).Select(n => values[n * values.Count / 256]).ToList();
            }
            samplesReady = true;
        }
        internal static int Feature(int[] cells, int x, int y)
        {
            int key = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                int depth = 0;
                while (depth < 3 && Terrain.Solid(cells, x + dx[direction] * (depth + 1), y + dy[direction] * (depth + 1))) depth++;
                key |= depth << (direction * 2);
            }
            for (int n = 0; n < 4; n++)
                if (Terrain.Solid(cells, x + cx[n], y) && Terrain.Solid(cells, x, y + cy[n]) && !Terrain.Solid(cells, x + cx[n], y + cy[n])) key |= 1 << (8 + n);
            return key;
        }
        private static int Difference(int a, int b)
        {
            int cost = 0;
            for (int n = 0; n < 4; n++)
            {
                int x = (a >> (n * 2)) & 3, y = (b >> (n * 2)) & 3;
                cost += Math.Abs(x - y) + ((x == 0) != (y == 0) ? 12 : 0);
                if (((a ^ b) & (1 << (8 + n))) != 0) cost += 16;
            }
            return cost;
        }
        internal int Source(int[] cells, int x, int y)
        {
            if (FoundationSource != null) return FoundationSource.Source(cells, x, y);
            if (Sample != null)
            {
                int feature = Feature(cells, x, y), mirrored = 0;
                for (int n = 0; n < 4; n++)
                {
                    int d = n; if (FlipX && (d == 1 || d == 3)) d = 4 - d; if (FlipY && (d == 0 || d == 2)) d = 2 - d;
                    mirrored |= ((feature >> (n * 2)) & 3) << (d * 2);
                    int c = n; if (FlipX) c ^= 1; if (FlipY) c = 3 - c;
                    mirrored |= ((feature >> (8 + n)) & 1) << (8 + c);
                }
                return Sample.SourceFeature(mirrored, FlipX ? Bounds.Right - 1 - Terrain.Mod(x - Bounds.X, Bounds.Width) : x,
                    FlipY ? Bounds.Bottom - 1 - Terrain.Mod(y - Bounds.Y, Bounds.Height) : y);
            }
            return SourceFeature(Feature(cells, x, y), x, y);
        }
        private int SourceFeature(int feature, int x, int y)
        {
            PrepareSamples();
            if (samples.Count == 0) return Region.Pixels.Count == 0 ? 0 : Region.Pixels[0];
            int sx = Bounds.X + Terrain.Mod(x - Bounds.X, Bounds.Width), sy = Bounds.Y + Terrain.Mod(y - Bounds.Y, Bounds.Height);
            if (sourcePixels.Contains(sy * Terrain.Width + sx) && Feature(original, sx, sy) == feature) return sy * Terrain.Width + sx;
            List<int> candidates;
            if (!matches.TryGetValue(feature, out candidates))
            {
                int bestCost = int.MaxValue; candidates = null;
                foreach (var pair in samples)
                {
                    int cost = Difference(feature, pair.Key);
                    if (cost < bestCost) { bestCost = cost; candidates = pair.Value; }
                }
                matches.Add(feature, candidates);
            }
            int best = candidates[0], distance = int.MaxValue;
            foreach (int i in candidates)
            {
                int dx = Math.Abs(i % Terrain.Width - sx), dy = Math.Abs(i / Terrain.Width - sy);
                int d = dx * dx + dy * dy;
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }
        internal static int Choose(IList<Material> materials, Rectangle target)
        {
            var candidates = materials.Where(m => !m.Wall && !m.Untextured && (m.Template == null || m.Template.Error == null)).ToArray();
            if (candidates.Length == 0) { var blank = materials.FirstOrDefault(m => m.Untextured); return blank == null ? 0 : blank.Id; }
            return candidates.OrderBy(m => Math.Abs(Math.Log((m.Bounds.Width / (double)m.Bounds.Height) / (target.Width / (double)target.Height))) * 4
                + Math.Abs(Math.Log((m.Bounds.Width * (double)m.Bounds.Height) / (target.Width * (double)target.Height)))
                + Vector2.Distance(m.Bounds.Center.ToVector2(), target.Center.ToVector2()) / 1000).First().Id;
        }
        internal static int ChooseConnected(IList<Material> materials, Rectangle target, int[] cells, Func<Material, bool> filter = null)
        {
            var neighbours = new Dictionary<int, int>(); var r = target; r.Inflate(1, 1); r = Rectangle.Intersect(r, Terrain.Screen);
            for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++)
            { int id = cells[y * Terrain.Width + x], count; if (id != 0 && (filter == null || filter(materials[id - 1])) && !materials[id - 1].Wall && (materials[id - 1].Template == null || materials[id - 1].Template.Error == null)) { neighbours.TryGetValue(id, out count); neighbours[id] = count + 1; } }
            return neighbours.Count == 0 ? Choose(filter == null ? materials : materials.Where(filter).ToArray(), target) : neighbours.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key;
        }
    }

    internal static class PlatformArt
    {
        internal static Color[] Compose(Color[] source, int[] baseline, int[] cells, IList<Material> materials, bool bareSurface = false)
        {
            var output = (Color[])source.Clone();
            for (int y = 0; y < Terrain.Height; y++) for (int x = 0; x < Terrain.Width; x++)
            {
                int i = y * Terrain.Width + x, id = cells[i];
                if (id != 0)
                {
                    if (baseline[i] == id && Material.Feature(baseline, x, y) == Material.Feature(cells, x, y)) continue;
                    output[i] = materials[id - 1].Untextured ? (bareSurface ? new Color(160, 160, 160) : Color.Transparent) : source[materials[id - 1].Source(cells, x, y)];
                }
                else if (baseline[i] != 0) output[i] = Color.Transparent;
            }
            return output;
        }
        internal static Color[] WithDecor(Color[] source, int[] baseline, int[] cells, IList<Material> materials, int height, bool foreground)
        {
            var output = Compose(source, baseline, cells, materials);
            var tops = new Dictionary<int, List<int>>();
            for (int i = 0; i < baseline.Length; i++)
            {
                int id = baseline[i], x = i % Terrain.Width, y = i / Terrain.Width;
                if (id == 0 || materials[id - 1].Wall || Terrain.Solid(baseline, x, y - 1)) continue;
                // opaque scenery isn't a detachable fringe; foreground is already a separate layer
                if (!foreground && !OpenStrip(source, baseline, x, y, height)) continue;
                List<int> list; if (!tops.TryGetValue(id, out list)) tops[id] = list = new List<int>(); list.Add(i);
                if (cells[i] == id && !Terrain.Solid(cells, x, y - 1)) continue;
                for (int d = 1; d <= height && y - d >= 0 && !Terrain.Solid(baseline, x, y - d); d++)
                    if (!Terrain.Solid(cells, x, y - d)) output[i - d * Terrain.Width] = Color.Transparent;
            }
            foreach (var material in materials.Where(m => m.Composite))
                tops[material.Id] = material.Region.Pixels.Where(i => baseline[i] != 0 && !Terrain.Solid(baseline, i % Terrain.Width, i / Terrain.Width - 1)).ToList();
            foreach (var material in materials.Where(m => m.Sample != null && !m.FlipY))
            {
                List<int> sourceTops;
                if (tops.TryGetValue(material.Sample.Id, out sourceTops)) tops[material.Id] = sourceTops;
            }
            foreach (var material in materials.Where(m => m.FoundationSource != null))
            {
                List<int> sourceTops;
                if (tops.TryGetValue(material.FoundationSource.Id, out sourceTops)) tops[material.Id] = sourceTops;
            }
            for (int i = 0; i < cells.Length; i++)
            {
                int id = cells[i], x = i % Terrain.Width, y = i / Terrain.Width; List<int> candidates;
                if (id == 0 || Terrain.Solid(cells, x, y - 1) || !tops.TryGetValue(id, out candidates) || candidates.Count == 0) continue;
                if (baseline[i] == id && !Terrain.Solid(baseline, x, y - 1)) continue;
                var bounds = materials[id - 1].Bounds;
                int phase = materials[id - 1].FlipX ? bounds.Right - 1 - Terrain.Mod(x - bounds.X, bounds.Width) : bounds.X + Terrain.Mod(x - bounds.X, bounds.Width);
                int donor = candidates.OrderBy(n => Math.Abs(n % Terrain.Width - phase)).ThenBy(n => n / Terrain.Width).First();
                int sx = donor % Terrain.Width, sy = donor / Terrain.Width;
                int reach = 0;
                for (int d = 1; d <= height && sy - d >= 0 && !Terrain.Solid(baseline, sx, sy - d); d++)
                    if (source[donor - d * Terrain.Width].A != 0) reach = d;
                for (int d = 1; d <= reach && y - d >= 0; d++)
                {
                    if (Terrain.Solid(cells, x, y - d) || Terrain.Solid(baseline, sx, sy - d)) break;
                    Color colour = source[donor - d * Terrain.Width];
                    output[i - d * Terrain.Width] = colour;
                }
            }
            return output;
        }
        private static bool OpenStrip(Color[] source, int[] baseline, int x, int y, int height)
        {
            for (int d = 1; d <= height && y - d >= 0 && !Terrain.Solid(baseline, x, y - d); d++)
                if (source[(y - d) * Terrain.Width + x].A == 0) return true;
            return false;
        }
    }
}
