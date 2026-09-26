using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace MoreItems
{
    [Serializable]
    public sealed class ItemChordSetting
    {
        [XmlElement("Key")]
        public int[] Keys { get; set; }

        public ItemChordSetting() { Keys = new int[0]; }
        internal ItemChordSetting(JKRuntime.UI.UiChord chord)
        {
            Keys = chord.Buttons;
        }
    }

    [Serializable]
    public sealed class ItemBindingSetting
    {
        public string Id { get; set; }
        [XmlArray("Chords"), XmlArrayItem("Chord")]
        public ItemChordSetting[] Chords { get; set; }

        public ItemBindingSetting()
        {
            Chords = new ItemChordSetting[0];
        }

        internal ItemBindingSetting(
            KeyValuePair<string, JKRuntime.UI.UiChord[]> binding)
        {
            Id = binding.Key;
            Chords = (binding.Value ?? new JKRuntime.UI.UiChord[0])
                .Where(chord => chord != null && !chord.IsEmpty)
                .Take(2)
                .Select(chord => new ItemChordSetting(chord))
                .ToArray();
        }
    }

    [Serializable]
    public sealed class MoreItemsSettings
    {
        private Dictionary<string, JKRuntime.UI.UiChord[]> keyBindings;

        public bool JetpackEquipped { get; set; }
        public bool HammerEquipped { get; set; }
        public bool EnableHammer { get; set; }
        public int HammerStrength { get; set; }
        public float HammerSensitivity { get; set; }
        internal MoreItemsSettings Copy() { return (MoreItemsSettings)MemberwiseClone(); }
        public bool EnableRewinders { get; set; }
        public bool EnableJetpack { get; set; }
        public bool ShowJetpack { get; set; }
        public bool ShowJetpackTrail { get; set; }
        public bool RenderRewind { get; set; }
        public float JetpackVolume { get; set; }

        [XmlIgnore]
        public Dictionary<string, JKRuntime.UI.UiChord[]> KeyBindings
        {
            get { return keyBindings; }
            set
            {
                keyBindings = value
                    ?? new Dictionary<string, JKRuntime.UI.UiChord[]>(
                        StringComparer.OrdinalIgnoreCase);
            }
        }

        [XmlArray("Bindings"), XmlArrayItem("Binding")]
        public ItemBindingSetting[] Bindings
        {
            get
            {
                return keyBindings
                    .Select(pair => new ItemBindingSetting(pair))
                    .ToArray();
            }
            set
            {
                keyBindings = new Dictionary<string, JKRuntime.UI.UiChord[]>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (ItemBindingSetting entry
                    in value ?? new ItemBindingSetting[0])
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                        continue;
                    keyBindings[entry.Id] = (entry.Chords
                        ?? new ItemChordSetting[0])
                        .Where(chord => chord != null)
                        .Take(2)
                        .Select(chord => CreateChord(chord.Keys))
                        .Where(chord => !chord.IsEmpty)
                        .ToArray();
                }
            }
        }

        public MoreItemsSettings()
        {
            JetpackEquipped = false;
            EnableHammer = true;
            HammerStrength = 100;
            HammerSensitivity = 1f;
            EnableRewinders = true;
            EnableJetpack = true;
            ShowJetpack = true;
            ShowJetpackTrail = true;
            RenderRewind = true;
            JetpackVolume = 0.7f;
            keyBindings = new Dictionary<string, JKRuntime.UI.UiChord[]>(
                StringComparer.OrdinalIgnoreCase);
        }

        private static JKRuntime.UI.UiChord CreateChord(int[] values)
        {
            List<int> buttons = new List<int>();
            foreach (int value in values ?? new int[0])
            {
                if (value < 0 || buttons.Contains(value)) continue;
                buttons.Add(value);
                if (buttons.Count == 2) break;
            }
            return new JKRuntime.UI.UiChord(buttons.ToArray());
        }
    }

    internal static class SettingsStore
    {
        private const string FileName = "MoreItems.Settings.xml";
        private static bool loaded;
        private static JKRuntime.Settings.SettingsFile<MoreItemsSettings> file;

        internal static MoreItemsSettings Current { get; private set; }

        internal static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            file = new JKRuntime.Settings.SettingsFile<MoreItemsSettings>(GetPath(FileName), delegate { return new MoreItemsSettings(); });
            Current = file.Value;
            if (float.IsNaN(Current.JetpackVolume)
                || float.IsInfinity(Current.JetpackVolume))
            {
                Current.JetpackVolume = 0.7f;
            }
            Current.JetpackVolume = Math.Max(
                0f,
                Math.Min(1f, Current.JetpackVolume));
            Current.HammerStrength = HammerKing.Settings.NormalizeStrength(Current.HammerStrength);
            if (float.IsNaN(Current.HammerSensitivity) || float.IsInfinity(Current.HammerSensitivity)) Current.HammerSensitivity = 1f;
            Current.HammerSensitivity = Math.Max(.1f, Math.Min(4f, Current.HammerSensitivity));
        }

        private static void CommitSettings(Action<MoreItemsSettings> change)
        {
            EnsureLoaded();
            MoreItemsSettings next = Current.Copy(); change(next);
            file.Save(next);
            Current = next;
        }
        internal static void SetHammerEquipped(bool equipped) { CommitSettings(next => next.HammerEquipped = equipped); }
        internal static void SetHammerEnabled(bool enabled) { CommitSettings(next => next.EnableHammer = enabled); }
        internal static void SetHammerStrength(int strength) { CommitSettings(next => next.HammerStrength = strength); }

        internal static void SetJetpackEquipped(bool equipped)
        {
            CommitSettings(next => next.JetpackEquipped = equipped);
        }

        internal static void SetRewindersEnabled(bool enabled)
        {
            CommitSettings(next => next.EnableRewinders = enabled);
        }

        internal static void SetJetpackEnabled(bool enabled)
        {
            CommitSettings(next => next.EnableJetpack = enabled);
        }

        internal static void SetShowJetpack(bool show)
        {
            CommitSettings(next => next.ShowJetpack = show);
        }

        internal static void SetShowJetpackTrail(bool show)
        {
            CommitSettings(next => next.ShowJetpackTrail = show);
        }

        internal static void SetRenderRewind(bool render)
        {
            CommitSettings(next => next.RenderRewind = render);
        }

        internal static void SetJetpackVolume(float volume)
        {
            CommitSettings(next => next.JetpackVolume = Math.Max(0f, Math.Min(1f, volume)));
        }

        internal static void EnsureBinding(
            string id,
            JKRuntime.UI.UiChord[] defaults)
        {
            EnsureLoaded();
            if (Current.KeyBindings.ContainsKey(id)) return;
            Current.KeyBindings[id] = CloneChords(defaults);
            // Defaults keep discovery usable during recovery; explicit edits
            // still report the read-only state through Save.
            if (file.CanSave) Save();
        }

        internal static void ResetBinding(string id)
        {
            ConsumableDefinition definition;
            if (!MoreItemsApi.TryGet(id, out definition)
                || definition.Hotkey == null) return;
            Current.KeyBindings[id] = CloneChords(
                definition.Hotkey.DefaultChords);
        }

        internal static void Save()
        {
            EnsureLoaded();
            file.Save(Current);
        }

        private static JKRuntime.UI.UiChord[] CloneChords(
            JKRuntime.UI.UiChord[] chords)
        {
            return (chords ?? new JKRuntime.UI.UiChord[0])
                .Where(chord => chord != null && !chord.IsEmpty)
                .Take(2)
                .Select(chord => new JKRuntime.UI.UiChord(chord.Buttons))
                .ToArray();
        }

        private static string GetPath(string fileName)
        {
            string directory = JKRuntime.PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly());
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException(
                    "More Items assembly path is unavailable");
            }
            return Path.Combine(directory, fileName);
        }
    }
}
