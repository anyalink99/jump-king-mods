using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.UI;

namespace JKRuntime.Inspection
{
    internal sealed partial class GimmickPage
    {
        private bool persistentSearch = true;
        private string resultTitle = "SEARCH GIMMICKS";
        private void Home()
        {
            if (snapshot == null) RefreshCatalogue();
            draft = null;
            Show("MOD INSPECTOR", new[] {
                Row("world", "Loaded world & block factories", () => Navigate(LoadedWorld), "Inspect live blocks, observed origins and stored slope collision lines."),
                Row("active", "Active overrides & reset (" + ActiveIds().Length + ")", () => Navigate(ActiveGimmicks), "Inspector-owned overrides. Other mods' own settings are excluded. Keep configurations."),
                Row("restore", "Restore original map state", () => { ResetActive(); Home(); list.Select("restore"); }, "Remove only overrides applied through Mod Inspector. Restore authored geometry and leased state; keep global mod settings and configurations."),
                Row("configurations", "Saved configurations", () => Navigate(Configurations), "Named material and wind configurations, including unavailable providers."),
                Row("maps", "Browse maps & regions", () => Navigate(Maps), "Choose an installed map, then an authored multi-screen region. Search filters do not hide this list."),
                Row("mods", "Browse installed mods", () => Navigate(Mods), "Choose a provider to see its gimmicks with a fresh search."),
                Row("categories", "Browse by behaviour / Wind", () => Navigate(Categories), "Surfaces, media, environment, player mechanics and advanced states. Native Wind is under Environment."),
                Row("search", "Search & colour filters", () => OpenSearch(null), "Search names and colours; combine filters. Resume your previous search without affecting map browsing."),
                Row("saved", "Saved searches", () => Navigate(SavedSearches), "Load, save or remove a named search."),
                Row("refresh", "Refresh catalogue & maps", RefreshLibrary, "Refresh the current library and reindex installed maps. Active state leases must be released before rediscovering their paths.")
            });
        }
        private void GoHome()
        {
            UiPointer.CancelCapture(this); history.Clear(); list = new UiList();
            persistentSearch = true; query = null; current = Home; Home();
        }
        private void OpenSearch(GimmickSearchPreferences saved)
        {
            Navigate(() => {
                query = GimmickSearch.Copy(saved ?? InspectorSettings.Current.GimmickSearch ?? new GimmickSearchPreferences());
                persistentSearch = true; resultTitle = "SEARCH GIMMICKS";
                viewport = new UiListViewport(); results = new GimmickSearchRecord[0]; selected = 0;
                current = SearchResults; SearchResults();
            });
        }
        private void BrowseResults(string name, GimmickSearchPreferences preset)
        {
            query = preset; persistentSearch = false; resultTitle = name;
            viewport = new UiListViewport(); results = new GimmickSearchRecord[0]; selected = 0;
            current = SearchResults; SearchResults();
        }
        private void RefreshLibrary()
        {
            if (Gimmicks.Session != null) Gimmicks.Session.RefreshStates();
            GimmickBlocks.DiscoverLoaded(); GimmickMaps.Start(); RefreshCatalogue(); Home();
        }
        private void Categories()
        {
            string[] families = snapshot.Select(r => r.Entry.Family).Distinct().OrderBy(f => f).ToArray();
            Show("BROWSE BY BEHAVIOUR", families.Select(family => Row(family,
                (family == "Environment" ? "Environment / Wind" : family) + " (" + snapshot.Count(r => r.Entry.Family == family) + ")",
                () => Navigate(() => BrowseResults(family, new GimmickSearchPreferences { Families = new[] { family } })),
                family == "Unknown" ? "Unprepared or ambiguous entries remain available here. Use Prepare material sample to classify it." : "Open this category with no previous search restrictions.")));
        }
        private string[] ActiveIds()
        {
            return snapshot.Where(r => r.Enabled && IsInspectorEffect(r.Entry.Id)).Select(r => r.Entry.Id)
                .Concat((InspectorSettings.Current.GimmickRules ?? new GimmickRule[0]).Where(r => r != null && r.Enabled && IsInspectorEffect(r.Id)).Select(r => r.Id)).Distinct().ToArray();
        }
        private static bool IsInspectorEffect(string id)
        {
            if (id != null && id.StartsWith("setting:", StringComparison.Ordinal)) return false;
            GimmickEntry entry;
            return !Gimmicks.Entries.TryGetValue(id, out entry) || entry.Write == null;
        }
        private void ActiveGimmicks()
        {
            var ids = ActiveIds();
            int overrides = ids.Count(id => !Gimmicks.Entries.ContainsKey(id) || Gimmicks.Entries[id].Write == null);
            var rows = new List<UiListItem>();
            if (ids.Length > 0) {
                rows.Add(Row("reset:all", "Turn off all overrides (" + ids.Length + ")", () => ResetActive(), "Release inspector-owned overrides. Other mods' settings are never changed. Keep configurations."));
            } else rows.Add(new UiListItem("empty", "No active overrides", null, "No inspector overrides are enabled. Other mods and map-authored triggers remain independent.", false));
            foreach (string id in ids.OrderBy(key => Gimmicks.Entries.ContainsKey(key) ? Gimmicks.Entries[key].Label : key)) {
                string key = id; GimmickEntry entry; bool found = Gimmicks.Entries.TryGetValue(id, out entry);
                string name = found ? entry.Label : Gimmicks.Rule(id).Name ?? id;
                string state = found && Gimmicks.Enabled(entry) ? "[ON] " : "[SAVED] ";
                rows.Add(Row("entry:" + id, state + name, () => Navigate(() => ActiveItem(key)),
                    found ? entry.Owner + ". Open to turn off or edit. " + (entry.Write == null ? "Target: " + Range(Gimmicks.Rule(id)) : "Global setting")
                    : "Provider unavailable. This saved rule can still be disabled."));
            }
            Show("ACTIVE OVERRIDES: " + overrides, rows);
        }
        private void ActiveItem(string id)
        {
            if (!IsInspectorEffect(id)) { ActiveGimmicks(); return; }
            GimmickEntry entry; bool found = Gimmicks.Entries.TryGetValue(id, out entry);
            var rows = new List<UiListItem> { Row("off", "Turn off now", () => {
                if (found && entry.Write != null) { entry.Write(false); Gimmicks.Generation++; }
                else { var rule = Gimmicks.Copy(Gimmicks.Rule(id)); rule.Enabled = false; Gimmicks.Configure(rule); }
                RefreshCatalogue(); Back();
            }, "Disable this effect and retain its saved configuration.") };
            if (found) rows.Add(Row("edit", "Edit configuration...", () => { draft = null; Navigate(() => Details(id)); }));
            rows.Add(Row("back", "Back to active gimmicks", Back));
            Show(found ? entry.Label : "UNAVAILABLE SAVED OVERRIDE", rows);
        }
        private void ResetActive()
        {
            // discovery doesn't mean ownership, reset mustn't call foreign persistent controls
            var failures = new List<string>();
            try { Gimmicks.DisableOverrides(); } catch (Exception error) { failures.Add("Overrides: " + error.GetBaseException().Message); }
            Gimmicks.Generation++; RefreshCatalogue(); ActiveGimmicks();
            message = failures.Count == 0 ? "Disabled. Configurations kept; authored map triggers remain independent." : string.Join("; ", failures);
        }
    }
}
