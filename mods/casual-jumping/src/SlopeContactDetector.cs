using System.Collections.Generic;
using JumpKing.Level;
using JumpKing.Player;

namespace CasualJumping
{
    internal struct SlopeContact
    {
        internal readonly bool Touching;
        internal readonly bool Adjacent;
        internal readonly int BlockedDirection;

        internal SlopeContact(
            bool touching,
            bool adjacent,
            int blockedDirection)
        {
            Touching = touching;
            Adjacent = adjacent;
            BlockedDirection = blockedDirection;
        }
    }

    internal static class SlopeContactDetector
    {
        internal static SlopeContact Detect(
            BodyComp body,
            AdvCollisionInfo startCollision)
        {
            AdvCollisionInfo adjacentCollision = GetAdjacentCollisionInfo(body);
            int startDirection = GetBlockedDirection(startCollision);
            int adjacentDirection = GetBlockedDirection(adjacentCollision);
            int blockedDirection = startDirection != 0
                ? startDirection
                : adjacentDirection;
            bool adjacent = ContainsTopSlope(adjacentCollision);
            bool touching = adjacent || ContainsTopSlope(startCollision);
            return new SlopeContact(touching, adjacent, blockedDirection);
        }

        internal static int GetBlockedDirection(AdvCollisionInfo collision)
        {
            if (collision == null)
            {
                return 0;
            }

            int blockedDirection = 0;
            IReadOnlyList<IBlock> blocks = collision.GetCollidedBlocks();
            if (blocks != null)
            {
                foreach (IBlock block in blocks)
                {
                    SlopeBlock slope = block as SlopeBlock;
                    if (slope == null)
                    {
                        continue;
                    }

                    int candidate = GetBlockedDirection(
                        slope.GetSlopeType(),
                        slope.GetNormal().X);
                    if (candidate == 0)
                    {
                        continue;
                    }
                    if (blockedDirection != 0
                        && blockedDirection != candidate)
                    {
                        return 0;
                    }
                    blockedDirection = candidate;
                }
            }

            return blockedDirection != 0
                ? blockedDirection
                : GetBlockedDirection(
                    collision.SlopeType,
                    collision.SlopeNormal.X);
        }

        internal static bool ContainsTopSlope(AdvCollisionInfo collision)
        {
            if (collision == null)
            {
                return false;
            }

            IReadOnlyList<IBlock> blocks = collision.GetCollidedBlocks();
            if (blocks != null)
            {
                foreach (IBlock block in blocks)
                {
                    SlopeBlock slope = block as SlopeBlock;
                    if (slope != null && IsTopSlope(slope.GetSlopeType()))
                    {
                        return true;
                    }
                }
            }
            return IsTopSlope(collision.SlopeType);
        }

        private static AdvCollisionInfo GetAdjacentCollisionInfo(BodyComp body)
        {
            Microsoft.Xna.Framework.Rectangle hitbox = body.GetHitbox();
            Microsoft.Xna.Framework.Rectangle probe =
                new Microsoft.Xna.Framework.Rectangle(
                    hitbox.Left - 1,
                    hitbox.Top - 1,
                    hitbox.Width + 2,
                    hitbox.Height + 2);
            return LevelManager.GetCollisionInfo(probe);
        }

        private static int GetBlockedDirection(
            SlopeType slopeType,
            float normalX)
        {
            if (!IsTopSlope(slopeType))
            {
                return 0;
            }
            if (normalX > 0f)
            {
                return -1;
            }
            if (normalX < 0f)
            {
                return 1;
            }
            return slopeType == SlopeType.TopLeft ? 1 : -1;
        }

        private static bool IsTopSlope(SlopeType slopeType)
        {
            return slopeType == SlopeType.TopLeft
                || slopeType == SlopeType.TopRight;
        }
    }
}
