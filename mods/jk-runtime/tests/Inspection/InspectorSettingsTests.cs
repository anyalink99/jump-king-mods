using System;
using System.IO;

namespace JKRuntime.Inspection
{
    internal static partial class Tests
    {
        private static void InspectorSettingsRegression()
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "JKRuntime.Inspector.xml");
            InspectorSettings.Load(path);
            Require(InspectorSettings.Current.GimmickRules == null && InspectorSettings.Current.SavedSearches == null,
                "A new inspector starts without configurations or searches");
            InspectorSettings.Edit(value => {
                value.GimmickRules = new[] { new GimmickRule { Id = "config:fixture", Name = "Upper water", SourceId = "fixture:water", FirstScreen = 3, LastScreen = 7 } };
                value.GimmickSearch = new GimmickSearchPreferences { Text = "ice" };
                value.SavedSearches = new[] { new GimmickSearchPreferences { Name = "My search", Text = "water" } };
            });
            InspectorSettings.Load(path);
            Require(InspectorSettings.Current.GimmickRules[0].Name == "Upper water"
                && InspectorSettings.Current.GimmickRules[0].LastScreen == 7
                && InspectorSettings.Current.GimmickSearch.Text == "ice"
                && InspectorSettings.Current.SavedSearches[0].Text == "water", "Configurations and searches survive reload");
            var before = InspectorSettings.Current;
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)) {
                bool failed = false;
                try { InspectorSettings.Edit(value => value.GimmickSearch = new GimmickSearchPreferences { Text = "replacement" }); }
                catch (IOException) { failed = true; }
                Require(failed && ReferenceEquals(before, InspectorSettings.Current) && before.GimmickSearch.Text == "ice",
                    "Failed commit leaves the published inspector preferences intact");
            }
            Console.WriteLine("[OK] Inspector settings: clean defaults, configurations/searches and failed-commit isolation");
        }
    }
}
