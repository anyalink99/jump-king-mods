using System;
using System.Reflection;
using JKRuntime;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion.Construction
{
    internal sealed class SceneHooks:IDisposable
    {
        private readonly OwnedPatches patches=new OwnedPatches("mega-mapping-expansion.construction");
        private static readonly FieldInfo RenderTarget=typeof(Game1).GetField("m_render_target",OwnedPatches.Members);
        private static readonly FieldInfo TimerPosition=typeof(GameLoop).GetField("TIMER_DISPLAY_POSITION",OwnedPatches.Members);
        private static readonly Action<Game1> CalculateRect=(Action<Game1>)Delegate.CreateDelegate(typeof(Action<Game1>),typeof(Game1).GetMethod("CalculateGameRect",OwnedPatches.Members));
        private static Vector2 timerPosition;
        private static readonly Type Achievements=typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager",true);
        private static readonly FieldInfo AchievementInstance=Achievements.GetField("instance",OwnedPatches.Members);
        private static readonly MethodInfo CurrentStats=Achievements.GetMethod("GetCurrentStats",OwnedPatches.Members);
        private static readonly PropertyInfo StatsTime=CurrentStats.ReturnType.GetProperty("timeSpan",OwnedPatches.Members);
        private static readonly PropertyInfo GeneralSettings=typeof(Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube",true).GetProperty("generalSettings",OwnedPatches.Members);
        private static readonly FieldInfo PreciseTimer=GeneralSettings.PropertyType.GetField("gui_timer_precise",OwnedPatches.Members);
        internal SceneHooks()
        {
            try
            {
                NativeSceneAdapter.AssertContracts();NativeMapLayout.AssertContracts();
                NativeActorObservations.AssertContracts();
                if(RenderTarget==null||RenderTarget.FieldType!=typeof(RenderTarget2D)||TimerPosition==null||TimerPosition.FieldType!=typeof(Vector2))throw new NotSupportedException("Installed native frame and timer contracts are incompatible");
                timerPosition=(Vector2)TimerPosition.GetValue(null);
                Add(typeof(LevelScreen).GetMethod("DrawMG",OwnedPatches.Members),"BeforeMidground",null);
                Add(typeof(LevelScreen).GetMethod("Draw",Type.EmptyTypes),null,"AfterScreen");
                Add(typeof(LevelScreen).GetMethod("DrawForeground",Type.EmptyTypes),"BeforeForeground","AfterForeground");
                Add(typeof(JumpGame).GetMethod("Update",new[]{typeof(GameTime)}),null,"AfterUpdate");
                Add(typeof(JumpKing.Player.Walk).GetMethod("MyRun",OwnedPatches.Members),null,"AfterWalk");
                Add(typeof(GameLoop).GetMethod("DrawIngameOverlayItems"),"BeforeOverlay","AfterOverlay");
                Add(typeof(IntroState).GetMethod("MakeLegendHasItText",OwnedPatches.Members),"BeforeIntro",null);
                Add(typeof(LanguageManager).GetProperty("LEGEND_HAS_IT").GetGetMethod(),null,"AfterIntro");
                Add(typeof(Game1).GetMethod("DrawRenderTarget",OwnedPatches.Members),"BeforeBlit",null);
                var save=typeof(Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube",true);
                Add(save.GetMethod("SaveCombinedSaveFile",OwnedPatches.Members),null,"AfterSave");
                Add(save.GetMethod("DeleteSaves",OwnedPatches.Members),null,"AfterReset");
            }
            catch{patches.Dispose();throw;}
        }
        private void Add(MethodBase method,string prefix,string postfix)
        {
            if(method==null)throw new NotSupportedException("A native scene hook contract is unavailable");
            patches.Add(method,prefix==null?null:typeof(SceneHooks).GetMethod(prefix,OwnedPatches.Members),postfix==null?null:typeof(SceneHooks).GetMethod(postfix,OwnedPatches.Members));
        }
        private static NativeScene Active{get{return NativeCapture.Drawing==0&&NativeScene.Current!=null&&NativeScene.Current.Active?NativeScene.Current:null;}}
        private static RenderTarget2D Frame()
        {
            var targets=Game1.instance.GraphicsDevice.GetRenderTargets();
            return targets.Length==1?(RenderTarget2D)targets[0].RenderTarget:(RenderTarget2D)RenderTarget.GetValue(Game1.instance);
        }
        private static void BeforeMidground()
        {var scene=Active;if(scene==null)return;int screen=Camera.CurrentScreenIndex1;scene.Renderer.DrawLayer("background",screen);scene.Renderer.CaptureStage("background",Frame(),screen);}
        private static void AfterScreen(){var scene=Active;if(scene!=null)scene.Renderer.DrawLayer("world",Camera.CurrentScreenIndex1);}
        private static void BeforeForeground()
        {var scene=Active;if(scene==null)return;int screen=Camera.CurrentScreenIndex1;scene.Renderer.CaptureStage("world",Frame(),screen);scene.Renderer.DrawLayer("surface",screen);}
        private static void AfterForeground()
        {
            var scene=Active;if(scene==null)return;int screen=Camera.CurrentScreenIndex1;scene.Renderer.DrawLayer("foreground",screen);scene.Renderer.CaptureStage("foreground",Frame(),screen);scene.Renderer.DrawLayer("composite",screen);
            if(!FrameComposition.InScreenPass)scene.Compose(Frame());
            scene.Rendered();
        }
        private static void AfterUpdate(){var scene=NativeScene.Current;if(scene!=null)scene.Tick(1.0/60);}
        private static void AfterWalk(BehaviorTree.BTresult __result)
        {if(NativeScene.Current!=null&&__result==BehaviorTree.BTresult.Success)NativeSceneAdapter.Walking=true;}
        private static bool BeforeOverlay()
        {var scene=Active;return scene==null||scene.Renderer.Timer(Camera.CurrentScreenIndex1)=="inherit";}
        private static void AfterOverlay()
        {
            var scene=Active;if(scene==null)return;
            var achievements=AchievementInstance.GetValue(null);
            if(scene.Renderer.Timer(Camera.CurrentScreenIndex1)=="show"&&achievements!=null)
            {
                var time=(TimeSpan)StatsTime.GetValue(CurrentStats.Invoke(achievements,null),null);string text=((int)time.TotalHours).ToString("00")+":"+time.Minutes.ToString("00")+":"+time.Seconds.ToString("00");
                if((bool)PreciseTimer.GetValue(GeneralSettings.GetValue(null,null)))text+="."+time.Milliseconds.ToString("000");
                TextHelper.DrawString(Game1.instance.contentManager.font.MenuFont,text,timerPosition,Color.White,Vector2.Zero,true);
            }
            scene.Renderer.DrawLayer("hud",Camera.CurrentScreenIndex1);
        }
        private static bool BeforeIntro(bool isFast,ref BehaviorTree.IBTnode __result)
        {
            var scene=ModEntry.IntroScene;if(scene==null||!MappingSettings.Enabled)return true;
            var pages=NativeNarrativeText.Intro(scene.Program,isFast);if(pages==null)return true;__result=pages;return false;
        }
        private static void AfterIntro(ref string __result)
        {if(!MappingSettings.Enabled)return;var scene=ModEntry.IntroScene;if(scene==null)return;string intro=scene.Renderer.Intro(Camera.CurrentScreenIndex1);if(intro!="")__result=intro.Replace("<newline>","\n");}
        private static bool BeforeBlit(Game1 __instance)
        {
            var scene=Active;if(scene==null||FrameComposition.HasExternalPresentation)return true;var style=scene.Style;
            if(style.Effects==SpriteEffects.None)return true;
            CalculateRect(__instance);Rectangle destination=__instance.GetGameRect();destination.Location+=JumpGame.screenShakeManager.GetOffset();__instance.StartBatch();
            try{Game1.spriteBatch.Draw((RenderTarget2D)RenderTarget.GetValue(__instance),destination,null,style.Tint,0,Vector2.Zero,style.Effects,0);}finally{__instance.EndBatch();}return false;
        }
        private static void AfterSave(){SceneSaveCoordinator.Save();TransitionSaveCoordinator.Save();}
        private static void AfterReset(){SceneSaveCoordinator.Reset();TransitionSaveCoordinator.Reset(ScenePersistence.NativeDirectory());}
        public void Dispose(){patches.Dispose();}
    }
}
