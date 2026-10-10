using System.Collections.Generic;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal struct MorphSlopeContact
    {
        internal bool Supported;
        internal Vector2 Normal;
        internal IBlock Block;
    }

    internal static class MorphSlopeContactDetector
    {
        internal static MorphSlopeContact Detect(
            BodyComp body,
            AdvCollisionInfo collision)
        {
            Rectangle hitbox = body.GetHitbox();
            Rectangle supportProbe = new Rectangle(
                hitbox.Left,
                hitbox.Bottom - 1,
                hitbox.Width,
                3);
            AdvCollisionInfo support =
                LevelManager.GetCollisionInfo(supportProbe);
            AdvCollisionInfo slopeSupport = ContainsTopSlope(
                collision,
                supportProbe)
                ? collision
                : support;
            return Evaluate(
                slopeSupport,
                HasFlatTopSupport(support, hitbox, supportProbe),
                supportProbe);
        }

        internal static bool HasFlatTopSupport(
            AdvCollisionInfo collision,
            Rectangle hitbox,
            Rectangle supportProbe)
        {
            if (collision == null)
            {
                return false;
            }
            IReadOnlyList<IBlock> blocks = collision.GetCollidedBlocks();
            if (blocks == null)
            {
                return false;
            }
            foreach (IBlock block in blocks)
            {
                if (block is SlopeBlock)
                {
                    continue;
                }
                Rectangle blockRect = block.GetRect();
                int overlapWidth = System.Math.Min(
                    hitbox.Right,
                    blockRect.Right)
                    - System.Math.Max(hitbox.Left, blockRect.Left);
                if (overlapWidth <= 0
                    || blockRect.Top < hitbox.Bottom - 1
                    || blockRect.Top > hitbox.Bottom + 2)
                {
                    continue;
                }
                Rectangle overlap;
                if (block.Intersects(supportProbe, out overlap)
                    == BlockCollisionType.Collision_Blocking)
                {
                    return true;
                }
            }
            return false;
        }

        internal static MorphSlopeContact Evaluate(
            AdvCollisionInfo support,
            bool flatSupported)
        {
            return Evaluate(
                support,
                flatSupported,
                default(Rectangle));
        }

        internal static bool TryGetTopSlopeNormal(
            AdvCollisionInfo collision,
            out Vector2 normal)
        {
            normal = Vector2.Zero;
            if (collision == null)
            {
                return false;
            }
            bool found = false;
            IReadOnlyList<IBlock> blocks = collision.GetCollidedBlocks();
            if (blocks != null)
            {
                foreach (IBlock block in blocks)
                {
                    SlopeBlock slope = block as SlopeBlock;
                    Vector2 candidate;
                    if (slope == null
                        || !IsTopSlope(slope.GetSlopeType())
                        || !TryNormalize(slope.GetNormal(), out candidate))
                    {
                        continue;
                    }
                    if (found && Vector2.Dot(normal, candidate) < 0.999f)
                    {
                        normal = Vector2.Zero;
                        return false;
                    }
                    found = true;
                    normal = candidate;
                }
            }
            if (found)
            {
                return true;
            }
            return IsTopSlope(collision.SlopeType)
                && TryNormalize(collision.SlopeNormal, out normal);
        }

        private static MorphSlopeContact Evaluate(
            AdvCollisionInfo support,
            bool flatSupported,
            Rectangle probe)
        {
            if (flatSupported)
            {
                return default(MorphSlopeContact);
            }
            Vector2 normal;
            IBlock block;
            bool supported = TryGetTopSlope(
                support,
                probe,
                out normal,
                out block);
            return new MorphSlopeContact
            {
                Supported = supported,
                Normal = normal,
                Block = block
            };
        }

        private static bool TryGetTopSlope(
            AdvCollisionInfo collision,
            Rectangle probe,
            out Vector2 normal,
            out IBlock slopeBlock)
        {
            normal = Vector2.Zero;
            slopeBlock = null;
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
                    if (slope == null || !IsTopSlope(slope.GetSlopeType()))
                    {
                        continue;
                    }
                    Rectangle collisionProbe = probe.Width > 0
                        && probe.Height > 0
                            ? probe
                            : block.GetRect();
                    if (!MorphCollisionWorld.IsBlockingContact(
                        block,
                        collisionProbe))
                    {
                        continue;
                    }
                    if (!TryNormalize(slope.GetNormal(), out normal))
                    {
                        continue;
                    }
                    slopeBlock = block;
                    return true;
                }
            }
            if (!IsTopSlope(collision.SlopeType))
            {
                return false;
            }
            return TryNormalize(collision.SlopeNormal, out normal);
        }

        private static bool ContainsTopSlope(
            AdvCollisionInfo collision,
            Rectangle probe)
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
                    if (slope != null
                        && IsTopSlope(slope.GetSlopeType())
                        && MorphCollisionWorld.IsBlockingContact(
                            block,
                            probe))
                    {
                        return true;
                    }
                }
            }
            return IsTopSlope(collision.SlopeType);
        }

        private static bool IsTopSlope(SlopeType type)
        {
            return type == SlopeType.TopLeft
                || type == SlopeType.TopRight;
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
    }
}
