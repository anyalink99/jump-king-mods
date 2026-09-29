using JKRuntime.Settings;
namespace SubframeCharge
{
    internal static class Options
    {
        internal static readonly Setting<bool> Optimizations = JKRuntime.NativePerformance.Setting;
        internal static readonly Setting<bool> Inputs = new Setting<bool>("subframe-charge.inputs", "Subframe Inputs",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.SubframeInputs; }, SettingsStore.SetInputs, PerformanceFeatures.Apply);
        internal static readonly Setting<bool> Refresh = new Setting<bool>("subframe-charge.high-refresh", "240 Hz",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.HighRefresh; }, SettingsStore.SetRefresh, PerformanceFeatures.Apply);
        internal static readonly Setting<bool> Enabled = new Setting<bool>("subframe-charge.enabled", "Enabled",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.Enabled; }, SettingsStore.SetEnabled, JKRuntime.Gameplay.JumpSlot.Refresh);
        internal static readonly Setting<bool> QuarterSteps = new Setting<bool>("subframe-charge.quarter-step-charge", "Quarter-step Charge",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.QuarterStepCharge; }, SettingsStore.SetQuarterSteps, JKRuntime.Gameplay.JumpSlot.Refresh);
        internal static readonly Setting<bool> Measurement = new Setting<bool>("subframe-charge.measurement", "Show SFC",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.ShowMeasurement; }, SettingsStore.SetShowMeasurement, JKRuntime.Gameplay.JumpSlot.Refresh);
    }
}
