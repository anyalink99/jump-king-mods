using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JKRuntime;
using JKRuntime.Gameplay;
using OverlayPlus;

internal static class EnableTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Set(string name,object value){typeof(Controller).GetField(name,Native.Flags).SetValue(null,value);}
    private static object Get(string name){return typeof(Controller).GetField(name,Native.Flags).GetValue(null);}
    internal static void Run()
    {
        Check(new SettingsData().ModuleEnabled,"Overlay is enabled by default");
        var legacy=XmlData.Parse<SettingsData>("<SettingsData><Enabled>false</Enabled></SettingsData>");
        Check(legacy.ModuleEnabled&&!legacy.Enabled,"Legacy Hide HUD does not disable the module");
        // Persistence already supplies a temporary data directory. Never initialize
        // the controller against the user's installed data in this fixture.
        Set("initialized",true);
        string path=Path.Combine(Store.Root,"Layouts.xml");
        string layout=XmlData.Serialize(Store.Settings.Layouts[0]);
        Store.SaveSettings();Controller.SetEnabled(false);
        Check(!Store.Load<SettingsData>(path).ModuleEnabled,"Disable drains older checkpoints before committing");
        using(var world=new RuntimeScope())using(var attempt=new RuntimeScope()){
            Controller.PrepareWorld(world);Controller.PrepareAttempt(attempt);
            Check(Controller.Canvas==null&&Controller.Foreground==null&&Controller.Route==null&&!Hooks.Installed,
                "Disabled loading performs no graphics, route/archive IO or patch installation");
            Controller.BeforeUpdate();Controller.AfterUpdate();Controller.Draw();Controller.OpenEditor();
            Check(!Controller.Active&&!Editor.Open&&!Controller.CapturesInput,"Disabled entry points cannot start editing or capture input");
        }
        Check(!Harmony.GetAllPatchedMethods().Any(m=>Harmony.GetPatchInfo(m).Owners.Contains(Hooks.Id)),"Disabled world has no Overlay hooks");
        Controller.SetEnabled(true);
        Check(Store.Load<SettingsData>(path).ModuleEnabled&&Controller.Canvas==null,"Title-menu enable persists without preparing an absent world");
        // Use the actual session teardown and shared subscription without needing
        // to construct a live native player/window in the headless tier.
        var session=new RuntimeScope();
        session.Defer(()=>typeof(Controller).GetMethod("EndSession",Native.Flags).Invoke(null,null));
        session.Own(GameplayEvents.Subscribe("overlay-plus",delegate{}));Set("session",session);
        Controller.Active=true;Controller.Route=AreaRoutes.Create(new Area[0],"test","v1","Test",Campaign.Custom);
        Store.Archive=new RunArchive{Route=Controller.Route};
        Controller.Tracker=new RunTracker(Controller.Route,new Run{RouteKey=Controller.Route.Key},0);
        var run=Controller.Tracker.Run;
        Controller.SetEnabled(false);
        Check(!Controller.Active&&Controller.Tracker==null&&Get("session")==null,"Disable ends the active tracking lifetime");
        Check(!RuntimeResources.Inspect().Any(r=>r.Contains("overlay-plus")),"Disable releases gameplay subscriptions");
        Check(run.Status!="Running","Disable checkpoints the interrupted run");
        Check(XmlData.Serialize(Store.Settings.Layouts[0])==layout,"Toggling preserves the layout");
        // A locked settings file must leave both the in-memory and disk switch off.
        using(var locked=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
            bool failed=false;try{Controller.SetEnabled(true);}catch(IOException){failed=true;}
            Check(failed&&!Store.Settings.ModuleEnabled&&!Controller.Active,"Failed preference save does not activate the module");
        }
        Controller.SetEnabled(true);
        Console.WriteLine("[OK] Overlay Enable: legacy defaults, disabled loading, teardown, persistence and failed-save rollback");
    }
    internal static void Graphics(string gameDirectory)
    {
        // Production Canvas resolves the native font beside JumpKing.exe, which
        // the build copies into this isolated test directory.
        string font=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Content","font","ttf_lanapixel.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(font));File.Copy(Path.Combine(gameDirectory,"Content","font","ttf_lanapixel.ttf"),font,true);
        using(var world=new RuntimeScope()){
            Controller.PrepareWorld(world);
            var canvas=Controller.Canvas;var layer=Controller.Foreground;
            var target=(Microsoft.Xna.Framework.Graphics.RenderTarget2D)typeof(ForegroundLayer).GetField("target",Native.Flags).GetValue(layer);
            Check(canvas!=null&&Hooks.Installed,"Enabled world owns graphics and patches");
            Controller.SetEnabled(false);
            Check(canvas.Batch.IsDisposed&&target.IsDisposed&&Controller.Canvas==null&&Controller.Foreground==null,"Disable disposes GPU resources immediately");
            Check(!Harmony.GetAllPatchedMethods().Any(m=>Harmony.GetPatchInfo(m).Owners.Contains(Hooks.Id)),"Disable removes presentation and adapter patches");
            for(int i=0;i<3;i++){
                Controller.SetEnabled(true);Check(Hooks.Installed&&Controller.Canvas!=null&&!ReferenceEquals(canvas,Controller.Canvas),"Re-enable creates fresh world resources");
                Controller.SetEnabled(false);Check(!Hooks.Installed&&Controller.Canvas==null,"Repeated disable releases fresh resources");
            }
        }
        Controller.SetEnabled(true);
        Check(Controller.Canvas==null&&!Hooks.Installed,"World exit prevents stale enable from resurrecting resources");
        Console.WriteLine("[OK] Overlay Enable GPU: immediate disposal, all hooks removed, repeated re-enable, world exit");
    }
}
