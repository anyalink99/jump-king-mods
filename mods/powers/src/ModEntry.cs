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

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace Powers
{
    public sealed class Preferences
    {
        public bool WarpJump { get; set; }
        public bool AirDash { get; set; }
        public DashDeviceBinding[] DashBindings { get; set; }


    }
    internal static class Settings
    {
        private static Preferences current;
        private static SettingsFile<Preferences> file;
        private static string PathName { get { return Path.Combine(PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly()), "Powers.Settings.xml"); } }
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
            var candidate = new Preferences { WarpJump=old.WarpJump, AirDash=old.AirDash,
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
        internal static readonly Setting<bool> Warp = new Setting<bool>("powers.warp-jump", "Warp Jump",
            delegate { return Current.WarpJump; }, delegate(bool value) { JKRuntime.Gameplay.MapMechanics.RequireUserEnable("powers.warp", value); bool previous = Current.WarpJump; Current.WarpJump = value; try { Save(); } catch { Current.WarpJump = previous; throw; } });
        internal static readonly Setting<bool> Dash = new Setting<bool>("powers.air-dash", "Air Dash",
            delegate { return Current.AirDash; }, delegate(bool value) { JKRuntime.Gameplay.MapMechanics.RequireUserEnable("powers.air-dash", value); bool previous = Current.AirDash; Current.AirDash = value; try { Save(); } catch { Current.AirDash = previous; throw; } });
    }
    public sealed class WarpJumpOption : SettingToggle { public WarpJumpOption() : base(Settings.Warp, null, () => JKRuntime.Gameplay.MapMechanics.CanConfigure("powers.warp")) { } }
    public sealed class AirDashOption : SettingToggle { public AirDashOption() : base(Settings.Dash, null, () => JKRuntime.Gameplay.MapMechanics.CanConfigure("powers.air-dash")) { } }

    [RuntimeModule("powers", "Powers")]
    public static class ModEntry
    {
        private static bool factoryInstalled;
        private static WarpController controller;
        private static AirDashController dashController;
        private static AirDashSound preparedDashSound;
        [OnWorldReady]
        public static void PrepareWorld(RuntimeScope scope)
        {
            JKRuntime.Presentation.PlayerVisuals.Prepare(scope);
            JKRuntime.Gameplay.PlayerUpdates.Prepare(scope);
            using (RuntimeApi.MeasureStartup("powers.air-dash-audio"))
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
        }
        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            MapActivation.Register();
            DashBindings.Register();
            NativeFlight.Validate();
            BlockFactory.AssertExclusive(null);
            BlockFactory.ResetScreens();
            if (!factoryInstalled) { LevelManager.RegisterBlockFactory(new BlockFactory()); factoryInstalled = true; }
        }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            context.Track(JKRuntime.Presentation.ActorVisuals.Register("powers.dash",1,JKRuntime.Presentation.VisualPhase.Behind,
                ActorVisualState.CaptureDash,()=>new ActorVisualState.Dash()));
            context.Track(JKRuntime.Presentation.ActorVisuals.Register("powers.warp",1,JKRuntime.Presentation.VisualPhase.Replacement,
                ActorVisualState.CaptureWarp,()=>new ActorVisualState.Warp()));
            bool profile = Environment.GetEnvironmentVariable("JK_POWERS_PROFILE") == "1";
            if (profile) context.Track(FlightProfile.StartSession());
            context.Track(JKRuntime.Geometry.BlockCatalog.Register("powers", new[] {
                new JKRuntime.Geometry.BlockDeclaration("powers.warp", MapPixels.WarpSurface, JKRuntime.Geometry.BlockVariant.Solid, true),
                new JKRuntime.Geometry.BlockDeclaration("powers.warp", MapPixels.WarpZone, JKRuntime.Geometry.BlockVariant.Zone, false),
                new JKRuntime.Geometry.BlockDeclaration("powers.warp", MapPixels.WarpScreen, JKRuntime.Geometry.BlockVariant.Screen, false),
                new JKRuntime.Geometry.BlockDeclaration("powers.air-dash", MapPixels.AirDashSurface, JKRuntime.Geometry.BlockVariant.Solid, true),
                new JKRuntime.Geometry.BlockDeclaration("powers.air-dash", MapPixels.AirDashZone, JKRuntime.Geometry.BlockVariant.Zone, false),
                new JKRuntime.Geometry.BlockDeclaration("powers.air-dash", MapPixels.AirDashScreen, JKRuntime.Geometry.BlockVariant.Screen, false)
            }));
            WarpDiagnostics.Write("Powers " + typeof(ModEntry).Assembly.GetName().Version + "; level started; global Warp Jump=" + Settings.Current.WarpJump
                + "; global Air Dash=" + Settings.Current.AirDash + "; flight profile=" + (profile ? "first 12 forecasts" : "disabled"));
            BlockFactory.AssertExclusive(JumpKing.Game1.instance.contentManager.level);
            controller = new WarpController(JumpKing.GameManager.GameLoop.m_player);
            context.Track(controller);
            context.Track(JKRuntime.State.GameState.Snapshots.Register(controller));
            var dashSound = preparedDashSound ?? context.Track(new AirDashSound());
            context.Track(dashSound.BindToAttempt());
            dashController = new AirDashController(JumpKing.GameManager.GameLoop.m_player, dashSound.Play, dashSound.Stop);
            context.Track(dashController);
            context.Track(JKRuntime.State.GameState.Snapshots.Register(dashController));
            context.Track(RuntimeApi.Mechanics.RegisterControlled("powers",
                new JKRuntime.Gameplay.MechanicDefinition("powers.air-dash", new Version(1, 0),
                    JKRuntime.Gameplay.MechanicEffects.Movement | JKRuntime.Gameplay.MechanicEffects.Presentation,
                    JKRuntime.Gameplay.MechanicComposition.Exclusive,"player.air-movement",null,dashController.Id),dashController.Describe));
            context.Track(RuntimeApi.Mechanics.RegisterControlled("powers",
                new JKRuntime.Gameplay.MechanicDefinition("powers.warp", new Version(1, 0),
                    JKRuntime.Gameplay.MechanicEffects.Movement | JKRuntime.Gameplay.MechanicEffects.Presentation,
                    JKRuntime.Gameplay.MechanicComposition.Exclusive, "player.relocation", null, controller.Id), controller.Describe));

        }
        [OnLevelUnload]
        public static void Stop()
        {
            if (dashController != null) dashController.Dispose(); dashController = null;
            if (controller != null) controller.Dispose(); controller = null;
        }
        [OnLevelEnd]
        public static void End() { Stop(); }
        [MainMenuItemSetting]
        public static WarpJumpOption MainMenu(object factory, GuiFormat format) { return new WarpJumpOption(); }
        [PauseMenuItemSetting]
        public static WarpJumpOption PauseMenu(object factory, GuiFormat format) { return new WarpJumpOption(); }
        [MainMenuItemSetting]
        public static AirDashOption MainMenuAirDash(object factory, GuiFormat format) { return new AirDashOption(); }
        [PauseMenuItemSetting]
        public static AirDashOption PauseMenuAirDash(object factory, GuiFormat format) { return new AirDashOption(); }
        [MainMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton MainMenuDashBinding(object factory, GuiFormat format)
        { DashBindings.Register(); return new JumpKing.PauseMenu.BT.TextButton("Binds", JKRuntime.UI.UIApi.CreateMenuPage(factory,
            new JKRuntime.UI.UiBindingsPage("Binds", "Air Dash uses Jump until you assign a custom bind.", "powers.air-dash"))); }
        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton PauseMenuDashBinding(object factory, GuiFormat format)
        { return MainMenuDashBinding(factory, format); }
    }
}
