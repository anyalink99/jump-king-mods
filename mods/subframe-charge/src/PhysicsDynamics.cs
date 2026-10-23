using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq.Expressions;
using HarmonyLib;
using JKRuntime;
using JumpKing;
using JumpKing.API;
using JumpKing.BlockBehaviours;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;

namespace SubframeCharge
{
    // velocities stay in native pixels/tick, including values exposed to mods
    internal static class PhysicsDynamics
    {
        internal const string Owner = "subframe-charge.physics";
        private static OwnedPatches patches;
        private static int owners;
        private static BodyComp body;
        private static float fraction=1, wind;
        private static bool gravityOwned, gravityScaled;
        private static BodyComp gravityBody;
        private static float observedGravity;
        private static JKRuntime.Gameplay.PlayerPhysicsStep.Scope stepScope;
        private static double elapsed, directionAt;
        private static InputComponent.State direction;
        private static readonly FieldInfo Blocks=AccessTools.Field(typeof(BodyComp),"m_blockBehaviours");
        private static readonly Type BufferType=typeof(BodyComp).Assembly.GetType("JumpKing.Util.Buffer`1",true).MakeGenericType(typeof(InputComponent.State));
        private static readonly MethodInfo BufferPush=AccessTools.Method(BufferType,"Push");
        private static Action<object,InputComponent.State> Push;
        private static Action<object> Clear;
        private static Func<object,int> Count;
        private static Action<object,InputComponent.State> BindPush()
        { var target=Expression.Parameter(typeof(object)); var state=Expression.Parameter(typeof(InputComponent.State)); return Expression.Lambda<Action<object,InputComponent.State>>(Expression.Call(Expression.Convert(target,BufferType),BufferPush,state),target,state).Compile(); }
        private static Action<object> BindClear()
        { var target=Expression.Parameter(typeof(object)); return Expression.Lambda<Action<object>>(Expression.Call(Expression.Convert(target,BufferType),AccessTools.Method(BufferType,"Reset")),target).Compile(); }
        private static Func<object,int> BindCount()
        { var target=Expression.Parameter(typeof(object)); return Expression.Lambda<Func<object,int>>(Expression.Property(Expression.Convert(target,BufferType),"ValidCount"),target).Compile(); }
        internal static readonly List<MethodInfo> Targets=FindTargets();
        internal static bool Stepping { get { return body!=null; } }
        internal static IDisposable Acquire() { Prepare(); owners++; return new Lifetime(); }
        private sealed class Lifetime : IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                if(disposed) return;
                if(owners==1) { patches.Dispose(); patches=null; }
                owners--; disposed=true;
            }
        }
        private static MethodInfo Method(string name) { return AccessTools.Method(typeof(PhysicsDynamics),name); }
        private static MethodInfo Target(Type type,string name)
        {
            var method=AccessTools.Method(type,name);
            if(method==null) throw new MissingMethodException(type.FullName,name);
            return method;
        }
        private static MethodInfo CollisionTarget(Type type)
        {
            var map=type.GetInterfaceMap(typeof(IBlock));
            for(int i=0;i<map.InterfaceMethods.Length;i++)
                if(map.InterfaceMethods[i].Name=="Intersects") return map.TargetMethods[i];
            throw new MissingMethodException(type.FullName,"IBlock.Intersects");
        }
        private static List<MethodInfo> FindTargets()
        {
            return new List<MethodInfo> {
                Target(typeof(UpdateXPositionFromVelocityBehaviour),"ExecuteBehaviour"),
                Target(typeof(UpdateYPositionFromVelocityBehaviour),"ExecuteBehaviour"),
                Target(typeof(ApplyGravityBehaviour),"ExecuteBehaviour"),
                Target(typeof(WindVelocityUpdateBehaviour),"ExecuteBehaviour"),
                Target(typeof(IceBlockBehaviour),"ExecuteBlockBehaviour"), Target(typeof(Walk),"MyRun"),
                Target(typeof(PlayerValues),"get_ICE_FRICTION"),
                Target(typeof(WalkAnim),"MyRun"), Target(typeof(JumpState),"MyRun"),
                Target(typeof(BodyComp),"UpdateInternal"), Target(typeof(InputComponent),"Update"),
                Target(typeof(InputComponent),"GetState"), Target(typeof(InputComponent),"GetPressedState"),
                Target(typeof(ResolveXCollisionBehaviour),"ExecuteBehaviour"), Target(typeof(ResolveYCollisionBehaviour),"ExecuteBehaviour"),
                CollisionTarget(typeof(SlopeBlock)), CollisionTarget(typeof(BoxBlock))
            };
        }
        internal static void Prepare()
        {
            if(patches!=null) return;
            if(Push==null) Push=BindPush();
            if(Clear==null) Clear=BindClear();
            if(Count==null) Count=BindCount();
            var group=new OwnedPatches(Owner);
            try
            {
                group.Add(Target(typeof(UpdateXPositionFromVelocityBehaviour),"ExecuteBehaviour"),prefix:Method("BeforeX"),postfix:Method("AfterX"));
                group.Add(Target(typeof(UpdateYPositionFromVelocityBehaviour),"ExecuteBehaviour"),prefix:Method("BeforeY"),postfix:Method("AfterY"));
                group.Add(Target(typeof(ApplyGravityBehaviour),"ExecuteBehaviour"),prefix:Method("BeforeGravity"),postfix:Method("AfterGravity"));
                group.Add(Target(typeof(ApplyGravityBehaviour),"ExecuteBehaviour"),transpiler:Method("Gravity"),priority:Priority.Last);
                group.ReplaceCalls(Target(typeof(BodyComp),"UpdateInternal"),Target(typeof(IBodyCompBehaviour),"ExecuteBehaviour"),AccessTools.Method(typeof(PhysicsCallbacks),"Body"),1);
                group.ReplaceCalls(Target(typeof(ExecuteBlockBehaviours),"ExecuteBehaviour"),Target(typeof(IBlockBehaviour),"ExecuteBlockBehaviour"),AccessTools.Method(typeof(PhysicsCallbacks),"Block"),1);
                group.Add(Target(typeof(WindVelocityUpdateBehaviour),"ExecuteBehaviour"),prefix:Method("BeforeWind"),postfix:Method("AfterWind"));
                var friction=AccessTools.PropertyGetter(typeof(PlayerValues),"ICE_FRICTION");
                group.ReplaceCalls(Target(typeof(IceBlockBehaviour),"ExecuteBlockBehaviour"),friction,Method("Friction"),1);
                group.ReplaceCalls(Target(typeof(Walk),"MyRun"),friction,Method("Friction"),1);
                group.Add(friction,postfix:Method("MaterialFriction"));
                group.Add(Target(typeof(WalkAnim),"MyRun"),transpiler:Method("Animation"));
                group.Add(Target(typeof(JumpState),"MyRun"),transpiler:Method("DirectionBuffer"));
                patches=group;
            }
            catch { group.Dispose(); throw; }
        }
        internal static void Begin(BodyComp value,float delta)
        {
            if(body!=null || delta<=0 || delta>1f/60) throw new InvalidOperationException("Invalid nested physics step");
            body=value; fraction=delta*60; wind=0; elapsed+=delta; PhysicsCallbacks.Begin(value,delta);
            stepScope=JKRuntime.Gameplay.PlayerPhysicsStep.Enter(value,fraction);
        }
        internal static void End() { if(body!=null) stepScope.Dispose(); PhysicsCallbacks.End(); body=null; fraction=1; wind=0; }
        internal static void Reset() { End(); PhysicsCallbacks.Reset(); gravityBody=null; elapsed=directionAt=0; direction=default(InputComponent.State); }
        private static bool Owns(BehaviourContext context) { return body!=null && ReferenceEquals(context.BodyComp,body); }
        private static void BeforeX(BehaviourContext __0,out float __state) { __state=__0.BodyComp.Position.X; }
        private static void AfterX(BehaviourContext __0,float __state)
        {
            if(!Owns(__0)) return;
            float distance=body.Position.X-__state;
            float material=body.IsOnBlock(typeof(SandBlock)) ? .25f : 1;
            if(body.IsOnBlock(typeof(WaterBlock))) material*=.5f;
            // native wind kicks before moving; preserve that discrete trajectory
            body.Position.X=__state+Displacement(distance*fraction,wind*material*fraction*(1-fraction)*.5f);
        }
        private struct Vertical { internal float Position; internal bool Probe; }
        private static void BeforeY(BehaviourContext __0,out Vertical __state)
        {
            var target=__0.BodyComp;
            __state=new Vertical { Position=target.Position.Y,Probe=target.IsOnGround && target.Velocity.Y>0 && !target.IsOnBlock(typeof(SandBlock)) };
        }
        private static void AfterY(BehaviourContext __0,Vertical __state)
        {
            if(!Owns(__0)) return;
            float probe=__state.Probe ? 1 : 0;
            float distance=body.Position.Y-__state.Position-probe;
            float material=body.IsOnBlock(typeof(WaterBlock)) ? .5f : 1;
            bool sand=body.IsOnBlock(typeof(SandBlock));
            if(sand && body.Velocity.Y<=0) material*=.5f;
            float gravity=ReferenceEquals(gravityBody,body) ? observedGravity : PlayerValues.GRAVITY*(body.IsOnBlock(typeof(WaterBlock)) ? .5f : 1);
            bool capped=gravity<0 ? body.Velocity.Y<=-PlayerValues.MAX_FALL : body.Velocity.Y>=PlayerValues.MAX_FALL;
            float correction=__state.Probe || capped || sand && body.Velocity.Y>=.75f
                ? 0 : gravity*material*fraction*(fraction-1)*.5f;
            body.Position.Y=__state.Position+probe+Displacement(distance*fraction,correction);
        }
        private static float Displacement(float movement,float correction)
        {
            // native collision resolution chooses an exit side from velocity
            // don't let the trajectory correction reverse the actual movement
            float adjusted=movement+correction;
            return movement==0 || Math.Sign(adjusted)!=Math.Sign(movement) ? movement : adjusted;
        }
        private static void BeforeGravity(BehaviourContext __0,out float __state)
        { __state=__0.BodyComp.Velocity.Y; gravityOwned=Owns(__0); gravityScaled=false; }
        private static float ScaleGravity(float value)
        {
            if(!gravityOwned) return value;
            gravityScaled=true;
            gravityBody=body; observedGravity=value;
            return body.IsOnGround ? value : value*fraction;
        }
        private static IEnumerable<CodeInstruction> Gravity(IEnumerable<CodeInstruction> source)
        {
            var code=new List<CodeInstruction>(source); int count=0;
            for(int i=0;i<code.Count;i++)
            {
                // after material/reverse-gravity transforms, before addition and either cap
                if(code[i].opcode==OpCodes.Add && i+1<code.Count && code[i+1].opcode==OpCodes.Stind_R4)
                { yield return new CodeInstruction(OpCodes.Call,Method("ScaleGravity")); count++; }
                yield return code[i];
            }
            if(count!=1) throw new NotSupportedException("Native gravity accumulation changed");
        }
        private static void AfterGravity(BehaviourContext __0,float __state)
        {
            if(!Owns(__0) || body.IsOnGround || gravityScaled) return;
            // retain the real material pass and foreign patches, including reverse gravity
            body.Velocity.Y=__state+(body.Velocity.Y-__state)*fraction;
        }
        private static void BeforeWind(BehaviourContext __0,out float __state) { __state=__0.BodyComp.Velocity.X; }
        private static void AfterWind(BehaviourContext __0,float __state)
        {
            if(!Owns(__0)) return;
            wind=body.Velocity.X-__state; body.Velocity.X=__state+wind*fraction;
        }
        private static float Friction() { return PlayerValues.ICE_FRICTION*fraction; }
        private static void MaterialFriction(ref float __result) { __result*=PhysicsCallbacks.MaterialRate; }
        private static float AnimationStep() { return fraction; }
        private static IEnumerable<CodeInstruction> Animation(IEnumerable<CodeInstruction> source)
        {
            int count=0;
            foreach(var instruction in source)
            {
                if(instruction.opcode==OpCodes.Ldc_R4 && Equals(instruction.operand,1f))
                { instruction.opcode=OpCodes.Call; instruction.operand=Method("AnimationStep"); count++; }
                yield return instruction;
            }
            if(count!=1) throw new NotSupportedException("Native walk animation cadence changed");
        }
        private static IEnumerable<CodeInstruction> DirectionBuffer(IEnumerable<CodeInstruction> source)
        {
            int count=0;
            foreach(var instruction in source)
            {
                if(instruction.Calls(BufferPush)) { instruction.opcode=OpCodes.Call; instruction.operand=Method("PushDirection"); count++; }
                yield return instruction;
            }
            if(count!=1) throw new NotSupportedException("Native jump direction buffer changed");
        }
        private static void PushDirection(object buffer,InputComponent.State state)
        {
            if(!Stepping) { Push(buffer,state); return; }
            if(Count(buffer)==0) direction=default(InputComponent.State);
            if(state.dpad.X!=0) { direction=state; directionAt=elapsed; }
            Clear(buffer);
            if(direction.dpad.X!=0 && elapsed-directionAt<4.0/60-1e-7) Push(buffer,direction);
            Push(buffer,state);
        }
    }
}
