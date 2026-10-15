using JKRuntime.Settings;
namespace SubframeCharge
{
    internal static class Options
    {
        internal static readonly Setting<bool> Optimizations = JKRuntime.NativePerformance.Setting;
        internal static readonly Setting<bool> Inputs = new Setting<bool>("subframe-charge.inputs", "Subframe Inputs",
            delegate { SettingsStore.EnsureLoaded(); return !PerformanceSuspension.Active && SettingsStore.Current.SubframeInputs; }, SettingsStore.SetInputs, PerformanceFeatures.Apply);
        internal static readonly Setting<bool> Refresh = new Setting<bool>("subframe-charge.high-refresh", "240 Hz",
            delegate { SettingsStore.EnsureLoaded(); return !PerformanceSuspension.Active && SettingsStore.Current.HighRefresh; }, SettingsStore.SetRefresh, PerformanceFeatures.Apply);
        internal static readonly Setting<bool> Enabled = new Setting<bool>("subframe-charge.enabled", "Enabled",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.Enabled; }, SettingsStore.SetEnabled, JKRuntime.Gameplay.JumpSlot.Refresh);
        // keep the saved key and setting id so existing preferences and pins survive
        internal static readonly Setting<bool> QuarterSteps = new Setting<bool>("subframe-charge.quarter-step-charge", "Enable Substep Charge",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.QuarterStepCharge; }, SettingsStore.SetQuarterSteps, JKRuntime.Gameplay.JumpSlot.Refresh);
        internal static readonly Setting<SubstepMode> ChargeStep = new Setting<SubstepMode>("subframe-charge.charge-step", "Charge Step",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.ChargeStep; }, SettingsStore.SetChargeStep,
            JKRuntime.Gameplay.JumpSlot.Refresh, Substep.Valid);
        internal static readonly Setting<bool> Measurement = new Setting<bool>("subframe-charge.measurement", "Show SFC",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.ShowMeasurement; }, SettingsStore.SetShowMeasurement, JKRuntime.Gameplay.JumpSlot.Refresh);
    }
}
