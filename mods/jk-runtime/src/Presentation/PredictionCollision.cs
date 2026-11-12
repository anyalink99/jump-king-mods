using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Presentation
{
    /// <summary>Prepare the original native slope query for visual prediction, without invoking mod collision callbacks.</summary>
    internal static class PredictionCollision
    {
        public static readonly MethodInfo Slope = typeof(SlopeBlock).GetInterfaceMap(typeof(IBlock)).TargetMethods[
            Array.FindIndex(typeof(SlopeBlock).GetInterfaceMap(typeof(IBlock)).InterfaceMethods,m=>m.Name=="Intersects")];
        private static bool ready;
        private static readonly FieldInfo Orientation=typeof(SlopeBlock).GetField("m_type",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static Vector2 Normal(SlopeBlock block)
        {
            const float unit=.7071067811865475f;
            switch((SlopeType)Orientation.GetValue(block)) {
                case SlopeType.TopLeft:return new Vector2(-unit,-unit);
                case SlopeType.TopRight:return new Vector2(unit,-unit);
                case SlopeType.BottomLeft:return new Vector2(-unit,unit);
                case SlopeType.BottomRight:return new Vector2(unit,unit);
                default:return Vector2.Zero;
            }
        }
        public static void Prepare()
        {
            RuntimeApi.Kernel.CheckThread();if(ready)return;
            if(Orientation==null || Orientation.FieldType!=typeof(SlopeType))throw new NotSupportedException("Native slope orientation contract changed");
            var engine=OwnedPatches.SharedEngine();var harmony=engine.GetType("HarmonyLib.Harmony",true);
            var metadata=engine.GetType("HarmonyLib.HarmonyMethod",true);
            var owner=Activator.CreateInstance(harmony,new object[]{"jk-runtime.visual-prediction"});
            var standin=Activator.CreateInstance(metadata,new object[]{typeof(PredictionCollision).GetMethod("Original",OwnedPatches.Members)});
            var reverse=harmony.GetMethod("CreateReversePatcher").Invoke(owner,new[]{(object)Slope,standin});
            var kind=engine.GetType("HarmonyLib.HarmonyReversePatchType",true);
            reverse.GetType().GetMethod("Patch").Invoke(reverse,new[]{Enum.Parse(kind,"Original")});ready=true;
        }
        internal static BlockCollisionType Intersects(SlopeBlock block,Rectangle hitbox,out Rectangle overlap)
        { if(ready)return Original(block,hitbox,out overlap);overlap=default(Rectangle);return BlockCollisionType.NoCollision; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static BlockCollisionType Original(SlopeBlock block,Rectangle hitbox,out Rectangle overlap)
        { throw new InvalidOperationException("Native slope query isn't prepared"); }
    }
}
