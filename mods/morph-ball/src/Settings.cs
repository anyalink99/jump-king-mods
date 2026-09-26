using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace MorphBallMod
{
    public enum MorphBinding
    {
        Morph
    }

    [Serializable]
    public sealed class MorphBindingEntry
    {
        public MorphBinding Bind { get; set; }
        public int[] Keys { get; set; }

        public MorphBindingEntry()
        {
            Keys = new int[0];
        }

        internal MorphBindingEntry(
            KeyValuePair<MorphBinding, int[]> binding)
        {
            Bind = binding.Key;
            Keys = binding.Value;
        }
    }

    [Serializable]
    public sealed class MorphBallSettings
    {
        private Dictionary<MorphBinding, int[]> keyBindings;

        public bool EnableMorphBall { get; set; }
        public bool StickyMode { get; set; }
        public bool EnableDoubleJump { get; set; }

        [XmlIgnore]
        public Dictionary<MorphBinding, int[]> KeyBindings
        {
            get { return keyBindings; }
            set { keyBindings = value; }
        }

        public MorphBindingEntry[] Bindings
        {
            get
            {
                return keyBindings.Select(
                    delegate(KeyValuePair<MorphBinding, int[]> pair)
                    {
                        return new MorphBindingEntry(pair);
                    }).ToArray();
            }
            set
            {
                keyBindings = value == null
                    ? CreateDefaultBindings()
                    : value.ToDictionary(
                        delegate(MorphBindingEntry entry) { return entry.Bind; },
                        delegate(MorphBindingEntry entry) { return entry.Keys ?? new int[0]; });
            }
        }

        public MorphBallSettings()
        {
            EnableMorphBall = true;
            StickyMode = true;
            EnableDoubleJump = false;
            keyBindings = CreateDefaultBindings();
        }

        public void ForceUpdate()
        {
            SettingsStore.Save();
        }

        internal static Dictionary<MorphBinding, int[]> CreateDefaultBindings()
        {
            return new Dictionary<MorphBinding, int[]>
            {
                { MorphBinding.Morph, new[] { 77 } }
            };
        }
    }

    internal static class SettingsStore
    {
        private const string FileName = "MorphBall.Settings.xml";
        private static bool loaded;
        private static JKRuntime.Settings.SettingsFile<MorphBallSettings> file;

        internal static MorphBallSettings Current { get; private set; }

        internal static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            file = new JKRuntime.Settings.SettingsFile<MorphBallSettings>(GetPath(), delegate { return new MorphBallSettings(); });
            Current = file.Value;
            EnsureBindings();
        }

        internal static void SetEnabled(bool enabled)
        {
            Commit(delegate(MorphBallSettings value) { value.EnableMorphBall = enabled; });
        }

        internal static void SetStickyMode(bool enabled)
        {
            Commit(delegate(MorphBallSettings value) { value.StickyMode = enabled; });
        }

        internal static void SetDoubleJump(bool enabled)
        {
            Commit(delegate(MorphBallSettings value) { value.EnableDoubleJump = enabled; });
        }

        private static void Commit(Action<MorphBallSettings> change)
        {
            EnsureLoaded();
            var next = new MorphBallSettings { EnableMorphBall = Current.EnableMorphBall, StickyMode = Current.StickyMode,
                EnableDoubleJump = Current.EnableDoubleJump, KeyBindings = Current.KeyBindings.ToDictionary(pair => pair.Key, pair => (int[])pair.Value.Clone()) };
            change(next);
            file.Save(next);
            Current = next;
        }

        internal static void ResetBindings()
        {
            EnsureLoaded();
            Current.KeyBindings = MorphBallSettings.CreateDefaultBindings();
        }

        internal static void Save()
        {
            EnsureLoaded();
            file.Save(Current);
        }

        private static void EnsureBindings()
        {
            Dictionary<MorphBinding, int[]> defaults =
                MorphBallSettings.CreateDefaultBindings();
            foreach (MorphBinding binding in Enum.GetValues(typeof(MorphBinding)))
            {
                if (!Current.KeyBindings.ContainsKey(binding))
                {
                    Current.KeyBindings[binding] = defaults[binding];
                }
            }
        }

        private static string GetPath()
        {
            string directory = JKRuntime.PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly());
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException(
                    "Ball King assembly path is unavailable");
            }
            return Path.Combine(directory, FileName);
        }
    }
}
