using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.Settings;
using JKRuntime.UI;
using JumpKing.Level;
using JumpKing.PauseMenu;
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PowersTests")]
namespace NoWalkOff
{
    public sealed class Preferences
    {
        public Preferences() { NoWalkOff = true; }
        public bool NoWalkOff { get; set; }
    }
    internal static class Settings
    {
        private static Preferences current;
        private static SettingsFile<Preferences> file;
        private static string PathName { get { return Path.Combine(PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly()), "NoWalkOff.Settings.xml"); } }
        internal static void Save()
        {
            var value = Current;
            if (file == null) file = new SettingsFile<Preferences>(PathName, delegate { return new Preferences(); });
            file.Save(value);
        }
        // Editors replace owned arrays/records in a detached candidate. publish only after durable commit
        internal static void Edit(Action<Preferences> edit)
        {
            var old = Current;
            var candidate = new Preferences { NoWalkOff=old.NoWalkOff };
            edit(candidate);
            if (file == null) file = new SettingsFile<Preferences>(PathName, delegate { return new Preferences(); });
            file.Save(candidate); current=candidate;
        }
        internal static Preferences Current
        {
            get
            {
                if (current == null)
                {
                    file = new SettingsFile<Preferences>(PathName, delegate { return new Preferences(); });
                    current = file.Value;
                }
                return current;
            }
        }
        internal static readonly Setting<bool> EdgeStop = new Setting<bool>("no-walk-off.no-walk-off", "No Walk Off",
            delegate { return Current.NoWalkOff; }, delegate(bool value) { JKRuntime.Gameplay.MapMechanics.RequireUserEnable("no-walk-off", value); bool previous = Current.NoWalkOff; Current.NoWalkOff = value; try { Save(); } catch { Current.NoWalkOff = previous; throw; } if(value) ModEntry.EnsureMotion(); });
    }
    public sealed class NoWalkOffOption : SettingToggle { public NoWalkOffOption() : base(Settings.EdgeStop, null, () => JKRuntime.Gameplay.MapMechanics.CanConfigure("no-walk-off")) { } }
    [RuntimeModule("no-walk-off", "No Walk Off")]
    public static class ModEntry
    {
        private static bool factoryInstalled;
        private static NoWalkOffController edgeController;
        internal static JKRuntime.Gameplay.MotionObservationScope MotionScope;
        private static RuntimeScope motionOwner;
        internal static void EnsureMotion()
        {
            if(MotionScope!=null || motionOwner==null) return;
            try
            {
                using(RuntimeApi.MeasureStartup("no-walk-off.motion-analysis"))
                    MotionScope=motionOwner.Own(JKRuntime.Gameplay.MotionObservation.PrepareLoaded());
                if(edgeController!=null) edgeController.BindMotion();
            }
            catch(Exception error) { Diagnostics.Write("No Walk Off motion observation unavailable: "+error.GetBaseException().Message); }
        }
        [OnWorldReady]
        public static void PrepareWorld(RuntimeScope scope)
        {
            JKRuntime.Gameplay.PlayerUpdates.Prepare(scope);
            motionOwner=scope;
            scope.Defer(delegate { MotionScope=null; motionOwner=null; });
            scope.Own(RuntimeApi.Geometry.RegisterTerrainPreparation("no-walk-off", types => {
                if (types.Contains(typeof(NoWalkOffSurfaceBlock)) || types.Contains(typeof(NoWalkOffZoneBlock))) EnsureMotion();
            }));
        }
        [BeforeAttempt]
        public static void PrepareAttempt(RuntimeScope scope)
        {
            if(Settings.Current.NoWalkOff || JKRuntime.Gameplay.MapMechanics.HasRules("no-walk-off") || BlockFactory.NoWalkOffAuthored) EnsureMotion();
        }
        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            MapActivation.Register();
            BlockFactory.AssertExclusive(null);
            BlockFactory.ResetScreens();
            if (!factoryInstalled) { LevelManager.RegisterBlockFactory(new BlockFactory()); factoryInstalled = true; }
        }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            context.Track(JKRuntime.Geometry.BlockCatalog.Register("no-walk-off", new[] {
                new JKRuntime.Geometry.BlockDeclaration("no-walk-off", MapPixels.NoWalkOffSurface, JKRuntime.Geometry.BlockVariant.Solid, true),
                new JKRuntime.Geometry.BlockDeclaration("no-walk-off", MapPixels.NoWalkOffZone, JKRuntime.Geometry.BlockVariant.Zone, false),
                new JKRuntime.Geometry.BlockDeclaration("no-walk-off", MapPixels.NoWalkOffScreen, JKRuntime.Geometry.BlockVariant.Screen, false)
            }));
            BlockFactory.AssertExclusive(JumpKing.Game1.instance.contentManager.level);
            edgeController = new NoWalkOffController(JumpKing.GameManager.GameLoop.m_player);
            context.Track(edgeController);
            context.Track(JKRuntime.State.GameState.Snapshots.Register(edgeController));
            context.Track(RuntimeApi.Mechanics.RegisterControlled("no-walk-off",
                new JKRuntime.Gameplay.MechanicDefinition("no-walk-off", new Version(1, 0),
                    JKRuntime.Gameplay.MechanicEffects.Movement, JKRuntime.Gameplay.MechanicComposition.Additive,
                    "player.support", null, edgeController.Id), edgeController.Describe));
        }
        [OnLevelUnload]
        public static void Stop() { if (edgeController != null) edgeController.Dispose(); edgeController = null; }
        [OnLevelEnd]
        public static void End() { Stop(); }
        [MainMenuItemSetting]
        public static NoWalkOffOption MainMenu(object factory, GuiFormat format) { return new NoWalkOffOption(); }
        [PauseMenuItemSetting]
        public static NoWalkOffOption PauseMenu(object factory, GuiFormat format) { return new NoWalkOffOption(); }
    }
}
