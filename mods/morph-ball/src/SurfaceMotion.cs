using System;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal enum MorphSurface
    {
        None,
        Ground,
        RightWall,
        Ceiling,
        LeftWall
    }

    internal static class SurfaceMotion
    {
        internal static Vector2 GetNormal(MorphSurface surface)
        {
            switch (surface)
            {
                case MorphSurface.Ground:
                    return new Vector2(0f, -1f);
                case MorphSurface.RightWall:
                    return new Vector2(-1f, 0f);
                case MorphSurface.Ceiling:
                    return new Vector2(0f, 1f);
                case MorphSurface.LeftWall:
                    return new Vector2(1f, 0f);
                default:
                    return Vector2.Zero;
            }
        }

        internal static Vector2 GetTangent(MorphSurface surface)
        {
            return GetTangent(GetNormal(surface));
        }

        internal static Vector2 GetTangent(Vector2 normal)
        {
            return !IsFinite(normal) || normal == Vector2.Zero
                ? Vector2.Zero
                : new Vector2(-normal.Y, normal.X);
        }

        internal static MorphSurface FromNormal(Vector2 normal)
        {
            if (!IsFinite(normal) || normal == Vector2.Zero)
            {
                return MorphSurface.None;
            }
            if (Math.Abs(normal.X) >= Math.Abs(normal.Y))
            {
                return normal.X >= 0f
                    ? MorphSurface.LeftWall
                    : MorphSurface.RightWall;
            }
            return normal.Y >= 0f
                ? MorphSurface.Ceiling
                : MorphSurface.Ground;
        }

        internal static Vector2 ClosestPointOnRectangle(
            Rectangle rectangle,
            Vector2 point)
        {
            return new Vector2(
                Math.Max(rectangle.Left, Math.Min(rectangle.Right, point.X)),
                Math.Max(rectangle.Top, Math.Min(rectangle.Bottom, point.Y)));
        }

        internal static Vector2 ClosestPointOnSlope(
            Rectangle rectangle,
            Vector2 surfaceNormal,
            Vector2 point)
        {
            Vector2 normal;
            if (!TryNormalize(surfaceNormal, out normal))
            {
                return ClosestPointOnRectangle(rectangle, point);
            }
            Vector2 center = new Vector2(
                rectangle.Left + rectangle.Width / 2f,
                rectangle.Top + rectangle.Height / 2f);
            Vector2[] corners =
            {
                new Vector2(rectangle.Left, rectangle.Top),
                new Vector2(rectangle.Right, rectangle.Top),
                new Vector2(rectangle.Right, rectangle.Bottom),
                new Vector2(rectangle.Left, rectangle.Bottom)
            };
            float plane = Vector2.Dot(normal, center);
            int first = 0;
            int second = 1;
            float firstDistance = float.MaxValue;
            float secondDistance = float.MaxValue;
            for (int index = 0; index < corners.Length; index++)
            {
                float distance = Math.Abs(
                    Vector2.Dot(normal, corners[index]) - plane);
                if (distance < firstDistance)
                {
                    second = first;
                    secondDistance = firstDistance;
                    first = index;
                    firstDistance = distance;
                }
                else if (distance < secondDistance)
                {
                    second = index;
                    secondDistance = distance;
                }
            }
            Vector2 start = corners[first];
            Vector2 segment = corners[second] - start;
            float lengthSquared = segment.LengthSquared();
            if (lengthSquared <= 0.0001f)
            {
                return start;
            }
            float amount = Vector2.Dot(point - start, segment)
                / lengthSquared;
            amount = Math.Max(0f, Math.Min(1f, amount));
            return start + segment * amount;
        }

        internal static bool TryGetContactOffset(
            Vector2 center,
            Vector2 contactPoint,
            Vector2 surfaceNormal,
            float radius,
            float maximumGap,
            out Vector2 offset)
        {
            offset = Vector2.Zero;
            Vector2 towardSurface = contactPoint - center;
            float distance = towardSurface.Length();
            if (distance <= 0.0001f || radius <= 0f)
            {
                return false;
            }
            Vector2 direction = towardSurface / distance;
            Vector2 normal;
            if (!TryNormalize(surfaceNormal, out normal))
            {
                normal = -direction;
            }
            if (Vector2.Dot(direction, -normal) < 0.35f)
            {
                return false;
            }
            float normalDistance = Vector2.Dot(
                towardSurface,
                -normal);
            float gap = Math.Max(0f, normalDistance - radius);
            if (gap > maximumGap)
            {
                return false;
            }
            // Contact correction belongs to the surface normal. Using the
            // point-to-centre direction adds a tangential component at a
            // rectangle corner; on the last grounded frame that visually
            // pulls a rolling ball back toward the ledge before releasing it.
            offset = -normal * gap;
            return true;
        }

        internal static int GetUphillDirection(Vector2 surfaceNormal)
        {
            if (Math.Abs(surfaceNormal.X) <= 0.05f
                || surfaceNormal.Y >= -0.05f)
            {
                return 0;
            }
            return surfaceNormal.X > 0f ? -1 : 1;
        }

        internal static Vector2 AabbToCircleContactOffset(
            Vector2 surfaceNormal,
            float halfWidth,
            float halfHeight,
            float circleRadius)
        {
            Vector2 normal;
            if (!TryNormalize(surfaceNormal, out normal)
                || halfWidth <= 0f
                || halfHeight <= 0f
                || circleRadius <= 0f)
            {
                return Vector2.Zero;
            }
            float aabbSupportRadius = Math.Abs(normal.X) * halfWidth
                + Math.Abs(normal.Y) * halfHeight;
            float correction = Math.Max(
                0f,
                aabbSupportRadius - circleRadius);
            return -normal * correction;
        }

        internal static bool IsHorizontalGround(Vector2 normal)
        {
            Vector2 unitNormal;
            return TryNormalize(normal, out unitNormal)
                && Math.Abs(unitNormal.X) <= 0.001f
                && unitNormal.Y < -0.999f;
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y);
        }

        internal static bool TryNormalize(
            Vector2 value,
            out Vector2 normalized)
        {
            normalized = Vector2.Zero;
            if (!IsFinite(value) || value.LengthSquared() <= 0.0001f)
            {
                return false;
            }
            normalized = Vector2.Normalize(value);
            return IsFinite(normalized);
        }
    }

    internal static class MorphSurfaceMomentum
    {
        private const float DriveAcceleration = 0.27f;
        private const float CoastDeceleration = 0.06f;

        internal static float Update(
            float current,
            int direction,
            float maximumSpeed,
            float movementScale)
        {
            if (!SurfaceMotion.IsFinite(current)
                || !SurfaceMotion.IsFinite(maximumSpeed)
                || !SurfaceMotion.IsFinite(movementScale))
            {
                throw new InvalidOperationException(
                    "Ball King surface momentum must be finite");
            }
            float target = direction * maximumSpeed * movementScale;
            float acceleration = direction == 0
                ? CoastDeceleration
                : DriveAcceleration;
            return MoveTowards(
                current,
                target,
                acceleration * movementScale);
        }

        internal static float ProjectLandingSpeed(
            Vector2 velocity,
            Vector2 tangent,
            float maximumSpeed)
        {
            if (!SurfaceMotion.IsFinite(velocity)
                || !SurfaceMotion.IsFinite(tangent)
                || !SurfaceMotion.IsFinite(maximumSpeed))
            {
                throw new InvalidOperationException(
                    "Ball King landing projection must be finite");
            }
            float projected = Vector2.Dot(velocity, tangent);
            return Math.Max(-maximumSpeed, Math.Min(maximumSpeed, projected));
        }

        internal static Vector2 ResolveLandingVelocity(
            bool hasTeleportCarry,
            Vector2 teleportCarry,
            bool stickyMode,
            float horizontalVelocityBeforeCollision,
            Vector2 lastVelocity)
        {
            if (hasTeleportCarry)
            {
                return teleportCarry;
            }
            return stickyMode
                ? lastVelocity
                : new Vector2(
                    horizontalVelocityBeforeCollision,
                    lastVelocity.Y);
        }

        internal static float LimitSpeed(
            float current,
            float maximumSpeed)
        {
            return Math.Max(-maximumSpeed, Math.Min(maximumSpeed, current));
        }

        internal static float ApplySlopeGravity(
            float current,
            Vector2 surfaceNormal,
            float gravity,
            float maximumSpeed)
        {
            Vector2 unitNormal;
            if (!SurfaceMotion.TryNormalize(surfaceNormal, out unitNormal))
            {
                throw new InvalidOperationException(
                    "Ball King slope normal must be finite and non-zero");
            }
            Vector2 tangent = SurfaceMotion.GetTangent(
                unitNormal);
            return LimitSpeed(
                current + tangent.Y * gravity,
                maximumSpeed);
        }

        internal static float ApplySlopeMotion(
            float current,
            int direction,
            Vector2 surfaceNormal,
            float gravity,
            float maximumSpeed)
        {
            int uphillDirection =
                SurfaceMotion.GetUphillDirection(surfaceNormal);
            if (direction == uphillDirection)
            {
                float uncontrolled = Math.Sign(current)
                        == uphillDirection
                    ? 0f
                    : current;
                return ApplySlopeGravity(
                    uncontrolled,
                    surfaceNormal,
                    gravity,
                    maximumSpeed);
            }
            float withGravity = ApplySlopeGravity(
                current,
                surfaceNormal,
                gravity,
                maximumSpeed);
            if (direction == 0)
            {
                return withGravity;
            }
            return Update(
                withGravity,
                direction,
                maximumSpeed,
                1f);
        }

        internal static float SlopeTerminalSpeed(
            Vector2 surfaceNormal,
            float maximumFallSpeed)
        {
            Vector2 unitNormal;
            if (!SurfaceMotion.TryNormalize(surfaceNormal, out unitNormal))
            {
                throw new InvalidOperationException(
                    "Ball King slope normal must be finite and non-zero");
            }
            Vector2 tangent = SurfaceMotion.GetTangent(
                unitNormal);
            float verticalShare = Math.Abs(tangent.Y);
            return verticalShare <= 0.0001f
                ? maximumFallSpeed
                : maximumFallSpeed / verticalShare;
        }

        private static float MoveTowards(
            float current,
            float target,
            float maximumDelta)
        {
            if (Math.Abs(target - current) <= maximumDelta)
            {
                return target;
            }
            return current + Math.Sign(target - current) * maximumDelta;
        }
    }

    internal static class MorphSurfaceJumpPolicy
    {
        internal static bool AllowsJump(
            Vector2 surfaceNormal,
            bool stickyMode,
            bool supportedBySlope)
        {
            return stickyMode
                || (!supportedBySlope
                    && SurfaceMotion.IsHorizontalGround(surfaceNormal));
        }
    }

    internal static class MorphSlopeInputPolicy
    {
        internal static int FilterDirection(
            int direction,
            bool touchingSlope,
            Vector2 slopeNormal)
        {
            return touchingSlope
                && direction
                    == SurfaceMotion.GetUphillDirection(slopeNormal)
                    ? 0
                    : direction;
        }

        internal static Vector2 RemoveUphillControlFromCollision(
            Vector2 incomingVelocity,
            int heldDirection,
            Vector2 slopeNormal)
        {
            int uphillDirection =
                SurfaceMotion.GetUphillDirection(slopeNormal);
            if (uphillDirection == 0
                || heldDirection != uphillDirection
                || Math.Sign(incomingVelocity.X) != uphillDirection)
            {
                return incomingVelocity;
            }

            Vector2 allowed = new Vector2(0f, incomingVelocity.Y);
            Vector2 normal;
            if (!SurfaceMotion.TryNormalize(slopeNormal, out normal)
                || Vector2.Dot(allowed, normal) >= 0f)
            {
                return allowed;
            }
            Vector2 tangent = SurfaceMotion.GetTangent(normal);
            return tangent * Vector2.Dot(allowed, tangent);
        }
    }

    internal static class MorphBouncePhysics
    {
        private const float WallRestitution = 0.9f;
        private const float FloorHeightRatio = 0.2f;
        private const float MinimumFallHeight = 100f;

        internal static bool ShouldHandleWallCollision(
            bool usingBallHitbox,
            bool stickyMode,
            bool surfaceAttached)
        {
            return usingBallHitbox
                && !stickyMode
                && !surfaceAttached;
        }

        internal static bool IsFloorBounceSurface(Vector2 surfaceNormal)
        {
            return SurfaceMotion.IsHorizontalGround(surfaceNormal);
        }

        internal static float WallVelocity(float incomingVelocity)
        {
            return -incomingVelocity * WallRestitution;
        }

        internal static float FloorSpeed(
            float fallHeight,
            float gravity)
        {
            return FloorSpeed(fallHeight, gravity, true);
        }

        internal static float ContinuationFloorSpeed(
            float fallHeight,
            float gravity)
        {
            return FloorSpeed(fallHeight, gravity, false);
        }

        private static float FloorSpeed(
            float fallHeight,
            float gravity,
            bool requireMinimumHeight)
        {
            if ((requireMinimumHeight && fallHeight < MinimumFallHeight)
                || fallHeight <= 0f
                || gravity <= 0f)
            {
                return 0f;
            }
            float bounceHeight = fallHeight * FloorHeightRatio;
            return (float)Math.Sqrt(2f * gravity * bounceHeight);
        }

        internal static int FloorBounceCount(
            float fallHeight,
            float gravity,
            float maximumFallVelocity)
        {
            if (gravity <= 0f)
            {
                return 0;
            }
            if (fallHeight >= SplatFallHeight(
                gravity,
                maximumFallVelocity))
            {
                return 2;
            }
            return fallHeight >= MinimumFallHeight ? 1 : 0;
        }

        internal static float SplatFallHeight(
            float gravity,
            float maximumFallVelocity)
        {
            if (gravity <= 0f || maximumFallVelocity <= 0f)
            {
                return float.PositiveInfinity;
            }
            float height = 0f;
            float velocity = 0f;
            while (velocity < maximumFallVelocity - 0.0001f)
            {
                height += velocity;
                velocity = Math.Min(
                    maximumFallVelocity,
                    velocity + gravity);
            }
            return height;
        }

        internal static Vector2 SurfaceVelocity(
            Vector2 resolvedVelocity,
            Vector2 surfaceNormal,
            float bounceSpeed)
        {
            Vector2 normal;
            if (!SurfaceMotion.TryNormalize(surfaceNormal, out normal))
            {
                throw new InvalidOperationException(
                    "Ball King bounce normal must be finite and non-zero");
            }
            Vector2 tangent = SurfaceMotion.GetTangent(normal);
            return tangent * Vector2.Dot(resolvedVelocity, tangent)
                + normal * bounceSpeed;
        }

        internal static bool IsSplatImpact(
            float impactVelocity,
            float maximumFallVelocity)
        {
            return impactVelocity >= maximumFallVelocity - 0.0001f;
        }
    }
}
