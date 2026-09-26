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
        private static void Tick(UpdateXPositionFromVelocityBehaviour stage,BehaviourContext context,float speed)
        {context.BodyComp.Position=Vector2.Zero;context.BodyComp.Velocity=new Vector2(speed,0);stage.ExecuteBehaviour(context);}
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            var types=new[]{typeof(Scale),typeof(Carry),typeof(Conditional),typeof(Escaping),typeof(Throwing)};
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
                    handlers.Clear();handlers.AddLast(new Conditional());Tick(stage,context,1.5f);
                    Require(observer.Read().Kind==MotionKind.Unknown,"Speed-dependent branch was certified");
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
            // GC allocation counter is available on the installed CLR, queried
            // once and bound to a delegate rather than reflected in the loop.
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
