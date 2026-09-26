using System.Collections.Generic;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    // Observe the existing collision map, including third-party IBlock factories.
    // No replacement block palette, synthetic collision or private BodyComp state.
    internal static class NativeSupport
    {
        internal static Rectangle[] Sample(Rectangle body)
        {
            var probe = new Rectangle(body.Left, body.Bottom, body.Width, 1);
            var contacts = new List<Rectangle>();
            foreach (IBlock block in LevelManager.GetCollisionInfo(probe).GetCollidedBlocks())
            {
                Rectangle overlap;
                if (block.Intersects(probe, out overlap) == BlockCollisionType.Collision_Blocking && overlap.Width > 0 && overlap.Height > 0)
                    contacts.Add(Camera.TransformRect(overlap));
            }
            return contacts.ToArray();
        }
    }
}
