using System;
using System.IO;
using JKRuntime.Settings;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void SettingsCommitRollback()
        {
            var current = typeof(Settings).GetField("current", Flags);
            var backing = typeof(Settings).GetField("file", Flags);
            object previous = current.GetValue(null), previousFile = backing.GetValue(null);
            int revision = DashBindings.Revision;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-commit-" + Guid.NewGuid().ToString("N") + ".xml");
            var file = new SettingsFile<Preferences>(path, () => new Preferences());
            var initial = new Preferences { GimmickSearch = new GimmickSearchPreferences { Text = "before" }, GimmickPins = new[] { "kept" } };
            file.Save(initial); current.SetValue(null, initial); backing.SetValue(null, file);
            try
            {
                using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    bool rejected = false;
                    try { Settings.Edit(value => { value.GimmickSearch = new GimmickSearchPreferences { Text = "after" }; value.GimmickPins = new[] { "replacement" }; }); }
                    catch (IOException) { rejected = true; }
                    Require(rejected && ReferenceEquals(Settings.Current, initial) && initial.GimmickSearch.Text == "before" && initial.GimmickPins[0] == "kept",
                        "Failed search/pin commit leaves the published configuration unchanged");
                    rejected = false;
                    try { DashBindings.Set("fixture", new[] { new[] { 65 } }); } catch (IOException) { rejected = true; }
                    Require(rejected && DashBindings.Revision == revision && ReferenceEquals(Settings.Current, initial),
                        "Failed binding commit does not publish a new revision or mutate settings");
                }
                Settings.Edit(value => value.GimmickSearch = new GimmickSearchPreferences { Text = "after" });
                Require(!ReferenceEquals(Settings.Current, initial) && Settings.Current.GimmickSearch.Text == "after"
                    && new SettingsFile<Preferences>(path, () => new Preferences()).Value.GimmickSearch.Text == "after",
                    "Storage recovery publishes the same committed search value");
            }
            finally { current.SetValue(null, previous); backing.SetValue(null, previousFile); }
        }
    }
}
