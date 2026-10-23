using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using JKRuntime;
using JKRuntime.Simulation;
using JumpKingMultiplayer.Models;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class WorldTests
    {
        private sealed class Group {public bool State;public float Progress;}
        private sealed class State {public Dictionary<int,Group> Groups=new Dictionary<int,Group>();public HashSet<int> Touched=new HashSet<int>();public int Tick;}
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        private static int identityReads;
        private static ulong identityValue;
        private static bool Identity(ref ulong? __result)
        {identityReads++;__result=identityValue;return false;}
        private static void FrameIdentity()
        {
            var originals=new Dictionary<FieldInfo,object>();
            foreach(var pair in new[]{Tuple.Create(typeof(AdvancedSession),"lobby",(object)1UL),Tuple.Create(typeof(AdvancedSession),"owner",(object)ulong.MaxValue),
                Tuple.Create(typeof(WorldSession),"enabled",(object)true),Tuple.Create(typeof(WorldSession),"received",(object)true),
                Tuple.Create(typeof(WorldSession),"worldMap",(object)44UL),Tuple.Create(typeof(WorldSession),"Ready",(object)true)}){
                var field=AccessTools.Field(pair.Item1,pair.Item2);originals.Add(field,field.GetValue(null));field.SetValue(null,pair.Item3);
            }
            var hook=new Harmony("multiplayer-expansion.frame-identity-test");
            var getter=AccessTools.Method(typeof(MultiplayerManager).Assembly.GetType("JumpKingMultiplayer.Extensions.PlayerSpriteStateExtensions",true),"GetLevelId");
            try{
                hook.Patch(getter,prefix:new HarmonyMethod(typeof(WorldTests),"Identity"));
                identityReads=0;identityValue=44;AdvancedSession.BeginFrame();
                for(int i=0;i<10000;i++)Check(WorldSession.Follower,"Matching host world isn't followed");
                Check(identityReads==1,"Entity checks repeatedly queried the legacy save identity");
                identityValue=55;AdvancedSession.BeginFrame();
                for(int i=0;i<10000;i++)Check(!WorldSession.Follower,"Previous world survived a native frame boundary");
                Check(identityReads==2,"Map change caused repeated identity reads");
                identityValue=44;
                using(var scope=new RuntimeScope())WorldLifecycle.Attempt(scope);
                Check(WorldSession.Follower && identityReads==3,"Attempt preparation retained an old map identity");
            }finally{
                hook.UnpatchAll(hook.Id);
                foreach(var pair in originals)pair.Key.SetValue(null,pair.Value);
                AdvancedSession.BeginFrame();
            }
            Console.WriteLine("[OK] World entity checks: 20,000 follower queries make two identity reads; native frames and attempt preparation refresh the map");
        }
        internal static void Run()
        {
            FrameIdentity();
            SnapshotWork();
            var shell=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MultiplayerExpansion.dll"));
            var manifest=new System.Xml.XmlDocument();using(var stream=shell.GetManifestResourceStream("JKRuntime.Manifest"))manifest.Load(stream);
            Check(manifest.DocumentElement.GetAttribute("entry")=="MultiplayerExpansion.NativeMod","World lifecycle package has the wrong entry");
            using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MultiplayerExpansion.Module.dll")))
                Check(manifest.DocumentElement.GetAttribute("sha256")==BitConverter.ToString(sha.ComputeHash(file)).Replace("-",""),"World package embeds another main assembly");
            using(var stream=shell.GetManifestResourceStream("JKRuntime.Module"))using(var moduleBytes=new MemoryStream()){
                stream.CopyTo(moduleBytes);var entry=Assembly.Load(moduleBytes.ToArray()).GetType("MultiplayerExpansion.NativeMod",true);
                Check(entry.GetMethod("Prepare")!=null && entry.GetMethod("Attempt")!=null && entry.GetMethod("Activate")!=null,"World SDK lifecycle callbacks are missing");
            }
            var state=new State();state.Groups.Add(1,new Group{State=true,Progress=.7f});state.Touched.Add(3);state.Tick=92;
            var codec=new JKRuntime.World.WorldStateCodec(typeof(State));byte[] bytes=codec.Capture(state);var copy=(State)codec.Decode(bytes);
            Check(copy.Groups[1].State&&copy.Tick==92&&copy.Touched.Contains(3),"World collections lost data");
            var groups=state.Groups;var group=groups[1];var touched=state.Touched;state.Groups.Add(2,new Group());state.Tick=500;group.State=false;touched.Clear();codec.Apply(state,copy);
            Check(ReferenceEquals(groups,state.Groups)&&ReferenceEquals(group,groups[1])&&ReferenceEquals(touched,state.Touched)&&groups.Count==1&&group.State&&touched.Contains(3),"World restore broke cached mod references");
            for(int n=0;n<bytes.Length;n++){bool rejected=false;try{codec.Decode(bytes.Take(n).ToArray());}catch(EndOfStreamException){rejected=true;}Check(rejected,"Truncated world fields accepted");}
            int changed=0;
            using(WorldState.Register("a",1,()=>new byte[]{1},data=>delegate{changed++;}))
            using(WorldState.Register("b",2,()=>new byte[]{2},data=>{throw new InvalidDataException("fixture");})){
                try{JKRuntime.World.WorldRegistry.Shared.Prepare(JKRuntime.World.WorldRegistry.Shared.Capture());throw new Exception("Invalid provider accepted");}catch(InvalidDataException){}
                Check(changed==0,"World applied a partial snapshot before validating all providers");
            }
            var packet=new WorldPacket{Epoch=1,Sequence=2,Map=3,Enabled=true,Time=9.25,State=bytes};
            byte[] wire=WorldWire.Encode(packet);WorldPacket decoded;
            Check(WorldWire.Decode(wire,out decoded)&&decoded.Time==9.25&&decoded.State.SequenceEqual(bytes),"World wire did not round trip");
            Check(WorldWire.Accepts(packet,10,10,1,3,1,1),"Current host world state rejected");
            Check(!WorldWire.Accepts(packet,11,10,1,3,1,1)&&!WorldWire.Accepts(packet,10,10,2,3,1,1)&&!WorldWire.Accepts(packet,10,10,1,4,1,1)&&!WorldWire.Accepts(packet,10,10,1,3,1,2),"Stale or foreign world state accepted");
            InteractionFrame oldFrame;
            Check(InteractionWire.Recognizes(wire)&&!InteractionWire.Decode(wire,out oldFrame),"Older clients won't safely skip world packets");
            for(int n=0;n<wire.Length;n++)Check(!WorldWire.Decode(wire.Take(n).ToArray(),out decoded),"World packet truncation accepted");
            wire[4]=99;Check(!WorldWire.Decode(wire,out decoded),"Unknown world protocol accepted");
            for(int tick=0;tick<100000;tick+=17)foreach(float force in new[]{0f,1f,4f}){
                double seconds=TimeSpan.FromSeconds(tick/60.0).TotalSeconds;
                Check(Math.Abs(JKRuntime.World.NativeWorldState.WindAt(seconds,true,force,null)-NativeWind.Velocity(tick,1.0/60,0,true,force,null))<.000001f,"Host wind differs from native curve");
            }
            string runtime=Environment.GetEnvironmentVariable("MPEX_TEST_RUNTIME");
            string switchPath=Environment.GetEnvironmentVariable("MPEX_TEST_SWITCH_BLOCKS");
            var mod=Client.LoadShared(switchPath);
            foreach(string name in new[]{"Auto","Basic","Countdown","Group","Jump","Sand","Sequence","Threshold"}){
                Type t=mod.GetType("SwitchBlocks.Data.Data"+name,true);object root=Activator.CreateInstance(t,true);var c=new JKRuntime.World.WorldStateCodec(t);
                var dict=t.GetProperty("Groups");if(dict!=null){var values=(IDictionary)dict.GetValue(root,null);values.Add(3,Activator.CreateInstance(mod.GetType("SwitchBlocks.Data.BlockGroup",true)));}
                byte[] payload=c.Capture(root);c.Apply(root,c.Decode(payload));Check(c.Capture(root).SequenceEqual(payload),"Installed Switch Blocks state lost data: "+name);
            }
            using(var scope=new RuntimeScope()){
                JKRuntime.World.SwitchBlocksWorld.Prepare(scope);using(var other=new RuntimeScope()){JKRuntime.World.SwitchBlocksWorld.Prepare(other);Check(JKRuntime.World.WorldRegistry.Shared.Inspect().Length==8,"Shared compatibility registered twice");}
                Check(JKRuntime.World.WorldRegistry.Shared.Inspect().Length==8,"World profile didn't register every installed data root");JKRuntime.World.WorldRegistry.Shared.Prepare(JKRuntime.World.WorldRegistry.Shared.Capture())();
                Lever(mod);
            }
            Check(JKRuntime.World.WorldRegistry.Shared.Inspect().Length==0,"World providers survived their scope");
            var ledger=new WorldInputLedger();bool jp,sp;
            var intent=new WorldInput{Epoch=1,Map=2,Sequence=1,Jump=1,Switch=1};
            Check(ledger.Accept(intent,1,2),"Valid world input rejected");ledger.Consume(out jp,out sp);Check(jp&&sp,"World input lost");
            ledger.Consume(out jp,out sp);Check(!jp&&!sp,"World input repeated");
            Check(!ledger.Accept(intent,1,2)&&!ledger.Accept(intent,2,2)&&!ledger.Accept(intent,1,3),"World input crossed an authority boundary");
            Check(ledger.Accept(new WorldInput{Epoch=1,Map=2,Sequence=3,Jump=2,Switch=1},1,2),"Lost world input packet blocked the next one");
            ledger.Consume(out jp,out sp);Check(jp&&!sp,"Cumulative world input did not recover packet loss");
            Console.WriteLine("[OK] World state: bounded wire, truncation, transactional validation, reference-preserving collections, native wind and eight installed Switch Blocks data layouts");
        }
        private static void SnapshotWork()
        {
            int prepares=0;byte value=1;
            using(JKRuntime.World.WorldRegistry.Shared.Register("audit.snapshot",1,()=>new[]{value},bytes=>{prepares++;return ()=>value=bytes[0];})) {
                try {
                    bool rejected=false;
                    try { WorldSession.RestoreSnapshot(new WorldPacket{Map=10,Epoch=20,State=new byte[]{255}}); }
                    catch(InvalidDataException) {rejected=true;}
                    Check(rejected && JKRuntime.World.WorldControl.Owner==null,"Invalid snapshot took world authority");
                    var snapshot=JKRuntime.World.WorldRegistry.Shared.Capture();
                    WorldSession.RestoreSnapshot(new WorldPacket{Map=10,Epoch=20,State=snapshot});
                    prepares=0;
                    for(int i=0;i<100;i++) WorldSession.RestoreSnapshot(new WorldPacket{Map=10,Epoch=20,State=snapshot});
                    Check(prepares==200,"Steady snapshots decoded twice in addition to rollback preparation");
                    Console.WriteLine("[COST] World receive: 100 snapshots, 100 incoming decodes + 100 rollback preparations; no redundant preflight decode");
                } finally {WorldSession.Reset();}
            }
        }
        private static void Lever(Assembly mod)
        {
            Type dataType=mod.GetType("SwitchBlocks.Data.DataBasic",true);var instance=AccessTools.Field(dataType,"instance");
            object original=instance.GetValue(null),data=Activator.CreateInstance(dataType,true);instance.SetValue(null,data);
            var changed=dataType.GetProperty("SwitchOnceSafe");var edge=dataType.GetProperty("HasSwitched");
            try{
                Type behaviourType=mod.GetType("SwitchBlocks.Behaviours.BehaviourBasicLever",true);
                Type direction=behaviourType.GetConstructors()[0].GetParameters()[0].ParameterType;
                var behaviour=(IBlockBehaviour)Activator.CreateInstance(behaviourType,new[]{Enum.Parse(direction,"All")});
                var block=(IBlock)Activator.CreateInstance(mod.GetType("SwitchBlocks.Blocks.BlockBasicLever",true),new object[]{new Rectangle(0,0,24,32)});
                Type actorType=typeof(JKRuntime.World.SwitchBlocksWorld).GetNestedType("Actor",BindingFlags.NonPublic);object actor=Activator.CreateInstance(actorType,true);
                var body=(BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));var context=new BehaviourContext(body);
                AccessTools.Field(actorType,"Body").SetValue(actor,body);AccessTools.Field(actorType,"Context").SetValue(actor,context);
                AccessTools.Field(actorType,"Behaviours").SetValue(actor,new List<IBlockBehaviour>{behaviour});
                var run=AccessTools.Method(typeof(JKRuntime.World.SwitchBlocksWorld),"RunActor");
                Action<bool> contact=touch=>{
                    AccessTools.Method(typeof(BehaviourContext),"ClearCollisionForNewFrame").Invoke(context,null);
                    AccessTools.Method(typeof(BehaviourContextCollisionInfo),"AggregateCollisionInfo").Invoke(context.CollisionInfo,new object[]{new AdvCollisionInfo(touch?new List<IBlock>{block}:new List<IBlock>(),false,SlopeType.None,Vector2.Zero)});
                    run.Invoke(null,new[]{actor,(object)0UL});
                };
                contact(true);Check((bool)changed.GetValue(data,null),"A guest touching a native mod lever did not request the host switch");
                Check(!(bool)edge.GetValue(data,null),"A guest overwrote the host's lever contact edge");
                changed.SetValue(data,false,null);contact(true);Check(!(bool)changed.GetValue(data,null),"Held guest contact fired the lever again");
                contact(false);contact(true);Check((bool)changed.GetValue(data,null),"Leaving and re-entering didn't re-arm the lever");
                byte[] snapshot=JKRuntime.World.WorldRegistry.Shared.Capture();changed.SetValue(data,false,null);
                using(var receiver=JKRuntime.World.WorldControl.Begin("test.switch","map",1,JKRuntime.World.WorldRole.Replica)){
                    receiver.Receive(snapshot);
                    Check((bool)changed.GetValue(data,null),"Client did not apply the host mod state");
                    Check(!(bool)AccessTools.Method(typeof(JKRuntime.World.SwitchBlocksWorld),"SaveAllowed").Invoke(null,new[]{data}),"Guest state was allowed into a local save");
                }
                Check(!(bool)changed.GetValue(data,null),"Leaving world sync did not restore local mod state");
                Check((bool)AccessTools.Method(typeof(JKRuntime.World.SwitchBlocksWorld),"SaveAllowed").Invoke(null,new[]{data}),"Leaving playback or sync kept local saves blocked");
                changed.SetValue(data,true,null);
                using(var playback=JKRuntime.World.WorldControl.Begin("test.switch","map",2,JKRuntime.World.WorldRole.Playback))playback.Receive(snapshot);
                JKRuntime.World.SwitchBlocksWorld.Release();Check((bool)changed.GetValue(data,null),"Later cleanup reverted newer local progress");
            }finally{instance.SetValue(null,original);}
        }
        internal static void Legacy()
        {
            TrackData data;
            foreach(string invalid in new[]{"oops","[1]","{invalid}","{\"posX\":\"NaN\"}","{\"colorIdx\":400}","{\"sprite\":999}"})
                Check(!LegacySafety.Decode(Encoding.ASCII.GetBytes(invalid),out data),"Malformed legacy packet accepted: "+invalid);
            Check(!LegacySafety.Decode(new byte[5000],out data),"Oversized legacy packet accepted");
            var queue=new Queue<Tuple<ulong,byte[]>>();for(int i=0;i<1000;i++)queue.Enqueue(Tuple.Create(2UL,Encoding.ASCII.GetBytes(Parser.ToString(new TrackData{posX=i}))));
            int applied=0;float last=-1;LegacySafety.Drain(()=>queue.Count==0?null:queue.Dequeue(),id=>id==2,(id,value)=>{applied++;last=value.posX;});
            Check(queue.Count==936 && applied==1 && last==63,"Legacy receive budget or coalescing failed");
            LegacySafety.Drain(()=>queue.Count==0?null:queue.Dequeue(),id=>false,(id,value)=>{throw new Exception("Non-member supplied movement");});
            Console.WriteLine("[OK] Legacy receive: malformed JSON, invalid values, allocation limit, peer membership, bounded draining and newest-state coalescing");
        }
    }
}
