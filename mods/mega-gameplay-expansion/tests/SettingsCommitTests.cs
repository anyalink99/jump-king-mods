using System;
using System.IO;
using System.Xml;
using JKRuntime.Settings;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void SettingsCommitRollback()
        {
            RemovedLibrarySettings();
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

        private static void RemovedLibrarySettings()
        {
            var current = typeof(Settings).GetField("current", Flags);
            var backing = typeof(Settings).GetField("file", Flags);
            object previous = current.GetValue(null), previousFile = backing.GetValue(null);
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-upgrade-" + Guid.NewGuid().ToString("N") + ".xml");
            const string mechanics = "<WarpJump>true</WarpJump><NoWalkOff>true</NoWalkOff><AirDash>false</AirDash>"
                + "<DashBindings><DashDeviceBinding><Device>pc_keyboard_jump_king</Device><Chords>"
                + "<ArrayOfInt><int>161</int></ArrayOfInt><ArrayOfInt><int>222</int></ArrayOfInt>"
                + "</Chords></DashDeviceBinding></DashBindings>";
            string[] removed = { "GimmickSearch", "GimmickPins", "GimmickRules", "SavedSearches" };
            try
            {
                // old releases wrote empty and populated sections; both must remain editable
                foreach (bool populated in new[] { false, true })
                {
                    string sections = "";
                    foreach (string name in removed)
                        sections += "<" + name + ">" + (populated ? "<Nested><Value>discard</Value></Nested>" : "") + "</" + name + ">";
                    string original = "<Preferences>" + mechanics + sections + "</Preferences>";
                    File.WriteAllText(path, original);
                    var file = new SettingsFile<Preferences>(path, () => new Preferences());
                    Require(file.Status == SettingsReadStatus.Loaded && file.CanSave,
                        "Removed library sections don't lock mechanic settings");
                    Require(file.Value.WarpJump && file.Value.NoWalkOff && !file.Value.AirDash
                        && file.Value.DashBindings[0].Chords[0][0] == 161 && file.Value.DashBindings[0].Chords[1][0] == 222,
                        "Upgrade keeps mechanic values and both custom binds");
                    Require(File.ReadAllText(path) == original, "Reading settings doesn't rewrite the file");
                    current.SetValue(null, file.Value); backing.SetValue(null, file);
                    Settings.Dash.Set(true);
                    var saved = new XmlDocument(); saved.Load(path);
                    foreach (string name in removed)
                        Require(saved.SelectSingleNode("/Preferences/" + name) == null, "Removed data isn't written back: " + name);
                    var reloaded = new SettingsFile<Preferences>(path, () => new Preferences());
                    Require(reloaded.CanSave && reloaded.Value.AirDash && reloaded.Value.WarpJump && reloaded.Value.NoWalkOff
                        && reloaded.Value.DashBindings[0].Chords[1][0] == 222, "Air Dash toggle commits and survives reload");
                    Settings.Warp.Set(false); Settings.EdgeStop.Set(false);
                    DashBindings.Set("pc_keyboard_jump_king", new[] { new[] { 65, 66 } });
                    reloaded = new SettingsFile<Preferences>(path, () => new Preferences());
                    Require(reloaded.Value.AirDash && !reloaded.Value.WarpJump && !reloaded.Value.NoWalkOff
                        && reloaded.Value.DashBindings[0].Chords[0][1] == 66, "All mechanic and binding edits remain writable");

                    // rebinding can be the first write after an upgrade, before any mechanic toggle
                    File.WriteAllText(path, original);
                    file = new SettingsFile<Preferences>(path, () => new Preferences());
                    current.SetValue(null, file.Value); backing.SetValue(null, file);
                    Require(DashBindings.Custom("pc_keyboard_jump_king").Chords[0][0] == 161,
                        "Old custom Air Dash binding stays selected instead of falling back to Jump");
                    int revision = DashBindings.Revision;
                    DashBindings.Set("pc_keyboard_jump_king", new[] { new[] { 161, 70 }, new[] { 222 } });
                    reloaded = new SettingsFile<Preferences>(path, () => new Preferences());
                    Require(DashBindings.Revision == revision + 1 && reloaded.CanSave
                        && reloaded.Value.DashBindings[0].Chords[0][1] == 70
                        && reloaded.Value.DashBindings[0].Chords[1][0] == 222
                        && reloaded.Value.WarpJump && reloaded.Value.NoWalkOff && !reloaded.Value.AirDash,
                        "First-edit Air Dash rebind commits without changing mechanics or the secondary bind");
                    saved.Load(path);
                    foreach (string name in removed)
                        Require(saved.SelectSingleNode("/Preferences/" + name) == null, "Rebinding drops retired sections too");
                }
                foreach (string unknown in new[] { "<FutureSetting>keep</FutureSetting>",
                    "<GimmickSearch xmlns=\"urn:future\">keep</GimmickSearch>" })
                {
                    string guardedPath = path + Guid.NewGuid().ToString("N");
                    string original = "<Preferences>" + mechanics + unknown + "</Preferences>";
                    File.WriteAllText(guardedPath, original);
                    var file = new SettingsFile<Preferences>(guardedPath, () => new Preferences());
                    Require(file.Status == SettingsReadStatus.Unsupported && !file.CanSave, "Other unknown fields stay protected");
                    Require(File.ReadAllText(guardedPath) == original, "Unsupported data stays untouched");
                }
            }
            finally { current.SetValue(null, previous); backing.SetValue(null, previousFile); }
        }
    }
}
