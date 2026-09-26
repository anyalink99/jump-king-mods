using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using JKRuntime.Gameplay;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using UnfamiliarInterop;

namespace JKRuntime
{
    internal static class InteropTests
    {
        private static void Require(bool value,string reason) { if(!value)throw new Exception(reason); }
        private static void ForeignPatch() { }
        private sealed class CollisionQuery : ICollisionQuery
        {
            internal bool Blocking;
            internal int Calls;
            private readonly AdvCollisionInfo info=new AdvCollisionInfo();
            public AdvCollisionInfo GetCollisionInfo(Rectangle rect) { return info; }
            public bool CheckCollision(Rectangle rect,out Rectangle overlap,out AdvCollisionInfo value)
            { Calls++; overlap=default(Rectangle); value=info; return Blocking && rect.Y>10; }
            public bool CheckCollision(Rectangle rect,out Rectangle overlap,out AggregateCollisionInfo value)
            { throw new Exception("Wrong overload"); }
            public bool IsInWater(Rectangle rect) { return false; }
        }
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            Materials(); Validity(); Lifetimes();
            for(int i=0;i<3;i++)Observe(i%2==0);
            Console.WriteLine("[OK] Interop: support budgets, ownership, actual contacts, stage deltas, shared bounded trace, patch invalidation, repeated lifetimes");
        }
        private static void Lifetimes()
        {
            var scope=MovementObservation.Prepare();
            var body=new BodyComp(Vector2.Zero,18,26); var context=new BehaviourContext(body);
            var observer=scope.Observe(body); scope.Dispose();
            new UpdateXPositionFromVelocityBehaviour(new LinkedList<IBlockBehaviour>()).ExecuteBehaviour(context);
            Require(observer.Read(MovementStage.XMovement).Covered,"Preparation disposal broke an active observer");
            observer.Dispose(); observer.Dispose();
            using(var next=MovementObservation.Prepare())using(var clean=next.Observe(body))
                Require(clean.Read(MovementStage.XMovement).Sequence==0,"Released body retained old samples");
        }
        private static void Materials()
        {
            var registry=new MaterialRegistry();
            var surface=new Surface(new Rectangle(0,30,40,10));
            Rectangle body=new Rectangle(10,4,18,26);
            var budget=new SupportBudget(3);
            using(registry.Register("test.interop",new MaterialCapabilities(typeof(Surface),SpeedCapability.Scaling,SupportPredicates.TopFace,"test.geometry","test.state",Surface.Capture)))
            {
                Require(registry.Inspect().Length==1 && registry.Inspect()[0].Owner=="test.interop" && surface.Reads==0,"Inventory invoked provider or lost owner");
                Require(registry.Resolve(typeof(Surface)).GeometryProfile=="test.geometry","Typed links lost");
                Require(registry.QuerySupport(surface,body,Vector2.Zero,false,ref budget)==SupportKind.Supported,"Top face absent");
                surface.State=0;
                Require(registry.QuerySupport(surface,body,Vector2.Zero,false,ref budget)==SupportKind.Unsupported,"State snapshot stale");
                surface.State=1;
                Require(registry.QuerySupport(surface,body,new Vector2(0,-1),false,ref budget)==SupportKind.Unsupported,"Upward support");
                Require(registry.QuerySupport(surface,body,Vector2.Zero,false,ref budget)==SupportKind.Unknown && surface.Reads==3,"Exhaustion still invoked provider");
                bool duplicate=false;
                try { registry.Register("test.other",new MaterialCapabilities(typeof(Surface),SpeedCapability.Identity)); } catch(InvalidOperationException) { duplicate=true; }
                Require(duplicate,"Duplicate ownership accepted");
            }
            budget=new SupportBudget(4);
            Require(registry.Resolve(typeof(Surface))==null && registry.QuerySupport(surface,body,Vector2.Zero,false,ref budget)==SupportKind.Unknown,"Declaration survived release");
            using(registry.Register("test.interop",new MaterialCapabilities(typeof(Surface),SpeedCapability.Unknown,delegate { throw new Exception("provider failure"); })))
                Require(registry.QuerySupport(typeof(Surface),new SupportQuery(body,((IBlock)surface).GetRect(),Vector2.Zero,false,1),ref budget)==SupportKind.Unknown,"Throw certified");
            IDisposable owned=null;
            owned=registry.Register("test.interop",new MaterialCapabilities(typeof(Surface),SpeedCapability.Unknown,delegate { owned.Dispose(); return SupportKind.Supported; }));
            Require(registry.QuerySupport(typeof(Surface),new SupportQuery(body,((IBlock)surface).GetRect(),Vector2.Zero,false,1),ref budget)==SupportKind.Unknown,"Mutation during query accepted");
            owned.Dispose();
            Require(registry.Resolve(typeof(Surface))==null,"Failed disposal was not retryable");
        }
        private static void Validity()
        {
            var method=typeof(Handler).GetMethod("ModifyXVelocity");
            using(var lease=MethodValidity.Watch(method))
            using(var same=MethodValidity.Watch(method))
            {
                Require(lease.IsValid && same.Generation==lease.Generation,"Unshared generation");
                using(var patch=new OwnedPatches("test.foreign"))
                {
                    patch.Add(method,prefix:typeof(InteropTests).GetMethod("ForeignPatch",OwnedPatches.Members));
                    Require(!lease.IsValid && !same.IsValid,"Patch failed to invalidate leases");
                    using(var rejected=MethodValidity.Watch(method))Require(!rejected.IsValid,"Existing foreign patch accepted");
                    using(var reviewed=MethodValidity.Watch(method,"test.foreign"))Require(reviewed.IsValid,"Reviewed generation refused");
                }
                Require(!lease.IsValid,"Unpatch resurrected stale lease");
                using(var renewed=MethodValidity.Watch(method))Require(renewed.IsValid && renewed.Generation!=lease.Generation,"Cannot renew generation");
            }
        }
        private static void Observe(bool movementFirst)
        {
            MovementObservationScope movement=null; MotionObservationScope arithmetic=null;
            try
            {
                if(movementFirst)movement=MovementObservation.Prepare();
                arithmetic=MotionObservation.Prepare(new[]{typeof(Handler)});
                if(movement==null)movement=MovementObservation.Prepare();
                var body=new BodyComp(Vector2.Zero,18,26); var context=new BehaviourContext(body);
                var handler=new Handler(); var handlers=new LinkedList<IBlockBehaviour>(); handlers.AddLast(handler);
                var step=new UpdateXPositionFromVelocityBehaviour(handlers);
                using(var observe=movement.Observe(body))
                using(var peer=movement.Observe(body))
                using(var motion=arithmetic.Observe(body))
                {
                    body.Velocity=new Vector2(2,1); step.ExecuteBehaviour(context);
                    var first=observe.Read(MovementStage.XMovement);
                    Require(first.Covered && first.Completed && first.PositionAfter.X-first.PositionBefore.X==1,"Stage delta or coverage missing");
                    Require(motion.Read().Kind==MotionKind.ControlledOnly && handler.Calls==1,"Observers interfere with arithmetic or repeat calls");
                    Require(peer.Read(MovementStage.XMovement).Sequence==first.Sequence,"Duplicate body observation");
                    Require(observe.Capture().Read().Length==0,"Trace recording was not dormant");
                    observe.Recording=true;
                    for(int i=0;i<300;i++)step.ExecuteBehaviour(context);
                    var trace=observe.Capture(); var rows=trace.Read();
                    Require(rows.Length==256 && trace.Dropped==44 && rows[255].Sequence-rows[0].Sequence==255,"Ring wrap/order invalid");
                    string json=trace.ToJson(); Require(json.Contains("jkruntime.movement-trace.v1"),"Missing export schema");
                    string path=Path.Combine(Path.GetTempPath(),"runtime-trace-"+Guid.NewGuid().ToString("N")+".json");
                    try { trace.Export(path); bool refused=false; try { trace.Export(path); } catch(IOException) { refused=true; } Require(refused && File.ReadAllText(path)==json,"Export overwrote an existing file"); }
                    finally { File.Delete(path); }
                    observe.Enabled=false; peer.Enabled=false;
                    long sequence=observe.Read(MovementStage.XMovement).Sequence;
                    step.ExecuteBehaviour(context); Require(observe.Read(MovementStage.XMovement).Sequence==sequence,"Disabled observer sampled");
                    observe.Enabled=true; handler.Throw=true;
                    bool threw=false; try { step.ExecuteBehaviour(context); } catch(InvalidOperationException) { threw=true; }
                    Require(threw && !observe.Read(MovementStage.XMovement).Completed,"Original exception swallowed");
                    handler.Throw=false; step.ExecuteBehaviour(context); Require(observe.Read(MovementStage.XMovement).Covered,"Exception poisoned later sample");
                    Contacts(body,context,handlers,handler,observe);
                    Benchmark(step,context,observe);
                    using(var patch=new OwnedPatches("test.foreign-stage"))
                    {
                        patch.Add(typeof(UpdateXPositionFromVelocityBehaviour).GetMethod("ExecuteBehaviour"),prefix:typeof(InteropTests).GetMethod("ForeignPatch",OwnedPatches.Members));
                        step.ExecuteBehaviour(context);
                        Require(!observe.Read(MovementStage.XMovement).Covered && motion.Read().Kind==MotionKind.Unknown,"Foreign stage patch remained certified");
                    }
                }
            }
            finally { if(arithmetic!=null)arithmetic.Dispose(); if(movement!=null)movement.Dispose(); }
        }
        private static void Contacts(BodyComp body,BehaviourContext context,LinkedList<IBlockBehaviour> handlers,Handler handler,MovementObserver observer)
        {
            var query=new CollisionQuery();
            var resolver=(ResolveYCollisionBehaviour)Activator.CreateInstance(typeof(ResolveYCollisionBehaviour),OwnedPatches.Members,null,new object[]{query,handlers},null);
            body.Position=new Vector2(0,12); body.Velocity=new Vector2(0,1); query.Blocking=true;
            int calls=handler.Calls;
            resolver.ExecuteBehaviour(context); var sample=observer.Read(MovementStage.YCollision);
            Require(sample.Covered && sample.Contact.Path==ContactPath.Native && sample.Contact.AcceptedQueries==2 && sample.Contact.Direction==1 && sample.Contact.Probe.Y==11 && body.Position.Y==10,"Native contact evidence incorrect");
            Require(query.Calls==3 && handler.Calls==calls+1,"Native collision work repeated");
            query.Blocking=false; handler.Collision=true; body.Position=new Vector2(0,12); body.Velocity=new Vector2(0,1); calls=handler.Calls;
            resolver.ExecuteBehaviour(context); sample=observer.Read(MovementStage.YCollision);
            Require(sample.Covered && sample.Contact.Path==ContactPath.Additional && sample.Contact.HandlerType==typeof(Handler) && sample.Contact.AcceptedQueries==2,"Additional contact source absent");
            Require(handler.Calls==calls+3,"Additional callback replayed");
            handler.Collision=false; body.Position=Vector2.Zero; body.Velocity=new Vector2(0,-1); resolver.ExecuteBehaviour(context);
            Require(observer.Read(MovementStage.YCollision).Contact.Path==ContactPath.None,"Previous contact leaked");
        }
        private static void Benchmark(UpdateXPositionFromVelocityBehaviour step,BehaviourContext context,MovementObserver observer)
        {
            var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Static|BindingFlags.Public);
            if(method==null)return;
            var bytes=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
            context.BodyComp.Velocity=new Vector2(1,0);
            for(int i=0;i<1000;i++)step.ExecuteBehaviour(context);
            var clock=new Stopwatch(); long before=bytes(); clock.Start();
            for(int i=0;i<100000;i++)step.ExecuteBehaviour(context);
            clock.Stop(); long allocated=bytes()-before;
            Require(allocated==0,"Observation allocated per pass: "+allocated);
            Console.WriteLine("[TIME] Shared stage + arithmetic + recording: "+(clock.Elapsed.TotalMilliseconds/100).ToString("F3")+"us/pass; "+allocated+" bytes/100000");
        }
    }
}
