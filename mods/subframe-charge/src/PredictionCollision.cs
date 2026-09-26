using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace SubframeCharge
{
    // A read-only native shape query. Foreign collision callbacks belong to
    // actual physics, never to speculative drawing. Unknown shapes are skipped.
    internal static class PredictionCollision
    {
        internal static readonly MethodInfo Slope = typeof(SlopeBlock).GetInterfaceMap(typeof(IBlock)).TargetMethods[
            Array.FindIndex(typeof(SlopeBlock).GetInterfaceMap(typeof(IBlock)).InterfaceMethods,m=>m.Name=="Intersects")];
        private static bool ready;
        private static readonly FieldInfo SlopeTypeField=typeof(SlopeBlock).GetField("m_type",BindingFlags.Instance|BindingFlags.NonPublic);

        internal static Vector2 Normal(SlopeBlock block)
        {
            // Read the stored orientation rather than invoking foreign patches.
            var type=(SlopeType)SlopeTypeField.GetValue(block);
            const float unit=.7071067811865475f;
            switch(type)
            {
                case SlopeType.TopLeft: return new Vector2(-unit,-unit);
                case SlopeType.TopRight: return new Vector2(unit,-unit);
                case SlopeType.BottomLeft: return new Vector2(-unit,unit);
                case SlopeType.BottomRight: return new Vector2(unit,unit);
                default: return Vector2.Zero;
            }
        }

        internal static void Prepare()
        {
            if(ready) return;
            if(SlopeTypeField==null || SlopeTypeField.FieldType!=typeof(SlopeType))
                throw new NotSupportedException("Native slope orientation contract is unavailable");
            // Original IL only: no SwitchBlocks state writes, foreign prefixes or
            // orientation callbacks run against a speculative hitbox.
            new Harmony(PerformanceFeatures.Id).CreateReversePatcher(Slope,
                new HarmonyMethod(AccessTools.Method(typeof(PredictionCollision),"OriginalSlope"))).Patch(HarmonyReversePatchType.Original);
            ready=true;
        }

        internal static BlockCollisionType Intersects(SlopeBlock block,Rectangle hitbox,out Rectangle overlap)
        {
            if(ready) return OriginalSlope(block,hitbox,out overlap);
            overlap=default(Rectangle);
            return BlockCollisionType.NoCollision;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static BlockCollisionType OriginalSlope(SlopeBlock block,Rectangle hitbox,out Rectangle overlap)
        { throw new InvalidOperationException("Native slope query has not been prepared"); }
    }
}
