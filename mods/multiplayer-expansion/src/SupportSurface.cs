using System.Collections.Generic;
using HarmonyLib;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    // read-only geometry for foot probes; never add player bodies to the map
    internal sealed class PlayerHeadSurface : BoxBlock
    {
        internal PlayerHeadSurface(Rectangle bounds) : base(bounds) { }
    }
    internal static class SupportSurface
    {
        internal static void Install(Harmony hooks)
        { hooks.Patch(AccessTools.Method(typeof(LevelManager),"GetCollisionInfo",new[]{typeof(Rectangle)}),postfix:new HarmonyMethod(typeof(SupportSurface),"Probe")); }
        private static void Probe(Rectangle p_hitbox,ref AdvCollisionInfo __result)
        { __result=Merge(__result,p_hitbox,AdvancedSession.SupportFrames); }
        internal static AdvCollisionInfo Merge(AdvCollisionInfo terrain,Rectangle probe,IEnumerable<InteractionFrame> peers)
        {
            if(probe.Height!=1) return terrain;
            List<IBlock> blocks=null;
            foreach(var other in peers) {
                var head=new Rectangle((int)other.Position.X,(int)other.Position.Y,other.Width,1);
                if(!head.Intersects(probe)) continue;
                if(blocks==null) blocks=new List<IBlock>(terrain.GetCollidedBlocks());
                blocks.Add(new PlayerHeadSurface(head));
            }
            return blocks==null ? terrain : new AdvCollisionInfo(blocks,terrain.IsInWind(),terrain.SlopeType,terrain.SlopeNormal);
        }
    }
}
