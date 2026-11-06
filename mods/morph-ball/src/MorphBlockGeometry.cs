using System;
using System.Collections.Generic;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal static partial class MorphContourGeometry
    {
        internal static bool ExportSolidPolygon(IBlock block, out Vector2[] vertices)
        {
            vertices = GetSolidPolygon(block).ToArray();
            return vertices.Length >= 3;
        }

        private static List<Vector2> GetSolidPolygon(IBlock block)
        {
            List<Vector2> suppliedPolygon;
            if (BallKingGeometryApi.TryGetPolygon(
                block,
                out suppliedPolygon))
            {
                return suppliedPolygon;
            }
            var slope = block as SlopeBlock;
            return new List<Vector2>(JKRuntime.Geometry.VisualGeometry.DeclaredSlope(block.GetRect(),
                slope == null ? SlopeType.None : slope.GetSlopeType()));
        }

        private static List<Vector2> ConvexHull(List<Vector2> points)
        {
            points.Sort(ComparePoints);
            List<Vector2> unique = new List<Vector2>();
            foreach (Vector2 point in points)
            {
                if (unique.Count == 0
                    || !Near(unique[unique.Count - 1], point, GeometryEpsilon))
                {
                    unique.Add(point);
                }
            }
            if (unique.Count <= 2)
            {
                return unique;
            }
            List<Vector2> lower = new List<Vector2>();
            foreach (Vector2 point in unique)
            {
                while (lower.Count >= 2
                    && Cross(
                        lower[lower.Count - 1] - lower[lower.Count - 2],
                        point - lower[lower.Count - 1]) <= GeometryEpsilon)
                {
                    lower.RemoveAt(lower.Count - 1);
                }
                lower.Add(point);
            }
            List<Vector2> upper = new List<Vector2>();
            for (int index = unique.Count - 1; index >= 0; index--)
            {
                Vector2 point = unique[index];
                while (upper.Count >= 2
                    && Cross(
                        upper[upper.Count - 1] - upper[upper.Count - 2],
                        point - upper[upper.Count - 1]) <= GeometryEpsilon)
                {
                    upper.RemoveAt(upper.Count - 1);
                }
                upper.Add(point);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static int ComparePoints(Vector2 first, Vector2 second)
        {
            int x = first.X.CompareTo(second.X);
            return x != 0 ? x : first.Y.CompareTo(second.Y);
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.X * second.Y - first.Y * second.X;
        }

        private static bool Near(
            Vector2 first,
            Vector2 second,
            float tolerance)
        {
            return (first - second).LengthSquared()
                <= tolerance * tolerance;
        }
    }
}
