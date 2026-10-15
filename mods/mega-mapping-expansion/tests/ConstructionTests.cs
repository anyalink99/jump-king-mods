using System;
using System.IO;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static ValueNodeData Node(string id, string op, string inputs = "", string value = "0", SceneValueKind kind = SceneValueKind.Number)
        { return new ValueNodeData { Id = id, Operation = op, Inputs = inputs, Value = value, Kind = kind }; }
        private static void ConstructionChecks()
        {
            var recipeCompiler = new RecipeCompiler(PrimitiveRegistry.Create());
            var layoutDocument=recipeCompiler.Compile(System.Xml.Linq.XDocument.Parse("<MappingSettings version='1'><MapLayout version='2' source='native' screens='2'/></MappingSettings>"));
            Require(layoutDocument.MapLayout!=null&&layoutDocument.MapLayout.Name=="MapLayout"&&layoutDocument.MapLayout.GetAttribute("source")=="native","Generic scene authoring retains the native layout element without a nested wrapper");
            var recipeDocument = recipeCompiler.Compile(System.Xml.Linq.XDocument.Parse("<MappingSettings version='1'><Assets><Asset id='surface' path='surface.png'/></Assets><Instances><Instance id='orb' recipe='planet'><Arg name='surface' value='surface'/></Instance><Instance id='pointer' recipe='follow-offset'/></Instances></MappingSettings>"));
            using (var recipe = new SceneProgram(recipeDocument))
            {
                recipe.Signals.Publish("player.center", SceneValue.Vector(400, 100), ObservationQuality.Sampled);
                recipe.Tick(.1, .1);
                Require(recipe.Properties.Read("orb.surfaceMaterial", "uvOffset").X > 0, "Planet recipe compiles to ordinary UV animation and mesh components");
                Require(recipe.Properties.Read("pointer.root", "position").X > 240, "Follow recipe uses ordinary vector math, smoothing and transform binding");
                Require(recipe.Components["orb.surface"].Descriptor.Kind == "mesh", "Recipe expansion introduces no semantic planet component");
            }
            var connected=recipeCompiler.Compile(System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'>
<Components><Component id='attachment' kind='sprite'><Value name='transform' value='pointer.transform'/></Component><Component id='readout' kind='transform'/></Components>
<Graph><Node id='copied' op='input' source='node:pointer.offset'/></Graph><Bindings><Bind id='copy' source='copied' target='readout/position'/></Bindings>
<Instances><Instance id='pointer' recipe='follow-offset'/></Instances></MappingSettings>"));
            using(var scene=new SceneProgram(connected))
            {
                scene.Signals.Publish("player.center",SceneValue.Vector(400,100),ObservationQuality.Sampled);scene.Tick(.1,.1);
                Require(scene.Properties.Read("attachment","transform").Text=="pointer.root"&&scene.Properties.Read("readout","position").X>0,"Recipes expose typed component and graph ports that another recipe or authored component can connect");
            }
            var logicDocument = recipeCompiler.Compile(System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1' saveId='test'>
              <Components><Component id='lamp' kind='material'/></Components>
              <Variables><Variable id='count' kind='Integer' value='0' scope='Save'/></Variables>
              <Effects><Effect id='flash' duration='1'><Set target='lamp/opacity' value='0.25'/></Effect></Effects>
              <Rules><Rule id='count' event='hit'><Action op='increment' target='count' value='1'/><Action op='effect' target='flash'/></Rule></Rules>
            </MappingSettings>"));
            using (var logic = new SceneProgram(logicDocument))
            {
                logic.Signals.Emit("hit", "wall", "player", 1, 0); logic.Tick(.1, .1);
                Require(logic.ReadVariable("count").X == 1 && logic.Properties.Read("lamp", "opacity").X == .25, "One generic contact rule updates saved state and activates an owned effect");
                var saved = logic.Capture(); logic.Tick(1, 1);
                Require(logic.Properties.Read("lamp", "opacity").X == 1, "Expired effects release their property contributions");
                logic.Restore(saved); Require(logic.ReadVariable("count").X == 1 && logic.Properties.Read("lamp", "opacity").X == .25, "Snapshot restore resumes effects without replaying counted actions");
                Require(logic.SavedVariables()["count"] == "1", "Only declared saved variables reach the persistence packet");
                using(var gateway=new MegaMappingExpansion.Construction.SceneGateway(logic))
                {
                    var lease=gateway.Activate("consumer","flash");var active=logic.Capture();long generation=gateway.Generation;logic.Restore(active);
                    var fresh=gateway.Activate("consumer","flash");lease.Dispose();
                    Require(gateway.Generation!=generation&&!lease.Active&&fresh.Active,"Old API leases can't cancel a restored effect with the same instance ID");fresh.Dispose();
                }
            }
            var treeDocument=recipeCompiler.Compile(System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'><Variables><Variable id='count' kind='Integer' value='0'/></Variables>
              <Trees><Tree id='steps' event='go'><Node op='sequence'><Node op='action'><Action op='increment' target='count' value='1'/></Node><Node op='wait' seconds='0.5'/><Node op='action'><Action op='increment' target='count' value='1'/></Node></Node></Tree></Trees></MappingSettings>"));
            using(var scene=new SceneProgram(treeDocument))
            {
                scene.Signals.Emit("go","test","player",1,0);scene.Tick(.1,.1);Require(scene.ReadVariable("count").X==1,"Generic trees execute immediate actions before a wait");
                var saved=scene.Capture();scene.Tick(.6,.6);Require(scene.ReadVariable("count").X==2,"Generic trees resume after their gameplay wait");
                scene.Restore(saved);scene.Tick(.6,.6);Require(scene.ReadVariable("count").X==2,"Restored tree cursors don't replay their completed actions");
                var packet=scene.CaptureWorld();var codec=new JKRuntime.World.WorldStateCodec(typeof(SceneWorldPacket),true);var copy=(SceneWorldPacket)codec.Decode(codec.Capture(packet));
                using(var replica=new SceneProgram(treeDocument))
                {replica.RestoreWorld(copy);Require(replica.ReadVariable("count").X==2&&replica.InspectTrees()[0].Contains("Success"),"Portable world packets restore variables and control state without replaying actions");}
            }
            Require(SceneValue.Parse(SceneValueKind.Vector2, "1.25,-3").Equals(SceneValue.Vector(1.25, -3)), "Typed values round-trip invariant vector channels");
            bool invalid = false; try { SceneValue.Number(double.NaN); } catch (ArgumentException) { invalid = true; }
            Require(invalid, "Non-finite graph values cannot enter the scene");
            var springDocument=recipeCompiler.Compile(System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'>
<Components><Component id='surface' kind='field'><Value name='function' value='spring'/><Value name='amplitude' value='0,1'/><Value name='length' value='100'/></Component></Components>
<Rules><Rule id='push' event='hit'><Action op='impulse' target='surface' value='80'/></Rule></Rules></MappingSettings>"));
            using(var scene=new SceneProgram(springDocument))
            {
                scene.Signals.Emit("hit","test","player",1,0,new SignalSample("position",SceneValue.Vector(50,0),ObservationQuality.Exact,1));scene.Tick(.1,.1);
                double displaced=scene.Fields.Sample("surface",SceneValue.Vector(50,0)).Y;
                Require(displaced>0,"A generic event impulse displaces an ordinary spring field");var state=scene.Capture();scene.Tick(0,.5);
                Require(scene.Fields.Sample("surface",SceneValue.Vector(50,0)).Y==displaced,"Spatial spring state freezes during gameplay pause");
                scene.Tick(.2,.2);scene.Restore(state);Require(scene.Fields.Sample("surface",SceneValue.Vector(50,0)).Y==displaced,"Practice restore returns spatial fields to their captured shape");
                using(var replica=new SceneProgram(springDocument))
                {replica.RestoreWorld(scene.CaptureWorld());Require(Math.Abs(replica.Fields.Sample("surface",SceneValue.Vector(50,0)).Y-displaced)<.00001,"World state carries spatial field displacement without replaying the impulse");}
            }
            var sensorDocument=recipeCompiler.Compile(System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'>
<Components><Component id='pose' kind='transform'><Value name='position' value='20,20'/></Component><Component id='area' kind='sensor'><Value name='transform' value='pose'/><Value name='size' value='20,20'/><Value name='initial' value='fire'/></Component></Components>
<Variables><Variable id='entries' kind='Integer' value='0'/></Variables><Rules><Rule id='count' event='area.enter'><Action op='increment' target='entries' value='1'/></Rule></Rules></MappingSettings>"));
            using(var scene=new SceneProgram(sensorDocument))
            {
                var actor=new ActorObservation{Id="player",Screen=1,Bounds=new SceneBox{X=16,Y=16,Width=8,Height=8},Velocity=SceneValue.Vector(0,0)};
                scene.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.1);scene.Tick(.1,.1);Require(scene.ReadVariable("entries").X==1,"Generic sensor entry reaches an ordinary counting rule");
                var saved=scene.Capture();actor.Bounds=new SceneBox{X=100,Y=100,Width=8,Height=8};scene.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.1);scene.Tick(.1,.1);scene.Restore(saved);
                actor.Bounds=new SceneBox{X=16,Y=16,Width=8,Height=8};scene.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.1);scene.Tick(.1,.1);
                Require(scene.ReadVariable("entries").X==1,"Restored sensor membership doesn't invent a second entry while the actor stays inside");
                scene.Signals.Publish("charge.active",SceneValue.Boolean(true),ObservationQuality.Sampled);scene.Signals.Publish("charge.heldFrames",SceneValue.Number(11.5),ObservationQuality.Estimated);
                using(var replica=new SceneProgram(sensorDocument))
                {
                    replica.RestoreWorld(scene.CaptureWorld());Require(replica.Signals.Read("charge.active").Value.X==1&&replica.Signals.Read("charge.heldFrames").Value.X==11.5,"World state preserves native observations even when the authored graph doesn't reference them");
                    replica.Sensors.Observe(actor,new SurfaceObservation[0],"contact",.1);replica.Tick(.1,.1);Require(replica.ReadVariable("entries").X==1,"World restore preserves sensor membership without replaying region entry");
                }
            }
            using (var hub = new SignalHub())
            {
                hub.Declare(new SignalDescriptor("player.position", SceneValueKind.Vector2, "px"), SceneValue.Vector(0, 0));
                hub.Declare(new SignalDescriptor("rate", SceneValueKind.Number), SceneValue.Number(1));
                int good = 0, bad = 0;
                var broken = hub.Subscribe("rate", value => { bad++; throw new Exception("consumer failure"); });
                var healthy = hub.Subscribe("rate", value => good++);
                hub.Publish("rate", SceneValue.Number(2), ObservationQuality.Sampled); hub.Publish("rate", SceneValue.Number(3), ObservationQuality.Exact);
                Require(good == 2 && bad == 1 && hub.Errors.Length == 1, "Failed signal consumers are isolated and removed");
                broken.Dispose(); healthy.Dispose();
                double observed=0;hub.Subscribe("rate",sample=>observed=hub.Read("player.position").Value.X);
                using(hub.Batch()){hub.Publish("rate",SceneValue.Number(4),ObservationQuality.Exact);hub.Publish("player.position",SceneValue.Vector(123,0),ObservationQuality.Sampled);Require(observed==0,"Batch notifications wait until all observations are committed");}
                Require(observed==123,"Signal observers read a fully committed restore batch");
                int delivered = 0; SceneEventSample first = null;
                hub.SubscribeEvent("hit", value => { delivered++; first = value; hub.Emit("after", "recipe", "player", 2, 2); });
                hub.SubscribeEvent("after", value => delivered++);
                hub.Emit("hit", "surface", "player", 1, 1, new SignalSample("position", SceneValue.Vector(30, 40), ObservationQuality.Sampled, 1));
                hub.Dispatch(); Require(delivered == 1 && first.Screen == 1 && first.Payload[0].Value.Equals(SceneValue.Vector(30, 40)), "Events retain contact location and screen without recursive delivery");
                hub.Dispatch(); Require(delivered == 2, "Events emitted by listeners wait for the next delivery batch");
                var graph = new ValueGraph(new[] {
                    new ValueNodeData { Id="rate", Operation="input", Source="rate" },
                    Node("phase", "integrate", "rate"), Node("period", "constant", value:"1"), Node("wrapped", "wrap", "phase;period"),
                    Node("response", "constant", value:"8"), Node("smooth", "smooth", "rate;response"),
                    new ValueNodeData { Id="position", Operation="input", Source="player.position" },
                    Node("origin", "constant", value:"10,10", kind:SceneValueKind.Vector2), Node("direction", "subtract", "position;origin"), Node("unit", "normalize", "direction")
                }, hub);
                graph.Tick(.1, .1); double phase = graph.Read("phase").X;
                hub.Publish("rate", SceneValue.Number(6), ObservationQuality.Sampled); graph.Tick(.1, .1);
                Require(Math.Abs(graph.Read("phase").X - phase - .6) < 1e-8, "Changing angular rate retains accumulated phase");
                var snapshot = graph.Capture(); graph.Tick(0, .1);
                Require(graph.Read("phase").X == snapshot.Values[1].X, "Gameplay graph state freezes while presentation advances");
                graph.Tick(.1, .1); graph.Restore(snapshot);
                Require(graph.Read("phase").X == snapshot.Values[1].X, "Snapshot restore returns integrators to the captured phase");
                hub.Publish("player.position", SceneValue.Vector(10, 10), ObservationQuality.Exact); graph.Tick(.01, .01);
                Require(graph.Read("unit").Equals(SceneValue.Vector(0, 0)), "Direction normalization is defined at the target origin");
                invalid = false;
                try { new ValueGraph(new[] { Node("a", "add", "b;b"), Node("b", "add", "a;a") }, hub); } catch (InvalidDataException) { invalid = true; }
                Require(invalid, "Combinational graph cycles fail before activation");
                var recoverable=new ValueGraph(new[]{Node("one","constant",value:"1"),Node("zero","constant"),Node("bad","divide","one;zero"),Node("available","available","bad"),Node("good","add","one;one")},hub);
                recoverable.Tick(.1,.1);Require(!recoverable.Available("bad")&&recoverable.Read("available").X==0&&recoverable.Read("good").X==2,"A bad graph branch is unavailable while independent branches keep running");
                var feedback = new ValueGraph(new[] { Node("one", "constant", value:"1"), Node("old", "delay", "sum"), Node("sum", "add", "old;one") }, hub);
                feedback.Tick(.1, .1); Require(feedback.Read("sum").X == 1, "Explicit delay starts feedback from its initial state");
                feedback.Tick(.1, .1); Require(feedback.Read("sum").X == 2, "Explicit delay advances feedback once per eligible tick");
                feedback.Tick(0,0);Require(feedback.Read("sum").X==2,"A zero-time graph refresh doesn't expose another delay step");
                var registry = PrimitiveRegistry.Create();
                var component = registry.Compile(new ComponentData { Id="glow", Kind="material" });
                var store = new PropertyStore(new[] { component }, graph, new[] { new BindingData { Id="pulse", Source="wrapped", Target="glow/opacity" } });
                store.Compose(); double baseOpacity = store.Read("glow", "opacity").X;
                store.Set(new PropertyContribution { Id="dim", Owner="zone", Target="glow/opacity", Mode="multiply", Value=SceneValue.Number(.5) });
                store.Compose(); Require(Math.Abs(store.Read("glow", "opacity").X - baseOpacity * .5) < 1e-8, "Owned effects compose over bound values");
                store.Set(new PropertyContribution { Id="flash", Owner="impact", Target="glow/opacity", Mode="set", Priority=10, Value=SceneValue.Number(1) });
                store.RemoveOwner("zone"); store.Compose(); Require(store.Read("glow", "opacity").X == 1, "Removing one owner preserves another owner's winning contribution");
                store.RemoveOwner("impact"); store.Compose(); Require(store.Read("glow", "opacity").X == baseOpacity, "Removing all effects restores the current binding rather than a stale captured value");
                invalid = false;
                try { registry.Compile(new ComponentData { Id="bad", Kind="sprite", Values=new[] { new ComponentValueData { Name="lookAtKing", Value="true" } } }); } catch (InvalidDataException) { invalid = true; }
                Require(invalid, "Specialized behavior flags aren't part of primitive sprite fields");
            }
        }
    }
}
