using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace God
{
    internal sealed class LayerSurface : IDisposable
    {
        private readonly Texture2D[] textures = new Texture2D[2];
        private readonly Rectangle[] pending = { Terrain.Screen, Terrain.Screen };
        private readonly Color[] source, upload = new Color[Terrain.Count];
        private readonly int[] baseline;
        private readonly IList<Material> materials;
        private int active = -1, staged;
        private Rectangle change;
        private DecorSampler decor;
        internal int CreatedTextures, UploadedPixels;
        internal LayerSurface(Color[] original, int[] mask, IList<Material> samples)
        { source = original; baseline = mask; materials = samples; }
        internal Texture2D Stage(GraphicsDevice device, int[] cells, Rectangle dirty, int decorHeight)
        {
            staged = active == 0 ? 1 : 0; change = dirty;
            pending[staged] = Union(pending[staged], dirty);
            var area = pending[staged];
            if (textures[staged] == null)
            { textures[staged] = new Texture2D(device, Terrain.Width, Terrain.Height); CreatedTextures++; }
            if (area.Width == 0 || area.Height == 0) return textures[staged];
            Compose(source, baseline, cells, materials, area, upload, decorHeight == 0);
            if (decorHeight > 0)
            {
                if (decor == null || decor.Height != decorHeight) decor = new DecorSampler(source, baseline, materials, decorHeight);
                decor.Apply(cells, area, upload);
            }
            textures[staged].SetData(0, area, upload, 0, area.Width * area.Height);
            UploadedPixels += area.Width * area.Height;
            return textures[staged];
        }
        internal void Commit()
        { active = staged; pending[active] = Rectangle.Empty; pending[1 - active] = Union(pending[1 - active], change); }
        internal void Reset() { active = -1; pending[0] = pending[1] = Terrain.Screen; }
        internal static Rectangle Union(Rectangle a, Rectangle b)
        { return a.Width == 0 || a.Height == 0 ? b : b.Width == 0 || b.Height == 0 ? a : Rectangle.Union(a, b); }
        internal static Rectangle Dirty(int[] before, int[] after)
        {
            int left = Terrain.Width, top = Terrain.Height, right = -1, bottom = -1;
            for (int i = 0; i < before.Length; i++) if (before[i] != after[i])
            { int x = i % Terrain.Width, y = i / Terrain.Width; left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
            if (right < left) return Rectangle.Empty;
            var area = new Rectangle(left, top, right - left + 1, bottom - top + 1);
            // contour sampling looks at most three pixels past a changed cell
            area.Inflate(3, 3); return Rectangle.Intersect(area, Terrain.Screen);
        }
        internal static void Compose(Color[] source, int[] baseline, int[] cells, IList<Material> materials, Rectangle area, Color[] output, bool bare)
        {
            int n = 0;
            for (int y = area.Top; y < area.Bottom; y++) for (int x = area.Left; x < area.Right; x++, n++)
            {
                int i = y * Terrain.Width + x, id = cells[i]; var color = source[i];
                if (id != 0)
                {
                    if (baseline[i] != id || Material.Feature(baseline, x, y) != Material.Feature(cells, x, y))
                        color = materials[id - 1].Untextured ? (bare ? new Color(160, 160, 160) : Color.Transparent) : source[materials[id - 1].Source(cells, x, y)];
                }
                else if (baseline[i] != 0) color = Color.Transparent;
                output[n] = color;
            }
        }
        public void Dispose() { foreach (var texture in textures) if (texture != null) texture.Dispose(); }
    }

    internal sealed class DecorSampler
    {
        private struct Column { internal int Index, Reach; }
        private readonly Color[] source;
        private readonly int[] baseline;
        private readonly IList<Material> materials;
        private readonly List<int> originals = new List<int>();
        private readonly Dictionary<int, List<int>> tops = new Dictionary<int, List<int>>();
        private readonly Dictionary<long, Column> columns = new Dictionary<long, Column>();
        internal readonly int Height;
        internal DecorSampler(Color[] image, int[] mask, IList<Material> samples, int height)
        {
            source = image; baseline = mask; materials = samples; Height = height;
            for (int i = 0; i < mask.Length; i++)
            {
                int id = mask[i]; if (id == 0 || samples[id - 1].Wall || Terrain.Solid(mask, i % Terrain.Width, i / Terrain.Width - 1)) continue;
                originals.Add(i); List<int> list; if (!tops.TryGetValue(id, out list)) tops[id] = list = new List<int>(); list.Add(i);
            }
            foreach (var material in samples.Where(m => m.Composite))
                tops[material.Id] = material.Region.Pixels.Where(i => mask[i] != 0 && !Terrain.Solid(mask, i % Terrain.Width, i / Terrain.Width - 1)).ToList();
            foreach (var material in samples.Where(m => m.Sample != null && !m.FlipY)) Alias(material.Id, material.Sample.Id);
            foreach (var material in samples.Where(m => m.FoundationSource != null)) Alias(material.Id, material.FoundationSource.Id);
        }
        private void Alias(int id, int donor) { List<int> list; if (tops.TryGetValue(donor, out list)) tops[id] = list; }
        private Column Read(int id, int x, List<int> candidates)
        {
            var material = materials[id - 1]; var bounds = material.Bounds;
            int phase = material.FlipX ? bounds.Right - 1 - Terrain.Mod(x - bounds.X, bounds.Width) : bounds.X + Terrain.Mod(x - bounds.X, bounds.Width);
            long key = ((long)id << 32) | (uint)phase; Column column;
            if (columns.TryGetValue(key, out column)) return column;
            int donor = candidates[0], best = int.MaxValue;
            foreach (int n in candidates)
            {
                int distance = Math.Abs(n % Terrain.Width - phase);
                if (distance < best || (distance == best && n / Terrain.Width < donor / Terrain.Width)) { best = distance; donor = n; }
            }
            column.Index = donor; column.Reach = 0; int sx = donor % Terrain.Width, sy = donor / Terrain.Width;
            for (int d = 1; d <= Height && sy - d >= 0 && !Terrain.Solid(baseline, sx, sy - d); d++)
                if (source[donor - d * Terrain.Width].A != 0) column.Reach = d;
            columns[key] = column; return column;
        }
        internal void Apply(int[] cells, Rectangle area, Color[] output)
        {
            foreach (int i in originals)
            {
                int x = i % Terrain.Width, y = i / Terrain.Width;
                if (x < area.Left || x >= area.Right || y < area.Top || y > area.Bottom + Height) continue;
                if (cells[i] == baseline[i] && !Terrain.Solid(cells, x, y - 1)) continue;
                for (int d = 1; d <= Height && y - d >= 0 && !Terrain.Solid(baseline, x, y - d); d++)
                    if (area.Contains(x, y - d) && !Terrain.Solid(cells, x, y - d)) output[(y - d - area.Y) * area.Width + x - area.X] = Color.Transparent;
            }
            for (int y = area.Top; y < Math.Min(Terrain.Height, area.Bottom + Height); y++) for (int x = area.Left; x < area.Right; x++)
            {
                int i = y * Terrain.Width + x, id = cells[i]; List<int> candidates;
                if (id == 0 || Terrain.Solid(cells, x, y - 1) || !tops.TryGetValue(id, out candidates) || candidates.Count == 0) continue;
                if (baseline[i] == id && !Terrain.Solid(baseline, x, y - 1)) continue;
                var column = Read(id, x, candidates); int sx = column.Index % Terrain.Width, sy = column.Index / Terrain.Width;
                for (int d = 1; d <= column.Reach && y - d >= 0; d++)
                {
                    if (Terrain.Solid(cells, x, y - d) || Terrain.Solid(baseline, sx, sy - d)) break;
                    if (area.Contains(x, y - d)) output[(y - d - area.Y) * area.Width + x - area.X] = source[column.Index - d * Terrain.Width];
                }
            }
        }
    }
}
