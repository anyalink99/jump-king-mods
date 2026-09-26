using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal sealed partial class GimmickPage
    {
        private GimmickSearchPreferences query;
        private GimmickSearchRecord[] snapshot, results = new GimmickSearchRecord[0];
        private UiListViewport viewport = new UiListViewport();
        private readonly GimmickText editor = new GimmickText();
        private int selected;
        private bool browsing;
        private Action<string> edited;
        private string editOriginal, editTitle;
        private GimmickSearchPreferences Query { get { return query ?? (query = GimmickSearch.Copy(Settings.Current.GimmickSearch ?? new GimmickSearchPreferences())); } }
        private void RefreshCatalogue()
        {
            Gimmicks.RefreshConfigurations();
            foreach (var entry in Gimmicks.Entries.Values.ToArray()) {
                if (string.IsNullOrEmpty(entry.Label)) entry.Label = entry.Id ?? "Unknown entry";
                if (string.IsNullOrEmpty(entry.Owner)) entry.Owner = "Unknown provider";
                GimmickClassification.Inspect(entry);
            }
            snapshot = GimmickSearch.Snapshot();
            var known = new HashSet<uint>(snapshot.Where(r => r.Entry.Colour.HasValue && r.Entry.Kind != "Colour").Select(r => r.Entry.Colour.Value.PackedValue));
            foreach (uint packed in GimmickMaps.Maps.SelectMany(m => m.Colours.Keys).Distinct().Where(c => !known.Contains(c))) {
                var colour = new Color { PackedValue = packed }; string id = "colour:" + packed;
                if (!Gimmicks.Entries.ContainsKey(id)) Gimmicks.Entries[id] = new GimmickEntry { Id = id, Label = "Unknown colour " + GimmickSearch.Hex(colour),
                    Owner = "Unresolved", Kind = "Colour", Colour = colour, Detail = "No factory declared this indexed colour. It may be ignored by the game." };
            }
            foreach (string key in Gimmicks.Entries.Where(p => p.Value.Kind == "Colour" && known.Contains(p.Value.Colour.Value.PackedValue)).Select(p => p.Key).ToArray()) Gimmicks.Entries.Remove(key);
            snapshot = GimmickSearch.Snapshot();
        }
        private void SearchResults()
        {
            if (snapshot == null) RefreshCatalogue();
            string id = results.Length > selected ? results[selected].Entry.Id : null;
            string error;
            results = GimmickSearch.Filter(snapshot, Query, GimmickMaps.Maps, out error);
            int restored = Array.FindIndex(results, r => r.Entry.Id == id);
            selected = restored >= 0 ? restored : Math.Max(0, Math.Min(selected, results.Length - 1));
            if (restored < 0) viewport.FollowSelection(selected, results.Length, 6);
            title = resultTitle; browsing = true; drawnGeneration = Gimmicks.Generation;
            if (error != null) message = error;
        }
        private void SearchText()
        { EditText("SEARCH", Query.Text, value => { Query.Text = value; SearchResults(); }); }
        private void EditText(string name, string value, Action<string> accept)
        {
            editTitle = name; editOriginal = value ?? ""; edited = accept;
            editor.Open(editOriginal); typing = true;
        }
        private void FinishText(bool accept)
        {
            string value = accept ? editor.Text : editOriginal;
            typing = false; editor.Dispose();
            try { edited(value); SaveQuery(); }
            catch (Exception error) { typing = true; editor.Open(value); editor.Error = error.GetBaseException().Message; }
        }
        private void SaveQuery() { if (persistentSearch) { Settings.Edit(value => value.GimmickSearch = GimmickSearch.Copy(Query)); } }
        private static int Count(string[] values) { return values == null ? 0 : values.Length; }
        private void Filters()
        {
            string error; int matches = GimmickSearch.Filter(snapshot, Query, GimmickMaps.Maps, out error).Length;
            Show("SEARCH FILTERS", new[] {
                Row("done", "Show " + matches + " results", Back, error ?? "Different filters combine with AND; choices within a filter combine with OR."),
                Row("providers", "Providers (" + Count(Query.Providers) + ")", () => Navigate(() => Facet("PROVIDERS", snapshot.Select(r => r.Entry.Owner), () => Query.Providers, v => Query.Providers = v))),
                Row("maps", "Source maps (" + Count(Query.Maps) + ")", () => Navigate(SourceMaps)),
                Row("regions", "Source regions (" + Count(Query.Regions) + ")", () => Navigate(SourceRegions), "Numbered authored zones spanning several screens. Region keys include their map."),
                Row("family", "Behaviour (" + Count(Query.Families) + ")", () => Navigate(() => Facet("BEHAVIOUR", snapshot.Select(r => r.Entry.Family), () => Query.Families, v => Query.Families = v))),
                Row("geometry", "Geometry (" + Count(Query.Geometry) + ")", () => Navigate(() => Facet("GEOMETRY", snapshot.Select(r => r.Entry.Geometry), () => Query.Geometry, v => Query.Geometry = v))),
                Row("ready", "Availability (" + Count(Query.Readiness) + ")", () => Navigate(() => Facet("AVAILABILITY", snapshot.Select(r => r.Readiness), () => Query.Readiness, v => Query.Readiness = v))),
                Row("usage", "Pinned / enabled (" + Count(Query.Usage) + ")", () => Navigate(() => Facet("USAGE", new[] { "Pinned", "Enabled" }, () => Query.Usage, v => Query.Usage = v))),
                Row("colour", "Exact colour: " + (Query.Colour ?? "Any"), () => Navigate(Colours), "Collision palette colour, independent of the visible artwork."),
                Row("sort", "Sort: " + (Query.Sort ?? "Name"), () => { string[] choices = { "Name", "Provider", "Readiness" }; Query.Sort = choices[(Array.IndexOf(choices, Query.Sort ?? "Name") + 1) % 3]; SaveQuery(); Filters(); }),
                Row("clear", "Clear all filters", () => { Query.Providers = Query.Maps = Query.Regions = Query.Families = Query.Geometry = Query.Readiness = Query.Usage = null; Query.Colour = Query.Sort = null; SaveQuery(); Filters(); }, "Keeps the search text."),
                Row("saved", "Saved searches", () => Navigate(SavedSearches))
            });
        }
        private void Facet(string name, IEnumerable<string> values, Func<string[]> get, Action<string[]> set)
        {
            string[] choices = values.Distinct().OrderBy(v => v).ToArray();
            var rows = new List<UiListItem> { Row("done", "Done", Back), Row("any", "Any", () => { set(null); SaveQuery(); Facet(name, choices, get, set); }) };
            rows.AddRange(choices.Select(value => Row(value, ((get() ?? new string[0]).Contains(value) ? "[x] " : "[ ] ") + value,
                () => { set(GimmickSearch.Toggle(get(), value)); SaveQuery(); Facet(name, choices, get, set); }, "Multiple choices match ANY here; different filters must ALL match.")));
            Show(name, rows);
        }
        private void SourceMaps()
        {
            var rows = new List<UiListItem> { Row("done", "Done", Back), Row("any", "Any map", () => { Query.Maps = Query.Regions = null; SaveQuery(); SourceMaps(); }) };
            rows.AddRange(GimmickMaps.Maps.Select(map => Row(map.Id, ((Query.Maps ?? new string[0]).Contains(map.Id) ? "[x] " : "[ ] ") + map.Title,
                () => { Query.Maps = GimmickSearch.Toggle(Query.Maps, map.Id); SaveQuery(); SourceMaps(); }, map.Error ?? "Source colour occurrence; not proof that its handler is active.")));
            Show("SOURCE MAPS", rows);
        }
        private void SourceRegions()
        {
            var rows = new List<UiListItem> { Row("done", "Done", Back), Row("any", "Any region", () => { Query.Regions = null; SaveQuery(); SourceRegions(); }) };
            foreach (var map in GimmickMaps.Maps.Where(m => GimmickSearch.Selected(Query.Maps, m.Id)))
                foreach (var region in map.Regions) {
                    string key = map.Id + "|" + region.Number;
                    rows.Add(Row(key, ((Query.Regions ?? new string[0]).Contains(key) ? "[x] " : "[ ] ") + map.Title + " / " + region.Number + ". " + region.Name,
                        () => { Query.Regions = GimmickSearch.Toggle(Query.Regions, key); SaveQuery(); SourceRegions(); }, "Screens " + region.First + " - " + region.Last));
                }
            Show("SOURCE REGIONS", rows);
        }
        private void SavedSearches()
        {
            var rows = new List<UiListItem> { Row("save", "Save current search...", () => EditText("SEARCH NAME", "", value => {
                if (string.IsNullOrWhiteSpace(value)) { SavedSearches(); return; }
                var saved = GimmickSearch.Copy(Query); saved.Name = value;
                Settings.Edit(candidate => candidate.SavedSearches = (Settings.Current.SavedSearches ?? new GimmickSearchPreferences[0]).Where(q => q.Name != value).Concat(new[] { saved }).ToArray()); SavedSearches();
            })) };
            foreach (var search in Settings.Current.SavedSearches ?? new GimmickSearchPreferences[0]) {
                var saved = search;
                rows.Add(Row(saved.Name, "Load: " + saved.Name, () => OpenSearch(saved)));
                rows.Add(Row("delete:" + saved.Name, "Delete: " + saved.Name, () => { Settings.Edit(value => value.SavedSearches = Settings.Current.SavedSearches.Where(q => q != saved).ToArray()); SavedSearches(); }));
            }
            Show("SAVED SEARCHES", rows);
        }
        private void BrowserUpdate(UiInput input)
        {
            if (input.Secondary) { Navigate(Filters); return; }
            if (input.Left) { SearchText(); return; }
            if (input.Right) { GoHome(); return; }
            if (input.Up || input.Down) {
                selected = Math.Max(0, Math.Min(results.Length - 1, selected + (input.Down ? 1 : -1)));
                viewport.FollowSelection(selected, results.Length, 6);
            }
            if (input.Confirm && results.Length > 0) OpenResult(selected);
        }
        private void OpenResult(int index)
        { selected = index; string id = results[index].Entry.Id; draft = null; Navigate(() => Details(id)); }
        private void Button(Rectangle bounds, string text, Action click)
        {
            UiTheme.Panel(bounds, UiTheme.PanelFill, UiTheme.Border);
            UiTheme.TextLine(UiTheme.FitText(text, bounds.Width - 10, true), new Vector2(bounds.X + 5, bounds.Y + (bounds.Height <= 20 ? 1 : 6)), UiTheme.Text, true);
            UiPointer.Region(bounds, null, () => Try(click));
        }
        private void BrowserDraw()
        {
            Button(new Rectangle(28, 53, 276, 25), string.IsNullOrEmpty(Query.Text) ? "Search name, provider, class..." : Query.Text, SearchText);
            Button(new Rectangle(310, 53, 82, 25), "Filters", () => Navigate(Filters));
            Button(new Rectangle(398, 53, 50, 25), "Menu", GoHome);
            if (!string.IsNullOrEmpty(Query.Text)) Button(new Rectangle(283, 53, 21, 25), "x", () => { Query.Text = null; SaveQuery(); SearchResults(); });
            int count = Count(Query.Providers) + Count(Query.Maps) + Count(Query.Regions) + Count(Query.Families) + Count(Query.Geometry) + Count(Query.Readiness) + Count(Query.Usage) + (string.IsNullOrEmpty(Query.Colour) ? 0 : 1);
            UiTheme.TextLine(results.Length + " results  /  " + count + " filters" + (GimmickMaps.Busy ? "  /  indexing maps..." : ""), new Vector2(28, 84), UiTheme.Muted, true);
            FilterChips();
            int first = viewport.FirstVisible(results.Length, 6, selected);
            var bounds = new Rectangle(28, 120, 420, 168);
            UiPointer.ScrollRegion(bounds, rows => selected = viewport.Scroll(-rows, results.Length, 6, selected));
            for (int i = first; i < Math.Min(results.Length, first + 6); i++) {
                int index = i; var record = results[i]; var entry = record.Entry; int y = 120 + (i - first) * 28;
                var row = new Rectangle(28, y, 420, 27);
                if (i == selected) UiTheme.Panel(row, UiTheme.PanelFill, UiTheme.Gold);
                UiPointer.Region(new Rectangle(28, y, 389, 27), () => selected = index, () => Try(() => OpenResult(index)));
                if (entry.Colour.HasValue) UiTheme.Panel(new Rectangle(34, y + 7, 10, 10), entry.Colour.Value, UiTheme.Muted);
                else UiTheme.TextLine("-", new Vector2(34, y + 3), UiTheme.Muted, true);
                UiTheme.TextLine(UiTheme.FitText((record.Enabled ? "+ " : "") + entry.Label, 363, true), new Vector2(50, y + 1), i == selected ? UiTheme.Gold : UiTheme.Text, true);
                UiTheme.TextLine(UiTheme.FitText(entry.Owner + " / " + entry.Family + " / " + record.Readiness, 363, true), new Vector2(50, y + 12), UiTheme.Muted, true);
                UiTheme.TextLine(Gimmicks.Pins.Contains(entry.Id) ? "*" : "+", new Vector2(430, y + 6), UiTheme.Gold, true);
                UiPointer.Region(new Rectangle(418, y, 30, 27), null, () => Try(() => { Gimmicks.Pin(entry.Id); SearchResults(); }));
            }
            if (results.Length == 0) UiTheme.WrappedText("No matches. Open Filters to clear a condition; unknown colours remain searchable.", bounds, UiTheme.Muted);
            if (results.Length > 6) {
                int thumb = Math.Max(8, 168 * 6 / results.Length);
                UiTheme.Panel(new Rectangle(451, 120 + (168 - thumb) * first / (results.Length - 6), 3, thumb), UiTheme.Gold, UiTheme.Gold);
            }
            string info = message.Length != 0 ? message : results.Length == 0 ? "" : results[selected].Entry.Classification ?? results[selected].Entry.Detail;
            UiTheme.TextLine(UiTheme.FitText(info ?? "", 420, true), new Vector2(28, 295), message.Length != 0 ? Color.Orange : UiTheme.Muted, true);
            UiTheme.CommandBar(new Rectangle(28, 321, 420, 18), new[] { UiInputHints.Command(UiAction.Confirm, "Open"), UiInputHints.Command(UiAction.Left, "Search"), UiInputHints.Command(UiAction.Secondary, "Filters"), UiInputHints.Command(UiAction.Cancel, "Back"), UiInputHints.Command(UiAction.Right, "Menu") });
        }
        private void FilterChips()
        {
            var chips = new List<Tuple<string, Action>>();
            if (Count(Query.Providers) > 0) chips.Add(Tuple.Create("Providers", new Action(() => Query.Providers = null)));
            if (Count(Query.Maps) > 0) chips.Add(Tuple.Create("Maps", new Action(() => Query.Maps = null)));
            if (Count(Query.Regions) > 0) chips.Add(Tuple.Create("Regions", new Action(() => Query.Regions = null)));
            if (Count(Query.Families) > 0) chips.Add(Tuple.Create(Query.Families[0], new Action(() => Query.Families = null)));
            if (Count(Query.Geometry) > 0) chips.Add(Tuple.Create("Geometry", new Action(() => Query.Geometry = null)));
            if (Count(Query.Readiness) > 0) chips.Add(Tuple.Create("Availability", new Action(() => Query.Readiness = null)));
            if (Count(Query.Usage) > 0) chips.Add(Tuple.Create(Query.Usage[0], new Action(() => Query.Usage = null)));
            if (!string.IsNullOrEmpty(Query.Colour)) chips.Add(Tuple.Create(Query.Colour, new Action(() => Query.Colour = null)));
            if (chips.Count == 0) UiTheme.TextLine("All sources and behaviours", new Vector2(28, 104), UiTheme.Muted, true);
            for (int i = 0; i < Math.Min(4, chips.Count); i++) {
                var chip = chips[i]; var bounds = new Rectangle(28 + i * 106, 99, 101, 19);
                Button(bounds, "x " + chip.Item1, () => { chip.Item2(); SaveQuery(); SearchResults(); });
            }
        }
    }
}
