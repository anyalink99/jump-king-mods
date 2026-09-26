using System;
using System.Collections.Generic;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal struct MorphSurfaceState
    {
        internal MorphSurface Surface;
        internal Vector2 Normal;
        internal IBlock Block;
        internal MorphContourSegment Segment;
        internal bool HasSegment;
        internal bool MovementBlocked;

        internal bool Attached
        {
            get { return Surface != MorphSurface.None; }
        }
    }

    internal static class MorphSurfaceFollower
    {
        private const float MaximumStep = 0.5f;
        private const float MaximumVisualGap = 6f;
        private const int ContactReach = 2;

        internal static MorphSurfaceState Resolve(
            BodyComp body,
            bool sticky,
            bool supportedBySand)
        {
            return Resolve(
                body,
                sticky,
                supportedBySand,
                false);
        }

        internal static MorphSurfaceState ResolveStickySurface(BodyComp body)
        {
            return Resolve(
                body,
                true,
                false,
                true);
        }

        internal static bool TryResolveSweep(
            BodyComp body,
            Vector2 start,
            Vector2 displacement,
            bool stickySurfacesOnly,
            out MorphSurfaceState state,
            out Vector2 contactPosition)
        {
            state = default(MorphSurfaceState);
            contactPosition = start;
            if (body == null
                || !SurfaceMotion.IsFinite(start)
                || !SurfaceMotion.IsFinite(displacement)
                || displacement.LengthSquared() <= 0.0001f)
            {
                return false;
            }
            List<MorphContourSegment> segments = MorphCollisionWorld.GatherContourSegments(
                body,
                start,
                start + displacement,
                null,
                stickySurfacesOnly);
            MorphContourSegment contact;
            float fraction;
            if (!MorphContourGeometry.TrySweepContact(
                segments,
                start,
                displacement,
                out contact,
                out fraction))
            {
                return false;
            }
            contactPosition = start + displacement * fraction;
            state = Create(contact);
            return true;
        }

        private static MorphSurfaceState Resolve(
            BodyComp body,
            bool sticky,
            bool supportedBySand,
            bool stickySurfacesOnly)
        {
            if (body == null)
            {
                throw new ArgumentNullException("body");
            }

            MorphSurfaceState contact;
            if (TryResolveContourContact(
                body,
                stickySurfacesOnly,
                !sticky,
                out contact))
            {
                if (sticky || contact.Normal.Y < -0.25f)
                {
                    return contact;
                }
            }
            if (supportedBySand)
            {
                return Create(new Vector2(0f, -1f), null);
            }
            return default(MorphSurfaceState);
        }

        internal static MorphSurfaceState Acquire(
            BodyComp body,
            bool sticky)
        {
            return Resolve(
                body,
                sticky,
                false,
                false);
        }

        internal static MorphSurfaceState MoveSticky(
            BodyComp body,
            MorphSurfaceState state,
            float distance,
            bool stickySurfacesOnly)
        {
            return MoveContourSurface(
                body,
                state,
                distance,
                stickySurfacesOnly,
                false,
                0);
        }

        internal static MorphSurfaceState MoveRollingSurface(
            BodyComp body,
            MorphSurfaceState state,
            float distance,
            int inputDirection)
        {
            return MoveContourSurface(
                body,
                state,
                distance,
                false,
                true,
                inputDirection);
        }

        internal static MorphSurfaceState ResolveRollingSurface(
            BodyComp body,
            Vector2 normal,
            IBlock block)
        {
            if (body == null)
            {
                return default(MorphSurfaceState);
            }
            List<MorphContourSegment> segments = MorphCollisionWorld.GatherContourSegments(
                body,
                body.Position,
                body.Position,
                block,
                false);
            MorphContourSegment contact;
            if (!MorphContourGeometry.TrySelectOrientedContact(
                segments,
                body.Position,
                normal,
                MaximumVisualGap,
                out contact))
            {
                return default(MorphSurfaceState);
            }
            body.Position = MorphContourGeometry.ClosestPoint(
                contact,
                body.Position);
            return Create(contact);
        }

        private static bool TryNormalize(
            Vector2 value,
            out Vector2 normalized)
        {
            normalized = Vector2.Zero;
            if (float.IsNaN(value.X)
                || float.IsNaN(value.Y)
                || float.IsInfinity(value.X)
                || float.IsInfinity(value.Y)
                || value.LengthSquared() <= 0.0001f)
            {
                return false;
            }
            normalized = Vector2.Normalize(value);
            return true;
        }

        internal static Vector2 GetRollingVisualContactOffset(
            BodyComp body,
            Vector2 surfaceNormal,
            float radius)
        {
            if (body == null
                || surfaceNormal == Vector2.Zero
                || radius <= 0f)
            {
                return Vector2.Zero;
            }
            Vector2 normal;
            if (!TryNormalize(surfaceNormal, out normal))
            {
                return Vector2.Zero;
            }
            Rectangle hitbox = body.GetHitbox();
            Vector2 offset = SurfaceMotion.AabbToCircleContactOffset(
                normal,
                hitbox.Width / 2f,
                hitbox.Height / 2f,
                radius);
            return offset;
        }

        internal static Vector2 GetStickyVisualContactOffset(
            BodyComp body,
            IBlock activeBlock,
            Vector2 surfaceNormal,
            float radius)
        {
            if (body == null
                || surfaceNormal == Vector2.Zero
                || radius <= 0f)
            {
                return Vector2.Zero;
            }
            Rectangle hitbox = body.GetHitbox();
            Vector2 center = new Vector2(
                hitbox.Left + hitbox.Width / 2f,
                hitbox.Top + hitbox.Height / 2f);
            Vector2 offset;
            if (TryGetMeasuredContactOffset(
                activeBlock,
                center,
                surfaceNormal,
                radius,
                out offset))
            {
                return offset;
            }

            Rectangle search = hitbox;
            search.Inflate(
                (int)Math.Ceiling(MaximumVisualGap),
                (int)Math.Ceiling(MaximumVisualGap));
            AdvCollisionInfo collision = LevelManager.GetCollisionInfo(search);
            IReadOnlyList<IBlock> blocks = collision == null
                ? null
                : collision.GetCollidedBlocks();
            if (blocks == null)
            {
                return Vector2.Zero;
            }
            bool found = false;
            Vector2 closest = Vector2.Zero;
            float closestDistance = float.MaxValue;
            foreach (IBlock block in blocks)
            {
                if (!MorphCollisionWorld.IsBlockingContact(block, search))
                {
                    continue;
                }
                Vector2 candidate;
                if (!TryGetMeasuredContactOffset(
                    block,
                    center,
                    surfaceNormal,
                    radius,
                    out candidate))
                {
                    continue;
                }
                float distance = candidate.LengthSquared();
                if (distance >= closestDistance)
                {
                    continue;
                }
                found = true;
                closest = candidate;
                closestDistance = distance;
            }
            return found ? closest : Vector2.Zero;
        }

        internal static bool TryGetNativeContactPose(
            BodyComp body,
            Vector2 logicalPosition,
            Vector2 logicalNormal,
            Vector2 preferredOffset,
            out Vector2 nativePosition)
        {
            nativePosition = logicalPosition;
            if (!HasNativeBlockingCollision(body, logicalPosition))
            {
                return true;
            }
            Vector2 preferred = logicalPosition + preferredOffset;
            if (preferredOffset != Vector2.Zero
                && SurfaceMotion.IsFinite(preferred)
                && !HasNativeBlockingCollision(body, preferred))
            {
                nativePosition = preferred;
                return true;
            }

            Vector2 separationNormal;
            if (!SurfaceMotion.TryNormalize(
                logicalNormal,
                out separationNormal))
            {
                return false;
            }

            // The logical union contour is authoritative, but Jump King's
            // collision pass still needs a temporary non-overlapping sensor
            // pose for block behaviours.  At compound corners (and at the
            // executable's asymmetric bottom-slope triangle) the nearest
            // native-free pose is not necessarily on the selected face's
            // exact normal. Search the outward half-plane by distance so a
            // first contact gets the same stable sensor as an established
            // contact, without slope-type exceptions.
            const float separationStep = 0.5f;
            const int maximumSteps = 64;
            for (int step = 1; step <= maximumSteps; step++)
            {
                float distance = step * separationStep;
                Vector2 direct = logicalPosition
                    + separationNormal * distance;
                if (!HasNativeBlockingCollision(body, direct))
                {
                    nativePosition = direct;
                    return true;
                }
                const int directionCount = 16;
                for (int directionIndex = 0;
                    directionIndex < directionCount;
                    directionIndex++)
                {
                    float angle = MathHelper.TwoPi
                        * directionIndex / directionCount;
                    Vector2 direction = new Vector2(
                        (float)Math.Cos(angle),
                        (float)Math.Sin(angle));
                    if (Vector2.Dot(direction, separationNormal) < 0.1f)
                    {
                        continue;
                    }
                    Vector2 candidate = logicalPosition
                        + direction * distance;
                    if (!HasNativeBlockingCollision(body, candidate))
                    {
                        nativePosition = candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryGetMeasuredContactOffset(
            IBlock block,
            Vector2 center,
            Vector2 surfaceNormal,
            float radius,
            out Vector2 offset)
        {
            offset = Vector2.Zero;
            if (block == null)
            {
                return false;
            }
            SlopeBlock slope = block as SlopeBlock;
            Vector2 slopeNormal = Vector2.Zero;
            if (slope != null
                && !SurfaceMotion.TryNormalize(
                    slope.GetNormal(),
                    out slopeNormal))
            {
                return false;
            }
            Vector2 contactPoint = slope == null
                ? SurfaceMotion.ClosestPointOnRectangle(
                    block.GetRect(),
                    center)
                : SurfaceMotion.ClosestPointOnSlope(
                    block.GetRect(),
                    slopeNormal,
                    center);
            return SurfaceMotion.TryGetContactOffset(
                center,
                contactPoint,
                surfaceNormal,
                radius,
                MaximumVisualGap,
                out offset);
        }

        private static MorphSurfaceState MoveContourSurface(
            BodyComp body,
            MorphSurfaceState state,
            float distance,
            bool stickySurfacesOnly,
            bool topSurfacesOnly,
            int rollingInputDirection)
        {
            if (!state.Attached || !SurfaceMotion.IsFinite(body.Position)
                || float.IsNaN(distance) || float.IsInfinity(distance))
            {
                body.Velocity = Vector2.Zero;
                return default(MorphSurfaceState);
            }
            if (topSurfacesOnly && state.Normal.Y >= -0.05f)
            {
                body.Velocity = SurfaceMotion.GetTangent(state.Normal)
                    * distance;
                return default(MorphSurfaceState);
            }

            MorphContourSegment current;
            if (!TrySelectCurrentSegment(
                body,
                state,
                topSurfacesOnly,
                out current))
            {
                body.Velocity = SurfaceMotion.GetTangent(state.Normal)
                    * distance;
                return default(MorphSurfaceState);
            }

            if (Math.Abs(distance) < 0.0001f)
            {
                body.Velocity = Vector2.Zero;
                return Create(current);
            }

            Vector2 initialPosition = body.Position;
            MorphContourSegment initialSegment = current;
            float contourReach = Math.Abs(distance);
            Vector2 contourExtent = new Vector2(
                contourReach,
                contourReach);
            // A path of length D cannot leave this square. Build the static
            // block union once for the complete movement instead of rebuilding
            // the same local topology for every half-pixel integration step.
            List<MorphContourSegment> movementSegments =
                MorphCollisionWorld.GatherContourSegments(
                    body,
                    initialPosition - contourExtent,
                    initialPosition + contourExtent,
                    current.Block,
                    stickySurfacesOnly);
            int stepCount = Math.Max(
                1,
                (int)Math.Ceiling(Math.Abs(distance) / MaximumStep));
            float signedStep = distance / stepCount;
            for (int step = 0; step < stepCount; step++)
            {
                Vector2 nextPosition;
                MorphContourSegment nextSegment;
                float untraversedStep;
                bool movementBlocked;
                if (!TryFollowContourStep(
                    body,
                    movementSegments,
                    current,
                    signedStep,
                    topSurfacesOnly,
                    rollingInputDirection,
                    out nextPosition,
                    out nextSegment,
                    out untraversedStep,
                    out movementBlocked))
                {
                    if (movementBlocked)
                    {
                        body.Position = nextPosition;
                        body.Velocity = Vector2.Zero;
                        MorphSurfaceState blocked = Create(nextSegment);
                        blocked.MovementBlocked = true;
                        return blocked;
                    }
                    if (topSurfacesOnly
                        && SurfaceMotion.IsFinite(nextPosition)
                        && untraversedStep >= 0f)
                    {
                        body.Position = nextPosition;
                        float remainingDistance = untraversedStep
                            + (stepCount - step - 1)
                                * Math.Abs(signedStep);
                        body.Velocity = nextSegment.Tangent
                            * Math.Sign(distance)
                            * remainingDistance;
                        return default(MorphSurfaceState);
                    }
                    body.Position = initialPosition;
                    body.Velocity = initialSegment.Tangent * distance;
                    return default(MorphSurfaceState);
                }
                body.Position = nextPosition;
                current = nextSegment;
            }
            body.Velocity = Vector2.Zero;
            return Create(current);
        }

        private static bool TryFollowContourStep(
            BodyComp body,
            List<MorphContourSegment> segments,
            MorphContourSegment current,
            float signedStep,
            bool topSurfacesOnly,
            int rollingInputDirection,
            out Vector2 nextPosition,
            out MorphContourSegment nextSegment,
            out float untraversedDistance,
            out bool movementBlocked)
        {
            Vector2 origin = body.Position;
            movementBlocked = false;
            untraversedDistance = Math.Abs(signedStep);
            MorphContourSegment refreshed;
            if (!MorphContourGeometry.TryFindSegment(
                segments,
                current,
                origin,
                topSurfacesOnly,
                out refreshed))
            {
                nextPosition = origin;
                nextSegment = current;
                return false;
            }
            if (!MorphContourGeometry.TryAdvanceRolling(
                segments,
                refreshed,
                origin,
                signedStep,
                topSurfacesOnly,
                rollingInputDirection,
                out nextPosition,
                out nextSegment,
                out untraversedDistance,
                out movementBlocked))
            {
                return false;
            }
            bool valid = IsContourPoseValid(body, nextPosition, segments);
            if (valid)
            {
                untraversedDistance = 0f;
            }
            else
            {
                untraversedDistance = -1f;
            }
            return valid;
        }

        private static bool TryResolveContourContact(
            BodyComp body,
            bool stickySurfacesOnly,
            bool rollingSurfacesOnly,
            out MorphSurfaceState state)
        {
            Vector2 position = body.Position;
            List<MorphContourSegment> segments = MorphCollisionWorld.GatherContourSegments(
                body,
                position,
                position,
                null,
                stickySurfacesOnly);
            MorphContourSegment best;
            bool found = MorphContourGeometry.TrySelectContact(
                segments,
                position,
                ContactReach,
                rollingSurfacesOnly,
                out best);
            state = found
                ? Create(best)
                : default(MorphSurfaceState);
            return found;
        }

        private static bool TrySelectCurrentSegment(
            BodyComp body,
            MorphSurfaceState state,
            bool rollingSurfacesOnly,
            out MorphContourSegment segment)
        {
            List<MorphContourSegment> candidates = MorphCollisionWorld.GatherContourSegments(
                body,
                body.Position,
                body.Position,
                state.Block,
                false);
            if (state.HasSegment
                && MorphContourGeometry.TryFindSegment(
                    candidates,
                    state.Segment,
                    body.Position,
                    rollingSurfacesOnly,
                    out segment))
            {
                return true;
            }
            return MorphContourGeometry.TrySelectContinuousContact(
                candidates,
                body.Position,
                state.Normal,
                ContactReach,
                rollingSurfacesOnly,
                out segment);
        }


        private static bool IsContourPoseValid(
            BodyComp body,
            Vector2 position,
            List<MorphContourSegment> nearbySegments)
        {
            if (!SurfaceMotion.IsFinite(position))
            {
                return false;
            }
            foreach (MorphContourSegment segment in nearbySegments)
            {
                if (MorphContourGeometry.ContainsPoint(
                    segment,
                    position,
                    0.1f))
                {
                    return true;
                }
            }
            return false;
        }


        private static bool HasNativeBlockingCollision(
            BodyComp body,
            Vector2 position)
        {
            Rectangle hitbox = GetHitboxAt(body, position);
            AdvCollisionInfo collision = LevelManager.GetCollisionInfo(hitbox);
            IReadOnlyList<IBlock> blocks = collision == null
                ? null
                : collision.GetCollidedBlocks();
            if (blocks == null)
            {
                return false;
            }
            foreach (IBlock block in blocks)
            {
                if (MorphCollisionWorld.IsBlockingContact(block, hitbox))
                {
                    return true;
                }
            }
            return false;
        }

        private static Rectangle GetHitboxAt(
            BodyComp body,
            Vector2 position)
        {
            Vector2 original = body.Position;
            body.Position = position;
            Rectangle hitbox = body.GetHitbox();
            body.Position = original;
            return hitbox;
        }


        private static MorphSurfaceState Create(
            Vector2 normal,
            IBlock block)
        {
            Vector2 unitNormal;
            if (!TryNormalize(normal, out unitNormal))
            {
                return default(MorphSurfaceState);
            }
            return new MorphSurfaceState
            {
                Surface = SurfaceMotion.FromNormal(unitNormal),
                Normal = unitNormal,
                Block = block
            };
        }

        private static MorphSurfaceState Create(MorphContourSegment segment)
        {
            return new MorphSurfaceState
            {
                Surface = SurfaceMotion.FromNormal(segment.Normal),
                Normal = segment.Normal,
                Block = segment.Block,
                Segment = segment,
                HasSegment = true
            };
        }
    }
}
