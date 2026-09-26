using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static class GimmickSpace
    {
        internal static int Compilations;
        private static readonly System.Reflection.FieldInfo SlopeLines = typeof(SlopeBlock).GetField("m_lines", Gimmicks.Members);
        private static Point[] Polygon(SlopeBlock slope)
        {
            if (SlopeLines == null) throw new InvalidOperationException("Native slope polygon contract is unavailable.");
            var lines = SlopeLines.GetValue(slope) as ErikMaths.Line[];
            if (lines == null || lines.Length != 3) throw new InvalidOperationException("Native slope polygon is invalid.");
            var points = lines.Select(line => line.p0).ToArray();
            if (points.Distinct().Count() != 3) throw new InvalidOperationException("Degenerate slope cannot define empty space.");
            return points;
        }
        private static bool Inside(Point[] points, int x, int y)
        {
            // Pixel centres, doubled integer coordinates. Native slope Intersects
            // tests edges only and misses a hitbox wholly inside its triangle.
            bool positive = false, negative = false;
            for (int i = 0; i < points.Length; i++) {
                Point a = points[i], b = points[(i + 1) % points.Length];
                long cross = ((long)b.X - a.X) * (2L * y + 1 - 2L * a.Y) - ((long)b.Y - a.Y) * (2L * x + 1 - 2L * a.X);
                positive |= cross > 0; negative |= cross < 0;
            }
            return !(positive && negative);
        }
        // Compile once during preparation or an explicit paused edit, never Draw/idle Update.
        internal static Rectangle[] Empty(IBlock[] blocks, int screen)
        {
            Compilations++;
            var occupied = new bool[480 * 360]; int top = -360 * screen;
            foreach (var block in blocks)
            {
                bool? blocking = GimmickClassification.Blocking(block);
                if (!blocking.HasValue) throw new InvalidOperationException("Exact empty-space fill cannot classify " + block.GetType().Name + ". Use an explicit overlay or another scope.");
                if (!blocking.Value) continue;
                Point[] polygon = block is SlopeBlock ? Polygon((SlopeBlock)block) : null;
                Rectangle rect = Rectangle.Intersect(block.GetRect(), new Rectangle(0, top, 480, 360));
                for (int y = rect.Top; y < rect.Bottom; y++) for (int x = rect.Left; x < rect.Right; x++)
                {
                    Rectangle overlap;
                    if (polygon == null || Inside(polygon, x, y) || block.Intersects(new Rectangle(x, y, 1, 1), out overlap) == BlockCollisionType.Collision_Blocking)
                        occupied[(y - top) * 480 + x] = true;
                }
            }
            var result = new List<Rectangle>(); var previous = new Dictionary<int, int>();
            for (int y = 0; y < 360; y++)
            {
                var next = new Dictionary<int, int>();
                for (int x = 0; x < 480; )
                {
                    if (occupied[y * 480 + x]) { x++; continue; }
                    int start = x; while (x < 480 && !occupied[y * 480 + x]) x++;
                    int width = x - start, key = start * 481 + width, index;
                    if (previous.TryGetValue(key, out index)) { var r = result[index]; r.Height++; result[index] = r; }
                    else { index = result.Count; result.Add(new Rectangle(start, y + top, width, 1)); }
                    next[key] = index;
                }
                previous = next;
            }
            return result.ToArray();
        }
        internal static IBlock[] Fill(IBlock[] source, int screen, IBlock material)
        {
            if (GimmickClassification.Blocking(material) != false) throw new InvalidOperationException("Empty-space fill needs a confirmed nonblocking material.");
            return source.Concat(Empty(source, screen).Select(r => GimmickBlocks.Copy(material, r))).ToArray();
        }
    }
}
