using System;
using System.IO;
using System.Reflection;
using System.Linq;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.Settings;
using JKRuntime.UI;
using JumpKing.Level;
using JumpKing.PauseMenu;

[assembly: AssemblyVersion("0.15.2.0")]
[assembly: AssemblyFileVersion("0.15.2.0")]

namespace MegaGameplayExpansion
{
    public sealed class Preferences
    {
        public bool WarpJump { get; set; }
        public bool NoWalkOff { get; set; }
        public bool AirDash { get; set; }
        public DashDeviceBinding[] DashBindings { get; set; }

        // accept only the retired sections, discard them instead of keeping a second settings model
        [System.Xml.Serialization.XmlAnyElement("GimmickPins", Namespace = "")]
        [System.Xml.Serialization.XmlAnyElement("GimmickRules", Namespace = "")]
        [System.Xml.Serialization.XmlAnyElement("GimmickSearch", Namespace = "")]
        [System.Xml.Serialization.XmlAnyElement("SavedSearches", Namespace = "")]
        public System.Xml.XmlElement[] DiscardedLibrarySettings { get { return null; } set { } }
    }
    internal static class Settings
    {
        private static Preferences current;
        private static SettingsFile<Preferences> file;
        private static string PathName { get { return Path.Combine(PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly()), "MegaGameplayExpansion.Settings.xml"); } }
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
            var candidate = new Preferences { WarpJump=old.WarpJump, NoWalkOff=old.NoWalkOff, AirDash=old.AirDash,
                DashBindings=old.DashBindings };
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
        internal static readonly Setting<bool> Warp = new Setting<bool>("mega-gameplay-expansion.warp-jump", "Warp Jump",
            delegate { return Current.WarpJump; }, delegate(bool value) { JKRuntime.Gameplay.MapMechanics.RequireUserEnable("mega.warp", value); bool previous = Current.WarpJump; Current.WarpJump = value; try { Save(); } catch { Current.WarpJump = previous; throw; } });
        internal static readonly Setting<bool> EdgeStop = new Setting<bool>("mega-gameplay-expansion.no-walk-off", "No Walk Off",
            delegate { return Current.NoWalkOff; }, delegate(bool value) { JKRuntime.Gameplay.MapMechanics.RequireUserEnable("mega.no-walk-off", value); bool previous = Current.NoWalkOff; Current.NoWalkOff = value; try { Save(); } catch { Current.NoWalkOff = previous; throw; } if(value) ModEntry.EnsureMotion(); });
        internal static readonly Setting<bool> Dash = new Setting<bool>("mega-gameplay-expansion.air-dash", "Air Dash",
            delegate { return Current.AirDash; }, delegate(bool value) { JKRuntime.Gameplay.MapMechanics.RequireUserEnable("mega.air-dash", value); bool previous = Current.AirDash; Current.AirDash = value; try { Save(); } catch { Current.AirDash = previous; throw; } });
    }
    public sealed class WarpJumpOption : SettingToggle { public WarpJumpOption() : base(Settings.Warp, null, () => JKRuntime.Gameplay.MapMechanics.CanConfigure("mega.warp")) { } }
    public sealed class NoWalkOffOption : SettingToggle { public NoWalkOffOption() : base(Settings.EdgeStop, null, () => JKRuntime.Gameplay.MapMechanics.CanConfigure("mega.no-walk-off")) { } }
    public sealed class AirDashOption : SettingToggle { public AirDashOption() : base(Settings.Dash, null, () => JKRuntime.Gameplay.MapMechanics.CanConfigure("mega.air-dash")) { } }

    [RuntimeModule("mega-gameplay-expansion", "Mega Gameplay Expansion")]
    public static class ModEntry
    {
        private static bool factoryInstalled;
        private static WarpController controller;
        private static NoWalkOffController edgeController;
        private static AirDashController dashController;
        private static AirDashSound preparedDashSound;
        internal static JKRuntime.Gameplay.MotionObservationScope MotionScope;
        private static RuntimeScope motionOwner;
        internal static void EnsureMotion()
        {
            if(MotionScope!=null || motionOwner==null) return;
            try
            {
                using(RuntimeApi.MeasureStartup("mega-gameplay.motion-analysis"))
                    MotionScope=motionOwner.Own(JKRuntime.Gameplay.MotionObservation.PrepareLoaded());
                if(edgeController!=null) edgeController.BindMotion();
            }
            catch(Exception error) { WarpDiagnostics.Write("No Walk Off motion observation unavailable: "+error.GetBaseException().Message); }
        }
        [OnWorldReady]
        public static void PrepareWorld(RuntimeScope scope)
        {
            JKRuntime.Presentation.PlayerVisuals.Prepare(scope);
            JKRuntime.Gameplay.PlayerUpdates.Prepare(scope);
            motionOwner=scope;
            scope.Defer(delegate { MotionScope=null; motionOwner=null; });
            scope.Own(RuntimeApi.Geometry.RegisterTerrainPreparation("mega-gameplay-expansion", types => {
                if (types.Contains(typeof(NoWalkOffSurfaceBlock)) || types.Contains(typeof(NoWalkOffZoneBlock))) EnsureMotion();
            }));
            using (RuntimeApi.MeasureStartup("mega-gameplay.air-dash-audio"))
            {
                var sound = scope.Own(new AirDashSound());
                preparedDashSound = sound;
                scope.Defer(delegate { if (ReferenceEquals(preparedDashSound, sound)) preparedDashSound = null; });
            }
        }
        [BeforeAttempt]
        public static void PrepareAttempt(RuntimeScope scope)
        {
            if (preparedDashSound != null) scope.Defer(preparedDashSound.Stop);
            if(Settings.Current.NoWalkOff || JKRuntime.Gameplay.MapMechanics.HasRules("mega.no-walk-off") || MegaBlockFactory.NoWalkOffAuthored) EnsureMotion();
        }
        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            MapActivation.Register();
            DashBindings.Register();
            NativeFlight.Validate();
            MegaBlockFactory.AssertExclusive(null);
            MegaBlockFactory.ResetScreens();
            if (!factoryInstalled) { LevelManager.RegisterBlockFactory(new MegaBlockFactory()); factoryInstalled = true; }
        }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            bool profile = Environment.GetEnvironmentVariable("JK_MGE_PROFILE") == "1";
            if (profile) context.Track(FlightProfile.StartSession());
            context.Track(JKRuntime.Geometry.BlockCatalog.Register("mega-gameplay-expansion", new[] {
                new JKRuntime.Geometry.BlockDeclaration("mega.warp", MapPixels.WarpSurface, JKRuntime.Geometry.BlockVariant.Solid, true),
                new JKRuntime.Geometry.BlockDeclaration("mega.warp", MapPixels.WarpZone, JKRuntime.Geometry.BlockVariant.Zone, false),
                new JKRuntime.Geometry.BlockDeclaration("mega.warp", MapPixels.WarpScreen, JKRuntime.Geometry.BlockVariant.Screen, false),
                new JKRuntime.Geometry.BlockDeclaration("mega.no-walk-off", MapPixels.NoWalkOffSurface, JKRuntime.Geometry.BlockVariant.Solid, true),
                new JKRuntime.Geometry.BlockDeclaration("mega.no-walk-off", MapPixels.NoWalkOffZone, JKRuntime.Geometry.BlockVariant.Zone, false),
                new JKRuntime.Geometry.BlockDeclaration("mega.no-walk-off", MapPixels.NoWalkOffScreen, JKRuntime.Geometry.BlockVariant.Screen, false),
                new JKRuntime.Geometry.BlockDeclaration("mega.air-dash", MapPixels.AirDashSurface, JKRuntime.Geometry.BlockVariant.Solid, true),
                new JKRuntime.Geometry.BlockDeclaration("mega.air-dash", MapPixels.AirDashZone, JKRuntime.Geometry.BlockVariant.Zone, false),
                new JKRuntime.Geometry.BlockDeclaration("mega.air-dash", MapPixels.AirDashScreen, JKRuntime.Geometry.BlockVariant.Screen, false)
            }));
            WarpDiagnostics.Write("Mega Gameplay Expansion " + typeof(ModEntry).Assembly.GetName().Version + "; level started; global Warp Jump=" + Settings.Current.WarpJump
                + "; global No Walk Off=" + Settings.Current.NoWalkOff + "; global Air Dash=" + Settings.Current.AirDash
                + "; flight profile=" + (profile ? "first 12 forecasts" : "disabled"));
            MegaBlockFactory.AssertExclusive(JumpKing.Game1.instance.contentManager.level);
            controller = new WarpController(JumpKing.GameManager.GameLoop.m_player);
            context.Track(controller);
            context.Track(JKRuntime.State.GameState.Snapshots.Register(controller));
            edgeController = new NoWalkOffController(JumpKing.GameManager.GameLoop.m_player);
            context.Track(edgeController);
            context.Track(JKRuntime.State.GameState.Snapshots.Register(edgeController));
            var dashSound = preparedDashSound ?? context.Track(new AirDashSound());
            context.Track(dashSound.BindToAttempt());
            dashController = new AirDashController(JumpKing.GameManager.GameLoop.m_player, dashSound.Play, dashSound.Stop);
            context.Track(dashController);
            context.Track(JKRuntime.State.GameState.Snapshots.Register(dashController));
            context.Track(RuntimeApi.Mechanics.RegisterControlled("mega-gameplay-expansion",
                new JKRuntime.Gameplay.MechanicDefinition("mega.air-dash", new Version(1, 0),
                    JKRuntime.Gameplay.MechanicEffects.Movement | JKRuntime.Gameplay.MechanicEffects.Presentation,
                    JKRuntime.Gameplay.MechanicComposition.Exclusive,"player.air-movement",null,dashController.Id),dashController.Describe));
            context.Track(RuntimeApi.Mechanics.RegisterControlled("mega-gameplay-expansion",
                new JKRuntime.Gameplay.MechanicDefinition("mega.warp", new Version(1, 0),
                    JKRuntime.Gameplay.MechanicEffects.Movement | JKRuntime.Gameplay.MechanicEffects.Presentation,
                    JKRuntime.Gameplay.MechanicComposition.Exclusive, "player.relocation", null, controller.Id), controller.Describe));
            context.Track(RuntimeApi.Mechanics.RegisterControlled("mega-gameplay-expansion",
                new JKRuntime.Gameplay.MechanicDefinition("mega.no-walk-off", new Version(1, 0),
                    JKRuntime.Gameplay.MechanicEffects.Movement, JKRuntime.Gameplay.MechanicComposition.Additive,
                    "player.support", null, edgeController.Id), edgeController.Describe));
        }
        [OnLevelUnload]
        public static void Stop()
        {
            if (dashController != null) dashController.Dispose(); dashController = null;
            if (edgeController != null) edgeController.Dispose(); edgeController = null;
            if (controller != null) controller.Dispose(); controller = null;
        }
        [OnLevelEnd]
        public static void End() { Stop(); }
        [MainMenuItemSetting]
        public static WarpJumpOption MainMenu(object factory, GuiFormat format) { return new WarpJumpOption(); }
        [PauseMenuItemSetting]
        public static WarpJumpOption PauseMenu(object factory, GuiFormat format) { return new WarpJumpOption(); }
        [MainMenuItemSetting]
        public static NoWalkOffOption MainMenuNoWalkOff(object factory, GuiFormat format) { return new NoWalkOffOption(); }
        [PauseMenuItemSetting]
        public static NoWalkOffOption PauseMenuNoWalkOff(object factory, GuiFormat format) { return new NoWalkOffOption(); }
        [MainMenuItemSetting]
        public static AirDashOption MainMenuAirDash(object factory, GuiFormat format) { return new AirDashOption(); }
        [PauseMenuItemSetting]
        public static AirDashOption PauseMenuAirDash(object factory, GuiFormat format) { return new AirDashOption(); }
        [MainMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton MainMenuDashBinding(object factory, GuiFormat format)
        { DashBindings.Register(); return new JumpKing.PauseMenu.BT.TextButton("Binds", JKRuntime.UI.UIApi.CreateMenuPage(factory,
            new JKRuntime.UI.UiBindingsPage("Binds", "Air Dash uses Jump until you assign a custom bind.", "mega-gameplay-expansion.air-dash"))); }
        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton PauseMenuDashBinding(object factory, GuiFormat format)
        { return MainMenuDashBinding(factory, format); }
    }
}
