using System;
using System.Collections.Generic;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal static partial class MorphContourGeometry
    {
        internal static void AddSegments(
            IBlock block,
            int bodyWidth,
            int bodyHeight,
            List<MorphContourSegment> destination)
        {
            if (block == null || bodyWidth <= 0 || bodyHeight <= 0)
            {
                return;
            }
            AddPolygonSegments(
                block,
                GetExpandedPolygon(block, bodyWidth, bodyHeight),
                destination);
        }

        internal static bool TryFindSegment(
            List<MorphContourSegment> segments,
            MorphContourSegment previous,
            Vector2 position,
            out MorphContourSegment result)
        {
            return TryFindSegment(
                segments,
                previous,
                position,
                false,
                out result);
        }

        internal static bool TryFindSegment(
            List<MorphContourSegment> segments,
            MorphContourSegment previous,
            Vector2 position,
            bool rollingSurfacesOnly,
            out MorphContourSegment result)
        {
            bool found = false;
            float bestScore = float.MinValue;
            result = default(MorphContourSegment);
            foreach (MorphContourSegment segment in segments)
            {
                if (rollingSurfacesOnly && !segment.RollingSurface)
                {
                    continue;
                }
                float distance = DistanceSquared(segment, position);
                if (distance > 0.25f)
                {
                    continue;
                }
                float continuity = Vector2.Dot(
                    previous.Normal,
                    segment.Normal);
                float score = continuity * 16f - distance;
                if (score <= bestScore)
                {
                    continue;
                }
                found = true;
                bestScore = score;
                result = segment;
            }
            return found;
        }

        internal static bool TrySelectContact(
            List<MorphContourSegment> segments,
            Vector2 position,
            float reach,
            out MorphContourSegment result)
        {
            return TrySelectContact(
                segments,
                position,
                reach,
                false,
                out result);
        }

        internal static bool TrySelectContact(
            List<MorphContourSegment> segments,
            Vector2 position,
            float reach,
            bool rollingSurfacesOnly,
            out MorphContourSegment result)
        {
            bool found = false;
            float bestDistance = float.MaxValue;
            result = default(MorphContourSegment);
            foreach (MorphContourSegment segment in segments)
            {
                if (rollingSurfacesOnly && !segment.RollingSurface)
                {
                    continue;
                }
                float distance = DistanceSquared(segment, position);
                if (distance > reach * reach
                    || !IsExteriorContact(
                        segments,
                        segment,
                        position,
                        reach)
                    || distance >= bestDistance)
                {
                    continue;
                }
                found = true;
                bestDistance = distance;
                result = segment;
            }
            return found;
        }

        internal static bool TrySelectOrientedContact(
            List<MorphContourSegment> segments,
            Vector2 position,
            Vector2 normal,
            float reach,
            out MorphContourSegment result)
        {
            Vector2 unitNormal;
            result = default(MorphContourSegment);
            if (!SurfaceMotion.TryNormalize(normal, out unitNormal))
            {
                return false;
            }
            bool found = false;
            float bestDistance = float.MaxValue;
            foreach (MorphContourSegment segment in segments)
            {
                if (Vector2.Dot(segment.Normal, unitNormal) < 0.999f)
                {
                    continue;
                }
                float distance = DistanceSquared(segment, position);
                if (distance > reach * reach || distance >= bestDistance)
                {
                    continue;
                }
                found = true;
                bestDistance = distance;
                result = segment;
            }
            return found;
        }

        internal static bool TrySelectContinuousContact(
            List<MorphContourSegment> segments,
            Vector2 position,
            Vector2 previousNormal,
            float reach,
            out MorphContourSegment result)
        {
            return TrySelectContinuousContact(
                segments,
                position,
                previousNormal,
                reach,
                false,
                out result);
        }

        internal static bool TrySelectContinuousContact(
            List<MorphContourSegment> segments,
            Vector2 position,
            Vector2 previousNormal,
            float reach,
            bool rollingSurfacesOnly,
            out MorphContourSegment result)
        {
            Vector2 unitNormal;
            result = default(MorphContourSegment);
            if (!SurfaceMotion.TryNormalize(previousNormal, out unitNormal))
            {
                return false;
            }
            bool found = false;
            float bestScore = float.MinValue;
            foreach (MorphContourSegment candidate in segments)
            {
                if (rollingSurfacesOnly && !candidate.RollingSurface)
                {
                    continue;
                }
                float continuity = Vector2.Dot(
                    candidate.Normal,
                    unitNormal);
                float distance = DistanceSquared(candidate, position);
                if (continuity < 0.5f
                    || distance > reach * reach
                    || !IsExteriorContact(
                        segments,
                        candidate,
                        position,
                        reach))
                {
                    continue;
                }
                float score = continuity * 8f - distance;
                if (score <= bestScore)
                {
                    continue;
                }
                found = true;
                bestScore = score;
                result = candidate;
            }
            return found;
        }

        internal static bool TrySweepContact(
            List<MorphContourSegment> segments,
            Vector2 start,
            Vector2 displacement,
            out MorphContourSegment result,
            out float fraction)
        {
            bool found = false;
            float bestFraction = float.MaxValue;
            float bestApproach = float.MinValue;
            result = default(MorphContourSegment);
            fraction = 0f;
            if (!SurfaceMotion.IsFinite(start)
                || !SurfaceMotion.IsFinite(displacement)
                || displacement.LengthSquared() <= GeometryEpsilon)
            {
                return false;
            }
            foreach (MorphContourSegment segment in segments)
            {
                float approach = -Vector2.Dot(
                    displacement,
                    segment.Normal);
                if (approach <= GeometryEpsilon)
                {
                    continue;
                }
                float startDistance = Vector2.Dot(
                    start - segment.Start,
                    segment.Normal);
                if (startDistance < -GeometryEpsilon
                    || startDistance - approach > GeometryEpsilon)
                {
                    continue;
                }
                float candidateFraction = Math.Max(
                    0f,
                    Math.Min(1f, startDistance / approach));
                Vector2 contact = start
                    + displacement * candidateFraction;
                if (DistanceSquared(segment, contact) > 0.01f
                    || candidateFraction
                        > bestFraction + GeometryEpsilon
                    || (Math.Abs(candidateFraction - bestFraction)
                            <= GeometryEpsilon
                        && approach <= bestApproach))
                {
                    continue;
                }
                found = true;
                bestFraction = candidateFraction;
                bestApproach = approach;
                result = segment;
            }
            if (found)
            {
                fraction = bestFraction;
            }
            return found;
        }

        internal static bool TryAdvance(
            List<MorphContourSegment> segments,
            MorphContourSegment current,
            Vector2 position,
            float distance,
            out Vector2 resultPosition,
            out MorphContourSegment resultSegment)
        {
            return TryAdvance(
                segments,
                current,
                position,
                distance,
                false,
                out resultPosition,
                out resultSegment);
        }

        internal static bool TryAdvance(
            List<MorphContourSegment> segments,
            MorphContourSegment current,
            Vector2 position,
            float distance,
            bool topSurfacesOnly,
            out Vector2 resultPosition,
            out MorphContourSegment resultSegment)
        {
            float untraversedDistance;
            return TryAdvance(
                segments,
                current,
                position,
                distance,
                topSurfacesOnly,
                out resultPosition,
                out resultSegment,
                out untraversedDistance);
        }

        internal static bool TryAdvance(
            List<MorphContourSegment> segments,
            MorphContourSegment current,
            Vector2 position,
            float distance,
            bool topSurfacesOnly,
            out Vector2 resultPosition,
            out MorphContourSegment resultSegment,
            out float untraversedDistance)
        {
            bool movementBlocked;
            return TryAdvanceRolling(
                segments,
                current,
                position,
                distance,
                topSurfacesOnly,
                0,
                out resultPosition,
                out resultSegment,
                out untraversedDistance,
                out movementBlocked);
        }

        internal static bool TryAdvanceRolling(
            List<MorphContourSegment> segments,
            MorphContourSegment current,
            Vector2 position,
            float distance,
            bool topSurfacesOnly,
            int inputDirection,
            out Vector2 resultPosition,
            out MorphContourSegment resultSegment,
            out float untraversedDistance,
            out bool movementBlocked)
        {
            resultPosition = position;
            resultSegment = current;
            untraversedDistance = Math.Abs(distance);
            movementBlocked = false;
            if (float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return false;
            }
            if (topSurfacesOnly && !current.RollingSurface)
            {
                return false;
            }
            int direction = Math.Sign(distance);
            float remaining = Math.Abs(distance);
            if (direction == 0)
            {
                return true;
            }
            for (int transition = 0; transition <= segments.Count; transition++)
            {
                Vector2 terminal = direction > 0
                    ? resultSegment.End
                    : resultSegment.Start;
                Vector2 tangent = resultSegment.Tangent * direction;
                float available = Math.Max(
                    0f,
                    Vector2.Dot(terminal - resultPosition, tangent));
                if (remaining <= available + GeometryEpsilon)
                {
                    resultPosition += tangent * remaining;
                    untraversedDistance = 0f;
                    return SurfaceMotion.IsFinite(resultPosition);
                }
                resultPosition = terminal;
                remaining -= available;
                MorphContourSegment next;
                if (!TryGetContinuation(
                    segments,
                    resultSegment,
                    terminal,
                    direction,
                    topSurfacesOnly,
                    out next))
                {
                    untraversedDistance = remaining;
                    return remaining <= GeometryEpsilon;
                }
                if (topSurfacesOnly
                    && IsControlledUphillEntry(
                        next,
                        direction,
                        inputDirection))
                {
                    movementBlocked = true;
                    untraversedDistance = remaining;
                    return false;
                }
                resultSegment = next;
            }
            untraversedDistance = remaining;
            return false;
        }

        internal static bool IsExteriorContact(
            List<MorphContourSegment> segments,
            MorphContourSegment segment,
            Vector2 position,
            float reach)
        {
            if (DistanceSquared(segment, position) > reach * reach)
            {
                return false;
            }
            Vector2 closest = ClosestPoint(segment, position);
            Vector2 delta = position - closest;
            return Vector2.Dot(delta, segment.Normal) >= -0.1f;
        }

        internal static Vector2 ClosestPoint(
            MorphContourSegment segment,
            Vector2 point)
        {
            Vector2 edge = segment.End - segment.Start;
            float lengthSquared = edge.LengthSquared();
            if (lengthSquared <= GeometryEpsilon)
            {
                return segment.Start;
            }
            float parameter = Vector2.Dot(
                point - segment.Start,
                edge) / lengthSquared;
            parameter = Math.Max(0f, Math.Min(1f, parameter));
            return segment.Start + edge * parameter;
        }

        internal static float DistanceSquared(
            MorphContourSegment segment,
            Vector2 point)
        {
            return (ClosestPoint(segment, point) - point).LengthSquared();
        }

        internal static bool ContainsPoint(
            MorphContourSegment segment,
            Vector2 point,
            float tolerance)
        {
            return DistanceSquared(segment, point)
                <= tolerance * tolerance;
        }

        internal static bool SameSegment(
            MorphContourSegment first,
            MorphContourSegment second,
            float tolerance)
        {
            return ReferenceEquals(first.Block, second.Block)
                && ((Near(first.Start, second.Start, tolerance)
                        && Near(first.End, second.End, tolerance))
                    || (Near(first.Start, second.End, tolerance)
                        && Near(first.End, second.Start, tolerance)));
        }


        private static bool TryGetContinuation(
            List<MorphContourSegment> segments,
            MorphContourSegment current,
            Vector2 terminal,
            int direction,
            bool topSurfacesOnly,
            out MorphContourSegment result)
        {
            bool found = false;
            float bestTurn = float.MaxValue;
            float bestContinuity = float.MinValue;
            result = default(MorphContourSegment);
            Vector2 travel = current.Tangent * direction;
            foreach (MorphContourSegment candidate in segments)
            {
                if (topSurfacesOnly && !candidate.RollingSurface)
                {
                    continue;
                }
                if (SameSegment(current, candidate, GeometryEpsilon))
                {
                    continue;
                }
                Vector2 entry = direction > 0
                    ? candidate.Start
                    : candidate.End;
                if (!Near(entry, terminal, 0.01f))
                {
                    continue;
                }
                float continuity = Vector2.Dot(
                    travel,
                    candidate.Tangent * direction);
                float orientedCross = Cross(
                    travel,
                    candidate.Tangent * direction) * direction;
                float turn = (float)Math.Atan2(
                    orientedCross,
                    continuity);
                if (turn < -GeometryEpsilon)
                {
                    turn += MathHelper.TwoPi;
                }
                else if (Math.Abs(turn) <= GeometryEpsilon)
                {
                    turn = 0f;
                }
                if (turn > bestTurn + GeometryEpsilon
                    || (Math.Abs(turn - bestTurn) <= GeometryEpsilon
                        && continuity <= bestContinuity))
                {
                    continue;
                }
                found = true;
                bestTurn = turn;
                bestContinuity = continuity;
                result = candidate;
            }
            return found;
        }

        private static bool IsControlledUphillEntry(
            MorphContourSegment next,
            int contourDirection,
            int inputDirection)
        {
            int uphillDirection =
                SurfaceMotion.GetUphillDirection(next.Normal);
            if (uphillDirection == 0 || inputDirection != uphillDirection)
            {
                return false;
            }
            Vector2 travel = next.Tangent * contourDirection;
            return Math.Sign(travel.X) == uphillDirection;
        }

    }
}
