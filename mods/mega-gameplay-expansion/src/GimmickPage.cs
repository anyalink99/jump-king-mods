using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.UI;
using JumpKing;
using JumpKing.PauseMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MegaGameplayExpansion
{
    internal sealed partial class GimmickPage : IUiPage, IUiPageInputPolicy
    {
        private readonly object factory;
        private readonly GuiFormat format;
        private readonly bool pause;
        private readonly UiFrame frame = new UiFrame(new Rectangle(12, 12, 456, 336));
        private UiList list = new UiList();
        private readonly Stack<Action> history = new Stack<Action>();
        private Action current;
        private string title = "GIMMICK LIBRARY", message = "";
        private bool typing;
        private long drawnGeneration;
        public bool WantsClose { get; private set; }
        public bool HandlesCancel { get { return true; } }
        internal GimmickPage(object menuFactory, GuiFormat menuFormat, bool isPause)
        { factory = menuFactory; format = menuFormat; pause = isPause; }
        public void OnOpen()
        {
            WantsClose = false; history.Clear(); list = new UiList(); query = null; persistentSearch = true; Gimmicks.Initialize();
            GimmickMenu.DiscoverSettings(factory, format, pause);
            GimmickBlocks.DiscoverLoaded();
            if (Gimmicks.Session != null) Gimmicks.Session.EnsureStates();
            if (GimmickMaps.Maps.Length == 0) GimmickMaps.Start();
            RefreshCatalogue(); current = Home; Home();
        }
        public void OnClose() { editor.Dispose(); SaveQuery(); UiPointer.CancelCapture(this); GimmickMenu.Schedule(factory); }
        private void Navigate(Action page)
        {
            Action parent = current ?? Home;
            var parentList = list; var parentQuery = query; var parentResults = results;
            var parentViewport = viewport; int parentSelection = selected;
            bool parentPersistent = persistentSearch; string parentTitle = resultTitle;
            Action restore = () => {
                current = parent; list = parentList; query = parentQuery; results = parentResults;
                viewport = parentViewport; selected = parentSelection;
                persistentSearch = parentPersistent; resultTitle = parentTitle; parent();
            };
            history.Push(restore); list = new UiList(); current = page;
            try { page(); }
            catch { history.Pop(); restore(); throw; }
        }
        private void Back()
        {
            if (typing) { FinishText(false); return; }
            number = null; preview = null;
            if (history.Count == 0) { WantsClose = true; return; }
            history.Pop()(); if (browsing || current == Home) draft = null;
        }
        private UiListItem Row(string id, string label, Action action, string detail = "")
        { return new UiListItem(id, label, () => Try(action), detail); }
        private void Try(Action action)
        {
            try { message = ""; action(); }
            catch (Exception error) { message = error.GetBaseException().Message; }
        }
        private void Show(string name, IEnumerable<UiListItem> items)
        { browsing = false; number = null; preview = null; title = name; list.SetItems(items); drawnGeneration = Gimmicks.Generation; }
        private void Mods()
        {
            Show("BROWSE BY MOD", Gimmicks.Entries.Values.Select(e => e.Owner).Distinct().OrderBy(s => s).Select(owner =>
                Row(owner, owner, () => Navigate(() => BrowseResults(owner, new GimmickSearchPreferences { Providers = new[] { owner } })), "Show this provider with no previous search restrictions.")));
        }
        private void Pinned()
        {
            var rows = new List<UiListItem>();
            foreach (string id in Gimmicks.Pins.Distinct())
            {
                GimmickEntry item; string key = id;
                if (Gimmicks.Entries.TryGetValue(id, out item)) rows.AddRange(EntryRows(new[] { item }));
                else rows.Add(Row(id, "Unpin unavailable: " + id.Split(':').Last(), () => { Gimmicks.Pin(key); Pinned(); }, id));
            }
            Show("PINNED", rows);
        }
        private void Maps()
        {
            var rows = GimmickMaps.Maps.OrderBy(map => map.Id != "native").ThenBy(map => map.Title).Select(map => Row(map.Id, map.Title, () => Navigate(() => Regions(map)),
                map.Error ?? map.Regions.Length + " regions. Choose the whole map or an authored multi-screen region.")).ToList();
            if (rows.Count == 0) rows.Add(new UiListItem("indexing", GimmickMaps.Busy ? "Reading installed maps..." : "No maps indexed", null, GimmickMaps.Status, false));
            rows.Add(Row("refresh:maps", "Refresh installed maps", () => { GimmickMaps.Start(); Maps(); }));
            Show("BROWSE MAPS & REGIONS", rows);
        }
        private void Regions(GimmickMap map)
        {
            var rows = new List<UiListItem> { Row("all", "All gimmicks in this map", () => Navigate(() => MapEntries(map, null)), map.Error ?? "Browse the complete map without previous search restrictions.") };
            rows.AddRange(map.Regions.Select(region => Row(region.Number.ToString(), region.Number + ". " + region.Name,
                () => Navigate(() => MapEntries(map, region)), "Screens " + region.First + " - " + region.Last)));
            Show(map.Title, rows);
        }
        private void MapEntries(GimmickMap map, GimmickRegion region)
        {
            BrowseResults(region == null ? map.Title : "Region " + region.Number + ": " + region.Name,
                new GimmickSearchPreferences { Maps = new[] { map.Id }, Regions = region == null ? null : new[] { map.Id + "|" + region.Number } });
        }
        private IEnumerable<UiListItem> EntryRows(IEnumerable<GimmickEntry> entries)
        {
            foreach (var entry in entries.OrderBy(e => e.Owner).ThenBy(e => e.Label))
            {
                var item = entry;
                yield return Row(item.Id, (Gimmicks.Pins.Contains(item.Id) ? "* " : "") + (Gimmicks.Enabled(item) ? "[ON] " : "") + item.Label,
                    () => { draft = null; Navigate(() => Details(item.Id)); }, item.Owner + " / " + item.Kind + ". " + (item.Error ?? item.Detail));
            }
        }
        private void Entries(string name, Func<GimmickEntry, bool> filter)
        { Show(name, EntryRows(Gimmicks.Entries.Values.Where(filter))); }
        private void Information(GimmickEntry item)
        {
            var lines = new List<UiListItem>();
            foreach (string paragraph in new[] { item.Owner, item.Id, item.Error, item.Classification, item.Detail, GimmickBlocks.HookStatus })
            {
                if (string.IsNullOrEmpty(paragraph)) continue;
                string remaining = paragraph;
                while (remaining.Length > 0)
                {
                    int length = remaining.Length;
                    while (length > 1 && UiTheme.FitText(remaining.Substring(0, length), 402, true) != remaining.Substring(0, length)) length--;
                    if (length < remaining.Length) { int space = remaining.LastIndexOf(' ', length - 1, length); if (space > length / 2) length = space + 1; }
                    lines.Add(new UiListItem("line:" + lines.Count, remaining.Substring(0, length), null, "", false));
                    remaining = remaining.Substring(length);
                }
            }
            Show("ENTRY DETAILS", lines);
        }
        private void Scope(string id)
        {
            var rows = new List<UiListItem> {
                Row("done", "Done", Back),
                Row("all", "Entire current map", () => SetRange(id, 0, 0)),
                Row("numbers", "Specific screens / ranges...", () => EditText("TARGET SCREENS", draft.Screens == null ? "" : string.Join(",", draft.Screens), value => {
                    draft.Screens = GimmickSearch.ScreenSelection(value); draft.FirstScreen = draft.LastScreen = 0; Scope(id);
                }), "Use 1,3-7,12. Empty selection targets no screens; Entire current map is a separate choice."),
                Row("screen", "Current screen (" + (Camera.CurrentScreen + 1) + ")", () => SetRange(id, Camera.CurrentScreen + 1, Camera.CurrentScreen + 1))
            };
            string root = Game1.instance.contentManager.root;
            if (!System.IO.Path.IsPathRooted(root)) root = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Game1).Assembly.Location), root);
            string normalized = System.IO.Path.GetFullPath(root).TrimEnd('\\', '/');
            var map = GimmickMaps.Maps.FirstOrDefault(m => string.Equals(System.IO.Path.GetFullPath(m.Root).TrimEnd('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
            if (map != null) rows.AddRange(map.Regions.Select(r => Row("region:" + r.Number, ((draft.Screens ?? new int[0]).Intersect(Enumerable.Range(r.First, r.Last-r.First+1)).Any() ? "[x] " : "[ ] ") + r.Number + ". " + r.Name, () => {
                var values = new HashSet<int>(draft.Screens ?? new int[0]); var region = Enumerable.Range(r.First, r.Last-r.First+1).ToArray();
                if (region.All(values.Contains)) values.ExceptWith(region); else values.UnionWith(region);
                draft.Screens = values.OrderBy(v => v).ToArray(); draft.FirstScreen = draft.LastScreen = 0; Scope(id);
            }, "Screens " + r.First + " - " + r.Last + ". Selected regions combine their screens.")));
            Show("APPLICATION SCOPE", rows);
        }
        private void SetRange(string id, int first, int last)
        { draft.FirstScreen = first; draft.LastScreen = last; draft.Screens = null; Back(); }
        private void Sources(string id)
        {
            var item = Gimmicks.Entries[id]; var rows = new List<UiListItem>();
            foreach (var map in GimmickMaps.Maps.Where(m => GimmickMaps.Occurs(m, null, item)))
            {
                var value = map;
                rows.Add(Row(map.Id, map.Title, () => Navigate(() => Regions(value)),
                    "Regions: " + string.Join(", ", map.Regions.Where(r => GimmickMaps.Occurs(map, r, item)).Select(r => r.Number + ". " + r.Name))));
            }
            Show("SOURCE MAPS", rows);
        }
        public void Update(UiInput input, float delta)
        {
            if (GimmickMaps.Poll())
            {
                GimmickBlocks.DiscoverPalette(GimmickMaps.Maps.SelectMany(m => m.Colours.Keys).Distinct().Select(c => new Color { PackedValue = c }));
                RefreshCatalogue(); if (!typing) current();
            }
            if (typing)
            {
                editor.Update(delta);
                // Text editing consumes menu confirm/cancel and navigation.
                bool pad = Keyboard.GetState().GetPressedKeys().Length == 0;
                if (editor.Cancel || (pad && input.Cancel)) FinishText(false);
                else if (editor.Confirm || (pad && input.Confirm)) FinishText(true);
                return;
            }
            if (input.Cancel) { Back(); return; }
            if (PaletteOpen) { PaletteUpdate(input); return; }
            if (number != null) { if (input.Confirm) Back(); else number.Update(input); return; }
            if (preview != null) { if (input.Left) previewScreen = Math.Max(0, previewScreen-1); if (input.Right) previewScreen = Math.Min(preview.Length-1, previewScreen+1); return; }
            if (browsing) BrowserUpdate(input); else list.Update(input);
            if (drawnGeneration != Gimmicks.Generation) { RefreshCatalogue(); current(); }
        }
        public void Draw()
        {
            frame.Draw();
            UiTheme.TextLine(UiTheme.FitText(typing ? editTitle : title, 420, false), new Vector2(28, 25), UiTheme.Gold, false);
            if (typing) {
                editor.Draw(new Rectangle(28, 71, 420, 29));
                UiTheme.WrappedText(editor.Error ?? "Type or paste text. Ctrl+A selects all; Shift+arrows selects a range. Enter confirms; Escape restores the previous value.", new Rectangle(28, 112, 420, 65), UiTheme.Muted);
                Button(new Rectangle(28, 215, 140, 27), "Confirm", () => FinishText(true));
                Button(new Rectangle(176, 215, 140, 27), "Cancel", () => FinishText(false)); return;
            }
            if (browsing) { BrowserDraw(); return; }
            if (PaletteOpen) { PaletteDraw(); return; }
            if (number != null) { number.Draw(new Rectangle(40, 94, 392, 42)); UiTheme.WrappedText("Drag the slider, use the wheel, or press Left / Right. This edits the draft only. Confirm to return.", new Rectangle(28, 164, 420, 65), UiTheme.Muted); Button(new Rectangle(28, 250, 120, 27), "Done", Back); return; }
            if (preview != null) { PreviewDraw(); Button(new Rectangle(340, 318, 105, 23), "Back", Back); return; }
            list.Draw(new Rectangle(28, 59, 424, 198), "No entries in this view", 22);
            string status = message.Length != 0 ? message : list.Description;
            UiTheme.WrappedText(status, new Rectangle(28, 264, 420, 34), message.Length != 0 ? Color.Orange : UiTheme.Muted);
            UiTheme.TextLine(UiTheme.FitText(GimmickMaps.Busy ? GimmickMaps.Status : Gimmicks.Status, 420, true), new Vector2(28, 302), UiTheme.Muted, true);
            UiTheme.CommandBar(new Rectangle(28, 321, 420, 18), new[] { UiInputHints.Command(UiAction.Confirm, "Select"), UiInputHints.Command(UiAction.Cancel, "Back") });
            UiPointer.Region(new Rectangle(350, 319, 95, 22), null, Back);
        }
    }
}
