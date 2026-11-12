using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.Geometry;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace God
{
    internal static class SlopePlatforms
    {
        private sealed class Piece
        {
            internal Rectangle Bounds;
            internal SlopeType Type;
            internal Vector2 A, B;
        }
        internal static SlopeType Type(Material material)
        {
            var slope = material.Template.Source as SlopeBlock;
            return slope == null ? SlopeType.None : slope.GetSlopeType();
        }
        internal static List<Region> Regions(ScreenEdit edit, int[] cells, int[] visible)
        {
            var pieces = new List<Piece>();
            foreach (var r in edit.Rectangles(cells))
            {
                var material = edit.Materials[cells[r.Y * Terrain.Width + r.X] - 1];
                if (material.Template.Source.GetType() != typeof(SlopeBlock) || Type(material) == SlopeType.None) continue;
                var p = VisualGeometry.ReadSlope((SlopeBlock)material.Template.Copy(r));
                // only join straight, declared ramps; foreign corrections keep their own contour
                var declared = VisualGeometry.DeclaredSlope(r, Type(material));
                if (p.Length != 3 || p.Any(v => !declared.Contains(v))) continue;
                for (int i = 0; i < p.Length; i++)
                {
                    var a = p[i]; var b = p[(i + 1) % p.Length];
                    if (a.X != b.X && a.Y != b.Y) { pieces.Add(new Piece { Bounds = r, Type = Type(material), A = a, B = b }); break; }
                }
            }
            var ends = new Dictionary<Vector2, List<int>>();
            for (int i = 0; i < pieces.Count; i++) foreach (var end in new[] { pieces[i].A, pieces[i].B })
            { List<int> list; if (!ends.TryGetValue(end, out list)) ends[end] = list = new List<int>(); list.Add(i); }
            if (pieces.Count == 0) return Terrain.Regions(cells, edit.Kind);
            var remaining = (int[])cells.Clone(); var result = new List<Region>(); var seen = new HashSet<int>();
            var walls = new HashSet<int>(Terrain.Regions(cells, edit.Kind).Where(r => r.Wall).SelectMany(r => r.Pixels));
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!seen.Add(i)) continue;
                var chain = new List<int>(); var queue = new Queue<int>(); queue.Enqueue(i);
                while (queue.Count != 0)
                {
                    int n = queue.Dequeue(); chain.Add(n); var piece = pieces[n]; var d = piece.B - piece.A;
                    foreach (var end in new[] { piece.A, piece.B }) foreach (int next in ends[end])
                    {
                        var other = pieces[next]; var e = other.B - other.A;
                        if (other.Type == piece.Type && Math.Abs(d.X * e.Y - d.Y * e.X) < .01f && seen.Add(next)) queue.Enqueue(next);
                    }
                }
                var bounds = pieces[i].Bounds; foreach (int n in chain) bounds = Rectangle.Union(bounds, pieces[n].Bounds);
                var shape = new VisualShape(VisualGeometry.DeclaredSlope(bounds, pieces[i].Type));
                var owned = new HashSet<int>();
                foreach (int n in chain)
                {
                    var r = pieces[n].Bounds;
                    for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) owned.Add(y * Terrain.Width + x);
                }
                bool complete = true;
                for (int y = bounds.Top; y < bounds.Bottom && complete; y++) for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    int index = y * Terrain.Width + x;
                    if (!shape.Contains(new Vector2(x + .5f, y + .5f))) continue;
                    int id = remaining[index];
                    if (id == 0 || visible[index] == 0 || walls.Contains(index)
                        || (!owned.Contains(index) && edit.Materials[id - 1].Template.Source.GetType() != typeof(BoxBlock))) { complete = false; break; }
                    owned.Add(index);
                }
                if (!complete) continue;
                var region = new Region { Bounds = bounds, Slope = pieces[i].Type }; region.Pixels.AddRange(owned.OrderBy(n => n));
                foreach (int n in region.Pixels) remaining[n] = 0;
                result.Add(region);
            }
            result.AddRange(Terrain.Regions(remaining, edit.Kind)); return result;
        }
        internal static void Prepare(ScreenEdit edit)
        {
            foreach (var region in edit.Regions.Where(r => r.Slope != SlopeType.None))
            {
                var slope = region.Pixels.Select(n => edit.Baseline[n]).Distinct().Select(id => edit.Materials[id - 1]).First(m => Type(m) == region.Slope);
                var material = new Material(edit.Materials.Count + 1, region, edit.VisualBaseline) {
                    Template = new BlockTemplate(slope.Template.Copy(region.Bounds)), Kind = slope.Kind, Composite = true };
                material.RebuildSamples(edit.VisualBaseline); edit.Materials.Add(material);
            }
            var donors = edit.Materials.Where(m => Type(m) != SlopeType.None).ToArray();
            foreach (SlopeType type in new[] { SlopeType.TopLeft, SlopeType.TopRight, SlopeType.BottomLeft, SlopeType.BottomRight })
            {
                if (donors.Any(m => Type(m) == type)) continue;
                var source = donors.OrderByDescending(m => m.Bounds.Width * m.Bounds.Height).FirstOrDefault();
                var region = source == null ? new Region { Bounds = new Rectangle(0, 0, edit.CellSize, edit.CellSize) } : source.Region;
                var material = new Material(edit.Materials.Count + 1, region, edit.VisualBaseline) {
                    Template = new BlockTemplate(new SlopeBlock(region.Bounds, type)), Kind = 100000 + (int)type,
                    Untextured = source == null, Sample = source };
                if (source != null)
                {
                    var a = Type(source); material.FlipX = Left(a) != Left(type); material.FlipY = Top(a) != Top(type);
                }
                edit.Materials.Add(material);
            }
            foreach (var source in edit.Materials.Where(m => Type(m) != SlopeType.None).ToArray())
            {
                var material = new Material(edit.Materials.Count + 1, source.Region, edit.VisualBaseline) {
                    Template = new BlockTemplate(new BoxBlock(source.Bounds)), FoundationSource = source, Untextured = source.Untextured };
                source.Foundation = material.Id; edit.Materials.Add(material);
            }
            foreach (var source in edit.Materials.Where(m => Type(m) != SlopeType.None).ToArray())
            {
                var material = new Material(edit.Materials.Count + 1, source.Region, edit.VisualBaseline) {
                    Template = new BlockTemplate(source.Template.Copy(new Rectangle(0, 0, edit.CellSize, edit.CellSize))),
                    FoundationSource = source, Foundation = source.Foundation, Untextured = source.Untextured, Kind = source.Kind, TiledSlope = true };
                source.SlopeCell = material.Id; material.SlopeCell = material.Id; edit.Materials.Add(material);
            }
        }
        internal static Rectangle DrawBounds(Point start, Point end, int grid)
        {
            int size = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)) + grid;
            return new Rectangle(end.X < start.X ? start.X + grid - size : start.X,
                end.Y < start.Y ? start.Y + grid - size : start.Y, size, size);
        }
        internal static Rectangle ResizeBounds(Region region, Point delta, int grid)
        {
            var bounds = region.Bounds;
            bounds.Width = Math.Max(grid, bounds.Width + delta.X); bounds.Height = Math.Max(grid, bounds.Height + delta.Y);
            if (region.Slope != SlopeType.None) bounds.Width = bounds.Height = Math.Max(bounds.Width, bounds.Height);
            return bounds;
        }
        internal static void Paint(ScreenEdit edit, int[] cells, Rectangle bounds, int material)
        {
            NormalizeTouched(edit, cells, bounds);
            PaintCells(edit, cells, bounds, material);
        }
        internal static void Erase(ScreenEdit edit, int[] cells, Rectangle bounds)
        {
            NormalizeTouched(edit, cells, bounds);
            Terrain.Paint(cells, bounds, 0);
        }
        private static void NormalizeTouched(ScreenEdit edit, int[] cells, Rectangle area)
        {
            foreach (var r in Terrain.Rectangles(cells))
            {
                int id = cells[r.Top * Terrain.Width + r.Left];
                if (!r.Intersects(area) || r.Width != r.Height || r.Width <= edit.CellSize || edit.Materials[id - 1].TiledSlope || Type(edit.Materials[id - 1]) == SlopeType.None) continue;
                // split old large triangles before a cut can turn their bounds into a stretched slope
                Terrain.Paint(cells, r, 0); PaintCells(edit, cells, r, id);
            }
        }
        private static void PaintCells(ScreenEdit edit, int[] cells, Rectangle bounds, int material)
        {
            var donor = edit.Materials[material - 1]; var type = Type(donor); int grid = edit.CellSize;
            if (type == SlopeType.None) { Terrain.Paint(cells, bounds, material); return; }
            if (bounds.Width != bounds.Height || bounds.Width % grid != 0 || bounds.X % grid != 0 || bounds.Y % grid != 0)
                throw new ArgumentException("Slope bounds must use square collision cells.");
            int count = bounds.Width / grid;
            var clipped = Rectangle.Intersect(Terrain.Screen, bounds);
            for (int y = clipped.Top; y < clipped.Bottom; y += grid) for (int x = clipped.Left; x < clipped.Right; x += grid)
            {
                int column = (x - bounds.X) / grid, row = (y - bounds.Y) / grid;
                int side = type == SlopeType.TopRight || type == SlopeType.BottomLeft ? row - column : row + column - count + 1;
                bool filled = type == SlopeType.TopLeft || type == SlopeType.TopRight ? side > 0 : side < 0;
                if (side == 0 || filled) Terrain.Paint(cells, new Rectangle(x, y, grid, grid), side == 0 ? donor.SlopeCell : donor.Foundation);
            }
        }
        internal static int[] Move(ScreenEdit edit, int[] source, Region region, Point offset, bool resize, Rectangle target, bool copy)
        {
            if (!resize)
            {
                target = region.Bounds;
                if (region.Slope != SlopeType.None) target.Width = target.Height = Math.Max(target.Width, target.Height);
                target.Offset(offset.X, offset.Y);
            }
            if (region.Slope == SlopeType.None)
            {
                var prepared = (int[])source.Clone(); NormalizeTouched(edit, prepared, target);
                return Terrain.Move(prepared, region, offset, resize, target, copy);
            }
            var donor = edit.Materials.FirstOrDefault(m => m.Composite && m.Bounds == region.Bounds && Type(m) == region.Slope);
            if (donor == null) donor = region.Pixels.Select(i => edit.Materials[source[i] - 1]).First(m => Type(m) == region.Slope);
            var next = (int[])source.Clone(); if (!copy) foreach (int i in region.Pixels) next[i] = 0;
            Paint(edit, next, target, donor.Id); return next;
        }
        internal static Vector2[] Clip(Vector2[] polygon)
        {
            var points = new List<Vector2>(polygon);
            for (int edge = 0; edge < 4 && points.Count != 0; edge++)
            {
                var output = new List<Vector2>(); var previous = points[points.Count - 1];
                foreach (var current in points)
                {
                    float a = Distance(previous, edge), b = Distance(current, edge);
                    if ((a >= 0) != (b >= 0)) output.Add(previous + (current - previous) * (a / (a - b)));
                    if (b >= 0) output.Add(current);
                    previous = current;
                }
                points = output;
            }
            return points.ToArray();
        }
        private static float Distance(Vector2 p, int edge)
        { return edge == 0 ? p.X : edge == 1 ? Terrain.Width - p.X : edge == 2 ? p.Y : Terrain.Height - p.Y; }
        private static bool Left(SlopeType t) { return t == SlopeType.TopLeft || t == SlopeType.BottomLeft; }
        private static bool Top(SlopeType t) { return t == SlopeType.TopLeft || t == SlopeType.TopRight; }
        internal static int Choose(ScreenEdit edit, Rectangle target, int donor, SlopeType shape, int[] cells)
        {
            if (donor > 0 && Type(edit.Materials[donor - 1]) == shape) return donor;
            var materials = edit.Materials.Where(m => Type(m) == shape).ToArray();
            return shape == SlopeType.None ? Material.ChooseConnected(edit.Materials, target, cells, m => Type(m) == shape && m.FoundationSource == null)
                : Material.Choose(materials, target);
        }
    }
}
