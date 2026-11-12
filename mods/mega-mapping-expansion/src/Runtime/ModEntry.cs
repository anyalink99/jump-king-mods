using System;
using System.IO;
using System.Reflection;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.Gameplay;
using JumpKing;
using JumpKing.Level;
using MegaMappingExpansion.Construction;

[assembly: AssemblyVersion("1.8.2.0")]
[assembly: AssemblyFileVersion("1.8.2.0")]

namespace MegaMappingExpansion
{
    [RuntimeModule("mega-mapping-expansion","Mega Mapping Expansion",Provides=new[]{"mega.mapping.scene:2:1"})]
    public static class ModEntry
    {
        public static bool SupportsPixelCollision{get{return true;}}
        public static bool RegisterPixelCollisionFactory(Type factoryType){return PixelCollision.RegisterFactory(factoryType);}
        public static bool IsPresentationEnabled{get{return MappingSettings.Enabled;}}
        public static bool SupportsCameraComposition{get{return true;}}
        public static bool IsCompositorActive{get{return NativeScene.Current!=null&&NativeScene.Current.Active;}}
        [MainMenuItemSetting]
        public static JKRuntime.UI.SettingToggle MainMenuEnabled(object factory,JumpKing.PauseMenu.GuiFormat format){return new JKRuntime.UI.SettingToggle(MappingSettings.Toggle);}
        [PauseMenuItemSetting]
        public static JKRuntime.UI.SettingToggle PauseMenuEnabled(object factory,JumpKing.PauseMenu.GuiFormat format){return MainMenuEnabled(factory,format);}
        [MainMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton MainMenu(object factory,JumpKing.PauseMenu.GuiFormat format){return new JumpKing.PauseMenu.BT.TextButton("Mapping inspector",JKRuntime.UI.UIApi.CreateMenuPage(factory,new SceneInspector()));}
        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton PauseMenu(object factory,JumpKing.PauseMenu.GuiFormat format){return MainMenu(factory,format);}
        private static NativeAssetPool assets;
        private static MovementObservationScope movement;
        private static MapPhysics physics;
        private static PendingAttempt pending;
        private static ActiveRun active;
        private sealed class PendingAttempt
        {
            internal string Root;
            internal NativeScene Scene;
            internal MapLayout Layout;
            internal NativeTransitions Transitions;
            internal NativeMovingPlatforms Platforms;
            internal MovingPlatformPlan PlatformPlan;
            internal bool Consumed;
        }
        private sealed class ActiveRun:IDisposable
        {
            internal NativeScene Scene;
            internal string Root;
            public void Dispose(){bool owns=ReferenceEquals(active,this);if(owns)active=null;if(Scene!=null){Scene.Dispose();Scene=null;}if(owns)NativeMapLayout.Release();}
        }
        [BeforeLevelLoad]
        public static void BeforeLevelLoad(){MappingSettings.Load();PixelCollision.Install();NativePhysicsFrame.Install();PhysicsCoordinates.Install();LevelManager.RegisterBlockFactory(MovingPlatformScan.Factory);Api.PixelCollisionFactories.Register(typeof(MovingPlatformFactory));}
        internal static NativeScene IntroScene{get{return pending!=null&&!pending.Consumed?pending.Scene:NativeScene.Current;}}
        [OnWorldReady]
        public static void PrepareWorld(RuntimeScope scope)
        {
            MappingSettings.Load();JKRuntime.Presentation.PlayerVisuals.Prepare(scope);PlayerUpdates.Prepare(scope);
            var layout=MapLayout.ReadForScreens(MappingPaths.ResolveLevelRoot(Game1.instance.contentManager.root),LevelManager.TotalScreens);
            if(layout!=null&&(layout.Physics.Enabled||layout.PhysicsFrames.Count>0))
                using(RuntimeApi.MeasureStartup("mega-mapping.map-physics"))physics=scope.Own(new MapPhysics(layout,MappingPaths.ResolveLevelRoot(Game1.instance.contentManager.root)));
            using(RuntimeApi.MeasureStartup("mega-mapping.movement-contract"))movement=scope.Own(MovementObservation.Prepare());
            assets=scope.Own(new NativeAssetPool());
            var materials=NativeMaterials.Loaded;
            if(materials!=null)scope.Defer(()=>{materials.Dispose();if(NativeMaterials.Loaded==materials){NativeMaterials.Loaded=null;MaterialContacts.Clear();}});
            scope.Own(new NativeResults(MappingPaths.ResolveLevelRoot(Game1.instance.contentManager.root)));
            JKRuntime.World.NativeWorldInteractions.Prepare(scope);
            using(RuntimeApi.MeasureStartup("mega-mapping.scene-hooks"))scope.Own(new SceneHooks());
            scope.Defer(delegate{Stop();NativeScene.ReleaseCompleted();pending=null;assets=null;movement=null;physics=null;Endings.NativeEndings.Release();});
        }
        [BeforeAttempt]
        public static void PrepareAttempt(RuntimeScope scope)
        {
            NativeScene.ReleaseCompleted();
            if(assets==null||movement==null)throw new InvalidOperationException("Mapping world preparation is required before attempt preparation");
            string root=MappingPaths.ResolveLevelRoot(Game1.instance.contentManager.root);var candidate=new PendingAttempt{Root=root};
            using(RuntimeApi.MeasureStartup("mega-mapping.layout-read"))candidate.Layout=MapLayout.ReadForScreens(root,LevelManager.TotalScreens);
            if(candidate.Layout!=null&&candidate.Layout.Transitions!=null&&candidate.Layout.Transitions.Dynamic)
                using(RuntimeApi.MeasureStartup("mega-mapping.transitions-read"))candidate.Transitions=new NativeTransitions(candidate.Layout.Transitions,ScenePersistence.NativeDirectory);
            using(RuntimeApi.MeasureStartup("mega-mapping.platforms-prepare"))
            {
                var plan=MovingPlatformScan.Prepare(root,LevelManager.TotalScreens);
                candidate.PlatformPlan=plan;
                if(plan.Platforms.Count>0)candidate.Platforms=scope.Own(new NativeMovingPlatforms(plan,root));
            }
            SceneDocument document;
            using(RuntimeApi.MeasureStartup("mega-mapping.scene-read"))document=ConstructionSceneFile.Read(root);
            NativeMovingPlatforms.ValidateTargets(candidate.PlatformPlan,document,true);
            if(NativeMaterials.Loaded!=null)
                using(RuntimeApi.MeasureStartup("mega-mapping.materials-prepare"))
                {
                    var materials=NativeMaterials.Loaded;
                    var fresh=MaterialPlan.Read(root,LevelManager.TotalScreens);
                    if(fresh.Identity!=materials.Plan.Identity)throw new InvalidOperationException("Materials changed since collision loading; reload the map");
                    NativeMaterials.ValidatePlan(materials.Plan,candidate.PlatformPlan,document,root,LevelManager.TotalScreens,false);
                    materials.Prepare();materials.PrepareState(materials.Instances.Count>0);
                }
            using(RuntimeApi.MeasureStartup("mega-mapping.endings-read")){Endings.NativeEndings.Prepare(root);Endings.NativeEndings.ValidateScene(document);}
            if(document!=null)using(RuntimeApi.MeasureStartup("mega-mapping.scene-resources"))candidate.Scene=scope.Own(new NativeScene(document,root,assets));
            pending=candidate;scope.Defer(delegate{if(ReferenceEquals(pending,candidate))pending=null;});
        }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            MappingSettings.Load();Stop();var value=pending;string root=MappingPaths.ResolveLevelRoot(Game1.instance.contentManager.root);
            if(value==null||!string.Equals(value.Root,root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Mapping activation requires a prepared attempt for this map");
            value.Consumed=true;
            using(RuntimeApi.MeasureStartup("mega-mapping.layout-apply"))NativeMapLayout.BeginRun(value.Layout,value.Transitions);
            if(physics!=null)context.Track(physics.Attach(JumpKing.GameManager.GameLoop.m_player.m_body));
            if(value.Transitions!=null)context.Track(value.Transitions.Activate(JumpKing.GameManager.GameLoop.m_player));
            if(value.Platforms!=null)context.Track(value.Platforms.Activate(JumpKing.GameManager.GameLoop.m_player));
            if(NativeMaterials.Loaded!=null&&NativeMaterials.Loaded.Instances.Count>0)context.Track(NativeMaterials.Loaded.Activate(JumpKing.GameManager.GameLoop.m_player.m_body));
            var run=new ActiveRun{Scene=value.Scene,Root=root};active=run;context.Track(run);
            try
            {
                if(run.Scene!=null)run.Scene.Activate(context,JumpKing.GameManager.GameLoop.m_player,movement);
                var gateway=context.Track(new Construction.SceneGateway(()=>NativeScene.Current==null?null:NativeScene.Current.Program));context.Publish("mega.mapping.scene",gateway);
                context.Track(RuntimeApi.Mechanics.Register("mega-mapping-expansion","mega-mapping.player-actions",new Version(1,0),MechanicEffects.Movement,delegate{
                    bool configured=NativeScene.Current!=null&&NativeScene.Current.HasPlayerControls;return new MechanicState(MappingSettings.Enabled,configured,configured&&MappingSettings.Enabled,MechanicSource.Controller,"Map-authored player control actions");}));
                context.Track(RuntimeApi.Mechanics.Register("mega-mapping-expansion","mega-mapping.scene",new Version(2,0),JKRuntime.Gameplay.MechanicEffects.Presentation,delegate{
                    bool loaded=NativeScene.Current!=null;return new MechanicState(MappingSettings.Enabled,loaded,MappingSettings.Enabled&&loaded,MechanicSource.Setting,loaded?"Map-authored components and recipes":"No construction scene in this map");}));
                context.Track(RuntimeApi.Mechanics.Register("mega-mapping-expansion","mega-mapping.side-links",new Version(1,0),MechanicEffects.Movement,delegate{
                    bool linked=NativeMapLayout.Current!=null&&(NativeMapLayout.Current.Links.Count>0||NativeTransitions.Current!=null);return new MechanicState(linked,linked,linked,MechanicSource.Screen,"Map-authored native screen links");}));
                context.Track(RuntimeApi.Mechanics.Register("mega-mapping-expansion","mega-mapping.physics-frame",new Version(1,0),MechanicEffects.Movement|MechanicEffects.Collision,delegate{
                    bool configured=value.Layout!=null&&value.Layout.PhysicsFrames.Count>0;
                    bool enabled=physics!=null&&physics.ReferenceActive;
                    return new MechanicState(enabled,configured,enabled,MechanicSource.Screen,"Map-authored reference coordinates for native Y motion and collision");}));
                context.Track(RuntimeApi.Mechanics.Register("mega-mapping-expansion","mega-mapping.map-physics",new Version(1,0),MechanicEffects.Movement|MechanicEffects.Collision,delegate{
                    bool enabled=physics!=null&&physics.Symmetric;
                    return new MechanicState(enabled,enabled,enabled,MechanicSource.Controller,enabled?"symmetric v1; "+physics.Coverage:"Native or reference physics");}));
                context.Track(RuntimeApi.Mechanics.Register("mega-mapping-expansion","mega-mapping.moving-platforms",new Version(1,0),MechanicEffects.Movement|MechanicEffects.Collision,delegate{
                    bool enabled=NativeMovingPlatforms.Current!=null;return new MechanicState(enabled,value.Platforms!=null,enabled,MechanicSource.Screen,"Map-authored moving collision and platform routes");}));
                context.Track(RuntimeApi.Mechanics.Register("mega-mapping-expansion","mega-mapping.materials",new Version(1,0),MechanicEffects.Movement|MechanicEffects.Collision,delegate{
                    bool enabled=NativeMaterials.Current!=null;return new MechanicState(enabled,enabled,enabled,MechanicSource.Screen,"Map-authored RGB aliases, native layers and material regions");}));
                Log(run.Scene==null?"No MME construction scene in '"+root+"'":"Loaded MME construction scene from '"+root+"'; components="+run.Scene.Program.Components.Count+", generation="+run.Scene.Program.Revision);
            }
            catch{run.Dispose();throw;}
        }
        [OnLevelUnload]
        public static void Unload(){Stop();NativeMapLayout.Release();}
        [OnLevelEnd]
        public static void End(){if(active!=null&&active.Scene!=null)active.Scene.Complete();if(NativeMovingPlatforms.Current!=null)NativeMovingPlatforms.Current.Complete();}
        private static void Stop(){if(active!=null)active.Dispose();}
        internal static bool Reload(string root)
        {
            if(active==null||active.Scene==null)return false;
            if(JKRuntime.World.WorldControl.Owner!=null){Log("Reload rejected: leave world synchronization or replay first");return false;}
            var previous=active.Scene;NativeScene candidate=null;bool detached=false;
            try
            {
                var document=ConstructionSceneFile.Read(root,true);if(document==null)throw new FileNotFoundException("mapping_settings.xml is missing",root);
                string oldLayout=previous.Program.Definition.MapLayout==null?"":previous.Program.Definition.MapLayout.OuterXml;
                string newLayout=document.MapLayout==null?"":document.MapLayout.OuterXml;
                if(oldLayout!=newLayout||(document.Physics==null?"":document.Physics.OuterXml)!=(previous.Program.Definition.Physics==null?"":previous.Program.Definition.Physics.OuterXml))throw new InvalidOperationException("Collision layout changes require a map restart");
                var platforms=MovingPlatformScan.Prepare(root,LevelManager.TotalScreens);
                if(NativeMovingPlatforms.Current!=null?NativeMovingPlatforms.Current.SourceIdentity!=platforms.Identity:platforms.Platforms.Count>0)throw new InvalidOperationException("Moving-platform changes require a map restart");
                if(NativeMaterials.Loaded!=null&&MaterialPlan.Read(root,LevelManager.TotalScreens).Identity!=NativeMaterials.Loaded.Plan.Identity)throw new InvalidOperationException("Material changes require a map reload");
                NativeMovingPlatforms.ValidateTargets(platforms,document,true);
                Endings.NativeEndings.ValidateScene(document);candidate=new NativeScene(document,root,assets);
                if(document.SaveId==previous.Program.Definition.SaveId){candidate.Program.LoadVariables(previous.Program.SavedVariables());candidate.Program.Tick(0,0);candidate.Renderer.ClearTransient();}
                previous.Deactivate();detached=true;candidate.Activate(null,JumpKing.GameManager.GameLoop.m_player,movement);active.Scene=candidate;candidate=null;previous.Dispose();Log("Construction scene reload committed");return true;
            }
            catch(Exception error)
            {
                if(candidate!=null)candidate.Dispose();if(detached)previous.Activate(null,JumpKing.GameManager.GameLoop.m_player,movement);Log("Reload rejected; the previous construction scene remains active: "+error);return false;
            }
        }
        internal static void Log(string message)
        {
            Console.WriteLine("[Mega Mapping Expansion] "+message);
            try{JumpKingModTools.BoundedTextLog.Append(Path.Combine(PackageHost.GetDataDirectory(typeof(ModEntry).Assembly),"MegaMappingExpansion.log"),"["+DateTime.UtcNow.ToString("o")+"] "+message);}catch{}
        }
    }
}
