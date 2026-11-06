using System;
using System.Linq;
using System.Reflection;
using BehaviorTree;
using BehaviorTree.Util;
using JKRuntime;
using JKRuntime.Gameplay;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.GameManager.MultiEnding;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class EndingPolicy
    {
        internal bool Credits=true,Picture=true,Statistics=true;
        internal static EndingPolicy Read(SceneProgram scene,string role)
        {
            var component=scene.Components.Values.FirstOrDefault(c=>c.Descriptor.Kind=="endingPresentation"&&scene.Properties.Read(c.Id,"role").Text==role);
            return component==null?new EndingPolicy():new EndingPolicy{
                Credits=scene.Properties.Read(component.Id,"credits").X!=0,Picture=scene.Properties.Read(component.Id,"picture").X!=0,Statistics=scene.Properties.Read(component.Id,"statistics").X!=0};
        }
    }
    internal sealed class NativeEndingPresentation
    {
        private readonly NativeScene scene;
        internal EndingPolicy Policy=new EndingPolicy();
        internal bool Begun,Finished;
        internal string Role;
        internal double Started;
        internal NativeEndingPresentation(NativeScene owner,RuntimeScope resources)
        {
            scene=owner;
            if(scene.Program.Components.Count==0&&scene.Program.Definition.Rules.Length==0&&scene.Program.Definition.Trees.Length==0)return;
            PreparedNativeHooks.Acquire("mega-mapping-expansion.ending-presentation",resources,hooks=>{
                foreach(var binding in Endings.NativeEndings.Bindings.Where(b=>!b.Actor))
                    hooks.Add(typeof(Game1).Assembly.GetType(binding.TypeName,true).GetMethod("CheckWin",new[]{typeof(PlayerEntity)}),postfix:Method("Win"),priority:0);
                hooks.Add(typeof(GameEnding).GetMethod("MakeBT",OwnedPatches.Members),postfix:Method("Controller"));
                hooks.Add(typeof(GameEnding).GetMethod("OnEnd",OwnedPatches.Members),postfix:Method("Finish"));
                hooks.Add(typeof(EndingPictureScreen).GetMethod("OnNewRun",OwnedPatches.Members),prefix:Method("Picture"));
                hooks.Add(typeof(EndingPictureScreen).GetMethod("MyRun",OwnedPatches.Members),prefix:Method("PictureRun"));
                hooks.Add(typeof(EndingPictureScreen).GetMethod("Draw"),prefix:Method("Picture"));
                hooks.Add(typeof(StatsScreen).GetMethod("MyRun",OwnedPatches.Members),prefix:Method("StatsRun"));
                hooks.Add(typeof(StatsScreen).GetMethod("Draw"),prefix:Method("StatsDraw"));
                foreach(var binding in Endings.NativeEndings.Bindings.Where(b=>b.Actor))
                    hooks.Add(typeof(Game1).Assembly.GetType(binding.TypeName,true).GetMethod("Draw"),prefix:Method("ActorDraw"));
            });
        }
        private static MethodInfo Method(string name){return typeof(NativeEndingPresentation).GetMethod(name,OwnedPatches.Members);}
        private static NativeEndingPresentation Current{get{return NativeScene.Current==null?null:NativeScene.Current.Ending;}}
        private static void Win(object __instance,PlayerEntity p_player,bool __result)
        {
            var current=Current;if(current==null||current.Begun||!__result||!MappingSettings.Enabled)return;
            var binding=Endings.NativeEndings.Bindings.First(b=>!b.Actor&&b.TypeName==__instance.GetType().FullName);
            current.Begin(binding.Role.Substring(0,binding.Role.IndexOf('_')),p_player);
        }
        private void Begin(string role,PlayerEntity player)
        {
            scene.Program.ComposeCurrentValues();
            Role=role;Policy=EndingPolicy.Read(scene.Program,role);Started=scene.Program.PresentationTime;
            var foot=FrameComposition.ProjectWorld(player.m_body.Position+new Vector2(9,26));
            scene.Program.Signals.Publish("ending.arrival",SceneValue.Vector(foot.X,foot.Y),ObservationQuality.Sampled);
            scene.Renderer.CaptureVictory();Begun=true;
            scene.Program.Signals.Emit("ending."+role+".begin","native.ending","player",Camera.CurrentScreenIndex1,Started);
            scene.Program.Signals.Dispatch();scene.Program.ComposeCurrentValues();scene.Media.Tick(true,NativePause.IsPaused||JKRuntime.UI.UIApi.IsOpen);
        }
        private static void Controller(ref BTmanager __result)
        {
            var current=Current;if(current==null||!current.Begun||current.Policy.Credits)return;
            var ending=(IBTnode)typeof(GameEnding).GetField("m_ending",OwnedPatches.Members).GetValue(null);
            var tree=new RunAllAnySuccess();tree.AddChild(ending);tree.AddChild(new GameEnding.SkipCredits());__result=new BTmanager(tree);
        }
        private static void Finish()
        {
            var current=Current;if(current==null||!current.Begun)return;
            current.Finished=true;current.scene.Media.Stop();
            current.scene.Program.Signals.Emit("ending."+current.Role+".finish","native.ending","",Camera.CurrentScreenIndex1,current.scene.Program.PresentationTime);
        }
        private static bool Picture(){var current=Current;return current==null||!current.Begun||current.Policy.Picture;}
        private static bool PictureRun(ref BTresult __result){if(Picture())return true;__result=BTresult.Success;return false;}
        internal static bool SkipNativeStats{get{var current=Current;return current!=null&&current.Begun&&!current.Policy.Statistics;}}
        private static bool StatsRun(StatsScreen __instance,ref BTresult __result)
        {if(!SkipNativeStats||NativeResults.ShowingCustom(__instance))return true;__result=BTresult.Success;return false;}
        private static bool StatsDraw(StatsScreen __instance){return !SkipNativeStats||NativeResults.ShowingCustom(__instance);}
        private static bool ActorDraw(object __instance)
        {
            if(NativeCapture.Drawing!=0)return true;
            var current=Current;if(current==null||!MappingSettings.Enabled)return true;
            var binding=Endings.NativeEndings.Bindings.FirstOrDefault(b=>b.Actor&&b.TypeName==__instance.GetType().FullName);
            if(binding==null)return true;string role=binding.Role.Substring(binding.Role.IndexOf('_')+1);if(role=="cherubs")role="cherub";
            var component=current.scene.Program.Components.Values.FirstOrDefault(c=>c.Descriptor.Kind=="endingActor"&&current.scene.Program.Properties.Read(c.Id,"role").Text==role);
            return component==null||current.scene.Program.Properties.Read(component.Id,"visible").X!=0;
        }
    }
}
