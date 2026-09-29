using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace JKRuntime.UI
{
    [Serializable]
    public sealed class UiChordSettingsEntry
    {
        public string Id { get; set; }
        [XmlArrayItem("Chord")]
        public int[][] Chords { get; set; }

        public UiChordSettingsEntry()
        {
            Chords = new int[0][];
        }
    }

    [Serializable]
    public sealed class UIApiSettings
    {
        private bool useCompactWorkshopGrids = true;
        public bool DiagnosticMode { get; set; }
        public bool ModCompatibilityFixes { get; set; }
        public bool UseCompactInventory { get; set; }
        public int[][] InteractChords { get; set; }
        public bool UseCompactWorkshopGrids
        {
            get { return useCompactWorkshopGrids; }
            set
            {
                useCompactWorkshopGrids = value;
            }
        }
        [XmlArrayItem("Setting")]
        public string[] PinnedSettings { get; set; }
        [XmlArrayItem("Setting")]
        public string[] BindableSettings { get; set; }
        public UiChordSettingsEntry[] BindingChords { get; set; }

        public UIApiSettings()
        {
            ModCompatibilityFixes = true;
            UseCompactInventory = true;
            InteractChords = new[] { new[] { 69 } };
            PinnedSettings = new string[0];
            BindableSettings = new string[0];
            BindingChords = new UiChordSettingsEntry[0];
        }

    }

    internal static class SettingsStore
    {
        private const string FileName = "JKRuntime.Settings.xml";
        private static bool loaded;
        private static JKRuntime.Settings.SettingsFile<UIApiSettings> file;
        internal static UIApiSettings Current { get; private set; }

        internal static void EnsureLoaded()
        {
            if (loaded) return;
            JKRuntime.Settings.DataMigration.MigrateUi(Path.GetDirectoryName(GetPath()));
            loaded = true;
            file = new JKRuntime.Settings.SettingsFile<UIApiSettings>(GetPath(), delegate { return new UIApiSettings(); });
            Current = file.Value;
            Current.InteractChords = NormalizeChords(Current.InteractChords);
            Current.BindingChords = NormalizeBindingEntries(Current.BindingChords);
            Current.PinnedSettings = NormalizeIds(Current.PinnedSettings);
            Current.BindableSettings = NormalizeIds(Current.BindableSettings);
        }

        internal static void Save()
        {
            EnsureLoaded();
            file.Save(Current);
        }

        internal static UiChord[] GetInteractChords()
        {
            EnsureLoaded();
            return ToUiChords(Current.InteractChords);
        }

        internal static void SetInteractChords(UiChord[] chords)
        {
            EnsureLoaded();
            int[][] previous = Current.InteractChords;
            Current.InteractChords = ToChordArrays(chords);
            try { Save(); }
            catch { Current.InteractChords = previous; throw; }
        }

        internal static void ResetInteractChords()
        {
            SetInteractChords(new[] { new UiChord(69) });
        }

        internal static UiChord[] GetBindingChords(string id, UiChord[] defaults)
        {
            EnsureLoaded();
            foreach (UiChordSettingsEntry entry in Current.BindingChords)
                if (string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                    return ToUiChords(entry.Chords);
            return CloneChords(defaults);
        }

        internal static void SetBindingChords(string id, UiChord[] chords)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Binding id is required", "id");
            EnsureLoaded();
            UiChordSettingsEntry[] previous = Current.BindingChords;
            List<UiChordSettingsEntry> entries = new List<UiChordSettingsEntry>(Current.BindingChords);
            UiChordSettingsEntry target = null;
            foreach (UiChordSettingsEntry entry in entries)
                if (string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase)) target = entry;
            if (target == null)
            {
                target = new UiChordSettingsEntry { Id = id };
                entries.Add(target);
            }
            else
            {
                int index = entries.IndexOf(target);
                target = new UiChordSettingsEntry { Id = target.Id };
                entries[index] = target;
            }
            target.Chords = ToChordArrays(chords);
            Current.BindingChords = entries.ToArray();
            try { Save(); }
            catch { Current.BindingChords = previous; throw; }
        }

        internal static bool IsSettingPinned(string id)
        {
            EnsureLoaded();
            return Array.Exists(
                Current.PinnedSettings,
                delegate(string value)
                {
                    return string.Equals(value, id, StringComparison.OrdinalIgnoreCase);
                });
        }

        internal static void SetSettingPinned(string id, bool pinned)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Setting id is required", "id");
            EnsureLoaded();
            List<string> values = new List<string>(Current.PinnedSettings);
            values.RemoveAll(
                delegate(string value)
                {
                    return string.Equals(value, id, StringComparison.OrdinalIgnoreCase);
                });
            if (pinned) values.Add(id);
            string[] previous = Current.PinnedSettings;
            Current.PinnedSettings = NormalizeIds(values.ToArray());
            try { Save(); }
            catch { Current.PinnedSettings = previous; throw; }
        }

        internal static bool IsSettingBindable(string id)
        {
            EnsureLoaded();
            return Array.Exists(
                Current.BindableSettings,
                delegate(string value)
                {
                    return string.Equals(value, id, StringComparison.OrdinalIgnoreCase);
                });
        }

        internal static void SetSettingBindable(string id, bool bindable)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Setting id is required", "id");
            EnsureLoaded();
            List<string> values = new List<string>(Current.BindableSettings);
            values.RemoveAll(
                delegate(string value)
                {
                    return string.Equals(value, id, StringComparison.OrdinalIgnoreCase);
                });
            if (bindable) values.Add(id);
            string[] previous = Current.BindableSettings;
            Current.BindableSettings = NormalizeIds(values.ToArray());
            try { Save(); }
            catch { Current.BindableSettings = previous; throw; }
        }

        private static int[][] NormalizeChords(int[][] chords)
        {
            return ToChordArrays(ToUiChords(chords));
        }

        private static UiChord[] ToUiChords(int[][] chords)
        {
            List<UiChord> result = new List<UiChord>();
            foreach (int[] chord in chords ?? new int[0][])
            {
                UiChord value = CreateChord(chord);
                if (!value.IsEmpty) result.Add(value);
                if (result.Count == 2) break;
            }
            return result.ToArray();
        }

        private static UiChord[] CloneChords(UiChord[] chords)
        {
            List<UiChord> result = new List<UiChord>();
            foreach (UiChord chord in chords ?? new UiChord[0])
            {
                if (chord == null || chord.IsEmpty) continue;
                result.Add(new UiChord(chord.Buttons));
                if (result.Count == 2) break;
            }
            return result.ToArray();
        }

        private static UiChord CreateChord(int[] values)
        {
            List<int> buttons = new List<int>();
            foreach (int value in values ?? new int[0])
            {
                if (value < 0 || buttons.Contains(value)) continue;
                buttons.Add(value);
                if (buttons.Count == 2) break;
            }
            return new UiChord(buttons.ToArray());
        }

        private static int[][] ToChordArrays(UiChord[] chords)
        {
            List<int[]> result = new List<int[]>();
            foreach (UiChord chord in chords ?? new UiChord[0])
            {
                if (chord == null || chord.IsEmpty) continue;
                result.Add(chord.Buttons);
                if (result.Count == 2) break;
            }
            return result.ToArray();
        }

        private static UiChordSettingsEntry[] NormalizeBindingEntries(UiChordSettingsEntry[] entries)
        {
            List<UiChordSettingsEntry> result = new List<UiChordSettingsEntry>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UiChordSettingsEntry entry in entries ?? new UiChordSettingsEntry[0])
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id)) continue;
                result.Add(new UiChordSettingsEntry
                {
                    Id = entry.Id,
                    Chords = ToChordArrays(ToUiChords(entry.Chords))
                });
            }
            return result.ToArray();
        }

        private static string[] NormalizeIds(string[] values)
        {
            List<string> result = new List<string>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values ?? new string[0])
            {
                if (string.IsNullOrWhiteSpace(value) || !ids.Add(value)) continue;
                result.Add(value);
            }
            return result.ToArray();
        }

        private static string GetPath()
        {
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("UIApi+ assembly path is unavailable");
            return Path.Combine(directory, FileName);
        }
    }
}
