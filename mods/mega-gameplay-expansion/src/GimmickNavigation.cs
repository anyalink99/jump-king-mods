using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.UI;

namespace MegaGameplayExpansion
{
    internal sealed partial class GimmickPage
    {
        private bool persistentSearch = true;
        private string resultTitle = "SEARCH GIMMICKS";
        private void Home()
        {
            if (snapshot == null) RefreshCatalogue();
            draft = null;
            Show("GIMMICK LIBRARY", new[] {
                Row("active", "Active MGE effects & reset (" + ActiveIds().Length + ")", () => Navigate(ActiveGimmicks), "MGE overrides and its three built-in modes. Other mods' own settings are excluded. Keep pins and configurations."),
                Row("restore", "Restore original map state", () => { ResetActive(false); Home(); }, "Remove only overrides applied through MGE. Restore authored geometry and leased state; keep global mod settings, pins and configurations."),
                Row("pins", "Pinned gimmicks (" + Gimmicks.Pins.Length + ")", () => Navigate(Pinned), "Your shortcuts and saved configurations, including unavailable providers."),
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
                query = GimmickSearch.Copy(saved ?? Settings.Current.GimmickSearch ?? new GimmickSearchPreferences());
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
                family == "Unknown" ? "Unprepared or ambiguous entries remain available here. Opening an entry can prepare its classification." : "Open this category with no previous search restrictions.")));
        }
        private string[] ActiveIds()
        {
            return snapshot.Where(r => r.Enabled && IsMgeEffect(r.Entry.Id)).Select(r => r.Entry.Id)
                .Concat((Settings.Current.GimmickRules ?? new GimmickRule[0]).Where(r => r != null && r.Enabled && IsMgeEffect(r.Id)).Select(r => r.Id)).Distinct().ToArray();
        }
        private static bool IsMgeEffect(string id)
        {
            if (id != null && id.StartsWith("setting:", StringComparison.Ordinal)) return false;
            GimmickEntry entry;
            return !Gimmicks.Entries.TryGetValue(id, out entry) || entry.Write == null
                || (entry.Native && entry.Kind == "Built-in");
        }
        private void ActiveGimmicks()
        {
            var ids = ActiveIds();
            int overrides = ids.Count(id => !Gimmicks.Entries.ContainsKey(id) || Gimmicks.Entries[id].Write == null);
            int settings = ids.Length - overrides;
            var rows = new List<UiListItem>();
            if (ids.Length > 0) {
                rows.Add(Row("reset:all", "Turn off all MGE effects (" + ids.Length + ")", () => ResetActive(true), "Release MGE overrides and disable Warp Jump, No Walk Off and Air Dash. Other mods' settings are never changed. Keep pins and configurations."));
                if (overrides > 0) rows.Add(Row("reset:overrides", "Release overrides only (" + overrides + ")", () => ResetActive(false), "Restore authored geometry and leased state; disable saved overrides while keeping their configurations. Global mod settings stay as they are."));
            } else rows.Add(new UiListItem("empty", "No active MGE effects", null, "No MGE overrides or built-in modes are enabled. Other mods and map-authored triggers remain independent.", false));
            foreach (string id in ids.OrderBy(key => Gimmicks.Entries.ContainsKey(key) ? Gimmicks.Entries[key].Label : key)) {
                string key = id; GimmickEntry entry; bool found = Gimmicks.Entries.TryGetValue(id, out entry);
                string name = found ? entry.Label : Gimmicks.Rule(id).Name ?? id;
                string state = found && Gimmicks.Enabled(entry) ? "[ON] " : "[SAVED] ";
                rows.Add(Row("entry:" + id, state + name, () => Navigate(() => ActiveItem(key)),
                    found ? entry.Owner + ". Open to turn off or edit. " + (entry.Write == null ? "Target: " + Range(Gimmicks.Rule(id)) : "Global setting")
                    : "Provider unavailable. This saved rule can still be disabled."));
            }
            Show("ACTIVE MGE: " + overrides + " overrides / " + settings + " built-in", rows);
        }
        private void ActiveItem(string id)
        {
            if (!IsMgeEffect(id)) { ActiveGimmicks(); return; }
            GimmickEntry entry; bool found = Gimmicks.Entries.TryGetValue(id, out entry);
            var rows = new List<UiListItem> { Row("off", "Turn off now", () => {
                if (found && entry.Write != null) { entry.Write(false); Gimmicks.Generation++; }
                else { var rule = Gimmicks.Copy(Gimmicks.Rule(id)); rule.Enabled = false; Gimmicks.Configure(rule); }
                RefreshCatalogue(); Back();
            }, "Disable this effect and retain its saved configuration and pin.") };
            if (found) rows.Add(Row("edit", "Edit configuration...", () => { draft = null; Navigate(() => Details(id)); }));
            rows.Add(Row("back", "Back to active gimmicks", Back));
            Show(found ? entry.Label : "UNAVAILABLE SAVED OVERRIDE", rows);
        }
        private void ResetActive(bool includeNativeModes)
        {
            // Discovery is not ownership. Never invoke foreign persistent controls from reset.
            var failures = new List<string>();
            try { Gimmicks.DisableOverrides(); } catch (Exception error) { failures.Add("Overrides: " + error.GetBaseException().Message); }
            if (includeNativeModes) foreach (var setting in new[] { Settings.Warp, Settings.EdgeStop, Settings.Dash }) {
                try { if (setting.Value) setting.Set(false); }
                catch (Exception error) { failures.Add(setting.Label + ": " + error.GetBaseException().Message); }
            }
            Gimmicks.Generation++; RefreshCatalogue(); ActiveGimmicks();
            message = failures.Count == 0 ? "Disabled. Pins and configurations kept; authored map triggers remain independent." : string.Join("; ", failures);
        }
    }
}
