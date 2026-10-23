using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using EntityComponent;
using EntityComponent.BT;
using JKRuntime.Gameplay;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class PlayerUpdatesTests
    {
        private sealed class Player : PlayerEntity { internal int WorldUpdates; protected override void Update(float delta) { WorldUpdates++; } protected override void OnDestroy() { } }
        private static int brainTicks;
        private static Action brainStep;
        private sealed class Empty : BehaviorTree.IBTnode { protected override BehaviorTree.BTresult MyRun(BehaviorTree.TickData data) { brainTicks++; if(brainStep!=null)brainStep(); return BehaviorTree.BTresult.Running; } }
        private class Marker : Component
        {
            internal string Name;
            internal bool Late;
            internal Action Step;
            protected override void Update(float delta) { trace.Add(Name); if(Step!=null) Step(); }
            protected override void LateUpdate(float delta) { if(Late) trace.Add(Name+"-late"); }
        }
        private sealed class ControlMarker : Marker, IPlayerControlObserver { }
        private static readonly List<string> trace = new List<string>();
        private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        private static void Throws(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected refusal"); }
        private static Player Make()
        {
            var player = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
            typeof(Entity).GetField("m_components", OwnedPatches.Members).SetValue(player, new List<Component>());
            player.m_body = new BodyComp(Vector2.Zero, 18, 26);
            var brain = new BehaviorTreeComp(new Empty());
            typeof(PlayerEntity).GetField("m_bt", OwnedPatches.Members).SetValue(player, brain);
            player.AddComponents(false, player.m_body, new InputComponent(), brain);
            player.AddComponents(new Marker { Name = "tail" });
            return player;
        }
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            using (var world = new RuntimeScope())
            {
                PlayerUpdates.Prepare(world);
                var player = Make(); var other = Make();
                Component[] initial = player.GetComponents();
                using (var b = PlayerUpdates.Register(player, "test.b", PlayerUpdatePhase.BeforeInput, d => trace.Add("before-b")))
                using (var a = PlayerUpdates.Register(player, "test.a", PlayerUpdatePhase.BeforeInput, d => trace.Add("before-a")))
                using (var after = PlayerUpdates.Register(player, "test.after", PlayerUpdatePhase.AfterInput, d => trace.Add("after")))
                {
                    trace.Clear(); player.UpdateComponents(.01f);
                    Check(trace.SequenceEqual(new[] { "before-a", "before-b", "after", "tail" }), "Stable ordering and disabled input still publishes the native slot");
                    Check(initial.SequenceEqual(player.GetComponents()), "No components inserted or reordered");
                    trace.Clear(); other.UpdateComponents(.01f);
                    Check(trace.SequenceEqual(new[] { "tail" }), "Registrations are player-specific");
                    Throws(() => PlayerUpdates.Register(player, "test.after", PlayerUpdatePhase.AfterInput, d => { }));
                    var list = (List<Component>)typeof(Entity).GetField("m_components", OwnedPatches.Members).GetValue(player);
                    var input = player.GetComponent<InputComponent>(); list.Remove(input); list.Insert(0, input);
                    Throws(() => player.UpdateComponents(.01f));
                    list.Remove(input); list.Insert(1, input);
                    trace.Clear(); player.UpdateComponents(.01f);
                    Check(trace.Count == 4, "Valid ordering can resume after refusal");
                }
                trace.Clear(); player.UpdateComponents(.01f);
                Check(trace.SequenceEqual(new[] { "tail" }), "Dispose removes callbacks without changing enumeration");
                PlayerUpdates.Registration self = null, victim = null;
                self = PlayerUpdates.Register(player, "test.self", PlayerUpdatePhase.AfterInput, d => { trace.Add("self"); self.Dispose(); victim.Dispose(); }, -1);
                victim = PlayerUpdates.Register(player, "test.victim", PlayerUpdatePhase.AfterInput, d => trace.Add("victim"));
                trace.Clear(); player.UpdateComponents(.01f); player.UpdateComponents(.01f);
                Check(trace.SequenceEqual(new[] { "self", "tail", "tail" }), "Disposal during dispatch neither mutates native iteration nor calls a removed peer");
                using (var fail = PlayerUpdates.Register(player, "test.failure", PlayerUpdatePhase.AfterInput, d => { throw new InvalidOperationException("fixture"); }))
                {
                    Throws(() => player.UpdateComponents(.01f));
                    Check(fail.LastError == "fixture", "Gameplay failure propagates and remains diagnosable");
                }
                var dying = PlayerUpdates.Register(player, "test.destroy", PlayerUpdatePhase.AfterInput, d => trace.Add("dead"));
                player.Destroy();
                Check(dying.Disposed, "Native destruction releases phase ownership");
                dying.Dispose();
                other.Destroy();
                var stepped=Make(); var observer=new Marker { Name="fast",Late=true }; stepped.AddComponents(observer);
                using(var phases=PlayerUpdates.Register(stepped,"test.native",PlayerUpdatePhase.BeforeInput,d=>trace.Add("native-slot")))
                using(var fast=PlayerUpdates.RegisterSubsteps(stepped,"test.fast",c=>ReferenceEquals(c,observer)))
                {
                    trace.Clear();
                    for(int i=0;i<4;i++) fast.Advance(1f/240);
                    Check(trace.SequenceEqual(Enumerable.Repeat(new[]{"fast","fast-late"},4).SelectMany(x=>x)),"Substeps retain update/late order and don't run native callbacks or unrelated components");
                    Check(stepped.WorldUpdates==0,"Fast player steps never execute the save/world update");
                    trace.Clear(); stepped.UpdateComponents(1f/60);
                    Check(trace.SequenceEqual(new[]{"native-slot","tail"}) && stepped.WorldUpdates==1,"Native tick keeps world update and callbacks, without double-stepping owned components");
                    Throws(()=>PlayerUpdates.RegisterSubsteps(stepped,"test.second",c=>false));
                }
                trace.Clear(); stepped.UpdateComponents(1f/60);
                Check(trace.SequenceEqual(new[]{"tail","fast","fast-late"}),"Releasing simulation returns components to native cadence");
                bool nativeControls=true;
                stepped.GetComponent<BehaviorTreeComp>().Enabled=true; brainTicks=0;
                using(var fast=PlayerUpdates.RegisterSubsteps(stepped,"test.alternate",c=>ReferenceEquals(c,observer),()=>nativeControls))
                {
                    for(int i=0;i<4;i++)fast.Advance(1f/240);
                    Check(brainTicks==0,"Alternate controller ran in fractional slots");
                    stepped.UpdateComponents(1f/60);
                    Check(brainTicks==1,"Alternate controller lost its native slot");
                    nativeControls=false;
                    for(int i=0;i<4;i++)fast.Advance(1f/240);
                    stepped.UpdateComponents(1f/60);
                    Check(brainTicks==5,"Returning native control duplicated or lost tree updates");
                }
                bool inFrame=false;
                var begin=new ControlMarker { Name="begin",Step=()=> { Check(!inFrame,"Control frame started twice"); inFrame=true; nativeControls=!nativeControls; } };
                var end=new ControlMarker { Name="end",Step=()=> { Check(inFrame,"Control frame ended without beginning"); inFrame=false; } };
                stepped.AddComponents(begin,end);
                var slots=(List<Component>)typeof(Entity).GetField("m_components",OwnedPatches.Members).GetValue(stepped);
                slots.Remove(begin); slots.Remove(end); slots.Insert(0,begin);
                slots.Insert(slots.IndexOf(stepped.GetComponent<BehaviorTreeComp>())+1,end);
                brainStep=()=>Check(inFrame,"Tree ran outside its control observers");
                nativeControls=true; brainTicks=0;
                using(var fast=PlayerUpdates.RegisterSubsteps(stepped,"test.control-observers",c=>false,()=>nativeControls))
                {
                    stepped.UpdateComponents(1f/60);
                    Check(brainTicks==1 && !nativeControls && !inFrame,"Native form transition split control observers");
                    fast.Advance(1f/240);
                    Check(brainTicks==2 && nativeControls && !inFrame,"Substep form transition split control observers");
                    fast.Advance(1f/240);
                    Check(brainTicks==2 && !inFrame,"Native controls leaked into a substep after transition");
                }
                brainStep=null; slots.Remove(begin); slots.Remove(end);
                var boundaryType=typeof(NativeGameplayObserver).GetNestedType("Boundary",BindingFlags.NonPublic);
                var boundary=(Component)Activator.CreateInstance(boundaryType,true);
                int boundaries=0;
                boundaryType.GetField("Step",OwnedPatches.Members).SetValue(boundary,(Action)(()=> { inFrame=true; boundaries++; }));
                boundaryType.GetField("Late",OwnedPatches.Members).SetValue(boundary,(Action)(()=>inFrame=false));
                stepped.AddComponents(boundary); slots.Remove(boundary); slots.Insert(0,boundary);
                brainStep=()=>Check(inFrame,"Alternate jump wasn't inside gameplay event observation");
                nativeControls=true; brainTicks=0;
                using(var fast=PlayerUpdates.RegisterSubsteps(stepped,"test.gameplay-events",c=>false,()=>nativeControls))
                {
                    for(int i=0;i<4;i++) fast.Advance(1f/240);
                    stepped.UpdateComponents(1f/60);
                    Check(boundaries==5 && brainTicks==1 && !inFrame,"Native control events or fractional body events lost their observation pass");
                    nativeControls=false;
                    for(int i=0;i<4;i++) fast.Advance(1f/240);
                    stepped.UpdateComponents(1f/60);
                    Check(boundaries==9 && brainTicks==5 && !inFrame,"Fractional controls duplicated gameplay event observation");
                }
                brainStep=null; slots.Remove(boundary);
                using(var fast=PlayerUpdates.RegisterSubsteps(stepped,"test.destroy-fast",c=>ReferenceEquals(c,observer)))
                {
                    observer.Step=stepped.Destroy; trace.Clear(); fast.Advance(1f/240);
                    Check(trace.SequenceEqual(new[]{"fast"}),"Destruction during a step cancels late updates and safely releases ownership");
                }
            }
            // No preparation lease is required for correctness, and the last callback can remove itself.
            var fallback = Make(); PlayerUpdates.Registration last = null;
            last = PlayerUpdates.Register(fallback, "test.last", PlayerUpdatePhase.AfterInput, d => last.Dispose());
            fallback.UpdateComponents(.01f); fallback.UpdateComponents(.01f); fallback.Destroy();
            Check(last.Disposed, "Fallback activation and final removal inside dispatch");
            Console.WriteLine("[OK] Player phases: actual native enumeration, ordering, lifetime, self-removal and failure propagation");
        }
    }
}
