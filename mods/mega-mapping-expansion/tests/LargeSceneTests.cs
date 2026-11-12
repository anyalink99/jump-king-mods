using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void LargeSceneChecks(string scratch)
        {
            var components=new List<ComponentData>();
            for(int i=0;i<1024;i++)
            {
                components.Add(new ComponentData{Id="pose"+i,Kind="transform",Values=new[]{
                    new ComponentValueData{Name="screen",Value=(i+1).ToString()}}});
                components.Add(new ComponentData{Id="sensor"+i,Kind="sensor",Values=new[]{
                    new ComponentValueData{Name="transform",Value="pose"+i},
                    new ComponentValueData{Name="size",Value="20,20"}}});
            }
            components[0].Values=components[0].Values.Concat(new[]{new ComponentValueData{Name="parent",Value="parent"}}).ToArray();
            components.Add(new ComponentData{Id="parent",Kind="transform"});
            var document=new SceneDocument{
                Components=components.ToArray(),
                Graph=Enumerable.Range(0,32768).Select(i=>Node("n"+i,"constant",value:"0")).ToArray(),
                Effects=Enumerable.Range(0,4096).Select(i=>new EffectData{Id="effect"+i,Values=new[]{new OverrideData{Target="pose0/rotation",Value="1"}}}).ToArray(),
                Variables=Enumerable.Range(0,2048).Select(i=>new VariableData{Id="v"+i}).ToArray(),
                Bindings=Enumerable.Range(0,16384).Select(i=>new BindingData{Id="b"+i,Source="n"+i,Target="pose"+(i%1024)+"/rotation"}).ToArray(),
                Trees=Enumerable.Range(0,1024).Select(i=>new ControlTreeData{Id="tree"+i,Event="sensor"+i+".enter",Nodes=new[]{
                    new ControlNodeData{Operation="sequence",Nodes=new[]{
                        new ControlNodeData{Operation="action",Action=new ActionData{Operation="increment",Target="v"+i,Value="1"}},
                        new ControlNodeData{Operation="wait",Seconds=.1}}}}}).ToArray()
            };
            using(var scene=new SceneProgram(document))
            {
                scene.Tick(0,0);
                var actor=new ActorObservation{Id="player",Screen=1024,Bounds=new SceneBox{X=-5,Y=-5,Width=10,Height=10},Velocity=SceneValue.Vector(0,0)};
                // establish baseline offscreen, then enter the first sensor
                scene.ObservationPoses.Refresh();scene.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.01);scene.Tick(.01,.01);
                actor.Screen=1;
                scene.ObservationPoses.Refresh();scene.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.01);scene.Tick(.01,.01);
                Require(scene.ReadVariable("v0").X==1,"Offscreen baseline observation still allows entry on arrival");
                var saved=scene.Capture();
                actor.Screen=2;
                scene.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.01);scene.Tick(.01,.01);
                Require(scene.Signals.Read("sensor0.touching").Value.X==0&&scene.ReadVariable("v1").X==1,"Screen departure clears contacts and activates the next tree");
                scene.Restore(saved);actor.Screen=1;
                scene.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.01);scene.Tick(.01,.01);
                Require(scene.ReadVariable("v0").X==1&&scene.ReadVariable("v1").X==0,"Large scene restore keeps contact and tree state without replaying entry");
                var packet=scene.CaptureWorld();
                scene.RestoreWorld(packet);scene.Tick(0,0);
                Require(scene.ReadVariable("v0").X==1&&scene.Errors.Length==0,"Large scene world state round-trips within its existing transport budget");
                scene.Properties.Set(new PropertyContribution{Id="move",Owner="test",Target="parent/screen",Mode="set",Value=SceneValue.Integer(3)});
                scene.Properties.Compose();scene.ObservationPoses.Refresh();
                scene.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.01);
                Require(scene.Signals.Read("sensor0.touching").Value.X==0,"Parent screen changes invalidate cached sensor membership");
                actor.Screen=3;scene.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.01);
                Require(scene.Signals.Read("sensor0.touching").Value.X==1,"A child sensor follows its parent's changed screen");
            }
            var storeRoot=Path.Combine(scratch,"large-scene-save");Directory.CreateDirectory(storeRoot);
            var store=new ScenePersistence("large-scene",()=>storeRoot);
            var values=Enumerable.Range(0,2048).ToDictionary(i=>"v"+i,i=>i.ToString());
            store.Save(values);
            Require(store.Load().Count==2048,"Expanded variable capacity round-trips through native persistence");

            using(var hub=new SignalHub())
            {
                hub.Declare(new SignalDescriptor("input",SceneValueKind.Number),SceneValue.Number(0));
                hub.Publish("input",SceneValue.Number(2),ObservationQuality.Sampled);
                var graph=new ValueGraph(new[]{Node("base","constant",value:"3"),Node("fixed","add","base;base"),
                    new ValueNodeData{Id="input",Operation="input",Source="input"},Node("sum","add","fixed;input"),
                    Node("integral","integrate","fixed"),Node("bad","divide","fixed;zero"),Node("zero","constant")},hub);
                graph.Tick(.1,.1);var before=graph.Capture();
                graph.Tick(.1,.1);
                Require(graph.Read("sum").X==8&&Math.Abs(graph.Read("integral").X-1.2)<.00001,"Constant pruning keeps stateful dependents ticking");
                hub.Publish("input",SceneValue.Number(4),ObservationQuality.Sampled);graph.Tick(.1,.1);
                Require(graph.Read("sum").X==10&&graph.Errors.Length==1,"Dynamic inputs and stable constant faults survive pruning");
                graph.Restore(before);graph.Tick(0,0);
                Require(graph.Read("sum").X==10&&Math.Abs(graph.Read("integral").X-.6)<.00001&&graph.Errors.Length==1,"Restore refreshes constants and faults without advancing stateful nodes");
                bool rejected=false;
                try{new ValueGraph(Enumerable.Range(0,32769).Select(i=>Node("n"+i,"constant")).ToArray(),hub);}
                catch(InvalidDataException){rejected=true;}
                Require(rejected,"Expanded graph capacity stays bounded");
            }
            foreach(var kind in new[]{"trees","variables","sensors","bindings","effects"})
            {
                var invalid=new SceneDocument();
                if(kind=="trees")invalid.Trees=Enumerable.Range(0,1025).Select(i=>new ControlTreeData{Id="t"+i,Nodes=new[]{new ControlNodeData{Operation="wait"}}}).ToArray();
                if(kind=="variables")invalid.Variables=Enumerable.Range(0,2049).Select(i=>new VariableData{Id="v"+i}).ToArray();
                if(kind=="sensors")invalid.Components=Enumerable.Range(0,1025).Select(i=>new ComponentData{Id="s"+i,Kind="sensor"}).ToArray();
                if(kind=="bindings")invalid.Bindings=new BindingData[16385];
                if(kind=="effects")
                {
                    invalid.Components=new[]{new ComponentData{Id="pose",Kind="transform"}};
                    invalid.Effects=Enumerable.Range(0,4097).Select(i=>new EffectData{Id="e"+i,Values=new[]{new OverrideData{Target="pose/rotation",Value="1"}}}).ToArray();
                }
                bool rejected=false;try{using(var scene=new SceneProgram(invalid)){}}
                catch(InvalidDataException error){rejected=error.Message.Contains("at most");}
                Require(rejected,"Expanded "+kind+" capacity stays bounded");
            }
        }
    }
}
