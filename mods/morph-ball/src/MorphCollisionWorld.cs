using System;
using System.Collections.Generic;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal static class MorphCollisionWorld
    {
        internal static List<MorphContourSegment> GatherContourSegments(
            BodyComp body,
            Vector2 from,
            Vector2 to,
            IBlock requiredBlock,
            bool stickySurfacesOnly)
        {
            Rectangle hitbox = body.GetHitbox();
            int left = (int)Math.Floor(Math.Min(from.X, to.X)) - 5;
            int top = (int)Math.Floor(Math.Min(from.Y, to.Y)) - 5;
            int right = (int)Math.Ceiling(Math.Max(from.X, to.X))
                + hitbox.Width + 5;
            int bottom = (int)Math.Ceiling(Math.Max(from.Y, to.Y))
                + hitbox.Height + 5;
            Rectangle search = new Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
            AdvCollisionInfo collision = LevelManager.GetCollisionInfo(search);
            IReadOnlyList<IBlock> blocks = collision == null
                ? null
                : collision.GetCollidedBlocks();
            List<IBlock> geometry = new List<IBlock>();
            if (requiredBlock != null
                && IsBlockingContact(requiredBlock, search)
                && (!stickySurfacesOnly
                    || BallKingMapRules.IsStickySurface(requiredBlock)))
            {
                geometry.Add(requiredBlock);
            }
            if (blocks == null)
            {
                return MorphContourGeometry.BuildExterior(
                    geometry,
                    hitbox.Width,
                    hitbox.Height);
            }
            foreach (IBlock block in blocks)
            {
                if (ReferenceEquals(block, requiredBlock)
                    || !IsBlockingContact(block, search)
                    || (stickySurfacesOnly
                        && !BallKingMapRules.IsStickySurface(block)))
                {
                    continue;
                }
                geometry.Add(block);
            }
            return MorphContourGeometry.BuildExterior(
                geometry,
                hitbox.Width,
                hitbox.Height);
        }

        internal static bool IsBlockingContact(
            IBlock block,
            Rectangle probe)
        {
            if (block == null || probe.Width <= 0 || probe.Height <= 0)
            {
                return false;
            }
            Rectangle overlap;
            return block.Intersects(probe, out overlap)
                == BlockCollisionType.Collision_Blocking;
        }

        internal static bool HasBlockingCollision(Rectangle hitbox)
        {
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
                if (IsBlockingContact(block, hitbox))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
