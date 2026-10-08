using System;
using System.IO;
using System.Xml.Serialization;
using JKRuntime.Settings;

namespace JKRuntime.Inspection
{
    [XmlRoot("Preferences")]
    public sealed class InspectorPreferences
    {
        public GimmickRule[] GimmickRules { get; set; }
        public GimmickSearchPreferences GimmickSearch { get; set; }
        public GimmickSearchPreferences[] SavedSearches { get; set; }
    }

    internal static class InspectorSettings
    {
        private static InspectorPreferences current;
        private static SettingsFile<InspectorPreferences> file;
        internal static InspectorPreferences Current { get {
            if (current == null) Load(Path.Combine(Path.GetDirectoryName(typeof(RuntimeApi).Assembly.Location), "JKRuntime.Inspector.xml"));
            return current;
        } }
        internal static void Load(string path)
        {
            file = new SettingsFile<InspectorPreferences>(path, () => new InspectorPreferences());
            current = file.Value;
            if (!file.CanSave) Gimmicks.Status = file.Error;
        }
        internal static void Save() { var value = Current; file.Save(value); }
        internal static void Edit(Action<InspectorPreferences> edit)
        {
            var old = Current;
            var next = new InspectorPreferences { GimmickRules = old.GimmickRules,
                GimmickSearch = old.GimmickSearch, SavedSearches = old.SavedSearches };
            edit(next);
            file.Save(next); current = next;
        }
    }
}
