using System;
using System.Collections.Generic;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal static partial class MorphContourGeometry
    {
        private const float GeometryEpsilon = 0.0001f;
        private const float ExteriorProbe = 0.05f;

        internal static List<MorphContourSegment> BuildExterior(
            IEnumerable<IBlock> blocks,
            int bodyWidth,
            int bodyHeight)
        {
            // Movement happens in configuration space: expanding every solid
            // by the body extents turns the moving AABB into a point.  The
            // union is essential.  Following each block's hull separately
            // exposes false seams at pixels, steps and compound shapes and
            // makes one physical contact look like repeated landings.
            List<List<Vector2>> polygons = new List<List<Vector2>>();
            List<MorphContourSegment> candidates =
                new List<MorphContourSegment>();
            HashSet<IBlock> unique = new HashSet<IBlock>();
            foreach (IBlock block in blocks)
            {
                if (block == null || !unique.Add(block))
                {
                    continue;
                }
                List<Vector2> expanded = GetExpandedPolygon(
                    block,
                    bodyWidth,
                    bodyHeight);
                if (expanded.Count < 3)
                {
                    continue;
                }
                polygons.Add(expanded);
                AddPolygonSegments(block, expanded, candidates);
            }

            List<MorphContourSegment> exterior =
                new List<MorphContourSegment>();
            for (int sourceIndex = 0;
                sourceIndex < candidates.Count;
                sourceIndex++)
            {
                MorphContourSegment source = candidates[sourceIndex];
                List<float> parameters = new List<float> { 0f, 1f };
                for (int otherIndex = 0;
                    otherIndex < candidates.Count;
                    otherIndex++)
                {
                    if (sourceIndex == otherIndex)
                    {
                        continue;
                    }
                    AddSplitParameters(
                        source,
                        candidates[otherIndex],
                        parameters);
                }
                parameters.Sort();
                for (int index = 1; index < parameters.Count; index++)
                {
                    float from = parameters[index - 1];
                    float to = parameters[index];
                    if (to - from <= GeometryEpsilon)
                    {
                        continue;
                    }
                    Vector2 edge = source.End - source.Start;
                    MorphContourSegment piece = new MorphContourSegment
                    {
                        Block = source.Block,
                        Start = source.Start + edge * from,
                        End = source.Start + edge * to,
                        Normal = source.Normal,
                        RollingSurface = source.RollingSurface
                    };
                    Vector2 middle = (piece.Start + piece.End) * 0.5f;
                    bool outsideOccupied = ContainsPoint(
                        polygons,
                        middle + piece.Normal * ExteriorProbe);
                    bool insideOccupied = ContainsPoint(
                        polygons,
                        middle - piece.Normal * ExteriorProbe);
                    if (outsideOccupied || !insideOccupied)
                    {
                        continue;
                    }
                    int equivalentIndex = FindEquivalent(
                        exterior,
                        piece);
                    if (equivalentIndex >= 0)
                    {
                        MorphContourSegment equivalent =
                            exterior[equivalentIndex];
                        if (!equivalent.RollingSurface
                            && piece.RollingSurface)
                        {
                            // Preserve the real supporting block rather than
                            // the slope vertex that only swept out this span.
                            equivalent.Block = piece.Block;
                        }
                        equivalent.RollingSurface |= piece.RollingSurface;
                        exterior[equivalentIndex] = equivalent;
                        continue;
                    }
                    exterior.Add(piece);
                }
            }
            return exterior;
        }


        private static List<Vector2> GetExpandedPolygon(
            IBlock block,
            int bodyWidth,
            int bodyHeight)
        {
            List<Vector2> polygon = GetSolidPolygon(block);
            if (polygon.Count < 3)
            {
                return new List<Vector2>();
            }
            List<Vector2> expanded = new List<Vector2>();
            Vector2[] offsets =
            {
                new Vector2(-bodyWidth, -bodyHeight),
                new Vector2(0f, -bodyHeight),
                Vector2.Zero,
                new Vector2(-bodyWidth, 0f)
            };
            foreach (Vector2 vertex in polygon)
            {
                foreach (Vector2 offset in offsets)
                {
                    expanded.Add(vertex + offset);
                }
            }
            return ConvexHull(expanded);
        }

        private static void AddPolygonSegments(
            IBlock block,
            List<Vector2> polygon,
            List<MorphContourSegment> destination)
        {
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 start = polygon[index];
                Vector2 end = polygon[(index + 1) % polygon.Count];
                Vector2 edge = end - start;
                Vector2 normal;
                if (!SurfaceMotion.TryNormalize(
                    new Vector2(edge.Y, -edge.X),
                    out normal))
                {
                    continue;
                }
                destination.Add(new MorphContourSegment
                {
                    Block = block,
                    Start = start,
                    End = end,
                    Normal = normal,
                    RollingSurface = IsNativeRollingSurface(
                        block,
                        normal)
                });
            }
        }

        private static bool IsNativeRollingSurface(
            IBlock block,
            Vector2 normal)
        {
            if (normal.Y >= -0.05f)
            {
                return false;
            }
            SlopeBlock slope = block as SlopeBlock;
            if (slope == null)
            {
                return true;
            }
            SlopeType type = slope.GetSlopeType();
            if (type != SlopeType.TopLeft
                && type != SlopeType.TopRight)
            {
                return true;
            }
            Vector2 slopeNormal;
            return SurfaceMotion.TryNormalize(
                    slope.GetNormal(),
                    out slopeNormal)
                && Vector2.Dot(normal, slopeNormal) > 0.999f;
        }

        private static void AddSplitParameters(
            MorphContourSegment source,
            MorphContourSegment other,
            List<float> destination)
        {
            Vector2 sourceEdge = source.End - source.Start;
            Vector2 otherEdge = other.End - other.Start;
            float sourceLengthSquared = sourceEdge.LengthSquared();
            if (sourceLengthSquared <= GeometryEpsilon)
            {
                return;
            }
            float denominator = Cross(sourceEdge, otherEdge);
            Vector2 delta = other.Start - source.Start;
            if (Math.Abs(denominator) > GeometryEpsilon)
            {
                float sourceParameter = Cross(delta, otherEdge)
                    / denominator;
                float otherParameter = Cross(delta, sourceEdge)
                    / denominator;
                if (sourceParameter > GeometryEpsilon
                    && sourceParameter < 1f - GeometryEpsilon
                    && otherParameter >= -GeometryEpsilon
                    && otherParameter <= 1f + GeometryEpsilon)
                {
                    AddUniqueParameter(destination, sourceParameter);
                }
                return;
            }
            if (Math.Abs(Cross(delta, sourceEdge)) > GeometryEpsilon)
            {
                return;
            }
            AddProjectedParameter(
                source,
                sourceLengthSquared,
                other.Start,
                destination);
            AddProjectedParameter(
                source,
                sourceLengthSquared,
                other.End,
                destination);
        }

        private static void AddProjectedParameter(
            MorphContourSegment source,
            float lengthSquared,
            Vector2 point,
            List<float> destination)
        {
            float parameter = Vector2.Dot(
                point - source.Start,
                source.End - source.Start) / lengthSquared;
            if (parameter > GeometryEpsilon
                && parameter < 1f - GeometryEpsilon)
            {
                AddUniqueParameter(destination, parameter);
            }
        }

        private static void AddUniqueParameter(
            List<float> destination,
            float parameter)
        {
            foreach (float existing in destination)
            {
                if (Math.Abs(existing - parameter) <= GeometryEpsilon)
                {
                    return;
                }
            }
            destination.Add(parameter);
        }

        private static bool ContainsPoint(
            List<List<Vector2>> polygons,
            Vector2 point)
        {
            foreach (List<Vector2> polygon in polygons)
            {
                bool inside = true;
                for (int index = 0; index < polygon.Count; index++)
                {
                    Vector2 edge = polygon[(index + 1) % polygon.Count]
                        - polygon[index];
                    if (Cross(edge, point - polygon[index])
                        < -GeometryEpsilon)
                    {
                        inside = false;
                        break;
                    }
                }
                if (inside)
                {
                    return true;
                }
            }
            return false;
        }

        private static int FindEquivalent(
            List<MorphContourSegment> segments,
            MorphContourSegment candidate)
        {
            for (int index = 0; index < segments.Count; index++)
            {
                MorphContourSegment segment = segments[index];
                if ((Near(segment.Start, candidate.Start, GeometryEpsilon)
                        && Near(segment.End, candidate.End, GeometryEpsilon))
                    || (Near(segment.Start, candidate.End, GeometryEpsilon)
                        && Near(segment.End, candidate.Start, GeometryEpsilon)))
                {
                    return index;
                }
            }
            return -1;
        }

    }
}
