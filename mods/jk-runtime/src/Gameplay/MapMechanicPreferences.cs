using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JKRuntime.Settings;

namespace JKRuntime.Gameplay
{
    // keep this separate from run attribution: authored use can count here too
    internal sealed class MapMechanicPreferences
    {
        private readonly SettingsFile<List<string>> file;
        private readonly Dictionary<string, bool> entries = new Dictionary<string, bool>(StringComparer.Ordinal);
        private bool dirty, failed;

        internal MapMechanicPreferences(string path)
        {
            file = new SettingsFile<List<string>>(path, () => new List<string>(), Validate);
            foreach (string entry in file.Value)
            {
                int colon = entry.IndexOf(':');
                entries.Add(entry.Substring(colon + 1), entry.StartsWith("protected:", StringComparison.Ordinal));
            }
        }
        private static void Validate(List<string> values)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                if (value == null || !(value.StartsWith("protected:", StringComparison.Ordinal) || value.StartsWith("map-only:", StringComparison.Ordinal)))
                    throw new InvalidDataException("Unknown map mechanic history entry");
                string id = value.Substring(value.IndexOf(':') + 1);
                ModuleDefinition.ValidId(id);
                if (!ids.Add(id)) throw new InvalidDataException("Duplicate map mechanic history: " + id);
            }
        }
        internal void Initialize(string id, bool preserveExisting)
        {
            ModuleDefinition.ValidId(id);
            if (entries.ContainsKey(id)) return;
            entries.Add(id, preserveExisting); dirty = true;
        }
        internal bool Protected(string id)
        {
            bool value;
            // unknown or damaged history isn't evidence that a preference is disposable
            return failed || !file.CanSave || !entries.TryGetValue(id, out value) || value;
        }
        internal void Protect(string id)
        {
            ModuleDefinition.ValidId(id);
            bool value;
            if (entries.TryGetValue(id, out value) && value) return;
            entries[id] = true; dirty = true;
        }
        internal void Flush()
        {
            if (!dirty || failed || !file.CanSave) return;
            try
            {
                file.Save(entries.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => (p.Value ? "protected:" : "map-only:") + p.Key).ToList());
                dirty = false;
            }
            catch (Exception error)
            {
                failed = true;
                Console.WriteLine("[JK Runtime] Map mechanic preferences preserved; history could not be saved: " + error.Message);
            }
        }
    }

    public static partial class MapMechanics
    {
        private static MapMechanicPreferences preferences;
        private static bool entryActive;
        private static MapMechanicPreferences Preferences
        {
            get
            {
                return preferences ?? (preferences = new MapMechanicPreferences(Path.Combine(
                    PackageHost.GetDataDirectory(typeof(MapMechanics).Assembly), "JKRuntime.MapMechanics.xml")));
            }
        }

        /// <summary>Call once after reading the provider's settings, before any defaults are saved. Preparation only stages history</summary>
        public static void InitializePreference(string id, bool preserveExisting)
        { RuntimeApi.Kernel.CheckThread(); Preferences.Initialize(id, preserveExisting); }

        /// <summary>Call after successfully saving an explicit user enable. Map-controlled activation doesn't establish a global preference</summary>
        public static void RecordUserEnable(string id, bool enabled)
        {
            RuntimeApi.Kernel.CheckThread();
            if (!enabled || !CanConfigure(id)) return;
            Preferences.Protect(id); Preferences.Flush();
        }

        /// <summary>Call at actual gameplay use, never from permission queries, previews or diagnostics</summary>
        public static void RecordUse(string id)
        {
            RuntimeApi.Kernel.CheckThread();
            if (!entryActive || !compiled || IsControlled(id) || !providers.ContainsKey(id) || !MapPolicy.AllowsMechanic(id)) return;
            Preferences.Protect(id); Preferences.Flush();
        }
    }
}
