using System;
using System.Linq;
using JKRuntime.World;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void WorldStateChecks()
        {
            var authored=TreeScene(Tree("lamp","start",Sequence(new BehaviorEffect{Effect="lantern"},new BehaviorWait{Seconds=1},new BehaviorSetFlag{Flag="powered",Value="true"})));
            byte[] host;
            using(var engine=new SceneBehaviorEngine(SceneBehaviorEngine.Copy(authored),null))using(engine.BindWorld()){
                engine.Tick(.4,true,true,At(0,0));host=WorldRegistry.Shared.Capture();
            }
            using(var scene=new SceneBehaviorEngine(SceneBehaviorEngine.Copy(authored),null))using(scene.BindWorld()){
                var local=WorldRegistry.Shared.Capture();int saves=0;scene.FlagsChanged=()=>saves++;
                using(var receiver=WorldControl.Begin("test.mapping","map",1,WorldRole.Replica)){
                    receiver.Receive(host);
                    Require(scene.InspectTrees()[0].Contains("BehaviorWait")&&scene.InspectEffects().Length==1,"common state restores a running Mapping tree and its effect");
                    Require(saves==0,"network scene restoration doesn't publish personal save flags");
                    bool refused=false;try{scene.ClearOwner("bt:0:0");}catch(InvalidOperationException){refused=true;}
                    Require(refused&&scene.InspectEffects().Length==1,"a local API call can't cancel the host's restored effect");
                    scene.Tick(.5,true,true,At(0,0));Require(scene.GetFlag("powered")=="false","receiving Mapping engine doesn't advance independently");
                }
                Require(WorldRegistry.Shared.Capture().SequenceEqual(local),"leaving world control restores local Mapping queues, trees and effects");
                WorldRegistry.Shared.Apply(host);scene.Tick(.6,true,true,At(0,0));
                Require(scene.GetFlag("powered")=="false","restored running wait keeps its original start tick");
                scene.Tick(.4,true,true,At(0,0));Require(scene.GetFlag("powered")=="true"&&scene.InspectEffects().Length==0,"transferred tree completes once after the remaining wait");
            }
            var changed=SceneBehaviorEngine.Copy(authored);changed.BehaviorTrees[0].Id="different";
            using(var scene=new SceneBehaviorEngine(changed,null))using(scene.BindWorld()){
                bool refused=false;try{WorldRegistry.Shared.Apply(host);}catch(System.IO.InvalidDataException){refused=true;}
                Require(refused&&scene.InspectEffects().Length==0,"different authored scene is rejected before mutation");
            }
            Require(WorldRegistry.Shared.Inspect().Length==0,"Mapping state releases its Runtime registration");
            var remoteScene=new SceneFile {
                Flags=new[]{new FlagData{Id="visits",Type="integer",Value="0"}},
                Regions=new[]{new RegionData{Id="remote",Screen=2,X=0,Y=0,Width=100,Height=100,Dwell=.25f,SpawnInside="fire"}},
                Rules=new[]{new RuleData{Id="visit",Event="enter:remote",Screen=2,Increment="visits",Amount=1}}
            };
            using(var scene=new SceneBehaviorEngine(remoteScene,null))using(var hostControl=WorldControl.Begin("test.regions","map",1,WorldRole.Host)){
                hostControl.SetActors(new[]{new WorldActor(2,1,new Microsoft.Xna.Framework.Vector2(10,-350),Microsoft.Xna.Framework.Vector2.Zero,true),new WorldActor(3,1,new Microsoft.Xna.Framework.Vector2(20,-350),Microsoft.Xna.Framework.Vector2.Zero,true)});
                scene.Tick(.125,true,true,At(200,200,1));Require(scene.GetFlag("visits")=="0","two remote contacts don't advance dwell twice per tick");
                scene.Tick(.125,true,true,At(200,200,1));Require(scene.GetFlag("visits")=="1","remote contact dispatches its own screen to scene rules");
                hostControl.SetActors(new WorldActor[0]);scene.Tick(.1,true,true,At(200,200,1));
                Require(scene.InspectRegions()[0].Contains("outside"),"remote departure releases its aggregate region contact");
            }
            Console.WriteLine("[OK] Runtime Mapping world state: tree/effect transfer, remaining waits, save isolation, role gate, local restoration and authored-scene validation");
        }
    }
}
