using System;
using System.IO;
using JKRuntime;
using JKRuntime.Settings;

namespace MegaMappingExpansion
{
    [Serializable]
    public sealed class MappingSettingsData
    {
        public bool Enabled = true;
    }

    internal sealed class MappingSettingsStore
    {
        private readonly SettingsFile<MappingSettingsData> file;
        internal bool Enabled { get; private set; }
        internal MappingSettingsStore(string file)
        {
            this.file = new SettingsFile<MappingSettingsData>(file, delegate { return new MappingSettingsData(); });
            Enabled = this.file.Value.Enabled;
        }
        internal void Set(bool enabled)
        {
            file.Save(new MappingSettingsData { Enabled = enabled });
            Enabled = enabled;
        }
    }

    internal static class MappingSettings
    {
        private static MappingSettingsStore store;
        internal static bool Enabled { get { return store == null || store.Enabled; } }
        internal static readonly Setting<bool> Toggle = new Setting<bool>("mega-mapping-expansion.enabled", "Mega Mapping Expansion",
            delegate { Load(); return Enabled; }, delegate(bool value) { Load(); store.Set(value); });
        internal static void Load()
        {
            if (store == null) store = new MappingSettingsStore(Path.Combine(
                PackageHost.GetDataDirectory(typeof(ModEntry).Assembly), "MegaMappingExpansion.Settings.xml"));
        }
    }
}
