using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    internal static class PortalOpenings
    {
        private static readonly Dictionary<LevelScreen, byte> cache = new Dictionary<LevelScreen, byte>();
        internal static void Reset() { cache.Clear(); }

        internal static int Destination(LevelScreen[] screens, int source, bool left)
        {
            int destination = PortalViews.Destination(screens, source, left);
            if (destination < 0) return -1;
            byte sides;
            if (!cache.TryGetValue(screens[source], out sides))
            {
                sides = Scan(screens[source], source);
                cache.Add(screens[source], sides);
            }
            return (sides & (left ? 1 : 2)) != 0 ? destination : -1;
        }

        private static byte Scan(LevelScreen screen, int index)
        {
            ulong left = 0, right = 0;
            int top = -index * 360;
            foreach (var block in JKRuntime.Geometry.NativeWorldGeometry.ReadBlocks(screen))
            {
                if (block == null) continue;
                Type type = block.GetType();
                // Only audited solid shapes can prove that an edge is a wall.
                // Never call arbitrary mod collision callbacks while rendering.
                bool box = type == typeof(BoxBlock) || type == typeof(IceBlock) || type == typeof(SnowBlock);
                bool slope = type == typeof(SlopeBlock);
                if (!box && !slope) continue;
                Rectangle bounds = block.GetRect();
                bool touchesLeft = bounds.Left <= 0 && bounds.Right > 0;
                bool touchesRight = bounds.Left <= 479 && bounds.Right > 479;
                if (!touchesLeft && !touchesRight) continue;
                if (slope)
                {
                    var vertices = JKRuntime.Geometry.NativeWorldGeometry.ReadSlopeVertices((SlopeBlock)block);
                    if (touchesLeft) left |= SlopeEdge(vertices, .5f, top);
                    if (touchesRight) right |= SlopeEdge(vertices, 479.5f, top);
                    continue;
                }
                int first = Math.Max(0, (int)Math.Floor((bounds.Top - top) / 8.0));
                int last = Math.Min(44, (int)Math.Floor((bounds.Bottom - 1 - top) / 8.0));
                for (int row = first; row <= last; row++)
                {
                    if (touchesLeft) left |= 1UL << row;
                    if (touchesRight) right |= 1UL << row;
                }
            }
            return (byte)((HasGap(left) ? 1 : 0) | (HasGap(right) ? 2 : 0));
        }

        private static bool HasGap(ulong blocked)
        {
            ulong free = ~blocked & ((1UL << 45) - 1);
            return (free & (free >> 1)) != 0;
        }

        private static ulong SlopeEdge(Vector2[] vertices, float x, int top)
        {
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 a = vertices[i], b = vertices[(i + 1) % vertices.Length];
                if (x < Math.Min(a.X, b.X) || x > Math.Max(a.X, b.X)) continue;
                if (a.X == b.X) { low = Math.Min(low, Math.Min(a.Y, b.Y)); high = Math.Max(high, Math.Max(a.Y, b.Y)); }
                else
                {
                    float y = a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
                    low = Math.Min(low, y); high = Math.Max(high, y);
                }
            }
            ulong mask = 0;
            if (high <= low) return mask;
            for (int row = 0; row < 45; row++)
                if (top + row * 8 < high && top + (row + 1) * 8 > low) mask |= 1UL << row;
            return mask;
        }
    }
}
