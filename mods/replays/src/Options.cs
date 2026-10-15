using JKRuntime.Settings;
namespace Replays
{
    internal static class Options
    {
        internal static readonly Setting<bool> Recording = new Setting<bool>("replays.recording", "Record new runs",
            delegate { ReplaySettingsStore.EnsureLoaded(); return ReplaySettingsStore.Current.RecordingEnabled; },
            delegate(bool value) { ReplaySettingsStore.EnsureLoaded(); ReplaySettingsStore.Current.RecordingEnabled = value; ReplaySettingsStore.Save(); }, ReplayRuntime.ApplyRecordingSetting);
        internal static readonly Setting<bool> MainMenu = new Setting<bool>("replays.main-menu", "Replays in main menu",
            delegate { ReplaySettingsStore.EnsureLoaded(); return ReplaySettingsStore.Current.ReplaysInMainMenu; },
            delegate(bool value) { ReplaySettingsStore.EnsureLoaded(); ReplaySettingsStore.Current.ReplaysInMainMenu = value; ReplaySettingsStore.Save(); }, ReplayUI.SyncMenus);
        internal static readonly Setting<bool> SaveMenu = new Setting<bool>("replays.save-menu", "Save replay in pause menu",
            delegate { ReplaySettingsStore.EnsureLoaded(); return ReplaySettingsStore.Current.SaveReplayInPauseMenu; },
            delegate(bool value) { ReplaySettingsStore.EnsureLoaded(); ReplaySettingsStore.Current.SaveReplayInPauseMenu = value; ReplaySettingsStore.Save(); }, ReplayUI.SyncMenus);
    }
}
