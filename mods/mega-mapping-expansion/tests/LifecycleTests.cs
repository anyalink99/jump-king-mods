using BehaviorTree;
using System.Linq;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;
using MegaMappingExpansion.Endings;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void SceneLifecycleChecks()
        {
            Require(NativeScene.PresentationDelta(true,true,false,.1)==0,"Native pause freezes presentation by default");
            Require(NativeScene.PresentationDelta(true,true,true,.1)==.1,"A view explicitly permits presentation animation during pause");
            Require(NativeScene.PresentationDelta(false,true,true,.1)==0,"The scene toggle holds even explicitly permitted pause animation");
            Require(NativeScene.PresentationDelta(true,false,false,.1)==.1,"Unpaused presentation advances independently of completion");
            var pausePolicy=PrimitiveRegistry.Create().Require("view").Fields.First(f=>f.Name=="animateWhilePaused");
            Require(pausePolicy.Default.X==0,"The authored pause policy defaults to freezing animation");
            var document=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'>
<Components><Component id='material' kind='material'/></Components>
<Variables><Variable id='count' kind='Integer' value='0'/><Variable id='steps' kind='Integer' value='0'/></Variables>
<Effects><Effect id='pulse' duration='1' clock='Presentation'><Set target='material/opacity' value='0.4'/></Effect></Effects>
<Rules><Rule id='react' event='ending.spark' clock='Presentation' cooldown='0.2'><Action op='increment' target='count' value='1'/><Action op='effect' target='pulse'/></Rule></Rules>
<Trees><Tree id='cutscene' event='ending.spark' clock='Presentation'><Node op='sequence'><Node op='action'><Action op='increment' target='steps' value='1'/></Node><Node op='wait' seconds='0.2'/><Node op='action'><Action op='increment' target='steps' value='1'/></Node></Node></Tree></Trees>
</MappingSettings>"));
            using(var scene=new SceneProgram(document))
            {
                var bridge=new SceneBridgeNode("EmitSceneEvent","ending.spark",null,()=>scene);
                Require(bridge.Run(new TickData(.1f,1))==BTresult.Success,"Native ending bridges enqueue ordinary scene events");
                scene.Tick(0,.1);Require(scene.ReadVariable("count").X==0,"Native pause can advance visuals without dispatching scene logic");
                scene.Tick(0,.1,true);
                Require(scene.GameplayTime==0&&scene.ReadVariable("count").X==1&&scene.Properties.Read("material","opacity").X==.4,"Ending presentation dispatches effects while gameplay remains stopped");
                Require(scene.ReadVariable("steps").X==1,"Presentation-clock control trees start from native ending events");
                var snapshot=scene.Capture();scene.Tick(0,.3,true);Require(scene.ReadVariable("steps").X==2,"Ending waits use the tree's presentation clock");
                scene.Restore(snapshot);scene.Tick(0,.3,true);Require(scene.ReadVariable("steps").X==2,"Restoring an ending sequence resumes waits without replaying actions");
                bridge.Run(new TickData(.1f,2));scene.Tick(0,.1,true);Require(scene.ReadVariable("count").X==2,"Presentation cooldowns advance independently of gameplay time");
                var setter=new SceneBridgeNode("SetSceneVariable","count","7",()=>scene);setter.Run(new TickData(.1f,3));Require(scene.ReadVariable("count").X==7,"Ending setters use declared variable types");
                Require(scene.Signals.Describe("actor:ending.king.position").Kind==SceneValueKind.Vector2,"Cutscene actor positions use the same typed observation contract");
                scene.Signals.Publish("player.present",SceneValue.Boolean(true),ObservationQuality.Sampled);scene.Signals.Publish("player.center",SceneValue.Vector(42,60),ObservationQuality.Sampled);
                NativeScene.ReleasePlayerSignals(scene);
                Require(scene.Signals.Read("player.present").Value.X==0&&scene.Signals.Read("player.center").Quality==ObservationQuality.Unavailable,"Presentation survives completion without claiming a live gameplay player or measured position");
            }
            var pose=ComponentPose.Identity;pose.X=.3;pose.Y=.6;pose.ScaleX=1.1;pose.Rotation=13;
            var point=pose.RenderPoint(SceneValue.Vector(3.2,4.1));Require(point.X==System.Math.Round(point.X)&&point.Y==System.Math.Round(point.Y),"Strict rendering snaps the final transformed point, including origin, rotation and scale");
            pose.PixelSnap=false;point=pose.RenderPoint(SceneValue.Vector(3.2,4.1));Require(point.X!=System.Math.Round(point.X),"Fractional render geometry requires an explicit pixelSnap opt-out");
        }
    }
}
