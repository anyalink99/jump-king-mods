using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using JKRuntime.Gameplay;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class MotionObservationTests
    {
        private abstract class Handler : IBlockBehaviour
        {
            public float BlockPriority {get{return 1;}}
            public bool IsPlayerOnBlock {get;set;}
            public abstract float ModifyXVelocity(float value,BehaviourContext context);
            public float ModifyYVelocity(float value,BehaviourContext context){return value;}
            public float ModifyGravity(float value,BehaviourContext context){return value;}
            public bool ExecuteBlockBehaviour(BehaviourContext context){return true;}
            public bool AdditionalXCollisionCheck(AdvCollisionInfo info,BehaviourContext context){return false;}
            public bool AdditionalYCollisionCheck(AdvCollisionInfo info,BehaviourContext context){return false;}
        }
        private sealed class Scale : Handler
        {
            internal float Factor=.825f;
            internal int Calls,Reads;
            private float Multiplier {get{Reads++;return Factor;}}
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            {Calls++; float k=Multiplier; if(IsPlayerOnBlock) k*=.5f; return value*k;}
        }
        private sealed class Carry : Handler
        {
            internal float Speed;
            internal int Calls;
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            {Calls++; float result=value; if(IsPlayerOnBlock) result+=Speed; return result;}
        }
        private sealed class Conditional : Handler
        {
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            {return value>0?value*.5f:value;}
        }
        private sealed class HelperBranch : Handler
        {
            internal int Calls;
            internal float Offset;
            private float Apply(float value) { Calls++; return value+Offset; }
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            { return IsPlayerOnBlock ? Apply(value) : value; }
        }
        private sealed class VectorFields : Handler
        {
            internal Vector2 Factor=new Vector2(.5f,1);
            internal static Vector2 GlobalFactor=new Vector2(1,1);
            internal int Calls;
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            {
                Calls++;
                if(IsPlayerOnBlock) { context.BodyComp.Position.X+=2; context.BodyComp.Velocity.X*=-1; }
                return value*Factor.X*GlobalFactor.X;
            }
        }
        private sealed class FieldEscape : Handler
        {
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            { context.BodyComp.Velocity.X=value; return value; }
        }
        private sealed class AddressEscape : Handler
        {
            internal int Calls;
            private float Read(ref Vector2 vector) { Calls++; return vector.X; }
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            { return value*Read(ref context.BodyComp.Velocity); }
        }
        private sealed class Escaping : Handler
        {
            internal float Saved;
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            {Saved=value;return Saved;}
        }
        private sealed class Throwing : Handler
        {
            internal int Calls;
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            {Calls++;throw new InvalidOperationException("original failure");}
        }
        private sealed class Declared : Handler
        {
            internal int Calls;
            [MethodImpl(MethodImplOptions.NoInlining)] public override float ModifyXVelocity(float value,BehaviourContext context)
            {Calls++;float result=Math.Min(2,value); MotionObservation.Report(this,result,Math.Min(2,MotionObservation.GetNeutralInput(this)));return result;}
        }
        private static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
        private static void PatchResult(ref float __result){__result+=.25f;}
        private static void ReviewedPrefix() { }
        private static void ReviewedRebuild(UpdateXPositionFromVelocityBehaviour stage,BehaviourContext context,MotionObserver observer)
        {
            var method=typeof(Scale).GetMethod("ModifyXVelocity");
            var plan=MotionObservation.FindPlan(method);
            using(var patches=new OwnedPatches("test.motion.reviewed"))
            {
                patches.Add(method,prefix:typeof(MotionObservationTests).GetMethod("ReviewedPrefix",OwnedPatches.Members));
                Tick(stage,context,1.5f);
                Require(observer.Read().Kind==MotionKind.Unknown,"Reviewed rebuild was trusted before refreshing its lease");
                var next=MethodValidity.Watch(method,"jkruntime.motion-observation","test.motion.reviewed");
                plan.Validity.Dispose();plan.Validity=next;
                Tick(stage,context,1.5f);
                Require(observer.Read().Kind==MotionKind.ControlledOnly,"Reviewed rebuild lost its arithmetic recorder");
            }
            Tick(stage,context,1.5f);
            Require(observer.Read().Kind==MotionKind.Unknown,"Removed reviewed patch silently restored stale evidence");
            var restored=MethodValidity.Watch(method,"jkruntime.motion-observation");
            plan.Validity.Dispose();plan.Validity=restored;
            Tick(stage,context,1.5f);
            Require(observer.Read().Kind==MotionKind.ControlledOnly,"Reviewed teardown lost its arithmetic recorder");
        }
        private static void Tick(UpdateXPositionFromVelocityBehaviour stage,BehaviourContext context,float speed)
        {context.BodyComp.Position=Vector2.Zero;context.BodyComp.Velocity=new Vector2(speed,0);stage.ExecuteBehaviour(context);}
        private static bool offsetAfterScale;
        private static void BeforeFraction(BehaviourContext __0,out float __state) { __state=__0.BodyComp.Position.X; }
        private static void AfterFraction(BehaviourContext __0,float __state)
        {
            var body=__0.BodyComp;
            body.Position.X=__state+(body.Position.X-__state)*PlayerPhysicsStep.Fraction(body);
            if(offsetAfterScale) body.Position.X=BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(body.Position.X),0)+1),0);
        }
        private static void Fractional(MotionObserver observer,BodyComp body,BehaviourContext context,UpdateXPositionFromVelocityBehaviour stage,Scale scale,Carry carry)
        {
            int cases=0; float previousFactor=scale.Factor;
            using(var physics=new OwnedPatches("subframe-charge.physics")) {
                physics.Add(typeof(UpdateXPositionFromVelocityBehaviour).GetMethod("ExecuteBehaviour"),
                    prefix:typeof(MotionObservationTests).GetMethod("BeforeFraction",OwnedPatches.Members),
                    postfix:typeof(MotionObservationTests).GetMethod("AfterFraction",OwnedPatches.Members));
                MotionObservation.RefreshPhysicsAdapters();
                foreach(float fraction in new[]{.25f,(float)(41667.0/166667/60)*60,.06f,.875f})
                foreach(float start in new[]{0f,63.6250038f,127.625008f,255.625015f,256.374969f,511.625031f,-.1f,-255.625015f})
                foreach(float speed in new[]{-1.5f,1.5f})
                foreach(float factor in new[]{.25f,.825f,1f,1.1f})
                foreach(float added in new[]{0f,.5f,1e-9f}) {
                    body.Position=new Vector2(start,0); body.Velocity=new Vector2(speed,0); scale.Factor=factor; carry.Speed=added; carry.IsPlayerOnBlock=true;
                    int calls=scale.Calls;
                    using(PlayerPhysicsStep.Enter(body,fraction)) stage.ExecuteBehaviour(context);
                    var sample=observer.Read();
                    Require(sample.Kind==(added==0?MotionKind.ControlledOnly:MotionKind.ExternalMotion),"Fractional classification lost: start="+start.ToString("R")+" reason="+sample.Reason);
                    Require(body.Position.X==start+sample.Step && scale.Calls==calls+1,"Fractional evidence changed movement or replayed a callback");
                    cases++;
                }
                carry.Speed=0; scale.Factor=1; offsetAfterScale=true;
                try {
                    body.Position=new Vector2(100,0); body.Velocity=new Vector2(1.5f,0);
                    using(PlayerPhysicsStep.Enter(body,.25f)) stage.ExecuteBehaviour(context);
                    Require(observer.Read().Kind==MotionKind.Unknown,"A one-ULP external offset was accepted as ordinary rounding");
                } finally { offsetAfterScale=false; }
            }
            MotionObservation.RefreshPhysicsAdapters(); carry.Speed=0; carry.IsPlayerOnBlock=false; scale.Factor=previousFactor;
            Console.WriteLine("[OK] Fractional motion: "+cases+" rounding/direction/material/carry cases; exact validation rejects an extra one-ULP displacement");
        }
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            var types=new[]{typeof(Scale),typeof(Carry),typeof(Conditional),typeof(HelperBranch),typeof(Escaping),typeof(Throwing),typeof(VectorFields),typeof(FieldEscape),typeof(AddressEscape)};
            var watch=Stopwatch.StartNew();
            using(var scope=MotionObservation.Prepare(types))
            {
                Console.WriteLine("[TIME] Motion preparation: "+watch.Elapsed.TotalMilliseconds.ToString("F2")+"ms");
                var body=new BodyComp(Vector2.Zero,18,26);var context=new BehaviourContext(body);
                var scale=new Scale();var carry=new Carry {IsPlayerOnBlock=true};
                var handlers=new LinkedList<IBlockBehaviour>();handlers.AddLast(carry);handlers.AddLast(scale);
                var stage=new UpdateXPositionFromVelocityBehaviour(handlers);
                using(var observer=scope.Observe(body))
                using(var second=scope.Observe(body))
                {
                    foreach(float speed in new[]{-1.5f,0,1.5f}) foreach(float factor in new[]{.25f,.825f,1.1f,2f})
                    {
                        scale.Factor=factor;int calls=scale.Calls;Tick(stage,context,speed);
                        var sample=observer.Read();
                        Require(sample.Kind==MotionKind.ControlledOnly,"Scale rejected: "+sample.Reason);
                        Require(sample.Step==speed*factor && body.Position.X==sample.Step,"Real speed changed");
                        Require(scale.Calls==calls+1 && scale.Reads==scale.Calls,"A callback/getter was replayed");
                        Require(second.Read().Sequence==sample.Sequence,"Consumers did not share one capture");
                    }
                    carry.Speed=.5f;scale.Factor=.5f;Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.ExternalMotion && observer.Read().NeutralStep==.25f,"Carry/scaling order was lost");
                    carry.IsPlayerOnBlock=false;Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.ControlledOnly,"Inactive carry was not identity");
                    Fractional(observer,body,context,stage,scale,carry);
                    var vector=new VectorFields();handlers.Clear();handlers.AddLast(vector);Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.ControlledOnly && body.Position.X==.75f && vector.Calls==1,"Dormant vector-field handler blocked observation");
                    vector.IsPlayerOnBlock=true;Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.Unknown && body.Position.X==2.75f && body.Velocity.X==-1.5f && vector.Calls==2,"Active direct movement was certified or changed");
                    vector.IsPlayerOnBlock=false;Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.ControlledOnly && vector.Calls==3,"Inactive handler did not recover");
                    handlers.Clear();handlers.AddLast(new FieldEscape());Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.Unknown && body.Velocity.X==1.5f,"Speed escaped through a value-type field");
                    var address=new AddressEscape();handlers.Clear();handlers.AddLast(address);Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.Unknown && address.Calls==1,"By-ref helper was certified or replayed");
                    handlers.Clear();handlers.AddLast(new Conditional());Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.Unknown,"Speed-dependent branch was certified");
                    var helper=new HelperBranch();handlers.Clear();handlers.AddLast(helper);
                    foreach(float speed in new[]{-1.5f,0,1.5f})
                    {
                        helper.IsPlayerOnBlock=false;int calls=helper.Calls;Tick(stage,context,speed);
                        Require(observer.Read().Kind==MotionKind.ControlledOnly && helper.Calls==calls,
                            "Dormant opaque helper disabled ordinary walking or was replayed");
                        foreach(float offset in new[]{0f,.5f})
                        {
                            helper.IsPlayerOnBlock=true;helper.Offset=offset;calls=helper.Calls;Tick(stage,context,speed);
                            Require(observer.Read().Kind==MotionKind.Unknown && body.Position.X==speed+offset && helper.Calls==calls+1,
                                "Executed opaque helper was certified, changed or replayed");
                        }
                        helper.IsPlayerOnBlock=false;Tick(stage,context,speed);
                        Require(observer.Read().Kind==MotionKind.ControlledOnly,"Ordinary walking didn't recover after an opaque helper");
                    }
                    var escaping=new Escaping();handlers.Clear();handlers.AddLast(escaping);Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.Unknown && escaping.Saved==1.5f,"Escaped speed was certified or changed");
                    var declared=new Declared();handlers.Clear();handlers.AddLast(declared);Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.ControlledOnly && observer.Read().IncludesDeclaredReport && declared.Calls==1,"Explicit single-call provider report failed");
                    var throwing=new Throwing();handlers.Clear();handlers.AddLast(throwing);
                    bool threw=false;try{Tick(stage,context,1.5f);}catch(InvalidOperationException e){threw=e.Message=="original failure";}
                    Require(threw && throwing.Calls==1 && observer.Read().Kind==MotionKind.Unknown,"Exception changed or observer leaked");
                    handlers.Clear();handlers.AddLast(scale);Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.ControlledOnly,"Exception poisoned next observation");
                    observer.Enabled=false;second.Enabled=false;long sequence=observer.Read().Sequence;Tick(stage,context,1.5f);
                    Require(observer.Read().Sequence==sequence,"Disabled capture still recorded");observer.Enabled=true;
                    Benchmark(stage,context,observer);
                    for(int i=0;i<7;i++)handlers.AddLast(scale);
                    Console.WriteLine("[PERF] Eight-handler pass:");Benchmark(stage,context,observer);
                    handlers.Clear();handlers.AddLast(scale);
                    ReviewedRebuild(stage,context,observer);
                    using(var patch=new OwnedPatches("test.motion.foreign"))
                    {
                        patch.Add(typeof(Scale).GetMethod("ModifyXVelocity"),postfix:typeof(MotionObservationTests).GetMethod("PatchResult",OwnedPatches.Members));
                        Tick(stage,context,1.5f);
                        Require(observer.Read().Kind==MotionKind.Unknown && body.Position.X==1.0f,"Foreign mutation remained certified or changed behavior");
                    }
                    Tick(stage,context,1.5f);Require(observer.Read().Kind==MotionKind.Unknown,"Removed patch silently restored stale proof");
                }
            }
            using(var scope=MotionObservation.Prepare(new[]{typeof(Scale)}))
            {
                var body=new BodyComp(Vector2.Zero,18,26);var context=new BehaviourContext(body);var handlers=new LinkedList<IBlockBehaviour>();handlers.AddLast(new Scale());
                using(var observer=scope.Observe(body)) {Tick(new UpdateXPositionFromVelocityBehaviour(handlers),context,1.5f);Require(observer.Read().Kind==MotionKind.ControlledOnly,"Scope re-entry failed");}
            }
            Console.WriteLine("[OK] Motion: single calls/getter reads, changing scales, ordered carry, inactive branches, refusal, explicit reports, exceptions, consumers, dormancy, patch invalidation and re-entry");
        }
        private static void Benchmark(UpdateXPositionFromVelocityBehaviour stage,BehaviourContext context,MotionObserver observer)
        {
            const int count=200000;for(int i=0;i<1000;i++)Tick(stage,context,1.5f);
            // installed CLR has a GC allocation counter, look it up once and bind a delegate
            // no reflection in the loop
            var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);
            var allocated=method==null?null:(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
            long bytes=allocated==null?0:allocated();var clock=Stopwatch.StartNew();
            for(int i=0;i<count;i++)Tick(stage,context,1.5f);
            clock.Stop();long difference=allocated==null?-1:allocated()-bytes;
            Console.WriteLine("[PERF] Motion observed "+(clock.Elapsed.TotalMilliseconds*1000/count).ToString("F3")+"us/pass; allocation bytes="+difference);
            if(allocated!=null)Require(difference<1024,"Steady-state observer allocates per tick");
            observer.Enabled=false;clock.Restart();for(int i=0;i<count;i++)Tick(stage,context,1.5f);clock.Stop();observer.Enabled=true;
            Console.WriteLine("[PERF] Motion disabled "+(clock.Elapsed.TotalMilliseconds*1000/count).ToString("F3")+"us/pass");
        }
    }
}
