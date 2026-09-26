using JKRuntime.Input;
using JKRuntime.Modules;
using JKRuntime.Gameplay;
using JumpKing.PauseMenu;

namespace SubframeCharge
{
    [RuntimeModule("subframe-charge", "Subframe Charge", Requires = new[] { "player.movement:1:0:optional" })]
    public static class ModEntry
    {
        private static System.IDisposable charge, inputLog;
        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            SettingsStore.EnsureLoaded();
            PerformanceFeatures.Apply();
        }

        // Optional discovery contract for other presentation mods. Only one
        // scheduler owns the native accumulator at any time.
        public static bool OwnsPresentationClock { get { return PerformanceFeatures.InputsRequested; } }
        public static bool HasSubframePresentation { get { return FeatureClock.Active && FeatureClock.HighRefresh; } }
        public static void ConnectPresentationMods() { PerformanceFeatures.FindCameraInterval(); }
        [OnWorldReady] public static void PrepareWorld(JKRuntime.RuntimeScope scope)
        { /* Native caches are owned by JK Runtime. Retained discovery entry point. */ }
        [BeforeAttempt] public static void PrepareAttempt(JKRuntime.RuntimeScope scope)
        {
            using(JKRuntime.RuntimeApi.MeasureStartup("subframe-presentation.prepare-collision")) PredictionCollision.Prepare();
            using(JKRuntime.RuntimeApi.MeasureStartup("subframe-input.prepare-bindings")) ResponsiveInput.PrepareKeyboard();
            scope.Defer(PlayerPresentation.Reset);
        }

        [OnLevelStart]
        public static void OnLevelStart(JKRuntime.ModuleContext context)
        {
            PauseClockObserver.InstallForLevel();
            inputLog = context.Track(InputLog.Subscribe(DiagnosticLog.Write));
            charge = context.Track(JKRuntime.Gameplay.JumpSlot.RegisterChargePolicy(SubframeChargeInstaller.ApplyCurrentMode, SubframeChargeInstaller.DetachChargePolicy));
            context.Track(JKRuntime.RuntimeApi.Mechanics.Register("subframe-charge",
                new MechanicDefinition("subframe-charge.timing", new System.Version(1, 0), MechanicEffects.Input | MechanicEffects.Charge), delegate {
                    bool enabled = SettingsStore.Current.Enabled;
                    bool available = !GameFeatures.IsMorphed && GameFeatures.Movement != MovementMode.VariableJump;
                    return new MechanicState(enabled, available, enabled && available, MechanicSource.Setting,
                        !available ? "Alternative controller owns jumping" : enabled ? (SettingsStore.Current.QuarterStepCharge
                            ? "Quarter-frame direct/buffered holds; native eligibility, release/max ticks and surface scaling"
                            : "Physical timing policy; buffered jumps remain native; device evidence checked per charge")
                            : SettingsStore.Current.ShowMeasurement ? "Observation only" : "Disabled; native jump node retained");
                }));
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            PlayerPresentation.Reset(); ResponsiveInput.ClearPending();
            if (charge != null) { charge.Dispose(); charge = null; }
            SubframeChargeInstaller.Uninstall();
            if (inputLog != null) { inputLog.Dispose(); inputLog = null; }
            PauseClockObserver.Clear();
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            OnLevelUnload();
        }

        [MainMenuItemSetting]
        public static EnabledOption MainEnabledOption(
            object factory,
            GuiFormat format)
        {
            return new EnabledOption();
        }

        [PauseMenuItemSetting]
        public static EnabledOption PauseEnabledOption(
            object factory,
            GuiFormat format)
        {
            return new EnabledOption();
        }

        [MainMenuItemSetting] public static JKRuntime.UI.SettingToggle MainQuarterSteps(object factory, GuiFormat format)
        { return new JKRuntime.UI.SettingToggle(Options.QuarterSteps, null, delegate { return Options.Enabled.Value; }); }
        [PauseMenuItemSetting] public static JKRuntime.UI.SettingToggle PauseQuarterSteps(object factory, GuiFormat format) { return MainQuarterSteps(factory, format); }

        [MainMenuItemSetting]
        public static ShowMeasurementOption MainMeasurementOption(object factory, GuiFormat format)
        {
            return new ShowMeasurementOption();
        }

        [PauseMenuItemSetting]
        public static ShowMeasurementOption PauseMeasurementOption(object factory, GuiFormat format)
        {
            return new ShowMeasurementOption();
        }

        [MainMenuItemSetting] public static JKRuntime.UI.SettingToggle MainInputs(object factory, GuiFormat format) { return new JKRuntime.UI.SettingToggle(Options.Inputs); }
        [PauseMenuItemSetting] public static JKRuntime.UI.SettingToggle PauseInputs(object factory, GuiFormat format) { return MainInputs(factory, format); }
        [MainMenuItemSetting] public static JKRuntime.UI.SettingToggle MainRefresh(object factory, GuiFormat format)
        { return new JKRuntime.UI.SettingToggle(Options.Refresh, null, delegate { return Options.Inputs.Value; }); }
        [PauseMenuItemSetting] public static JKRuntime.UI.SettingToggle PauseRefresh(object factory, GuiFormat format) { return MainRefresh(factory, format); }
        public static JKRuntime.UI.SettingToggle MainOptimizations(object factory, GuiFormat format) { return new JKRuntime.UI.SettingToggle(Options.Optimizations); }
        public static JKRuntime.UI.SettingToggle PauseOptimizations(object factory, GuiFormat format) { return MainOptimizations(factory, format); }

    }
}
