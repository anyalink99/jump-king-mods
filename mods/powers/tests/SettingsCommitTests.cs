using System;
using System.IO;
using System.Xml;
using JKRuntime.Settings;

namespace Powers
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
            var initial = new Preferences { WarpJump = false };
            file.Save(initial); current.SetValue(null, initial); backing.SetValue(null, file);
            try
            {
                using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    bool rejected = false;
                    try { Settings.Edit(value => { value.WarpJump = true; }); }
                    catch (IOException) { rejected = true; }
                    Require(rejected && ReferenceEquals(Settings.Current, initial) && !initial.WarpJump,
                        "Failed setting commit leaves the published configuration unchanged");
                    rejected = false;
                    try { DashBindings.Set("fixture", new[] { new[] { 65 } }); } catch (IOException) { rejected = true; }
                    Require(rejected && DashBindings.Revision == revision && ReferenceEquals(Settings.Current, initial),
                        "Failed binding commit does not publish a new revision or mutate settings");
                }
                Settings.Edit(value => value.WarpJump = true);
                Require(!ReferenceEquals(Settings.Current, initial) && Settings.Current.WarpJump
                    && new SettingsFile<Preferences>(path, () => new Preferences()).Value.WarpJump,
                    "Storage recovery publishes the committed mechanic setting");
            }
            finally { current.SetValue(null, previous); backing.SetValue(null, previousFile); }
        }


    }
}
