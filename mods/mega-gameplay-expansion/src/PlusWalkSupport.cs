using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    // Optional, read-only contracts audited against JumpKingPlus. Never replay
    // a foreign movement/collision callback or make its DLL a package dependency.
    internal sealed class PlusWalkSupport
    {
        private readonly Dictionary<Type,IBlockBehaviour> blocks;
        private Type oneWay, oneWayBehaviour;
        private PropertyInfo direction;
        private int resolvedCount = -1;

        internal PlusWalkSupport(Dictionary<Type,IBlockBehaviour> value) { blocks = value; }

        private void Resolve()
        {
            if (oneWay != null || resolvedCount == blocks.Count) return;
            resolvedCount = blocks.Count;
            foreach (var pair in blocks)
            {
                var assembly = pair.Key.Assembly;
                if (assembly.GetName().Name != "JumpKingPlus") continue;
                oneWay = assembly.GetType("JumpKingPlus.Blocks.OneWayBlock");
                oneWayBehaviour = assembly.GetType("JumpKingPlus.BlockBehaviours.OneWayBlockBehaviour");
                direction = oneWay == null ? null : oneWay.GetProperty("Type");
                return;
            }
        }

        private bool Registered(Type block, Type behaviour)
        {
            IBlockBehaviour value;
            return block != null && behaviour != null && blocks.TryGetValue(block,out value) && value.GetType() == behaviour;
        }

        internal bool Supports(Rectangle box, AdvCollisionInfo feet)
        {
            Resolve();
            if (!Registered(oneWay,oneWayBehaviour) || direction == null) return false;
            // JumpKingPlus accepts an entry from above only when the body did
            // not already overlap ANY one-way block at the start of the tick.
            foreach (var block in LevelManager.GetCollisionInfo(box).GetCollidedBlocks())
                if (block.GetType() == oneWay) return false;
            foreach (var block in feet.GetCollidedBlocks())
            {
                if (block.GetType() != oneWay) continue;
                string side = Convert.ToString(direction.GetValue(block,null));
                // The provider returns for the first vertical one-way contact.
                if (side == "Bottom") return false;
                if (side == "Top") return JKRuntime.Gameplay.SupportPredicates.TopFace(
                    new JKRuntime.Gameplay.SupportQuery(box,block.GetRect(),Vector2.Zero,false,1))==JKRuntime.Gameplay.SupportKind.Supported;
            }
            return false;
        }
    }
}
