using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void NativeAdapterChecks()
        {
            var definition=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse(@"<MappingSettings version='1'>
<Components>
<Component id='room' kind='audio'><Value name='path' value='audio/room.wav'/></Component>
<Component id='hermit' kind='npc'><Value name='name' value='hermit'/><Value name='voice' value='room'/></Component>
<Component id='ring' kind='item'><Value name='name' value='GoldRing'/></Component>
<Component id='wall' kind='hiddenWall'><Value name='texture' value='secret'/></Component>
<Component id='shot' kind='camera'/><Component id='win' kind='ending'/>
</Components></MappingSettings>"));
            using(var scene=new SceneProgram(definition))
            {
                Require(scene.Signals.Read("hermit.talking").Quality==ObservationQuality.Unavailable,"Unbound native actors aren't reported as sampled false");
                Require(scene.Signals.Describe("ring.count").Kind==SceneValueKind.Integer,"Item inventory exposes a typed count");
                Require(scene.Properties.Require("shot/active").Descriptor.Bindable,"Ordinary bindings and owned effects control a shot");
                Require(!scene.Properties.Require("shot/screen").Descriptor.Bindable,"Prepared camera destinations can't silently change behind validation");
                int walking=0;scene.Signals.SubscribeEvent("player.walking.enter",e=>walking++);
                PlayerObservations.Publish(scene,100,0,true,false,false,true);scene.Signals.Dispatch();
                Require(scene.Signals.Read("player.moving").Value.X==1&&scene.Signals.Read("player.walking").Value.X==0,"Sliding motion doesn't claim a native walk action");
                PlayerObservations.Publish(scene,100,0,true,true,false,true);scene.Signals.Dispatch();Require(walking==1,"The native walk edge emits once");
                PlayerObservations.Publish(scene,100,0,true,true,false,true);scene.Signals.Dispatch();Require(walking==1,"Held walking doesn't flood events");
                PlayerObservations.Publish(scene,0,-60,false,false,false,true);
                Require(scene.Signals.Read("player.rising").Value.X==1&&scene.Signals.Read("player.falling").Value.X==0,"Airborne direction follows measured velocity");
                PlayerObservations.Publish(scene,0,60,false,false,false,true);Require(scene.Signals.Read("player.falling").Value.X==1,"Falling is independent of charge");
            }
            var envelope=new AudioEnvelope();Require(envelope.Advance(true,1,2,.25)==.25,"Audio enters over its configured duration");
            Require(envelope.Advance(false,1,2,.25)==.125,"Reversing a fade retains its current gain");
            Require(envelope.Advance(false,1,2,0)==.125,"A paused clock holds the audio envelope");
            Require(envelope.Advance(false,1,0,.1)==0,"Zero fade time stops immediately");
            Require(NativeControllers.CombineWin("replace",true,false)==false,"An authored replacement can reject a vanilla win");
            Require(NativeControllers.CombineWin("and",true,false)==false&&NativeControllers.CombineWin("and",true,true),"Additional win requirements preserve the native predicate");
            Require(NativeControllers.CombineWin("or",false,true),"Alternative victory conditions can complement native victory");
            bool failed=false;
            try{using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse("<MappingSettings version='1'><Components><Component id='a' kind='ending'/><Component id='b' kind='ending'/></Components></MappingSettings>")))){}}
            catch(InvalidDataException){failed=true;}Require(failed,"Two adapters cannot race to replace one ending condition");
            using(var scope=new JKRuntime.RuntimeScope())
            {
                JKRuntime.Presentation.NativeScreenView.Prepare(scope);
                using(var candidate=new JKRuntime.RuntimeScope())JKRuntime.Presentation.NativeScreenView.Prepare(candidate);
                Require(HarmonyLib.Harmony.GetPatchInfo(typeof(JumpKing.JumpGame).GetMethod("Draw")).Owners.Contains("jk-runtime.native-screen-view"),"Native cuts patch real draw boundaries during preparation");
            }
            var closed=new JKRuntime.RuntimeScope();closed.Dispose();failed=false;
            try{JKRuntime.Presentation.NativeScreenView.Prepare(closed);}catch(ObjectDisposedException){failed=true;}
            Require(failed,"A closed scope rejects native camera preparation without retaining hooks");
            using(var scope=new JKRuntime.RuntimeScope())JKRuntime.Presentation.NativeScreenView.Prepare(scope);
            var patch=HarmonyLib.Harmony.GetPatchInfo(typeof(JumpKing.JumpGame).GetMethod("Draw"));
            Require(patch==null||!patch.Owners.Contains("jk-runtime.native-screen-view"),"Shared native camera scopes release the final hook owner");
            var textDefinition=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse(@"<MappingSettings version='1'>
<Variables><Variable id='shown' kind='Number' value='0'/></Variables>
<Components><Component id='caption' kind='text'><Value name='value' value='ABCD'/></Component></Components>
<Graph><Node id='count' op='input' source='variable:shown' units='characters'/></Graph>
<Bindings><Bind id='count' source='count' target='caption/visibleCharacters'/></Bindings></MappingSettings>"));
            using(var scene=new SceneProgram(textDefinition))
            {
                int characters=0,finished=0;
                scene.Signals.SubscribeEvent("caption.character",e=>characters++);scene.Signals.SubscribeEvent("caption.finished",e=>finished++);
                scene.Tick(0,0);scene.Signals.Dispatch();Require(characters==0&&finished==0,"Initial text layout is a silent baseline");
                scene.SetVariable("shown",SceneValue.Number(2));scene.Tick(.1,.1);scene.Signals.Dispatch();
                Require(scene.Signals.Read("caption.revealed").Value.X==2&&characters==1,"A text prefix produces one batched character event");
                var snapshot=scene.Capture();scene.SetVariable("shown",SceneValue.Number(4));scene.Tick(.1,.1);scene.Signals.Dispatch();
                Require(finished==1&&scene.Signals.Read("caption.complete").Value.X==1,"Reaching the full text emits completion once");
                scene.Restore(snapshot);scene.Signals.Dispatch();Require(characters==2&&finished==1,"Text restore doesn't replay past sounds or completion");
                scene.Tick(0,0);scene.Signals.Dispatch();Require(characters==2,"A held scene doesn't reveal or sound characters");
            }
        }
    }
}
