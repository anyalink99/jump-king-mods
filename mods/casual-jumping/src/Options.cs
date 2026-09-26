using JKRuntime.Settings;
namespace CasualJumping
{
    internal static class Options
    {
        internal static readonly Setting<ControlMode> Controls = new Setting<ControlMode>("casual-jumping.controls", "Controls",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.Mode; }, SettingsStore.SetMode,
            CasualControllerInstaller.ApplyCurrentMode, delegate(ControlMode value) { return System.Enum.IsDefined(typeof(ControlMode), value); });
    }
}
