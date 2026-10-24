using System;
using System.Collections.Generic;
using JKRuntime.Geometry;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prism
{
    internal struct MirrorStrip { internal int X, Y, Width, SourceY, Depth; }
    internal sealed class ScreenArt : IDisposable
    {
        internal Texture2D Fill, Edge, Halo, Materials, Water;
        internal bool Supported = true;
        internal MirrorStrip[] Mirrors;
        internal readonly byte[] Cells = new byte[480 * 360];
        internal readonly byte[] WaterCells = new byte[480 * 360];
        internal readonly byte[] ShelterCells = new byte[480 * 360];
        internal static ScreenArt Read(LevelScreen screen, GraphicsDevice device)
        {
            var art = new ScreenArt();
            try
            {
                foreach (var block in NativeWorldGeometry.ReadBlocks(screen))
                {
                    Type t = block.GetType(); Rectangle box;
                    if (!NativeWorldGeometry.TryReadNativeBounds(block, out box)) { art.Supported = false; continue; }
                    box.Y += screen.GetIndex0() * 360;
                    if (t == typeof(NoWindBlock) || t == typeof(WaterBlock))
                    { art.RasterMask(box, t == typeof(WaterBlock) ? art.WaterCells : art.ShelterCells); continue; }
                    byte material = (byte)(t == typeof(IceBlock) ? 2 : t == typeof(SnowBlock) ? 3 : t == typeof(SandBlock) ? 4 : t == typeof(WaterBlock) ? 5 : t == typeof(QuarkBlock) ? 6 : 1);
                    Vector2[] polygon = null;
                    if (t == typeof(SlopeBlock))
                    {
                        polygon = VisualSlope((SlopeBlock)block);
                        for (int i = 0; i < polygon.Length; i++) polygon[i].Y += screen.GetIndex0() * 360;
                    }
                    art.Raster(box, polygon, material);
                }
                art.Build(device); return art;
            }
            catch { art.Dispose(); throw; }
        }
        internal static Vector2[] VisualSlope(SlopeBlock slope)
        {
            if (slope.GetSlopeType() != SlopeType.BottomLeft) return NativeWorldGeometry.ReadSlopeVertices(slope);
            // Same declared southwest triangle used by Ball King's contour adapter.
            // Native MakeLines duplicates BottomRight here; only the artwork is corrected.
            Rectangle r; NativeWorldGeometry.TryReadNativeBounds(slope, out r);
            return new[] { new Vector2(r.Left, r.Top), new Vector2(r.Right, r.Top), new Vector2(r.Right, r.Bottom) };
        }
        private void RasterMask(Rectangle box, byte[] mask)
        {
            for (int y = Math.Max(0, box.Top); y < Math.Min(360, box.Bottom); y++)
            for (int x = Math.Max(0, box.Left); x < Math.Min(480, box.Right); x++) mask[y * 480 + x] = 1;
        }
        internal void Raster(Rectangle box, Vector2[] polygon, byte material)
        {
            for (int y = Math.Max(0, box.Top); y < Math.Min(360, box.Bottom); y++)
            for (int x = Math.Max(0, box.Left); x < Math.Min(480, box.Right); x++)
                if (polygon == null || Inside(polygon, x + .5f, y + .5f)) Cells[y * 480 + x] = material;
        }
        internal static bool Inside(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].Y > y) != (p[j].Y > y) && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X) inside = !inside;
            return inside;
        }
        private bool Solid(int x, int y) { return x >= 0 && x < 480 && y >= 0 && y < 360 && Cells[y * 480 + x] != 0; }
        internal void Build(GraphicsDevice device)
        {
            var fill = new Color[Cells.Length]; var edge = new Color[Cells.Length];
            var halo = new Color[Cells.Length]; var materials = new Color[Cells.Length];
            var water = new Color[Cells.Length];
            var distance = new int[Cells.Length]; var mirror = new List<MirrorStrip>();
            for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++)
            {
                int i = y * 480 + x; byte m = Cells[i]; distance[i] = 100;
                if (WaterCells[i] != 0)
                {
                    bool surface = y == 0 || WaterCells[i - 480] == 0;
                    water[i] = new Color(48, 133, 186) * (surface ? .45f : .15f);
                }
                if (m == 0) continue;
                float face = .04f + ((x + y * 2) % 87 < 30 ? .045f : 0);
                fill[i] = new Color(.025f + face * .22f, .035f + face * .38f, .055f + face * .55f, m == 5 ? .55f : 1f);
                bool top = !Solid(x, y - 1);
                if (top || !Solid(x - 1, y) || !Solid(x + 1, y) || !Solid(x, y + 1))
                { edge[i] = Color.White * (top ? 1f : .44f); distance[i] = 0; }
                if (m > 1 && (top || ((x + y) % (m == 2 ? 12 : 7) == 0 && y % 4 == 0)))
                    materials[i] = (m == 2 ? Color.Cyan : m == 3 ? Color.White : m == 4 ? Color.Orange : m == 5 ? Color.CornflowerBlue : Color.Lime) * (top ? .9f : .35f);
            }
            // Two distance passes soften the silhouette; no per-frame blur or readback.
            for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++)
            { int i = y * 480 + x; if (x > 0) distance[i] = Math.Min(distance[i], distance[i - 1] + 1); if (y > 0) distance[i] = Math.Min(distance[i], distance[i - 480] + 1); }
            for (int y = 359; y >= 0; y--) for (int x = 479; x >= 0; x--)
            {
                int i = y * 480 + x;
                if (x < 479) distance[i] = Math.Min(distance[i], distance[i + 1] + 1);
                if (y < 359) distance[i] = Math.Min(distance[i], distance[i + 480] + 1);
                halo[i] = Color.White * ((float)Math.Exp(-distance[i] * .42) * .19f);
            }
            // Horizontal exposed tops only. Every strip is clipped to actual solid pixels.
            for (int y = 1; y < 359; y++) for (int x = 0; x < 480; x++)
            {
                if (!Solid(x, y) || Solid(x, y - 1)) continue;
                int start = x; while (x + 1 < 480 && Solid(x + 1, y) && !Solid(x + 1, y - 1)) x++;
                if (x - start < 5) continue;
                for (int d = 1; d <= 18 && y + d < 360 && y - d >= 0; d++)
                {
                    int a = start;
                    while (a <= x)
                    {
                        while (a <= x && !Solid(a, y + d)) a++;
                        int b = a; while (b <= x && Solid(b, y + d)) b++;
                        if (b > a) mirror.Add(new MirrorStrip { X = a, Y = y + d, Width = b - a, SourceY = y - d, Depth = d });
                        a = b + 1;
                    }
                }
            }
            Mirrors = mirror.ToArray();
            Fill = Texture(device, fill); Edge = Texture(device, edge); Halo = Texture(device, halo); Materials = Texture(device, materials); Water = Texture(device, water);
        }
        private static Texture2D Texture(GraphicsDevice device, Color[] pixels)
        { var t = new Texture2D(device, 480, 360); try { t.SetData(pixels); return t; } catch { t.Dispose(); throw; } }
        public void Dispose() { foreach (var t in new[] { Fill, Edge, Halo, Materials, Water }) if (t != null) t.Dispose(); }
    }
}
