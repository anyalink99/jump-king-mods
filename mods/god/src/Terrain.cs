using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace God
{
    internal sealed class Region
    {
        internal readonly List<int> Pixels = new List<int>();
        internal Rectangle Bounds;
        internal bool Wall;
        internal JumpKing.Level.SlopeType Slope = JumpKing.Level.SlopeType.None;
    }

    internal static class Terrain
    {
        internal const int Width = 480, Height = 360, Count = Width * Height;
        internal static readonly Rectangle Screen = new Rectangle(0, 0, Width, Height);
        internal static bool Solid(int[] cells, int x, int y)
        { return x >= 0 && x < Width && y >= 0 && y < Height && cells[y * Width + x] != 0; }

        internal static List<Region> Regions(int[] cells, Func<int, int> kind = null)
        {
            var seen = new bool[Count]; var result = new List<Region>();
            var walls = WallColumns(cells, kind);
            for (int i = 0; i < Count; i++)
            {
                if (cells[i] == 0 || seen[i]) continue;
                var region = new Region { Wall = walls[i % Width] }; region.Pixels.Add(i); seen[i] = true;
                int value = kind == null ? cells[i] : kind(cells[i]);
                int left = Width, top = Height, right = 0, bottom = 0;
                for (int cursor = 0; cursor < region.Pixels.Count; cursor++)
                {
                    int n = region.Pixels[cursor], x = n % Width, y = n / Width;
                    left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
                    if (x > 0) Visit(n - 1, value, region.Wall, cells, seen, walls, kind, region.Pixels);
                    if (x + 1 < Width) Visit(n + 1, value, region.Wall, cells, seen, walls, kind, region.Pixels);
                    if (y > 0) Visit(n - Width, value, region.Wall, cells, seen, walls, kind, region.Pixels);
                    if (y + 1 < Height) Visit(n + Width, value, region.Wall, cells, seen, walls, kind, region.Pixels);
                }
                region.Bounds = new Rectangle(left, top, right - left, bottom - top); result.Add(region);
            }
            return result;
        }
        private static bool[] WallColumns(int[] cells, Func<int, int> kind)
        {
            var result = new bool[Width];
            foreach (int direction in new[] { 1, -1 })
            {
                int x = direction == 1 ? 0 : Width - 1;
                for (; x >= 0 && x < Width; x += direction)
                {
                    int id = cells[x];
                    if (id == 0 || (kind != null && kind(id) != 1)) break;
                    bool full = true;
                    for (int y = 1; y < Height; y++)
                        if (cells[y * Width + x] == 0 || (kind == null ? cells[y * Width + x] != id : kind(cells[y * Width + x]) != 1)) { full = false; break; }
                    if (!full) break;
                    result[x] = true;
                }
            }
            return result;
        }
        private static void Visit(int next, int value, bool wall, int[] cells, bool[] seen, bool[] walls, Func<int, int> kind, List<int> pixels)
        {
            if (seen[next] || cells[next] == 0 || walls[next % Width] != wall || (kind == null ? cells[next] : kind(cells[next])) != value) return;
            seen[next] = true; pixels.Add(next);
        }
        internal static Region Pick(int[] cells, Point point)
        {
            if (!Screen.Contains(point) || cells[point.Y * Width + point.X] == 0) return null;
            return Regions(cells).First(r => r.Pixels.Contains(point.Y * Width + point.X));
        }
        internal static void Paint(int[] cells, Rectangle rectangle, int material)
        {
            rectangle = Rectangle.Intersect(Screen, rectangle);
            for (int y = rectangle.Top; y < rectangle.Bottom; y++)
                for (int x = rectangle.Left; x < rectangle.Right; x++) cells[y * Width + x] = material;
        }
        internal static int[] Move(int[] source, Region region, Point offset, bool resize, Rectangle target, bool copy = false)
        {
            var next = (int[])source.Clone(); if (!copy) foreach (int i in region.Pixels) next[i] = 0;
            foreach (int i in region.Pixels)
            {
                int x = i % Width, y = i / Width;
                if (!resize)
                {
                    x += offset.X; y += offset.Y;
                    if (!Screen.Contains(x, y)) continue;
                    next[y * Width + x] = source[i];
                }
            }
            if (resize)
            {
                var members = new HashSet<int>(region.Pixels);
                if (target.Width < 1 || target.Height < 1) throw new ArgumentException("Platform size must be positive.");
                var clipped = Rectangle.Intersect(Screen, target);
                // map from the requested size, so clipping doesn't stretch the remaining cells
                for (int y = clipped.Top; y < clipped.Bottom; y++) for (int x = clipped.Left; x < clipped.Right; x++)
                {
                    int sx = region.Bounds.X + (x - target.X) * region.Bounds.Width / target.Width;
                    int sy = region.Bounds.Y + (y - target.Y) * region.Bounds.Height / target.Height;
                    int index = sy * Width + sx;
                    int material = source[index]; if (material != 0 && members.Contains(index)) next[y * Width + x] = material;
                }
            }
            return next;
        }

        // merge equal horizontal runs vertically, so a big floor stays one collider
        internal static List<Rectangle> Rectangles(int[] cells)
        {
            var result = new List<Rectangle>(); var previous = new Dictionary<long, int>();
            for (int y = 0; y < Height; y++)
            {
                var current = new Dictionary<long, int>(); int x = 0;
                while (x < Width)
                {
                    if (cells[y * Width + x] == 0) { x++; continue; }
                    int start = x, material = cells[y * Width + x]; while (x < Width && cells[y * Width + x] == material) x++;
                    long key = ((long)material << 20) | ((long)start << 10) | (uint)(x - start); int index;
                    if (previous.TryGetValue(key, out index)) { var r = result[index]; r.Height++; result[index] = r; }
                    else { index = result.Count; result.Add(new Rectangle(start, y, x - start, 1)); }
                    current.Add(key, index);
                }
                previous = current;
            }
            return result;
        }
        internal static int Mod(int value, int length) { return (value % length + length) % length; }
        internal static Point Snap(Point p, int grid)
        { return new Point(Math.Max(0, Math.Min(Width - grid, p.X / grid * grid)), Math.Max(0, Math.Min(Height - grid, p.Y / grid * grid))); }
        internal static Rectangle DragRect(Point start, Point end, int grid)
        { return Rectangle.Intersect(Screen, new Rectangle(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X) + grid, Math.Abs(end.Y - start.Y) + grid)); }
    }

    internal sealed class EditHistory
    {
        private readonly List<int[]> undo = new List<int[]>(), redo = new List<int[]>();
        internal void Push(int[] cells) { undo.Add((int[])cells.Clone()); if (undo.Count > 32) undo.RemoveAt(0); redo.Clear(); }
        internal int[] Undo(int[] current) { return Take(undo, redo, current); }
        internal int[] Redo(int[] current) { return Take(redo, undo, current); }
        private static int[] Take(List<int[]> from, List<int[]> to, int[] current)
        {
            if (from.Count == 0) return current;
            to.Add((int[])current.Clone()); var value = from[from.Count - 1]; from.RemoveAt(from.Count - 1); return value;
        }
    }
}
