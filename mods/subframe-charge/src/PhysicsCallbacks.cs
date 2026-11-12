using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace SubframeCharge
{
    // body ticks keep native time; material entry/exit must also see short contacts
    internal static class PhysicsCallbacks
    {
        private static BodyComp body;
        private static double remainder;
        private static bool first = true, native;
        private static float stepDelta, materialRate=1;
        private static readonly MaterialCadence materials=new MaterialCadence();
        internal static float MaterialRate { get { return materialRate; } }
        private static BehaviourContext currentContext;
        private static BehaviourContextCollisionInfo previousCollision;
        private static readonly Action<BehaviourContext, BehaviourContextCollisionInfo> SetPrevious =
            (Action<BehaviourContext, BehaviourContextCollisionInfo>)Delegate.CreateDelegate(typeof(Action<BehaviourContext, BehaviourContextCollisionInfo>),
                AccessTools.PropertySetter(typeof(BehaviourContext), "LastFrameCollisionInfo"));
        private static readonly Dictionary<IBlockBehaviour, float> conveyorOffsets = new Dictionary<IBlockBehaviour, float>();
        private static JKRuntime.OwnedPatches conveyor;
        internal static void Prepare(JKRuntime.RuntimeScope scope)
        {
            if (conveyor != null) return;
            var patch = scope.Own(new JKRuntime.OwnedPatches("subframe-charge.conveyor-cadence"));
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("ConveyorBlockMod.BlocksBehaviour.ConveyorBlockBehaviour", false);
                if (type != null) patch.Add(AccessTools.Method(type, "ModifyXVelocity"),
                    prefix: AccessTools.Method(typeof(PhysicsCallbacks), "BeforeConveyor"),
                    postfix: AccessTools.Method(typeof(PhysicsCallbacks), "AfterConveyor"),
                    finalizer: AccessTools.Method(typeof(PhysicsCallbacks), "FinishConveyor"));
                // these queries return the input unchanged but add fixed per-tick acceleration
                foreach (string name in new[] {
                    "JumpKing_Expansion_Blocks.Behaviours.SpecialHighGravity",
                    "JumpKing_GhostOfTheImmortalBabeBlocks.Behaviours.SpecialRainGravity" })
                {
                    type=assembly.GetType(name,false);
                    if(type!=null) patch.Add(AccessTools.Method(type,"ModifyYVelocity"),prefix:AccessTools.Method(typeof(PhysicsCallbacks),"StatefulY"));
                }
            }
            conveyor = patch;
            scope.Defer(delegate { if (ReferenceEquals(conveyor, patch)) conveyor = null; });
        }
        private static bool StatefulY(float __0, BehaviourContext __1, ref float __result)
        { if(!Owns(__1) || native) return true; __result=__0; return false; }
        private struct ConveyorState { internal bool Called; internal float Input; internal BehaviourContextCollisionInfo Previous; }
        private static bool BeforeConveyor(IBlockBehaviour __instance, float __0, BehaviourContext __1, ref float __result, out ConveyorState __state)
        {
            __state = default(ConveyorState);
            if (__1 == null || !Owns(__1)) return true;
            if (!native)
            {
                float offset; conveyorOffsets.TryGetValue(__instance, out offset);
                __result = __0 + offset;
                JKRuntime.Gameplay.MotionObservation.Report(__instance,__result,JKRuntime.Gameplay.MotionObservation.GetNeutralInput(__instance)+offset);
                return false;
            }
            __state = new ConveyorState { Called = true, Input = __0, Previous = __1.LastFrameCollisionInfo };
            if (previousCollision != null) SetPrevious(__1, previousCollision);
            return true;
        }
        private static void AfterConveyor(IBlockBehaviour __instance, BehaviourContext __1, float __result, ConveyorState __state)
        {
            if (!__state.Called) return;
            conveyorOffsets[__instance] = __result - __state.Input;
            SetPrevious(__1, __state.Previous);
        }
        private static void FinishConveyor(Exception __exception, BehaviourContext __1, ConveyorState __state)
        { if(__exception!=null && __state.Called) SetPrevious(__1,__state.Previous); }
        private static readonly Dictionary<IBodyCompBehaviour, bool> bodyResults = new Dictionary<IBodyCompBehaviour, bool>();
        internal static void Begin(BodyComp value, float delta)
        {
            body = value; stepDelta=delta;
            native = first || remainder >= 1.0 / 60 - 1e-8;
            if (native) { if (!first) remainder -= 1.0 / 60; first = false; bodyResults.Clear(); }
            remainder += delta;
        }
        internal static void End() { if (native && currentContext != null) previousCollision = currentContext.CollisionInfo; currentContext = null; body = null; }
        internal static void Reset() { End(); previousCollision = null; remainder = 0; first = true; bodyResults.Clear(); materials.Reset(); conveyorOffsets.Clear(); materialRate=1; }
        private static bool Owns(BehaviourContext context) { return body != null && context != null && ReferenceEquals(body, context.BodyComp); }
        private static bool Native(Type type) { return type.Assembly == typeof(BodyComp).Assembly; }
        internal static bool Body(IBodyCompBehaviour callback, BehaviourContext context)
        {
            if (Owns(context)) currentContext = context;
            if (!Owns(context) || Native(callback.GetType())
                || JKRuntime.Gameplay.BodyPipeline.RunsAtSubsteps(context.BodyComp, callback)) return callback.ExecuteBehaviour(context);
            bool result;
            if (!native) return !bodyResults.TryGetValue(callback, out result) || result;
            float delta = context.FrameDelta;
            var previous = context.LastFrameCollisionInfo;
            try { context.FrameDelta = 1f / 60; if (previousCollision != null) SetPrevious(context, previousCollision); result = callback.ExecuteBehaviour(context); bodyResults[callback] = result; return result; }
            finally { context.FrameDelta = delta; SetPrevious(context, previous); }
        }
        internal static bool Block(IBlockBehaviour callback, BehaviourContext context)
        {
            if (!Owns(context) || Native(callback.GetType())) return callback.ExecuteBlockBehaviour(context);
            float elapsed;
            if(!materials.Due(callback,context,stepDelta,native,out elapsed)) return materials.Result(callback);
            float delta = context.FrameDelta;
            float rate=materialRate;
            try {
                context.FrameDelta=elapsed; materialRate=elapsed*60;
                bool result=callback.ExecuteBlockBehaviour(context); materials.Complete(callback,result); return result;
            }
            finally { context.FrameDelta=delta; materialRate=rate; }
        }
    }
}
