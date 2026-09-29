using JKRuntime.Settings;
using JKRuntime.UI;

namespace JKRuntime.Compatibility
{
    // optional user-facing adapters. required runtime crash guards keep their
    // own contracts (for example conveyor protection before node replacement)
    internal static class ModCompatibility
    {
        internal static readonly Setting<bool> Setting = new Setting<bool>(
            "jk-runtime.mod-compatibility", "Mod compatibility fixes",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.ModCompatibilityFixes; }, Save);

        private static void Save(bool value)
        {
            SettingsStore.EnsureLoaded();
            bool previous = SettingsStore.Current.ModCompatibilityFixes;
            SettingsStore.Current.ModCompatibilityFixes = value;
            try { SettingsStore.Save(); }
            catch { SettingsStore.Current.ModCompatibilityFixes = previous; throw; }
            JumpKingManagerCompatibility.Apply();
        }
    }
}
