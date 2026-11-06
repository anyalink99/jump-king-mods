using System;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Geometry
{
    /// <summary>Detached convex surface used by presentation and cosmetic collision, never a native physics replacement.</summary>
    public sealed class VisualShape
    {
        private readonly Vector2[] vertices, normals;
        public Vector2[] Vertices { get { return (Vector2[])vertices.Clone(); } }
        public VisualShape(Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3 || polygon.Length > 64) throw new ArgumentException("A convex polygon needs 3 to 64 vertices");
            vertices = (Vector2[])polygon.Clone(); normals = new Vector2[vertices.Length];
            float area = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                var a = vertices[i]; var b = vertices[(i + 1) % vertices.Length];
                if (!Finite(a.X) || !Finite(a.Y)) throw new ArgumentException("Non-finite polygon");
                area += a.X * b.Y - b.X * a.Y;
            }
            if (Math.Abs(area) < .0001f) throw new ArgumentException("Degenerate polygon");
            for (int i = 0; i < vertices.Length; i++)
            {
                var edge = vertices[(i + 1) % vertices.Length] - vertices[i];
                if (edge.LengthSquared() < .000001f) throw new ArgumentException("Repeated polygon vertex");
                var n = new Vector2(edge.Y, -edge.X) * Math.Sign(area); n.Normalize(); normals[i] = n;
                for (int j = 0; j < vertices.Length; j++)
                    if (Vector2.Dot(vertices[j] - vertices[i], n) > .001f) throw new ArgumentException("Polygon isn't convex");
            }
        }
        private static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        /// <summary>Filled area, including the boundary. Pixel masks should query pixel centres.</summary>
        public bool Contains(Vector2 point)
        {
            for (int i = 0; i < normals.Length; i++) if (Vector2.Dot(point - vertices[i], normals[i]) > .00001f) return false;
            return true;
        }
        /// <summary>Filled polygon versus rectangle, including full containment.</summary>
        public bool Intersects(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return false;
            float time; Vector2 normal;
            return Sweep(bounds.Center.ToVector2() + new Vector2(bounds.Width % 2 * .5f, bounds.Height % 2 * .5f),
                Vector2.Zero, new Vector2(bounds.Width * .5f, bounds.Height * .5f), out time, out normal);
        }
        /// <summary>Sweep an axis-aligned box. Returns earliest fraction and outward normal; initial overlap returns zero.</summary>
        public bool Sweep(Vector2 center, Vector2 movement, Vector2 halfSize, out float fraction, out Vector2 normal)
        {
            if (!Finite(center.X) || !Finite(center.Y) || !Finite(movement.X) || !Finite(movement.Y)
                || !Finite(halfSize.X) || !Finite(halfSize.Y) || halfSize.X < 0 || halfSize.Y < 0) throw new ArgumentException("Invalid sweep");
            float enter = 0, leave = 1, depth = float.MaxValue; Vector2 entryNormal = Vector2.Zero, overlapNormal = Vector2.Zero;
            fraction = 0; normal = Vector2.Zero;
            for (int i = 0; i < normals.Length + 2; i++)
            {
                Vector2 axis = i < normals.Length ? normals[i] : i == normals.Length ? Vector2.UnitX : Vector2.UnitY;
                float min = float.MaxValue, max = float.MinValue;
                foreach (var vertex in vertices) { float p = Vector2.Dot(vertex, axis); min = Math.Min(min, p); max = Math.Max(max, p); }
                float radius = Math.Abs(axis.X) * halfSize.X + Math.Abs(axis.Y) * halfSize.Y;
                float at = Vector2.Dot(center, axis), speed = Vector2.Dot(movement, axis);
                min -= radius; max += radius;
                float near = at - min, far = max - at;
                if (near >= 0 && far >= 0 && Math.Min(near, far) < depth)
                { depth = Math.Min(near, far); overlapNormal = near < far ? -axis : axis; }
                if (Math.Abs(speed) < .000001f) { if (at < min || at > max) return false; continue; }
                float a = (min - at) / speed, b = (max - at) / speed;
                Vector2 n = speed > 0 ? -axis : axis;
                if (a > b) { float swap = a; a = b; b = swap; }
                if (a > enter) { enter = a; entryNormal = n; }
                leave = Math.Min(leave, b);
                if (enter > leave || leave < 0) return false;
            }
            if (enter > 1) return false;
            fraction = Math.Max(0, enter); normal = entryNormal == Vector2.Zero ? overlapNormal : entryNormal;
            // a touching box moving away isn't a new collision
            if (fraction == 0 && depth <= .00001f && Vector2.Dot(movement, normal) >= 0 && movement != Vector2.Zero) return false;
            return true;
        }
    }

    /// <summary>Shared native visual contours. NativeWorldGeometry remains the source of actual gameplay collision lines.</summary>
    public static class VisualGeometry
    {
        /// <summary>Declared slope orientation, including the southwest correction. None is rectangular, as in Ball King.</summary>
        public static Vector2[] DeclaredSlope(Rectangle r, SlopeType type)
        {
            var tl = new Vector2(r.Left, r.Top); var tr = new Vector2(r.Right, r.Top);
            var br = new Vector2(r.Right, r.Bottom); var bl = new Vector2(r.Left, r.Bottom);
            switch (type)
            {
                case SlopeType.TopLeft: return new[] { tr, br, bl };
                case SlopeType.TopRight: return new[] { tl, br, bl };
                case SlopeType.BottomLeft: return new[] { tl, tr, br };
                case SlopeType.BottomRight: return new[] { tl, tr, bl };
                case SlopeType.None: return new[] { tl, tr, br, bl };
                default: return new Vector2[0];
            }
        }
        /// <summary>Prism's presentation contour: loaded native lines except the declared southwest triangle.</summary>
        public static Vector2[] ReadSlope(SlopeBlock slope)
        {
            if (slope == null) throw new ArgumentNullException("slope");
            return slope.GetSlopeType() == SlopeType.BottomLeft ? DeclaredSlope(slope.GetRect(), SlopeType.BottomLeft) : NativeWorldGeometry.ReadSlopeVertices(slope);
        }
        /// <summary>Known native blocking surfaces only. Unknown classes and nonblocking volumes return false.</summary>
        public static bool TryRead(IBlock block, out VisualShape shape)
        {
            RuntimeApi.Kernel.CheckThread(); shape = null; Rectangle bounds;
            if (!NativeWorldGeometry.TryReadNativeBounds(block, out bounds) || bounds.Width <= 0 || bounds.Height <= 0) return false;
            var type = block.GetType();
            if (type == typeof(WaterBlock) || type == typeof(NoWindBlock) || type == typeof(QuarkBlock)) return false;
            var slope = block as SlopeBlock;
            if (slope != null && slope.GetSlopeType() == SlopeType.None) return false;
            try { shape = new VisualShape(slope == null ? DeclaredSlope(bounds, SlopeType.None) : ReadSlope(slope)); return true; }
            catch (ArgumentException) { return false; }
        }
        /// <summary>Existing Prism half-open raster rule, kept separately from filled-area collision boundary rules.</summary>
        public static bool RasterContains(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].Y > y) != (p[j].Y > y) && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X) inside = !inside;
            return inside;
        }
    }
}
