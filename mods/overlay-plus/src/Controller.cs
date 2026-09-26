using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JKRuntime;
using JKRuntime.Gameplay;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.GameManager.MultiEnding;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OverlayPlus
{
    internal static class Controller
    {
        internal static bool Active,RequestEditor,Failed;
        internal static Canvas Canvas;
        internal static ForegroundLayer Foreground;
        internal static Surface Surface;
        internal static Layout Layout;
        internal static RunTracker Tracker;
        internal static Route Route;
        internal static string Status="";
        internal static float Charge,LastCharge;
        internal static double SessionStart,ToastUntil;
        internal static string Toast="";
        private static bool initialized,world,releaseGate,previousPause,previousMouseVisible;
        private static bool attempt,started;
        private static RuntimeScope resources,session;
        internal static readonly JKRuntime.Settings.Setting<bool> EnableSetting = new JKRuntime.Settings.Setting<bool>(
            "overlay-plus.enabled", "Enable", delegate { Initialize(); return Store.Settings.ModuleEnabled; }, SetEnabled);
        internal static Action AfterEditorClose;
        private static double checkpoint,nextMechanics;
        private static uint modifierCount;
        private static bool rulesCaptured;
        private static string mechanicRules;
        private static TextInputWindow textWindow;
        private static IDisposable cursorLease;
        internal static double Now {get{return Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;}}
        internal static bool CapturesInput {get{return Active&&(Editor.Open||releaseGate);}}
        internal static bool OwnsNativeTimer {get{return Active&&!Failed&&Store.Settings.Enabled&&Layout!=null&&Layout.Widgets.Any(w=>w.Enabled&&w.Kind==WidgetKind.Timer&&Widgets.Visible(w,false));}}
        internal static void Initialize()
        {
            if(initialized)return;
            string root=Path.Combine(Path.GetDirectoryName(typeof(Game1).Assembly.Location),"Content","OverlayPlus");
            Store.Initialize(root);SessionStart=Now;initialized=true;
        }
        internal static void PrepareWorld(RuntimeScope scope)
        {
            Initialize();world=true;scope.Defer(delegate{world=false;ReleaseWorld();});
            if(Store.Settings.ModuleEnabled)AcquireResources();
        }
        private static void AcquireResources()
        {
            if(resources!=null)return;
            resources=new RuntimeScope();
            try { PrepareResources(resources); }
            catch { ReleaseResources(); throw; }
        }
        private static void PrepareResources(RuntimeScope scope)
        {
            scope.Defer(delegate{Canvas=null;Foreground=null;});
            using(RuntimeApi.MeasureStartup("overlay-plus.render-resources")){Canvas=scope.Own(new Canvas(Game1.instance.GraphicsDevice));Foreground=scope.Own(new ForegroundLayer(Game1.instance.GraphicsDevice));Canvas.PrepareImages(Store.Settings.Layouts.SelectMany(l=>l.Widgets));}
            Hooks.Install();scope.Defer(Hooks.Remove);
            Adapters.Prepare(scope);
        }
        internal static void PrepareAttempt(RuntimeScope scope)
        {
            Initialize();attempt=true;scope.Defer(delegate{attempt=false;Route=null;});
            if(Store.Settings.ModuleEnabled)ReadAttempt();
        }
        private static void ReadAttempt()
        {
            using(RuntimeApi.MeasureStartup("overlay-plus.area-route")){Route=Native.ReadRoute();}
            using(RuntimeApi.MeasureStartup("overlay-plus.run-history")){Store.Flush();Store.LoadArchive(Route);}
        }
        internal static void Start(ModuleContext context)
        {
            started=true;context.Track(new Cleanup(End));
            if(Store.Settings.ModuleEnabled)Activate();
        }
        private static void Activate()
        {
            if(Active)return;
            session=new RuntimeScope();session.Defer(EndSession);
            try { StartSession(); }
            catch { StopSession(); throw; }
        }
        private static void StartSession()
        {
            if(Route==null||Canvas==null)throw new InvalidOperationException("Overlay+ world preparation is unavailable");
            Active=true;Failed=false;releaseGate=false;RequestEditor=false;Charge=LastCharge=0;Surface=GetSurface();Layout=Store.Current(Surface,Route.World);Inputs.Clear();
            if(Layout.FitPresetOnFirstUse){Defaults.FitPreset(Layout,Surface);Layout.FitPresetOnFirstUse=false;Store.SaveSettings();}
            double native=Native.Time();string attempt=Native.AttemptKey();
            Run previous=Store.Archive.Runs.LastOrDefault(r=>r.NativeAttempt==attempt&&r.Status!="Finished"&&r.NativeLast<=native+.05);
            Run run;
            if(previous!=null&&native>.05){run=XmlData.Clone(previous);run.Status="Running";run.Resumed=true;run.Flag("Continued session");}
            else {run=new Run {RouteKey=Route.Key,Category=Store.Settings.Category,NativeAttempt=attempt,NativeLast=native,Elapsed=new Times{Game=native},CurrentArea=0};if(native>.05)run.Flag("Joined existing attempt");}
            int area=AreaRoutes.Find(Route,Camera.CurrentScreenIndex1,-1);if(area>run.CurrentArea){run.CurrentArea=area;run.Flag("Started after first Area");}
            Tracker=new RunTracker(Route,run,Now);modifierCount=Native.Modifiers();rulesCaptured=false;mechanicRules="";
            checkpoint=Now+10;nextMechanics=Now;session.Own(GameplayEvents.Subscribe("overlay-plus",OnEvent));
            textWindow=new TextInputWindow(Game1.instance.Window.Handle);
            Adapters.Activate();Store.SaveRun(run);
        }
        internal static Surface GetSurface()
        {
            var pp=Game1.instance.GraphicsDevice.PresentationParameters;float scale=Math.Max(.25f,pp.BackBufferHeight/720f*(Store.Settings==null?1:Store.Settings.UiScale));var native=Game1.instance.GetGameRect();if(native.Width<=0){int width=Math.Min(pp.BackBufferWidth,(int)(pp.BackBufferHeight*4f/3));native=new Rectangle((pp.BackBufferWidth-width)/2,0,width,pp.BackBufferHeight);}
            return new Surface{Scale=scale,Window=new Rectangle(0,0,(int)(pp.BackBufferWidth/scale),(int)(pp.BackBufferHeight/scale)),Game=new Rectangle((int)(native.X/scale),(int)(native.Y/scale),(int)(native.Width/scale),(int)(native.Height/scale))};
        }
        internal static void BeforeUpdate()
        {
            if(!Active)return;var surface=GetSurface();bool resized=surface.Window!=Surface.Window||surface.Game!=Surface.Game;Surface=surface;Inputs.Poll(Surface,Now);
            if(resized){Editor.CancelDrag();if(!Editor.Open)Layout=Store.Current(Surface,Route.World);}
            if(!Inputs.Focused){Editor.CancelDrag();if(Editor.Open&&textWindow!=null)textWindow.Characters.Clear();return;}
            bool toggle=Inputs.Press((Keys)Store.Settings.EditorKey)||(Inputs.Pad.IsButtonDown(Buttons.Back)&&Inputs.PadPress(Buttons.Start));
            if(RequestEditor||toggle){RequestEditor=false;if(Editor.Open)CloseEditor();else if(!releaseGate&&!Adapters.PlaybackActive()&&!JKRuntime.UI.UIApi.IsOpen)OpenEditor();}
            if(Editor.Open&&JKRuntime.UI.UIApi.IsOpen)CloseEditor();
            if(Editor.Open){Editor.Update();if(textWindow!=null){while(textWindow.Characters.Count>0)Editor.Character(textWindow.Characters.Dequeue());}}
            else if(textWindow!=null)textWindow.Characters.Clear();
            if(releaseGate&&Inputs.AllReleased()){releaseGate=false;Native.SetPause(previousPause);Game1.instance.IsMouseVisible=previousMouseVisible;var action=AfterEditorClose;AfterEditorClose=null;if(action!=null)action();}
        }
        internal static void AfterUpdate()
        {
            if(!Active||Tracker==null)return;Adapters.Update();Charge=Native.Charge();
            bool playback=Adapters.PlaybackActive();if(playback)Tracker.Run.Flag("Replay playback");
            if(!playback&&Tracker.Run.Status=="Running"){
                if(!rulesCaptured){CaptureRules();rulesCaptured=true;}
                var stats=Native.Stats();Tracker.Run.Jumps=stats.jumps;Tracker.Run.Falls=stats.falls;
                bool split=Tracker.Advance(Native.Time(),Now,NativePause.IsPaused,Camera.CurrentScreenIndex1,GameLoop.m_player!=null&&GameLoop.m_player.m_body.IsOnGround);
                if(split){var latest=Tracker.Run.Splits.LastOrDefault();if(latest!=null){var gold=Records.Gold(Store.Archive,Tracker.Run.Category,latest.AreaId,Store.Settings.Clock,Tracker.Run.RulesKey);Notify(latest.Eligible&&(!gold.HasValue||latest.Duration.Value(Store.Settings.Clock)<gold.Value)?"BEST SEGMENT  "+Records.Format(latest.Duration.Value(Store.Settings.Clock)):"AREA COMPLETE  "+Records.Format(latest.End.Value(Store.Settings.Clock)));}Store.SaveRun(Tracker.Run);}
                if(Native.Modifiers()!=modifierCount){Tracker.Run.Flag("Gameplay modifiers changed");modifierCount=Native.Modifiers();}
                if(Now>=nextMechanics){nextMechanics=Now+1;if(MechanicRules()!=mechanicRules)Tracker.Run.Flag("Gameplay settings changed");}
                if(Now>=checkpoint){checkpoint=Now+10;Store.SaveRun(Tracker.Run);}
            }
            Widgets.Refresh();
        }
        private static void OnEvent(GameplayEvent e)
        {
            if(Tracker==null)return;
            if(e.Kind==GameplayEventKind.RestoreStarted)Tracker.Run.Flag("State restored");
            if(e.Kind==GameplayEventKind.Teleported&&e.Source!="native")Tracker.Run.Flag("Teleport: "+e.Source);
            if(e.Kind==GameplayEventKind.ChargeStarted)Charge=0;
            if(e.Kind==GameplayEventKind.Jump){if(e.Jump!=null){LastCharge=Charge;}Charge=0;}
        }
        private static string MechanicRules(){return string.Join(";",RuntimeApi.Mechanics.Inspect().Where(m=>(m.Effects&~MechanicEffects.Presentation)!=0).Select(m=>m.Id+"@"+m.Version+":"+(m.State==null?"unknown":m.State.Enabled.ToString())).OrderBy(s=>s));}
        private static void CaptureRules()
        {
            mechanicRules=MechanicRules();modifierCount=Native.Modifiers();
            string bodies=GameLoop.m_player==null?"":string.Join(";",GameLoop.m_player.m_body.GetBehaviourList().Select(b=>b.GetType().AssemblyQualifiedName).OrderBy(s=>s));
            Tracker.Run.RulesKey=AreaRoutes.Hash(mechanicRules+"|"+bodies+"|boots="+Native.Equipped("Boots")+"|ring="+Native.Equipped("Snake"));
            Tracker.Run.Rules=modifierCount>0?"Modified gameplay":"Native gameplay";
        }
        internal static void OpenEditor()
        {
            if(!Active||Editor.Open)return;previousPause=NativePause.IsPaused;previousMouseVisible=Game1.instance.IsMouseVisible;Native.SetPause(true);Editor.Begin();cursorLease=JKRuntime.UI.UiPointer.AcquireWindowCursor();Status="Drag widgets. Double-click text to edit. F10 / Back+Start closes.";
        }
        internal static void CloseEditor()
        {
            if(!Editor.Open)return;Editor.Finish();ReleaseCursor();Store.SaveSettings();Store.SaveRun(Tracker.Run);releaseGate=true;Game1.instance.IsMouseVisible=false;
        }
        internal static void Victory(EndingType ending)
        {
            if(!Active||Tracker==null||Adapters.PlaybackActive())return;
            Tracker.Advance(Native.Time(),Now,NativePause.IsPaused,Camera.CurrentScreenIndex1,GameLoop.m_player!=null&&GameLoop.m_player.m_body.IsOnGround);
            if(Route.Campaign!=Campaign.Custom&&(int)ending!=(int)Route.Campaign)Tracker.Run.Flag("Different campaign ending");
            Tracker.Finish(true);var pb=Records.Best(Store.Archive,Tracker.Run.Category,Store.Settings.Clock,Tracker.Run.RulesKey);Notify(!Tracker.Run.Practice&&(pb==null||Tracker.Run.Elapsed.Value(Store.Settings.Clock)<pb.Elapsed.Value(Store.Settings.Clock))?"PERSONAL BEST  "+Records.Format(Tracker.Run.Elapsed.Value(Store.Settings.Clock)):"RUN FINISHED  "+Records.Format(Tracker.Run.Elapsed.Value(Store.Settings.Clock)));Store.SaveRun(Tracker.Run);
        }
        internal static void End(){started=false;StopSession();}
        private static void StopSession()
        {
            if(session!=null){session.Dispose();session=null;}
        }
        private static void EndSession()
        {
            if(!Active)return;ReleaseCursor();GameTimer.Release();bool captured=CapturesInput;if(Editor.Open)Editor.Finish();releaseGate=false;Active=false;RequestEditor=false;AfterEditorClose=null;if(captured){Native.SetPause(previousPause);Game1.instance.IsMouseVisible=previousMouseVisible;}
            Adapters.ReleaseClaims();if(textWindow!=null){textWindow.Dispose();textWindow=null;}Inputs.Clear();
            if(Tracker!=null){Tracker.Finish(false);Store.SaveRun(Tracker.Run);}
        }
        internal static void Unload(){End();if(!world)ReleaseWorld();}
        private static void ReleaseResources()
        {
            if(resources!=null){resources.Dispose();resources=null;}
            Canvas=null;Foreground=null;
        }
        private static void ReleaseWorld(){End();ReleaseResources();Store.Flush();Route=null;Layout=null;Tracker=null;}
        internal static void SetEnabled(bool value)
        {
            Initialize();bool previous=Store.Settings.ModuleEnabled;
            Store.Flush();Store.Settings.ModuleEnabled=value;
            try {
                // Commit synchronously at the explicit menu action; queued layout
                // checkpoints must not overwrite the enabled preference later.
                Store.Write(Path.Combine(Store.Root,"Layouts.xml"),XmlData.Serialize(Store.Settings));
                ApplyEnabled();
            } catch {
                Store.Settings.ModuleEnabled=previous;
                Store.Write(Path.Combine(Store.Root,"Layouts.xml"),XmlData.Serialize(Store.Settings));
                ApplyEnabled();throw;
            }
        }
        private static void ApplyEnabled()
        {
            if(!Store.Settings.ModuleEnabled){
                StopSession();ReleaseResources();Store.Flush();
                Route=null;Layout=null;Tracker=null;Store.Archive=null;RequestEditor=false;
                return;
            }
            // Live enable is an explicit paused-menu action. Ordinary loading
            // still prepares resources in Runtime's world/attempt phases.
            if(world)AcquireResources();
            if(attempt&&Route==null)ReadAttempt();
            if(started)Activate();
        }
        private static void ReleaseCursor(){if(cursorLease!=null){cursorLease.Dispose();cursorLease=null;}}
        internal static void BeginFrame()
        {
            if(Foreground!=null)Foreground.BeginFrame();Adapters.BeginFrame();GameTimer.BeginFrame();
            if(Active&&Layout!=null&&(Store.Settings.Enabled||Editor.Open)&&Inputs.HasWidgets(Layout.Widgets))Inputs.SampleForDraw(Layout.Widgets,Now);
        }
        internal static void BeginUi()
        {
            if(Active&&!Failed&&Canvas!=null&&Layout!=null&&Foreground!=null)Foreground.Begin();
        }
        private static void DrawWidgets(){if(Store.Settings.Enabled||Editor.Open)foreach(var w in Layout.Widgets)Widgets.Draw(Canvas,w);}
        private static void DrawToast(){if(Now<ToastUntil){Canvas.Frame(new Rectangle(Surface.Window.Width/2-250,40,500,45));Canvas.Text(Canvas.Fit(Toast,460,19),Surface.Window.Width/2-230,52,19,new Color(240,195,92));}}
        internal static void Draw()
        {
            if(!Active||Canvas==null||Layout==null)return;Surface=GetSurface();
            Canvas.Begin(Surface);
            try{if(!Failed){DrawWidgets();if(Editor.Open)Editor.Draw(Canvas);else DrawToast();}}
            finally{
                // Foreground UI must survive a failing provider/editor draw as well.
                try{Canvas.Unclip();var destination=Game1.instance.GetGameRect();if(JumpGame.screenShakeManager!=null)destination.Location+=JumpGame.screenShakeManager.GetOffset();if(Foreground!=null)Foreground.Draw(Canvas.Batch,destination,Surface.Scale);}
                finally{Canvas.End();}
            }
        }
        internal static void Notify(string message){Toast=message;ToastUntil=Now+4;Status=message;}
        internal static void FailRendering(Exception e){Failed=true;Adapters.ReleaseClaims();FailEditor(e);Console.WriteLine("[Overlay+] Drawing disabled: "+e);}
        internal static void FailEditor(Exception e){Status="Overlay+ error: "+e.Message;Store.Warning=Status;if(Editor.Open){Editor.Abort();ReleaseCursor();releaseGate=true;}}
        internal static void FailUpdate(Exception e){if(Tracker!=null)Tracker.Run.Flag("Observation unavailable");Status="Overlay+ observation: "+e.Message;Console.WriteLine(Status);}
        private sealed class Cleanup:IDisposable{private Action action;internal Cleanup(Action a){action=a;}public void Dispose(){var a=action;action=null;if(a!=null)a();}}
    }
}
