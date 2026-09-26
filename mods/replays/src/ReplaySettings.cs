using System;
using System.IO;
using System.Xml.Serialization;

namespace Replays
{
    [Serializable]
    public sealed class ReplaySettings
    {
        public bool RecordingEnabled { get; set; }
        public bool ReplaysInMainMenu { get; set; }
        public bool SaveReplayInPauseMenu { get; set; }
        public string GhostReplayId { get; set; }

        public ReplaySettings()
        {
            RecordingEnabled = true;
            ReplaysInMainMenu = true;
            SaveReplayInPauseMenu = true;
            GhostReplayId = string.Empty;
        }
    }

    internal static class ReplaySettingsStore
    {
        private static readonly string PathValue = ReplayPaths.Settings;
        private static JKRuntime.Settings.SettingsFile<ReplaySettings> file;
        private static bool loaded;

        internal static ReplaySettings Current { get; private set; }

        internal static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            file = new JKRuntime.Settings.SettingsFile<ReplaySettings>(PathValue, delegate { return new ReplaySettings(); });
            Current = file.Value;
        }

        internal static void SetGhost(string id)
        {
            EnsureLoaded();
            var candidate = new ReplaySettings { RecordingEnabled = Current.RecordingEnabled,
                ReplaysInMainMenu = Current.ReplaysInMainMenu, SaveReplayInPauseMenu = Current.SaveReplayInPauseMenu,
                GhostReplayId = id };
            file.Save(candidate); Current = candidate;
        }
        internal static void Save()
        {
            EnsureLoaded();
            file.Save(Current);
        }
    }
}
