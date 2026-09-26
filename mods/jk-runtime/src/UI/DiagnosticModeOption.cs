using JKRuntime.Settings;

namespace JKRuntime.UI
{
    internal static class DiagnosticModeOption
    {
        internal static readonly Setting<bool> Setting = new Setting<bool>("jk-runtime.diagnostics", "Diagnostic mode",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.DiagnosticMode; }, Save);
        private static void Save(bool value)
        {
            SettingsStore.EnsureLoaded();
            bool previous = SettingsStore.Current.DiagnosticMode;
            SettingsStore.Current.DiagnosticMode = value;
            try { SettingsStore.Save(); Apply(); }
            catch { SettingsStore.Current.DiagnosticMode = previous; SettingsStore.Save(); Apply(); throw; }
        }
        internal static void Apply()
        {
            bool enabled = SettingsStore.Current.DiagnosticMode;
            try
            {
                PerformanceDiagnostics.Configure(enabled);
                StartupTrace.SetDiagnosticMode(enabled);
            }
            catch (System.Exception error)
            {
                // Optional measurement must not prevent a run from starting.
                // Preserve the requested setting and expose unavailable coverage.
                try { PerformanceDiagnostics.Configure(false); }
                catch (System.Exception cleanup) { RuntimeJournal.Record("jk-runtime", "diagnostic-cleanup", cleanup.ToString()); }
                PerformanceDiagnostics.Status = "Unavailable: " + error.GetBaseException().Message;
                RuntimeJournal.Record("jk-runtime", "diagnostic-mode", error.ToString());
            }
        }
    }
}
