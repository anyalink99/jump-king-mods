using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JKRuntime.World;

namespace JKRuntime
{
    internal static class WorldRuntimeTests
    {
        private sealed class Gate
        {
            [WorldField] public bool Open;
            [WorldField] public Dictionary<int,Cell> Cells=new Dictionary<int,Cell>();
            public int LocalPreference=17;
        }
        private sealed class Cell {[WorldField] public float Phase;}
        private static int checks;
        private static void Check(bool value,string message) {checks++;if(!value)throw new Exception(message);}
        private static void Reject(Action action,string message)
        {bool refused=false;try{action();}catch(Exception){refused=true;}Check(refused,message);}
        private static void Main(string[] args)
        {
            System.Reflection.Assembly.LoadFrom(args[0]);
            Contexts();Transactions();Codec();Authority();Inventory();NativeBoundaries();Trees();
            Console.WriteLine("[OK] Runtime world: "+checks+" checks (nested contexts, thread ownership, bounded effects, state isolation, stale plans, rollback, authority restoration and honest coverage)");
        }
        private static void Contexts()
        {
            var frame=new WorldFrame("map",1,2,3,WorldRole.Host,WorldPhase.Contact);
            var outer=WorldExecution.Enter(frame);var inner=WorldExecution.Enter(new WorldFrame("map",1,2,4,WorldRole.Host,WorldPhase.Input));
            Reject(outer.Dispose,"Out-of-order context disposal accepted");inner.Dispose();Check(WorldExecution.Current.Actor==3,"Nested context didn't restore actor");
            Exception error=null;var thread=new Thread(delegate(){try{outer.Dispose();}catch(Exception e){error=e;}});thread.Start();thread.Join();Check(error!=null,"Another thread released the context");
            int effects=0;long prior=WorldExecution.EffectSequence;
            Check(WorldExecution.Emit("sound",new byte[]{1},()=>effects++),"Host effect suppressed");
            using(WorldExecution.Enter(new WorldFrame("map",1,2,3,WorldRole.Playback,WorldPhase.Restore)))
                Check(!WorldExecution.Emit("sound",new byte[]{1},()=>effects++),"Restore emitted a sound");
            Check(effects==1 && WorldExecution.ReadEffects(prior).Length==1,"Effect journal duplicated suppression");
            var payload=WorldExecution.ReadEffects(prior)[0].Payload;payload[0]=9;Check(WorldExecution.ReadEffects(prior)[0].Payload[0]==1,"Effect payload wasn't isolated");
            for(int i=0;i<300;i++)WorldExecution.Emit("fixture",new byte[0],null);
            Check(WorldExecution.ReadEffects(0).Length==256,"Effect journal grew without bound");outer.Dispose();Check(WorldExecution.Current==null,"Context leaked");
            Reject(()=>WorldExecution.Run(frame,delegate{throw new Exception("fixture");}),"Callback didn't throw");Check(WorldExecution.Current==null,"Exception leaked actor context");
        }
        private static void Transactions()
        {
            var registry=new WorldRegistry();int a=1,b=2;
            using(registry.Register("a",1,()=>new[]{(byte)a},data=>()=>a=data[0]))
            using(registry.Register("b",2,()=>new[]{(byte)b},data=>()=>{b=data[0];if(b==99)throw new Exception("apply failure");})) {
                a=8;b=99;byte[] failed=registry.Capture();a=1;b=2;
                Reject(()=>registry.Apply(failed),"Throwing world application accepted");Check(a==1&&b==2,"Rollback didn't include the partially failing state");
                a=9;b=3;byte[] valid=registry.Capture();a=1;b=2;registry.Apply(valid);Check(a==9&&b==3,"Valid transaction failed after successful rollback");
                Action stale=registry.Prepare(valid);using(registry.Register("c",3,()=>new byte[0],data=>delegate{}))Reject(stale,"A stale world application survived registration changes");
                Check(registry.Inspect().Select(s=>s.Id).SequenceEqual(new[]{"a","b"}) && registry.Capture().SequenceEqual(valid),"Removing a world registration left stale snapshot metadata");
                using(registry.Register("0",4,()=>new byte[0],data=>delegate{})) {
                    Check(registry.Inspect()[0].Id=="0","New world registration wasn't inserted in canonical order");
                    registry.Apply(registry.Capture());
                }
                for(int length=0;length<valid.Length;length++)Reject(()=>registry.Prepare(valid.Take(length).ToArray()),"Truncated world payload accepted");
                byte[] extra=valid.Concat(new byte[]{0}).ToArray();Reject(()=>registry.Prepare(extra),"Trailing world bytes accepted");
            }
            Check(registry.Inspect().Length==0,"World registration survived release");
            var poison=new WorldRegistry();bool broken=false;
            var failedLease=poison.Register("fail",1,()=>new byte[]{0},data=>()=>{if(broken)throw new Exception("rollback failure");broken=true;throw new Exception("apply failure");});
            {
                Reject(()=>poison.Apply(poison.Capture()),"Broken rollback accepted");Reject(()=>poison.Capture(),"Poisoned world kept operating");
                Reject(failedLease.Dispose,"Poisoned registry hid incomplete state through unregister");
            }
        }
        private static void Codec()
        {
            var gate=new Gate{Open=true};gate.Cells.Add(1,new Cell{Phase=.5f});var codec=new WorldStateCodec(typeof(Gate),true);
            byte[] bytes=codec.Capture(gate);var copy=codec.Decode(bytes);var cells=gate.Cells;var cell=cells[1];gate.Open=false;cell.Phase=0;gate.LocalPreference=42;
            codec.Apply(gate,copy);Check(gate.Open&&cell.Phase==.5f&&ReferenceEquals(gate.Cells,cells)&&ReferenceEquals(cell,cells[1]),"Declared state broke references");
            Check(gate.LocalPreference==42,"World state overwrote a personal field");
            var registry=new WorldRegistry();using(registry.Attach("fixture.gate",gate)) {
                var initial=registry.Capture();gate.Open=false;registry.Apply(initial);Check(gate.Open,"Declarative attachment didn't restore state");
            }
            Reject(()=>new WorldStateCodec(typeof(Action),true),"Delegate accepted as world state");
        }
        private static void Authority()
        {
            var gate=new Gate();using(WorldRegistry.Shared.Attach("fixture",gate)) {
                gate.Open=true;byte[] target=WorldRegistry.Shared.Capture();gate.Open=false;
                using(var receiver=WorldControl.Begin("test","map",1,WorldRole.Replica)) {
                    Check(!WorldExecution.CanPersist&&!WorldExecution.Emit("fixture",new byte[0],null),"Unscoped replica work bypassed authority");
                    Reject(()=>WorldRegistry.Shared.Attach("late",new Gate()),"Shared registrations changed during receiver preservation");
                    receiver.Receive(target);Check(gate.Open,"Receiver didn't apply host state");
                    Reject(()=>WorldControl.Begin("other","map",1,WorldRole.Host),"Conflicting world authority accepted");
                    receiver.SetActors(new[]{new WorldActor(1,1,Microsoft.Xna.Framework.Vector2.Zero,Microsoft.Xna.Framework.Vector2.Zero,true)});
                    Check(WorldControl.Actors.Length==1,"World actors unavailable");
                    gate.Open=false;using(WorldControl.BeforeUpdate())Check(!WorldExecution.CanSimulate&&!gate.Open,"Replica advanced shared world or restored every tick");
                    WorldControl.Maintain();Check(gate.Open,"Explicit legacy state maintenance failed");
                }
                Check(!gate.Open&&WorldControl.CurrentRole==WorldRole.Local&&WorldControl.Actors.Length==0,"Stopping receiver didn't restore local state and actors");
            }
            var objects=new WorldObjects();using(objects.Add("gate.1",gate))Check(ReferenceEquals(objects.Find("gate.1"),gate),"Stable object lookup failed");
            Check(objects.Ids.Length==0,"World object survived lifetime");
        }
        private static void Inventory()
        {
            WorldObservation.Prepare(new System.Reflection.Assembly[]{null,typeof(Foreign).Assembly});
            var report=WorldObservation.Analyze(typeof(Foreign));Check(report.Status.Contains("unresolved"),"Observation was advertised as full simulation support");
            Check(report.Reasons.Any(r=>r.Contains("static"))&&report.Reasons.Contains("file IO")&&report.Reasons.Contains("external clock or random source"),"Foreign bypasses weren't reported");
        }
        [WorldMechanic] private sealed class Mechanic : EntityComponent.Entity
        {internal int Updates;internal bool Fail;protected override void Update(float delta){Updates++;if(Fail)throw new InvalidOperationException("fixture");}}
        private static void NativeBoundaries()
        {
            var mechanic=(Mechanic)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Mechanic));
            typeof(EntityComponent.Entity).GetField("m_components",OwnedPatches.Members).SetValue(mechanic,new List<EntityComponent.Component>());
            using(WorldObservation.Install()){
                mechanic.UpdateComponents(.01f);Check(mechanic.Updates==1,"Local native entity didn't update");
                using(var control=WorldControl.Begin("test.native","map",1,WorldRole.Replica)){
                    mechanic.UpdateComponents(.01f);Check(mechanic.Updates==1,"Replica ran declared native world entity");
                    using(control.Enter(8,WorldPhase.Personal))Check(WorldExecution.CanPersist,"Replica personal action lost persistence");
                }
                using(var control=WorldControl.Begin("test.native","map",1,WorldRole.Host)){
                    mechanic.UpdateComponents(.01f);Check(mechanic.Updates==2,"Host native entity was suppressed");
                    using(control.Enter(0,WorldPhase.Restore))Check(!WorldExecution.CanSimulate&&!WorldExecution.CanPersist,"Host restore allowed simulation or saving");
                }
            }
            using(var scope=new RuntimeScope())NativeWorldInteractions.Prepare(scope);
            Check(WorldRegistry.Shared.Inspect().Length==0,"Native interaction state survived its owner");
            var links=new[]{new JumpKing.Level.TeleportLink(7),new JumpKing.Level.TeleportLink(8)};
            var first=links[0];NativeSideLinks.SetPair(links,-1,512);Check(ReferenceEquals(first,links[0])&&links[1].GetIndex1()==512,"Native integer topology changed an unowned link");
        }
        private static void Trees()
        {
            var program=new WorldTreeProgram(new[]{new WorldTreeInstruction(WorldTreeOp.Repeat,new[]{1},3),new WorldTreeInstruction(WorldTreeOp.Leaf,new int[0],0)});
            var state=new WorldTreeState{Result=WorldTreeResult.Running,Nodes=new WorldTreeNodeState[2]};int calls=0;
            WorldTreeLeaf leaf=delegate(int n,ref WorldTreeNodeState node,ref int budget){calls++;return WorldTreeResult.Success;};
            Action<int> reset=n=>state.Nodes[n]=new WorldTreeNodeState();
            for(int i=1;i<=3;i++){int budget=10;var result=program.Step(state,0,ref budget,leaf,reset);Check(calls==i&&result==(i==3?WorldTreeResult.Success:WorldTreeResult.Running),"Repeat didn't yield or retain its cursor");}
            var codec=new WorldStateCodec(typeof(WorldTreeState),true);var restored=(WorldTreeState)codec.Decode(codec.Capture(state));
            Check(restored.Nodes[0].Iterations==3,"Tree cursor didn't round-trip through common state");
            Reject(()=>new WorldTreeProgram(new[]{new WorldTreeInstruction(WorldTreeOp.Repeat,new[]{0},0)}),"Cyclic behavior tree accepted");
            Reject(()=>new WorldTreeProgram(new[]{new WorldTreeInstruction(WorldTreeOp.Leaf,new int[0],0),new WorldTreeInstruction(WorldTreeOp.Leaf,new int[0],0)}),"Unreachable tree instruction accepted");
            var before=state.Nodes[0].Iterations;int empty=0;Reject(()=>program.Step(state,0,ref empty,leaf,reset),"Empty tree budget executed");Check(state.Nodes[0].Iterations==before,"Budget failure changed tree state");
        }
        private sealed class Foreign
        {private static int counter;public void Update(){counter++;if(DateTime.Now.Ticks==0)File.WriteAllText("unused","unused");}}
    }
}
