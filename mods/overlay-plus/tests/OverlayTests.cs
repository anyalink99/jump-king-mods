using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using HarmonyLib;
using JumpKing;
using JumpKing.MiscSystems.LocationText;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OverlayPlus;
using OverlayPlus.Api;
using ButtonState=Microsoft.Xna.Framework.Input.ButtonState;
using Keys=Microsoft.Xna.Framework.Input.Keys;

internal static class OverlayTests
{
    private static int checks;
    private static bool profileRender;
    private static string cameraAssembly, replayAssembly;
    private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    [STAThread] private static int Main(string[] args)
    {
        try{string game=args[0];cameraAssembly=Argument(args,"--camera");replayAssembly=Argument(args,"--replays");if(args.Contains("--profile")){PerformanceProbe.Run(game);return 0;}profileRender=args.Contains("--profile-render");Areas(game);Runs();Coordinates();Controls();Persistence();EnableTests.Run();Provider();NativeHooks();ReplayAdapter();if(args.Contains("--graphics")||profileRender)Graphics(game);Console.WriteLine("[OK] Overlay+: "+checks+" behavioral assertions");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static string Argument(string[] args,string name){int i=Array.IndexOf(args,name);return i<0?null:args[i+1];}
    private static Route Simple(){return AreaRoutes.Create(new[]{new Area{Id="a",Name="The Woods",Start=1,End=2,Unlock=1},new Area{Id="b",Name="The Keep",Start=2,End=4,Unlock=3},new Area{Id="c",Name="The Tower",Start=5,End=6,Unlock=5}},"test","revision","Test map",Campaign.Custom);}
    private static void Areas(string game)
    {
        var settings=XmlData.Parse<LocationSettings>(File.ReadAllText(Path.Combine(game,"Content/gui/location_settings.xml")));
        var all=settings.locations.Select(a=>new Area{Id=a.name,Name=a.name,Start=a.start,End=a.end,Unlock=a.unlock});
        var main=AreaRoutes.Create(all,"native","v1","Jump King",Campaign.MainBabe);var plus=AreaRoutes.Create(all,"native","v1","Jump King",Campaign.NewBabePlus);var ghost=AreaRoutes.Create(all,"native","v1","Jump King",Campaign.GhostOfTheBabe);
        Check(main.Areas.Count==10&&plus.Areas.Count==9&&ghost.Areas.Count==8,"Native campaigns must partition 27 Areas as 10/9/8");
        Check(ghost.Areas[0].Start==157&&ghost.Areas[1].Start==102,"GoTB must preserve authored order, not sort by height");
        Check(AreaRoutes.Find(ghost,102,0)==1,"GoTB transition to Bog");Check(AreaRoutes.Find(ghost,158,1)==0,"GoTB return to earlier Area");
        Check(!main.Areas.Any(a=>plus.Areas.Any(b=>b.Id==a.Id)||ghost.Areas.Any(b=>b.Id==a.Id)),"Campaigns must not mix");
        Check(main.Key!=plus.Key&&plus.Key!=ghost.Key,"Campaign records have separate identities");
        Check(Native.ResolveCampaign(false,JumpKing.GameManager.TitleScreenResult.StartNormalGame,true,true)==Campaign.MainBabe,"New Main start overrides stale expansion flags");
        Check(Native.ResolveCampaign(false,JumpKing.GameManager.TitleScreenResult.StartNewBabePlus,false,true)==Campaign.NewBabePlus,"Explicit NB+ start overrides saved Ghost flag");
        Check(Native.ResolveCampaign(false,JumpKing.GameManager.TitleScreenResult.StartOwlGame,false,false)==Campaign.GhostOfTheBabe,"Explicit GoTB start");
        Check(Native.ResolveCampaign(false,JumpKing.GameManager.TitleScreenResult.Continue,false,true)==Campaign.GhostOfTheBabe&&Native.ResolveCampaign(false,JumpKing.GameManager.TitleScreenResult.Continue,true,false)==Campaign.NewBabePlus,"Continue resolves saved campaign flags");
        Check(Native.ResolveCampaign(true,JumpKing.GameManager.TitleScreenResult.StartNormalGame,true,true)==Campaign.Custom,"Custom maps never inherit native campaign flags");
        var custom=AreaRoutes.Create(all,"custom","v1","27 custom Areas",Campaign.Custom);Check(custom.Areas.Count==27,"Never apply native campaign slicing to a Workshop map");
        var route=Simple();Check(AreaRoutes.Find(route,2,0)==0,"Overlap before unlock stays in previous Area");Check(AreaRoutes.Find(route,3,0)==1,"Area unlock disambiguates overlap");
        Check(route.Key!=AreaRoutes.Create(route.Areas,"test","other","Test",Campaign.Custom).Key,"Map revision separates records");
    }
    private static void Runs()
    {
        var route=Simple();var run=new Run{RouteKey=route.Key};var tracker=new RunTracker(route,run,0);
        Check(!tracker.Advance(3,3,false,3,false),"Airborne Area entry waits for native grounded discovery");
        Check(tracker.Advance(4,4,false,3,true)&&run.Splits.Count==1,"Area entry completes preceding segment");
        Check(!tracker.Advance(5,5,false,3,true)&&!tracker.Advance(6,6,false,1,true),"Repeated entries and falls never duplicate a split");
        tracker.Advance(6,9,true,5,true);Check(run.Splits.Count==1&&run.Elapsed.Real==9&&run.Elapsed.Active==6,"Real time includes pause; active time excludes it");
        Check(tracker.Advance(8,11,false,5,true),"Next Area completes once");tracker.Finish(true);Check(run.Splits.Count==3&&run.Status=="Finished"&&!run.Practice,"Final Area ends at victory");tracker.Finish(true);Check(run.Splits.Count==3,"Victory is idempotent");
        var archive=new RunArchive{Route=route,Runs=new List<Run>{run}};Check(Records.Best(archive,"Default",ClockMode.GameTime)==run,"Completed clean run is PB");Check(Records.Sum(archive,"Default",ClockMode.GameTime)==8,"Sum of best complete segments");
        var skip=new Run{RouteKey=route.Key};var skipped=new RunTracker(route,skip,0);skipped.Advance(1,1,false,5,true);skipped.Finish(true);archive.Runs.Add(skip);Check(skip.Practice&&skip.Splits.All(s=>!s.Eligible),"Skipped Areas cannot create zero-time golds");Check(Records.Best(archive,"Default",ClockMode.GameTime)==run,"Practice cannot replace PB");
        var restore=new Run{RouteKey=route.Key};var restored=new RunTracker(route,restore,0);restored.Advance(10,10,false,1,true);restored.Advance(2,11,false,3,true);Check(restore.Practice,"Clock reversal invalidates record eligibility");
        var partial=new Run{RouteKey=route.Key};var interrupted=new RunTracker(route,partial,0);interrupted.Advance(2,2,false,3,true);interrupted.Finish(false);archive.Runs.Add(partial);Check(Records.Gold(archive,"Default","a",ClockMode.GameTime)==2,"Valid segment from interrupted run is a gold");Check(Records.Best(archive,"Another",ClockMode.GameTime)==null,"Categories remain separate");
        var wrong=new Run{RouteKey=route.Key};new RunTracker(route,wrong,0).Finish(true);Check(wrong.Practice,"Early victory cannot manufacture missing Area records");
        var modified=XmlData.Clone(run);modified.Id=Guid.NewGuid().ToString("N");modified.RulesKey="boots-and-custom-mod";modified.Elapsed.Game=4;archive.Runs.Add(modified);
        Check(Records.Best(archive,"Default",ClockMode.GameTime)==run&&Records.Best(archive,"Default",ClockMode.GameTime,modified.RulesKey)==modified,"Stable modified runs keep their own PB without mixing rules");
        modified.Flag("State restored");Check(Records.Best(archive,"Default",ClockMode.GameTime,modified.RulesKey)==null,"Restored modified run loses record eligibility");
    }
    private static void Coordinates()
    {
        var s=new Surface{Window=new Rectangle(0,0,1280,720),Game=new Rectangle(160,0,960,720),Scale=1};var w=new Widget{Space=AnchorSpace.RightBar,Width=100,Height=50,AnchorX=.5f,X=0,Y=40};Check(s.Bounds(w).X==1150,"Pillarbox coordinate anchor");
        var wide=new Surface{Window=new Rectangle(0,0,1920,720),Game=new Rectangle(480,0,960,720),Scale=1};Check(wide.Bounds(w).X==1630,"Anchor follows changing aspect");
        s.SetPosition(w,1234,70);Check(s.Bounds(w).X==1234&&s.Bounds(w).Y==70,"Position round trip");s.Rescue(w);Check(s.Bounds(w).Right<=1280,"Rescue off-screen widgets");
        var narrow=new Surface{Window=new Rectangle(0,0,960,720),Game=new Rectangle(0,0,960,720),Scale=1};w.Missing=MissingSpace.Hide;Check(narrow.Bounds(w)==Rectangle.Empty,"Missing bar can hide widget");w.Missing=MissingSpace.MoveInside;Check(narrow.Bounds(w)!=Rectangle.Empty,"Missing bar can relocate widget");
        var layout=Defaults.Preset("Sidebars");var history=new EditHistory();history.Push(layout);layout.Widgets[0].X=999;var old=history.Undo(layout);Check(old.Widgets[0].X!=999,"Undo owns deep snapshot");Check(history.Redo(old).Widgets[0].X==999,"Redo restores change");
        var imported=XmlData.Clone(layout);imported.Widgets[0].Width=float.NaN;Store.ValidateLayout(imported);Check(imported.Widgets[0].Width==220,"Nonfinite dimensions normalize safely");
        var controls=imported.Widgets.First(v=>v.Kind==WidgetKind.Inputs);controls.Keys[0].Width=float.NaN;controls.Keys[0].X=float.PositiveInfinity;Store.ValidateLayout(imported);Check(controls.Keys[0].Width==56&&controls.Keys[0].X==0,"Imported key geometry normalizes safely");
        for(int i=0;i<10;i++)imported=imported.Clone();Check(imported.Widgets.First(v=>v.Kind==WidgetKind.Inputs).Keys.Count==5,"Repeated load, undo and duplicate never append default input keys");
        foreach(int count in new[]{5,12}){var bounds=new Rectangle(0,0,320,150);var keys=Enumerable.Range(0,count).Select(i=>Widgets.ControllerKeyBounds(bounds,i,count)).ToArray();Check(keys.All(k=>bounds.Contains(k))&&!keys.SelectMany((a,i)=>keys.Skip(i+1).Select(b=>a.Intersects(b))).Any(v=>v),"Controller buttons stay inside widget without overlap: "+count);}
    }
    private static void Controls()
    {
        var controls=Defaults.Make(WidgetKind.Inputs);
        for(int pass=0;pass<12;pass++){
            Defaults.SwitchControls(controls,InputStyle.Controller);Check(controls.Width==356&&controls.Height==180,"Controller restores its own default size");
            Defaults.SwitchControls(controls,InputStyle.Keyboard);Check(controls.Width==344&&controls.Height==136,"Keyboard restores its own default size");
            Defaults.SwitchControls(controls,InputStyle.Row);Check(controls.Width==356&&controls.Height==80,"Row returns to its original height after switching");
            controls=controls.Clone();
        }
        controls.Width=430;controls.Height=90;Defaults.SwitchControls(controls,InputStyle.Controller);controls.Width=380;controls.Height=200;
        Defaults.SwitchControls(controls,InputStyle.Row);Check(controls.Width==430&&controls.Height==90,"Custom row size survives switching");
        Defaults.SwitchControls(controls,InputStyle.Controller);Check(controls.Width==380&&controls.Height==200&&controls.ControlSizes.Count==3,"Custom controller size and XML history do not accumulate sizes");
        var surface=new Surface{Window=new Rectangle(0,0,1280,720),Game=new Rectangle(160,0,960,720),Scale=1};
        var preset=Defaults.Preset("Sidebars",surface);Check(surface.Area(AnchorSpace.RightBar).Contains(surface.Bounds(preset.Widgets.First(w=>w.Kind==WidgetKind.Splits))),"Sidebar preset fits the actual 16:9 black bar");
        Check(surface.Bounds(preset.Widgets[0]).Y==16,"Preset keeps native timer near the game edge");
        Inputs.Clear();Inputs.Focused=true;Inputs.Now=10;Inputs.Keyboard=new KeyboardState(Keys.Space);Inputs.Observe("Key:Space",InputDevice.Keyboard);
        var state=Inputs.Get("Key:Space",InputDevice.Keyboard);Check(Inputs.DisplayPressed(state,0),"Fresh physical key press is visible immediately");
        Inputs.Now=10.01;Inputs.Keyboard=new KeyboardState();Inputs.Observe("Key:Space",InputDevice.Keyboard);Check(!Inputs.DisplayPressed(state,controls.MinimumPress),"Default release has no artificial hold delay");
        Check(Inputs.DisplayPressed(state,.08f),"Optional minimum hold makes a short tap readable");Inputs.Now=10.081;Check(!Inputs.DisplayPressed(state,.08f),"Minimum hold expires from press, not release");
        Inputs.Now=11;Inputs.Keyboard=new KeyboardState(Keys.Space);Inputs.Observe("Key:Space",InputDevice.Keyboard);Inputs.Now=12;Inputs.Keyboard=new KeyboardState();Inputs.Observe("Key:Space",InputDevice.Keyboard);Check(!Inputs.DisplayPressed(state,.08f),"A long hold never has a release tail");
        Inputs.Now=13;Inputs.Keyboard=new KeyboardState(Keys.Space);Inputs.Observe("Key:Space",InputDevice.Keyboard);Inputs.Focused=false;Check(!Inputs.DisplayPressed(state,1),"Focus loss hides active keys and minimum hold");
        Check(Inputs.Matches(new[]{9001},new[]{17,9001})&&!Inputs.Matches(new[]{18},new[]{17,9001}),"Logical keys accept current physical or virtual chord codes");
        Check(Inputs.MapPointer(320,180,640,360,1280,720,1)==new Vector2(640,360),"Pointer respects client-to-backbuffer scale");Inputs.Clear();
        var original=JumpKing.Controller.ControllerManager.instance;bool wasActive=Controller.Active;Controller.Active=true;
        try{
            var manager=(JumpKing.Controller.ControllerManager)FormatterServices.GetUninitializedObject(typeof(JumpKing.Controller.ControllerManager));JumpKing.Controller.ControllerManager.instance=manager;
            var hardware=new PhysicalPad();var pad=new JumpKing.Controller.PadInstance(hardware,hardware.GetDefaultBind());
            typeof(JumpKing.Controller.ControllerManager).GetField("m_pads",Native.Flags).SetValue(manager,new List<JumpKing.Controller.PadInstance>{pad});
            controls=Defaults.Make(WidgetKind.Inputs);Inputs.Focused=true;Inputs.Now=20;hardware.Pressed=new[]{32};hardware.Polls=0;Inputs.BeginNativeSample();Inputs.CapturePhysical(hardware);Inputs.Update(new[]{controls});
            Check(Inputs.Get("Jump",InputDevice.Auto).Down&&hardware.Polls==1,"Logical buttons consume the native sample without an additional driver poll");
            hardware.Pressed=new int[0];Inputs.Now=20.01;Inputs.BeginNativeSample();Inputs.CapturePhysical(hardware);Inputs.Update(new[]{controls});Check(!Inputs.Get("Jump",InputDevice.Auto).Down,"Logical release is visible immediately after the native sample");
            pad.GetBind().jump=new[]{88};hardware.Pressed=new[]{88};Inputs.BeginNativeSample();Inputs.CapturePhysical(hardware);Inputs.Update(new[]{controls});Check(Inputs.Get("Jump",InputDevice.Auto).Down&&Inputs.Get("Jump",InputDevice.Auto).Label=="88","Rebinding is observed on the next sample");
            controls.Keys.Add(new InputKey{Source="Device:fixture-pad|88"});hardware.Polls=0;Inputs.Update(new[]{controls});Check(hardware.Polls==0&&Inputs.Get("Device:fixture-pad|88",InputDevice.Auto).Down,"Physical and logical keys share their native sample without polling");
            for(int frame=0;frame<120;frame++)Inputs.Update(new[]{controls});Check(hardware.Polls==0,"Extra render frames never repoll a controller driver");
            var originalButtons=hardware.Pressed;Check(ReferenceEquals(Inputs.CapturePhysical(hardware),originalButtons),"Native callers retain their original physical sample");originalButtons[0]=99;Inputs.Update(new[]{controls});Check(Inputs.Get("Jump",InputDevice.Auto).Down,"Native sample snapshot survives a device reusing its array");
            Inputs.BeginNativeSample();Inputs.Update(new[]{controls});Check(!Inputs.Get("Jump",InputDevice.Auto).Down,"A device absent from the next native sample cannot leave held buttons");
            Check(!Inputs.HasWidgets(Defaults.Preset("Minimal").Widgets),"Timer-only layouts do not need widget input sampling");
        }finally{JumpKing.Controller.ControllerManager.instance=original;Controller.Active=wasActive;Inputs.Clear();}
    }
    private sealed class PhysicalPad:JumpKing.Controller.IPad
    {
        internal int[] Pressed=new int[0];internal int Polls;
        public int[] GetPressedButtons(){Polls++;return Pressed;}
        public string ButtonToString(int code){return code.ToString();}
        public string GetSaveIdentifier(){return "fixture-pad";}
        public string GetPrintName(){return "Fixture";}
        public bool IsConnected(){throw new Exception("Overlay must not repoll device connection for every key");}
        public JumpKing.Controller.PadBinding GetDefaultBind(){return new JumpKing.Controller.PadBinding{jump=new[]{32}};}
    }
    private static void Persistence()
    {
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string file=Path.Combine(root,"test.xml");
        var first=Defaults.Settings();first.Layouts[0].Name="Original";Store.Write(file,XmlData.Serialize(first));var second=XmlData.Clone(first);second.Layouts[0].Name="Changed";Store.Write(file,XmlData.Serialize(second));Check(File.Exists(file+".bak"),"Atomic writes retain backup");Check(Store.Load<SettingsData>(file).Layouts[0].Name=="Changed","Latest atomic data loads");File.WriteAllText(file,"broken");Check(Store.Load<SettingsData>(file).Layouts[0].Name=="Original","Malformed file recovers backup");
        Store.Initialize(root);Store.Settings.Layouts[0].Widgets.Add(new Widget{Kind=WidgetKind.Text,Text="Привет, Jump King!"});Store.SaveSettings();Store.Flush();Check(Store.Load<SettingsData>(Path.Combine(root,"Layouts.xml")).Layouts[0].Widgets.Last().Text=="Привет, Jump King!","Unicode layout persists");
        bool rejected=false;try{XmlData.Parse<Layout>("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///c:/test'>]><Layout>&a;</Layout>");}catch{rejected=true;}Check(rejected,"Import rejects external entity expansion");
        Store.LoadArchive(Simple());var run=new Run{RouteKey=Store.Archive.Route.Key};Store.SaveRun(run);Store.Flush();Store.SaveRun(run);Store.Flush();Check(Store.Archive.Runs.Count==1,"Checkpoint replaces by run identity");Store.Export(Store.Settings.Layouts[0]);Check(File.Exists(Path.Combine(root,"Exports","Layout.xml")),"Layout export is separate from records");Store.Warning="";
        Check(Directory.GetFiles(root,"*.unreadable-*").Length>0&&Directory.GetFiles(root,"*.recovered-*").Length>0,"Corrupt originals and recovered backups survive later saves");
        var invalid=XmlData.Clone(Store.Archive);invalid.Runs[0].Elapsed.Real=double.NaN;rejected=false;try{Store.ValidateArchive(invalid,Simple());}catch(InvalidDataException){rejected=true;}Check(rejected,"Semantic archive corruption cannot create invalid PBs");
        var profile=Defaults.Preset("Map layout");profile.Map="test";profile.Aspect="Wide";Store.Settings.Layouts.Add(profile);Check(Store.Current(new Surface{Window=new Rectangle(0,0,1280,720)},"test")==profile,"Map and aspect profile overrides global fallback");
        var legacy=Defaults.Settings();legacy.PresentationRevision=0;var timer=legacy.Layouts[0].Widgets[0];timer.X=123;timer.MinimumPress=.08f;var alternate=new Widget{Kind=WidgetKind.Timer,Source="RealTime",MinimumPress=.2f};legacy.Layouts[0].Widgets.Add(alternate);
        Store.MigratePresentation(legacy);Check(timer.Kind==WidgetKind.Timer&&timer.X==123&&!timer.GameFrame&&timer.MinimumPress==0,"Migration retains timer position and removes the old default input delay");
        Check(alternate.Kind==WidgetKind.Text&&alternate.Text=="{real_time}"&&alternate.MinimumPress==.2f,"Migration preserves optional clock content and explicit minimum flash settings");
        string migrated=XmlData.Serialize(legacy);Store.MigratePresentation(legacy);Check(XmlData.Serialize(legacy)==migrated,"Presentation migration is idempotent");
        var queued=new RunArchive{Route=Simple(),Runs=new List<Run>{new Run{Note="Before",Flags=new List<string>{"Flag"},Splits=new List<Split>{new Split{AreaId="a",End=new Times{Game=3},Duration=new Times{Game=3}}}}}};
        string expected=XmlData.Serialize(queued),snapshotPath=Path.Combine(root,"snapshot.xml");var snapshot=queued.Copy();
        Store.Queue(snapshotPath,queued);queued.Route.Areas[0].Name="Changed";queued.Runs[0].Note="After";queued.Runs[0].Elapsed.Game=90;queued.Runs[0].Flags.Add("Later");queued.Runs[0].Splits[0].End.Game=50;queued.Runs[0].Splits[0].Duration.Game=40;queued.Runs.Add(new Run());Store.Flush();
        Check(XmlData.Serialize(snapshot)==expected,"Archive copy owns every mutable route, run, flag and split value");Check(XmlData.Serialize(Store.Load<RunArchive>(snapshotPath))==expected,"Worker serializes the detached snapshot, not subsequent game-thread edits");
        for(int i=0;i<20;i++){queued.Runs[0].Note="Revision "+i;Store.Queue(snapshotPath,queued);}Store.Flush();Check(Store.Load<RunArchive>(snapshotPath).Runs[0].Note=="Revision 19","Coalescing retains the newest checkpoint");
        string blocked=Path.Combine(root,"blocked");File.WriteAllText(blocked,"fixture");Store.Queue(Path.Combine(blocked,"cannot-write.xml"),queued);Store.Flush();Check(Store.Warning.StartsWith("Save failed:"),"Background save failure remains visible and Flush completes");Store.Queue(snapshotPath,queued);Store.Flush();Check(Store.Load<RunArchive>(snapshotPath).Runs.Count==2,"Worker accepts saves after a failed write");Store.Warning="";
    }
    private static void Provider()
    {
        bool restored=false;int reads=0;var source=new OverlaySource{Id="fixture.test",Name="Fixture",Text=()=>{reads++;return "42";},SetNativeVisible=v=>restored=v};long before=OverlayRegistry.Generation;
        using(var lease=OverlayRegistry.Register(source)){Check(OverlayRegistry.Sources().Contains(source)&&OverlayRegistry.Generation>before,"Provider discovery");bool rejected=false;try{OverlayRegistry.Register(source);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Provider IDs cannot collide");}
        Check(restored&&!OverlayRegistry.Sources().Contains(source),"Provider unregister restores native visibility");
        var oldLayout=Controller.Layout;bool oldActive=Controller.Active;try{Controller.Active=true;Controller.Layout=new Layout();using(OverlayRegistry.Register(source)){Adapters.Update();Check(reads==0,"Unused providers do not execute callbacks");var widget=new Widget{Kind=WidgetKind.External,Source=source.Id};Controller.Layout.Widgets.Add(widget);Adapters.Update();Check(reads==1,"Visible provider updates its content");widget.Enabled=false;Adapters.Update();Check(reads==1,"Disabled provider stops executing callbacks");}}finally{Controller.Layout=oldLayout;Controller.Active=oldActive;Adapters.Update();}
    }
    private static void NativeHooks()
    {
        Native.Validate();Hooks.Install();Check(Hooks.Installed,"Installed-game hook contract");var target=typeof(Game1).GetMethod("DrawRenderTarget",Native.Flags);Check(Harmony.GetPatchInfo(target).Postfixes.Any(p=>p.owner==Hooks.Id),"Full-window composition runs after native or Smooth Camera scene presentation");
        if(cameraAssembly!=null){var type=Assembly.LoadFrom(cameraAssembly).GetType("SmoothCamera.Hooks",true);type.GetMethod("Install",Native.Flags).Invoke(null,null);Check(Harmony.GetPatchInfo(target).Prefixes.Any(p=>p.owner=="smooth-camera.render")&&Harmony.GetPatchInfo(target).Postfixes.Any(p=>p.owner==Hooks.Id),"Smooth Camera native presentation and Overlay final draw coexist");type.GetMethod("Uninstall",Native.Flags).Invoke(null,null);Check(Harmony.GetPatchInfo(target).Owners.Contains(Hooks.Id),"Foreign unpatch leaves Overlay installed");}
        Hooks.Remove();var info=Harmony.GetPatchInfo(target);Check(info==null||!info.Owners.Contains(Hooks.Id),"Hooks release cleanly");
        using(var sibling=new JKRuntime.OwnedPatches(Hooks.Id)){
            var callback=typeof(OverlayTests).GetMethod("SiblingPatch",Native.Flags);sibling.Add(target,postfix:callback);
            Hooks.Install();Hooks.Remove();
            Check(Harmony.GetPatchInfo(target).Postfixes.Any(p=>p.PatchMethod==callback),"Releasing presentation preserves independent adapter leases with the same owner ID");
        }
    }
    private static void SiblingPatch(){}
    private static void ReplayAdapter()
    {
        if(replayAssembly!=null){
            var assembly=Assembly.LoadFrom(replayAssembly);
            Check(assembly.GetType("Replays.ReplaySettingsStore",true).GetMethod("SetGhost",Native.Flags)!=null,"Current Replays exposes transactional ghost selection");
            using(var scope=new JKRuntime.RuntimeScope()){
                Adapters.Prepare(scope);
                var queue=assembly.GetType("Replays.ReplayRepository",true).GetMethod("QueueSave",Native.Flags);
                Check(Harmony.GetPatchInfo(queue).Postfixes.Any(p=>p.owner==Hooks.Id),"Current replay queue adapter installs");
            }
            Check(!Adapters.PlaybackActive(),"Adapter scope clears playback references");
        }
        var settings=typeof(Adapters).GetField("replaySettings",Native.Flags);
        var refresh=typeof(Adapters).GetField("replayGhost",Native.Flags);
        settings.SetValue(null,typeof(FakeReplaySettings));refresh.SetValue(null,typeof(FakeReplaySettings).GetMethod("Refresh",Native.Flags));
        try{
            FakeReplaySettings.Value="old";FakeReplaySettings.Fail=true;FakeReplaySettings.Refreshed=false;
            bool failed=false;try{Adapters.Ghost("new");}catch(TargetInvocationException){failed=true;}
            Check(failed&&FakeReplaySettings.Value=="old"&&!FakeReplaySettings.Refreshed,"Failed ghost commit preserves selection and does not refresh playback");
            FakeReplaySettings.Fail=false;Check(Adapters.Ghost("new")&&FakeReplaySettings.Value=="new"&&FakeReplaySettings.Refreshed,"Ghost refresh follows successful commit");
        }finally{settings.SetValue(null,null);refresh.SetValue(null,null);}
    }
    private static class FakeReplaySettings
    {
        internal static string Value;internal static bool Fail,Refreshed;
        internal static void SetGhost(string id){if(Fail)throw new IOException("Locked settings fixture");Value=id;}
        internal static void Refresh(){Refreshed=true;}
    }
    private sealed class DeviceService:IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice{get;set;}
        public event EventHandler<EventArgs> DeviceCreated{add{}remove{}}public event EventHandler<EventArgs> DeviceDisposing{add{}remove{}}public event EventHandler<EventArgs> DeviceReset{add{}remove{}}public event EventHandler<EventArgs> DeviceResetting{add{}remove{}}
    }
    private static JumpKing.SaveThread.GeneralSettings timerSettings;
    private static bool GetSettings(ref JumpKing.SaveThread.GeneralSettings __result){__result=timerSettings;return false;}
    private static bool SetSettings(JumpKing.SaveThread.GeneralSettings __0){timerSettings=__0;return false;}
    private static bool GetTimerStats(ref JumpKing.MiscSystems.Achievements.PlayerStats __result){__result=new JumpKing.MiscSystems.Achievements.PlayerStats{time=3723.5f};return false;}
    private static int foreignTimerCalls;
    private static void ForeignTimer(){foreignTimerCalls++;}
    private static void NativeTimerFixture(Game1 game)
    {
        var h=new Harmony("overlay.fixture.timer");var general=Native.Game.GetType("JumpKing.SaveThread.SaveLube").GetProperty("generalSettings",Native.Flags);
        h.Patch(general.GetGetMethod(true),prefix:new HarmonyMethod(typeof(OverlayTests).GetMethod("GetSettings",Native.Flags)));
        h.Patch(general.GetSetMethod(true),prefix:new HarmonyMethod(typeof(OverlayTests).GetMethod("SetSettings",Native.Flags)));
        var achievements=Native.Game.GetType("JumpKing.MiscSystems.Achievements.AchievementManager");achievements.GetField("instance",Native.Flags).SetValue(null,FormatterServices.GetUninitializedObject(achievements));
        h.Patch(achievements.GetMethod("GetCurrentStats",Native.Flags),prefix:new HarmonyMethod(typeof(OverlayTests).GetMethod("GetTimerStats",Native.Flags)));
        var draw=typeof(JumpKing.GameManager.GameLoop).GetMethod("DrawIngameOverlayItems");h.Patch(draw,postfix:new HarmonyMethod(typeof(OverlayTests).GetMethod("ForeignTimer",Native.Flags)));
        Hooks.Install();var loop=FormatterServices.GetUninitializedObject(typeof(JumpKing.GameManager.GameLoop));var canvas=Controller.Canvas;Controller.Canvas=null;
        timerSettings=new JumpKing.SaveThread.GeneralSettings{gui_quick_load=true};GameTimer.Enabled=true;GameTimer.Precise=true;
        Check(timerSettings.gui_use_timer&&timerSettings.gui_timer_precise&&timerSettings.gui_quick_load,"Timer controls change native preferences without replacing unrelated settings");
        game.StartBatch();GameTimer.BeginFrame();draw.Invoke(loop,null);game.EndBatch();
        var capture=typeof(GameTimer).GetField("capture",Native.Flags).GetValue(null);
        Func<string> text=()=>{var commands=(System.Collections.IEnumerable)typeof(NativeCapture).GetField("commands",Native.Flags).GetValue(capture);foreach(var command in commands)return (string)command.GetType().GetField("Text",Native.Flags).GetValue(command);return "";};
        Check(GameTimer.HasDrawing&&text()=="01:02:03.500","Timer retains the actual native formatting and precision");
        GameTimer.Precise=false;game.StartBatch();GameTimer.BeginFrame();draw.Invoke(loop,null);game.EndBatch();Check(text()=="01:02:03","Native precision toggle takes effect on the next draw");
        GameTimer.Enabled=false;game.StartBatch();GameTimer.BeginFrame();draw.Invoke(loop,null);game.EndBatch();Check(!GameTimer.HasDrawing&&!Widgets.Visible(Controller.Layout.Widgets[0]),"Native disabled timer never leaves a stale overlay");
        Check(foreignTimerCalls==3,"Timer redirection preserves foreign postfixes even when disabled");GameTimer.Enabled=true;GameTimer.Precise=true;Controller.Canvas=canvas;
    }
    private static void RenderHud(Canvas canvas,RenderTarget2D target,Texture2D scene,Texture2D foreground,bool menu)
    {
        var device=canvas.Device;var game=Game1.instance;
        using(var nativeTarget=new RenderTarget2D(device,480,360)){
            device.SetRenderTarget(nativeTarget);device.Clear(Color.Black);Controller.Active=false;Controller.BeginFrame();Controller.Active=true;game.StartBatch();
            Game1.spriteBatch.Draw(scene,new Rectangle(0,0,480,360),Color.White);Game1.spriteBatch.Draw(foreground,new Rectangle(0,0,480,360),Color.White);Controller.BeginUi();
            typeof(JumpKing.GameManager.GameLoop).GetMethod("DrawIngameOverlayItems").Invoke(FormatterServices.GetUninitializedObject(typeof(JumpKing.GameManager.GameLoop)),null);game.EndBatch();
            Check(!Controller.Failed&&device.GetRenderTargets()[0].RenderTarget!=nativeTarget,"Native UI is separated from the scene without rebinding its discard target");
            if(menu){using(var pixel=new Texture2D(device,1,1)){pixel.SetData(new[]{Color.White});game.StartBatch();Game1.spriteBatch.Draw(pixel,new Rectangle(0,0,480,360),Color.Purple);game.EndBatch();}}
            device.SetRenderTarget(target);device.Clear(Color.Black);canvas.Begin(Controller.Surface);canvas.Batch.Draw(nativeTarget,Controller.Surface.Game,Color.White);canvas.End();Controller.Draw();
            if(menu){var pixels=new Color[target.Width*target.Height];target.GetData(pixels);var area=Controller.Surface.Game;Check(Enumerable.Range(area.Top,area.Height).All(y=>Enumerable.Range(area.Left,area.Width).All(x=>pixels[x+y*target.Width]==Color.Purple)),"Foreground native or mod menu remains above full-resolution HUD");}
        }
    }
    private static void SharpComposition(Canvas canvas,string output)
    {
        var device=canvas.Device;var game=Game1.instance;var layout=Controller.Layout;var pp=device.PresentationParameters;
        int oldWidth=pp.BackBufferWidth,oldHeight=pp.BackBufferHeight;var oldRect=game.GetGameRect();
        Controller.Layout=new Layout{Widgets=new List<Widget>{
            new Widget{Kind=WidgetKind.Panel,X=301,Y=101,Width=121,Height=53,Background=0xFF202018,Accent=0xFFFFFFFF,Border=1,GameFrame=false},
            new Widget{Kind=WidgetKind.Text,X=533,Y=101,Width=239,Height=89,Text="Precise HUD\nOne pixel borders",TextSize=19,Padding=0,Border=0,GameFrame=false,Background=0}
        }};
        try{
            foreach(var size in new[]{new Point(1280,720),new Point(1920,1080),new Point(1440,900)}){
                // Render fixtures provide output metrics without resizing a visible window.
                pp.BackBufferWidth=size.X;pp.BackBufferHeight=size.Y;int gameWidth=size.Y*4/3;
                var destination=new Rectangle((size.X-gameWidth)/2,0,gameWidth,size.Y);typeof(Game1).GetField("_game_rect",Native.Flags).SetValue(game,destination);
                Controller.Surface=Controller.GetSurface();var surface=Controller.Surface;
                using(var world=new RenderTarget2D(device,480,360))using(var actual=new RenderTarget2D(device,size.X,size.Y))using(var expected=new RenderTarget2D(device,size.X,size.Y))using(var pixel=new Texture2D(device,1,1))using(var uiReference=new Texture2D(device,480,360)){
                    var uiPixels=Enumerable.Repeat(Color.Black*.25f,480*360).ToArray();for(int y=45;y<145;y++)for(int x=100;x<260;x++)uiPixels[x+y*480]=Color.Purple;uiReference.SetData(uiPixels);
                    pixel.SetData(new[]{Color.White});
                    for(int frame=0;frame<2;frame++){
                        bool menu=frame==0;device.SetRenderTarget(world);device.Clear(Color.CornflowerBlue);Controller.Active=false;Controller.BeginFrame();Controller.Active=true;
                        game.StartBatch();Controller.BeginUi();
                        if(menu){Game1.spriteBatch.Draw(pixel,new Rectangle(0,0,480,360),Color.Black*.25f);Game1.spriteBatch.Draw(pixel,new Rectangle(100,45,160,100),Color.Purple);}
                        Controller.BeginUi();game.EndBatch();
                        device.SetRenderTarget(actual);device.Clear(Color.Black);canvas.Begin(surface);canvas.Batch.Draw(world,surface.Game,Color.White);canvas.End();Controller.Draw();
                        device.SetRenderTarget(expected);device.Clear(Color.Black);canvas.Begin(surface);canvas.Fill(surface.Game,Color.CornflowerBlue);foreach(var widget in Controller.Layout.Widgets)Widgets.Draw(canvas,widget);
                        if(menu)canvas.Batch.Draw(uiReference,surface.Game,Color.White);canvas.End();
                        var a=new Color[size.X*size.Y];var b=new Color[a.Length];actual.GetData(a);expected.GetData(b);
                        if(!a.SequenceEqual(b)){var differences=Enumerable.Range(0,a.Length).Where(i=>a[i]!=b[i]).ToArray();Console.WriteLine("Pixel differences: "+differences.Length+" first="+string.Join("; ",differences.Take(6).Select(i=>(i%size.X)+","+(i/size.X)+" actual="+a[i]+" expected="+b[i])));Save(actual,Path.Combine(output,"actual-failed.png"));Save(expected,Path.Combine(output,"expected-failed.png"));}
                        Check(a.SequenceEqual(b),"Window-resolution text and one-pixel borders match direct rendering, with "+(menu?"translucent/opaque UI on top":"no stale UI after close")+": "+size);
                        if(menu)Save(actual,Path.Combine(output,"sharp-hud-"+size.X+".png"));
                    }
                }
            }
        }finally{Controller.Layout=layout;pp.BackBufferWidth=oldWidth;pp.BackBufferHeight=oldHeight;typeof(Game1).GetField("_game_rect",Native.Flags).SetValue(game,oldRect);Controller.Surface=Controller.GetSurface();Controller.Foreground.BeginFrame();}
    }
    private static void ProfileRendering(Canvas canvas,RenderTarget2D output,Texture2D scene)
    {
        var device=canvas.Device;var game=Game1.instance;var layout=Controller.Layout;
        using(var world=new RenderTarget2D(device,480,360)){
            var draw=typeof(JumpKing.GameManager.GameLoop).GetMethod("DrawIngameOverlayItems");var loop=FormatterServices.GetUninitializedObject(typeof(JumpKing.GameManager.GameLoop));
            Action<bool> frame=overlay=>{
                Controller.Active=false;Controller.BeginFrame();Controller.Active=overlay;
                device.SetRenderTarget(world);device.Clear(Color.Black);game.StartBatch();Game1.spriteBatch.Draw(scene,new Rectangle(0,0,480,360),Color.White);if(overlay)Controller.BeginUi();draw.Invoke(loop,null);game.EndBatch();
                device.SetRenderTarget(output);device.Clear(Color.Black);canvas.Begin(Controller.Surface);canvas.Batch.Draw(world,Controller.Surface.Game,Color.White);canvas.End();if(overlay)Controller.Draw();
            };
            try{
                foreach(bool overlay in new[]{false,true}){
                    Controller.Layout=new Layout{Widgets=layout.Widgets.Where(w=>w.Kind==WidgetKind.Timer||w.Kind==WidgetKind.Splits).ToList()};
                    for(int i=0;i<30;i++)frame(overlay);
                    PerformanceProbe.Measure("render."+(overlay?"timer-splits":"native")+".1280x720.cpu-submit",()=>frame(overlay),240,0);
                }
                Controller.Layout=layout;for(int i=0;i<30;i++)frame(true);PerformanceProbe.Measure("render.sidebars-unicode.1280x720.cpu-submit",()=>frame(true),240,0);
            }finally{Controller.Layout=layout;Controller.Active=true;}
        }
    }
    private static void Graphics(string gameDirectory)
    {
        string output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"graphics");Directory.CreateDirectory(output);
        using(var form=new Form{ShowInTaskbar=false})using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,new PresentationParameters{DeviceWindowHandle=form.Handle,BackBufferWidth=1280,BackBufferHeight=720}))using(var target=new RenderTarget2D(device,1280,720))
        {
            var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));typeof(Game1).GetField("_instance",Native.Flags).SetValue(null,game);var services=new GameServiceContainer();services.AddService(typeof(IGraphicsDeviceService),new DeviceService{GraphicsDevice=device});typeof(Game).GetField("_services",Native.Flags).SetValue(game,services);foreach(var f in typeof(Game).GetFields(Native.Flags).Where(f=>f.FieldType==typeof(IGraphicsDeviceService)))f.SetValue(game,services.GetService(typeof(IGraphicsDeviceService)));typeof(Game1).GetField("_game_rect",Native.Flags).SetValue(game,new Rectangle(160,0,960,720));
            game.contentManager=new JKContentManager();Game1.spriteBatch=new SpriteBatch(device);
            using(var content=new Microsoft.Xna.Framework.Content.ContentManager(services,gameDirectory)){
            game.contentManager.font.MenuFont=content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");game.contentManager.font.MenuFontSmall=content.Load<SpriteFont>("Content/font/sf_small");game.contentManager.font.LocationFont=content.Load<SpriteFont>("Content/font/sf_pixolde_bold");game.contentManager.font.StyleFont=content.Load<SpriteFont>("Content/font/sf_pixolde");game.contentManager.font.GargoyleFont=content.Load<SpriteFont>("Content/font/sf_double_homicide");var frame=content.Load<Texture2D>("Content/gui/frame");int cell=frame.Width/3;game.contentManager.gui.FrameSprites=new Sprite[3,3];for(int x=0;x<3;x++)for(int y=0;y<3;y++)game.contentManager.gui.FrameSprites[x,y]=Sprite.CreateSprite(frame,new Rectangle(x*cell,y*cell,cell,cell));
            EnableTests.Graphics(gameDirectory);
            var scene=content.Load<Texture2D>("Content/screens/midground/1");var foreground=content.Load<Texture2D>("Content/screens/foreground/fg1");
            using(var canvas=new Canvas(device,gameDirectory))using(var layer=new ForegroundLayer(device)){
            Controller.Foreground=layer;
            Controller.Canvas=canvas;Controller.Surface=new Surface{Window=new Rectangle(0,0,1280,720),Game=new Rectangle(160,0,960,720),Scale=1};Controller.Route=Simple();Controller.Tracker=new RunTracker(Controller.Route,new Run{RouteKey=Controller.Route.Key},0);Store.Archive=new RunArchive{Route=Controller.Route};Controller.Layout=Defaults.Preset("Sidebars",Controller.Surface);Controller.Layout.Widgets.Add(new Widget{Kind=WidgetKind.Text,Name="Unicode",Text="Привет, Jump King!\n{area} · {time}",Width=430,Height=100,X=370,Y=190,TextSize=28});Store.Settings.Layouts=new List<Layout>{Controller.Layout};Store.Settings.SelectedLayout=Controller.Layout.Id;Controller.Active=true;Widgets.Refresh();
            device.SetRenderTarget(target);NativeTimerFixture(game);RenderHud(canvas,target,scene,foreground,false);Save(target,Path.Combine(output,"hud-16x9.png"));
            if(profileRender)ProfileRendering(canvas,target,scene);
            var pixels=new Color[1280*720];target.GetData(pixels);Check(Enumerable.Range(105,100).Any(y=>Enumerable.Range(1130,140).Any(x=>pixels[x+y*1280].R>100)),"Overlay draws glyphs and native frame into black side bars");Check(pixels.Any(p=>p.R>180&&p.G>180&&p.B>150),"Text glyphs rendered");
            RenderHud(canvas,target,scene,foreground,true);Save(target,Path.Combine(output,"menu-layering.png"));
            SharpComposition(canvas,output);device.SetRenderTarget(target);
            Editor.Begin();Editor.Preview=true;Input(550,225,true);Input(550,225,false);Controller.Status="F10 / Back+Start: done   Tab: panels   Drag widgets to arrange";device.Clear(Color.Black);canvas.Begin(Controller.Surface);canvas.Batch.Draw(scene,Controller.Surface.Game,Color.White);canvas.Batch.Draw(foreground,Controller.Surface.Game,Color.White);canvas.End();Controller.Draw();Save(target,Path.Combine(output,"editor-16x9.png"));
            EditorInteraction(canvas);Editor.Abort();Controller.Active=false;
            Check(canvas.Measure("Привет",24)>20,"Cyrillic glyph measurements available");
            var viewport=device.Viewport;var scissor=device.ScissorRectangle;var blend=device.BlendState;canvas.Begin(Controller.Surface);canvas.Clip(new Rectangle(12,15,60,80));canvas.Text("test",15,18,18,Color.White);canvas.End();Check(device.Viewport.Equals(viewport)&&device.ScissorRectangle==scissor&&device.BlendState==blend,"Renderer restores device state");
            using(var capture=new NativeCapture()){Check(capture.Begin()&&!capture.Begin(),"Foreign capture cannot nest");capture.Add(game.contentManager.font.MenuFont,"Jump: 75%",new Vector2(20,30),Color.White,Vector2.Zero,true);capture.End();Check(capture.Ready&&!capture.Capturing,"Foreign text retained only after complete scope");canvas.Begin(Controller.Surface);capture.Draw(canvas.Batch,new Rectangle(10,100,145,60),1);canvas.End();capture.Reset();Check(!capture.Ready,"Inactive foreign overlay cannot leave stale captured text");}
            foreach(int width in new[]{960,1920}){
                using(var aspectTarget=new RenderTarget2D(device,width,720)){device.SetRenderTarget(aspectTarget);var surface=new Surface{Window=new Rectangle(0,0,width,720),Game=new Rectangle((width-960)/2,0,960,720),Scale=1};device.Clear(Color.Black);Controller.Surface=surface;Editor.Begin();canvas.Begin(surface);canvas.Batch.Draw(scene,surface.Game,Color.White);canvas.Batch.Draw(foreground,surface.Game,Color.White);foreach(var w in Controller.Layout.Widgets)Widgets.Draw(canvas,w);Editor.Draw(canvas);canvas.End();Save(aspectTarget,Path.Combine(output,"editor-"+width+".png"));Editor.Abort();Check(Controller.Layout.Widgets.All(w=>surface.Bounds(w).Width>0),"Editor geometry remains valid at width "+width);}
            }
            Controller.Canvas=null;Controller.Foreground=null;Hooks.Remove();new Harmony("overlay.fixture.timer").UnpatchAll("overlay.fixture.timer");GameTimer.Release();Game1.spriteBatch.Dispose();device.SetRenderTarget(null);
            }}
        }
    }
    private static void Input(int x,int y,bool held,params Keys[] keys)
    {Inputs.PreviousMouse=Inputs.Mouse;Inputs.Mouse=new MouseState(x,y,0,held?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Inputs.Pointer=new Vector2(x,y);Inputs.PreviousKeyboard=Inputs.Keyboard;Inputs.Keyboard=new KeyboardState(keys);Inputs.Now+=.1;Inputs.Focused=true;Editor.Update();}
    private static void EditorInteraction(Canvas canvas)
    {
        Inputs.Clear();Inputs.PreviousKeyboard=new KeyboardState();Inputs.Keyboard=new KeyboardState(Keys.Space);Check(Inputs.Learn()=="Key:Space","Keyboard learning records physical key");Inputs.Clear();Inputs.Focused=true;Inputs.PreviousMouse=new MouseState();Inputs.Mouse=new MouseState(0,0,0,ButtonState.Pressed,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Check(Inputs.Learn()=="Mouse:Left","Mouse learning supports left button after activation release");
        Editor.Begin();Inputs.Clear();Inputs.Mouse=Inputs.PreviousMouse=new MouseState();Input(0,0,false,Keys.Tab);Input(0,0,false); // Hide panels while arranging on the full window.
        string id=Controller.Layout.Widgets.Last().Id;var widget=Controller.Layout.Widgets.Last();float x=widget.X,y=widget.Y;
        Input(550,225,true,Keys.LeftAlt);Input(190,285,true,Keys.LeftAlt);Input(190,285,false);Check(widget.X==x-360&&widget.Y==y+60,"Pointer drag moves text into the side bar");
        Input(190,285,false,Keys.LeftControl,Keys.Z);widget=Controller.Layout.Widgets.First(w=>w.Id==id);Check(widget.X==x&&widget.Y==y,"Editor undo restores drag");Input(190,285,false);Input(190,285,false,Keys.LeftControl,Keys.Y);widget=Controller.Layout.Widgets.First(w=>w.Id==id);Check(widget.X==x-360,"Editor redo restores drag");Input(190,285,false);
        Rectangle r=Controller.Surface.Bounds(widget);float width=widget.Width,height=widget.Height;Input(r.Right-2,r.Bottom-2,true,Keys.LeftAlt);Input(r.Right+48,r.Bottom+28,true,Keys.LeftAlt);Input(r.Right+48,r.Bottom+28,false);Check(widget.Width==width+50&&widget.Height==height+30,"Corner handle resizes selected widget");
        int count=Controller.Layout.Widgets.Count;Input(0,0,false,Keys.LeftControl,Keys.D);Check(Controller.Layout.Widgets.Count==count+1&&Controller.Layout.Widgets.Select(w=>w.Id).Distinct().Count()==count+1,"Duplicate owns unique identity");Input(0,0,false);Input(0,0,false,Keys.LeftControl,Keys.Z);Check(Controller.Layout.Widgets.Count==count,"Undo duplicate");Input(0,0,false);
        widget=Controller.Layout.Widgets.First(w=>w.Id==id);typeof(Editor).GetMethod("Edit",Native.Flags).Invoke(null,new object[]{"Text",widget.Text,(Action<string>)(s=>widget.Text=s),true});Check(JKRuntime.UI.UIApi.IsTextInputActive,"Overlay text editor participates in shared native/subframe input capture");foreach(char ch in "Привет! Новый текст")Editor.Character(ch);Input(0,0,false,Keys.Enter);Check(widget.Text=="Привет! Новый текст","In-game text field accepts and commits Unicode");Check(!JKRuntime.UI.UIApi.IsTextInputActive,"Committing Overlay text releases input capture");Input(0,0,false);
        typeof(Editor).GetMethod("Edit",Native.Flags).Invoke(null,new object[]{"Text",widget.Text,(Action<string>)(s=>widget.Text=s),true});Input(0,0,false,Keys.Escape);Check(!JKRuntime.UI.UIApi.IsTextInputActive,"Cancelling Overlay text releases input capture");Input(0,0,false);
        using(var sibling=JKRuntime.UI.UIApi.AcquireTextInput()){
            typeof(Editor).GetMethod("Edit",Native.Flags).Invoke(null,new object[]{"Text",widget.Text,(Action<string>)(s=>widget.Text=s),true});Editor.Abort();Check(JKRuntime.UI.UIApi.IsTextInputActive,"Editor abort preserves another text capture owner");
        }
        Check(!JKRuntime.UI.UIApi.IsTextInputActive,"Aborted editor leaves no text capture behind");Editor.Begin();Input(0,0,false,Keys.Tab);Input(0,0,false);
        Input(100,280,true);Input(100,280,false);widget.Locked=true;Input(0,0,false,Keys.Delete);Check(Controller.Layout.Widgets.Any(w=>w.Id==id),"Locked widget survives delete");widget.Locked=false;Input(0,0,false);
        float oldX=widget.X;Input(100,280,true,Keys.LeftAlt);Input(140,310,true,Keys.LeftAlt);Input(140,310,true,Keys.Escape);widget=Controller.Layout.Widgets.First(w=>w.Id==id);Check(widget.X==oldX,"Escape cancels a partial drag");Input(140,310,false);
        Input(0,0,false,Keys.Tab);Input(0,0,false);canvas.Begin(Controller.Surface);Editor.Draw(canvas);canvas.End();oldX=widget.X;Input(50,490,true);Input(150,530,true);Input(150,530,false);Check(widget.X==oldX,"Tool panel blocks canvas drag through empty panel space");
        Store.Flush();
    }
    private static void Save(RenderTarget2D target,string path){var colors=new Color[target.Width*target.Height];target.GetData(colors);using(var bitmap=new System.Drawing.Bitmap(target.Width,target.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){var data=bitmap.LockBits(new System.Drawing.Rectangle(0,0,target.Width,target.Height),System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);try{byte[] bytes=new byte[colors.Length*4];for(int i=0;i<colors.Length;i++){bytes[i*4]=colors[i].B;bytes[i*4+1]=colors[i].G;bytes[i*4+2]=colors[i].R;bytes[i*4+3]=colors[i].A;}System.Runtime.InteropServices.Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}finally{bitmap.UnlockBits(data);}bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
}
