using System;
using System.Linq;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using JumpKing.Workshop;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static class GimmickRecipes
    {
        internal static void Prepare(GimmickEntry entry)
        {
            if (entry.Template != null || entry.Factory == null || entry.ConstructionAttempted) return;
            entry.ConstructionAttempted = true;
            try
            {
                var factory = entry.Factory;
                var mapping = factory.GetType().GetInterfaceMap(typeof(IBlockFactory));
                var method = mapping.TargetMethods[Array.FindIndex(mapping.InterfaceMethods, m => m.Name == "GetBlock")];
                var construction = new GimmickConstruction(factory.GetType().Assembly);
                // Supply the native contract without changing contentManager.level.
                // On the vanilla campaign there is no Workshop Level object.
                object level = Game1.instance == null || Game1.instance.contentManager == null ? null : Game1.instance.contentManager.level;
                if (level == null) level = construction.Empty(typeof(Level));
                var block = construction.Run(method, factory, entry.Colour.Value, new Rectangle(0, 0, 8, 8),
                    level, LevelTexture.FromDimensions(60, 45), 0, 0, 0) as IBlock;
                string reason;
                if (!GimmickBlocks.CanCopy(block, out reason)) throw new InvalidOperationException(reason);
                entry.Template = block; entry.Error = null;
                entry.Label = Gimmicks.Human(block.GetType().Name) + " " + GimmickBlocks.RGB(entry.Colour.Value);
                entry.Detail = "Constructed from installed factory code. No source-map visit required. Native handler registrations are resolved when enabled.";
            }
            catch (Exception error)
            {
                entry.Error = "Cannot construct this entry: " + error.GetBaseException().Message;
            }
            Gimmicks.Generation++;
        }
    }
}
