using System;
using System.IO;

namespace JKRuntime.Inspection
{
    internal static partial class Tests
    {
        private sealed class InspectorSettingsFixture : IDisposable
        {
            private readonly System.Reflection.FieldInfo currentField=typeof(InspectorSettings).GetField("current",Flags);
            private readonly System.Reflection.FieldInfo fileField=typeof(InspectorSettings).GetField("file",Flags);
            private readonly object previousCurrent,previousFile;
            internal readonly string FilePath;
            internal InspectorSettingsFixture()
            {
                previousCurrent=currentField.GetValue(null);previousFile=fileField.GetValue(null);
                string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-"+Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);FilePath=Path.Combine(directory,"JKRuntime.Inspector.xml");
                InspectorSettings.Load(FilePath);
            }
            public void Dispose()
            {
                // restore the store and its value together; cleanup mustn't save over another fixture
                fileField.SetValue(null,previousFile);currentField.SetValue(null,previousCurrent);
            }
        }
        private static void InspectorSettingsRegression()
        {
            var original=InspectorSettings.Current;
            using(var fixture=new InspectorSettingsFixture())
            {
                string path=fixture.FilePath;
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
                string previous=File.ReadAllText(path);
                for(int i=0;i<4;i++) {
                    string text="retry "+i;
                    InspectorSettings.Edit(value=>value.GimmickSearch=new GimmickSearchPreferences { Text=text });
                    Require(File.ReadAllText(path+".bak")==previous&&InspectorSettings.Current.GimmickSearch.Text==text,
                        "Saving after a rejected edit keeps the previous bytes across repeated replacements");
                    previous=File.ReadAllText(path);
                }
            }
            Require(ReferenceEquals(original,InspectorSettings.Current),"A settings fixture restores the previous in-memory value");
            Console.WriteLine("[OK] Inspector settings: isolated stores, failed-commit recovery and exact rotating backups");
        }
    }
}
