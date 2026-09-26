using JKRuntime.Settings;
namespace MorphBallMod
{
    internal static class Options
    {
        internal static readonly Setting<bool> Enabled = new Setting<bool>("morph-ball.enabled", "Enable Ball King",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.EnableMorphBall; }, SettingsStore.SetEnabled, MorphBallInstaller.Apply);
        internal static readonly Setting<bool> Sticky = new Setting<bool>("morph-ball.sticky", "Sticky",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.StickyMode; }, SettingsStore.SetStickyMode);
        internal static readonly Setting<bool> DoubleJump = new Setting<bool>("morph-ball.double-jump", "Double jump",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.EnableDoubleJump; }, SettingsStore.SetDoubleJump);
    }
}
