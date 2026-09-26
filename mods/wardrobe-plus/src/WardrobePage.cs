using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JKRuntime.UI;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;

namespace WardrobePlus
{
    internal sealed partial class WardrobePage : IUiPage, IUiPageInputPolicy
    {
        private sealed class Row
        {
            internal string Label, Hint = "", SourceId = "";
            internal Func<string> Text;
            internal Func<string> HintText;
            internal Func<string> Summary;
            internal int PreviewItem = NativeAppearance.BaseItem;
            internal string CurrentLabel { get { return Text == null ? Label : Text(); } }
            internal string CurrentHint { get { return HintText == null ? Hint : HintText(); } }
            internal Action Select;
            internal Row(string label, Action select, string hint = "") { Label = label; Select = select; Hint = hint; }
        }
        private sealed class PageState
        {
            internal string Title, Description;
            internal List<Row> Rows;
            internal int Index;
            internal bool Main;
            internal string Query = "";
            internal int PreviewItem = NativeAppearance.BaseItem;
            internal readonly UiListViewport Viewport = new UiListViewport();
            internal Func<List<Row>> RefreshRows;
            internal Func<string> RefreshTitle;
            private static string RowKey(Row row) { return row.SourceId.Length > 0 ? row.SourceId : row.Label; }
            internal void Refresh()
            {
                string key = Rows.Count == 0 ? "" : RowKey(Rows[Math.Min(Index, Rows.Count - 1)]);
                if (RefreshRows != null) Rows = RefreshRows();
                if (RefreshTitle != null) Title = RefreshTitle();
                int found = Rows.FindIndex(x => RowKey(x) == key); if (found >= 0) Index = found;
                Index = Math.Max(0, Math.Min(Index, Rows.Count - 1));
            }
        }
        private readonly Stack<PageState> history = new Stack<PageState>();
        private PageState page;
        private Outfit draft;
        private Preview preview;
        private bool close, previewDirty, fitting, allPoses, checkerboard, equippedOnly;
        private int fitItem, tryOnItem;
        private WardrobeData observedData;
        private string fitGesture;
        private UiTextEntryPage textEntry;
        private readonly UiTextRenderer textRenderer = new UiTextRenderer();
        private Microsoft.Xna.Framework.Input.KeyboardState shortcutKeys;
        private int VisibleRows { get { return page.Main ? 5 : 8; } }
        public bool WantsClose { get { return close; } }
        public bool HandlesCancel { get { return true; } }
        public void OnOpen()
        {
            Controller.Ensure(); Controller.RefreshCatalog(); close = false;
            draft = Controller.Data.Current.Copy(); preview = new Preview(); previewDirty = true;
            tryOnItem = NativeAppearance.BaseItem; history.Clear(); fitting = false; CloseText();
            allPoses = true; checkerboard = true; observedData = Controller.Data;
            Controller.Status = "";
            shortcutKeys = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            Root(false);
        }
        public void OnClose() { Controller.EndGesture(); CloseText(); if (preview != null) preview.Dispose(); preview = null; textRenderer.Dispose(); }
        private void Show(string title, List<Row> rows, string description = "", bool push = true)
        {
            if (push && page != null) history.Push(page);
            page = new PageState { Title = title, Rows = rows, Description = description };
        }
        private void Back()
        {
            if (history.Count > 0) page = history.Pop();
            else if (Controller.Flush()) close = true;
        }
        private void Changed() { Controller.Edit(draft, fitting ? fitGesture : null); previewDirty = true; }
        private void Root(bool push)
        {
            Show("WARDROBE+", RootRows(), "Changes are immediate. Back closes the wardrobe.", push);
            page.Main = true; page.RefreshRows = RootRows;
        }
        private List<Row> RootRows()
        {
            var rows = new List<Row> {
                new Row("Collection", Parents, "Choose a collection, then change any item below.") { Summary = () => string.IsNullOrEmpty(draft.ParentId) ? "None" : draft.ParentName },
                ItemRow(NativeAppearance.BaseItem, false),
                new Row("Item filter", () => { equippedOnly = !equippedOnly; page.Refresh(); }) { Text = () => equippedOnly ? "Show: Equipped" : "Show: All items" } };
            var settings = EquipmentService.Settings();
            var worn = NativeAppearance.Worn();
            rows.AddRange(settings.skins.Where(x => !equippedOnly || worn.Contains((int)x.item)).OrderBy(x => NativeAppearance.LayerOrder(x.layers[0])).ThenBy(x => (int)x.item)
                .Select(s => ItemRow((int)s.item, worn.Contains((int)s.item))));
            return rows;
        }
        private Row ItemRow(int item, bool equipped)
        {
            return new Row(ItemName(item), () => Item(item),
                item == NativeAppearance.BaseItem ? "Choose the character's appearance."
                : equipped ? "Equipped. Choose another look or move it into place."
                : "Change this item's look, or equip it from its menu.")
                { Text = () => ItemName(item) + (item == NativeAppearance.BaseItem ? "" : NativeAppearance.Worn().Contains(item) ? " [On]" : EquipmentService.Available(item) ? " [Off]" : " [Unavailable]"),
                    Summary = () => (draft.MaterialFor(item) == MaterialKind.Original ? "" : MaterialBaker.Name(draft.MaterialFor(item)) + " | ")
                    + ChoiceLabel(draft.Choice(item)), PreviewItem = item };
        }
        private static string ItemName(int item)
        { return item == NativeAppearance.BaseItem ? "Character" : System.Text.RegularExpressions.Regex.Replace(((Items)item).ToString(), "([a-z])([A-Z])", "$1 $2"); }
        private string ChoiceLabel(AppearanceChoice choice)
        {
            if (choice.Mode == ChoiceMode.Inherit) return string.IsNullOrEmpty(draft.ParentId) ? "Map appearance" : "From collection: " + draft.ParentName;
            if (choice.Mode == ChoiceMode.LevelDefault) return "Map appearance";
            if (choice.Mode == ChoiceMode.OriginalGame) return "Original game";
            var source = Controller.Catalog.Find(choice.SourceId, choice.Item);
            return source == null ? "Missing: " + choice.Label : source.Name;
        }
        private void Parents()
        {
            var rows = new List<Row> { new Row("No collection", () => SetParent("", "")) };
            rows.AddRange(Controller.Catalog.Sets.OrderBy(x => x.Name).Select(s => new Row(s.Name, () => SetParent(s.Id, s.Name))));
            Show("COLLECTION", rows, "Choose a collection. Individual items can use other skins.");
        }
        private void SetParent(string id, string name)
        {
            draft.ParentId = id; draft.ParentName = name;
            if (!Controller.Data.KeepCustomizations) draft.Choices.Clear();
            Changed();
        }
        private void Item(int item)
        {
            Sources(item);
        }
        private void Sources(int item)
        {
            Show(ItemName(item).ToUpperInvariant(), SourceRows(item, ""), "Changes are saved immediately. Equip the item to wear it.");
            var state = page; state.RefreshRows = () => SourceRows(item, state.Query); page.PreviewItem = item;
        }
        private List<Row> SourceRows(int item, string query)
        {
            var rows = new List<Row>();
            if (item != NativeAppearance.BaseItem) rows.Add(new Row("Equipment", () => Controller.ToggleEquipment(item), "Uses the game's normal equipment slots and effects.")
                { Text = () => !EquipmentService.Available(item) ? "Unavailable in inventory" : NativeAppearance.Worn().Contains(item) ? "Unequip" : "Equip" });
            rows.Add(SearchRow());
            rows.AddRange(Controller.Choices(item, draft).Where(x => (x.Mode == ChoiceMode.Inherit || x.Mode == ChoiceMode.Source) && Matches(x.Label,query))
                .Select(choice => SourceRow(choice, false)));
            rows.Add(new Row("Move", () => { fitItem = item; allPoses = true; BeginFit(); }, "Adjust this item's position with the arrow buttons."));
            rows.Add(new Row("Material", () => Materials(item), "Apply a surface to this item, keeping its shape and shading.")
                { Text = () => "Material: " + MaterialBaker.Name(draft.MaterialFor(item)) });
            rows.Add(new Row("More options", () => ItemOptions(item), "Original art, pose-specific positions and randomizer lock."));
            return rows;
        }
        private Row SourceRow(AppearanceChoice choice, bool advanced)
        {
            return new Row(choice.Label, () => {
                var selected = choice.Copy(); selected.Locked = draft.Choice(choice.Item).Locked;
                draft.Set(selected); Changed();
            }, choice.Mode == ChoiceMode.Source ? "Use this skin immediately."
                : choice.Mode == ChoiceMode.LevelDefault ? "Use this map's artwork instead of the collection."
                : choice.Mode == ChoiceMode.OriginalGame ? "Use the original game's artwork instead of the map or collection."
                : "Use the collection's look, or the map's appearance when no collection provides it.")
            { SourceId = choice.SourceId, Text = () => (draft.Choice(choice.Item).Mode == choice.Mode && draft.Choice(choice.Item).SourceId == choice.SourceId ? "> " : "")
                + (Controller.Data.Favorites.Contains(choice.SourceId) ? "* " : "") + ChoiceLabel(choice) };
        }
        private void ItemOptions(int item)
        {
            var rows = Controller.Choices(item, draft).Where(x => x.Mode == ChoiceMode.LevelDefault || x.Mode == ChoiceMode.OriginalGame)
                .Select(x => SourceRow(x, true)).ToList();
            rows.Add(new Row("Pose-specific positioning", () => { allPoses = false; FitOptions(item); }));
            rows.Add(new Row("Randomizer lock", () => { var value = draft.Choice(item).Copy(); value.Locked = !value.Locked; draft.Set(value); Changed(); })
                { Text = () => draft.Choice(item).Locked ? "Randomizer lock: On" : "Randomizer lock: Off" });
            Show("MORE OPTIONS", rows, "Optional settings for " + ItemName(item) + "."); page.PreviewItem = item;
        }
        private void Materials(int? item)
        {
            var rows = new List<Row>();
            if (item.HasValue) rows.Add(new Row("Use outfit material", () => { draft.SetMaterial(item.Value, null); Changed(); },
                "Follow the material selected for the whole outfit.")
                { Text = () => (draft.Materials.Any(x => x.Item == item.Value) ? "" : "* ") + "Use outfit material" });
            foreach (MaterialKind value in Enum.GetValues(typeof(MaterialKind)))
            {
                MaterialKind kind = value;
                rows.Add(new Row(MaterialBaker.Name(kind), () => {
                    if (item.HasValue) draft.SetMaterial(item.Value, kind); else draft.Material = kind;
                    Changed();
                }, kind == MaterialKind.Cosmic ? "A white-edged window into animated galaxies and stars, anchored to the map."
                    : kind == MaterialKind.Diamond ? "Clear glass with translucent facets and bright edges."
                    : kind == MaterialKind.Gold ? "Polished gold with warm shadows and sharp highlights."
                    : kind == MaterialKind.RedVelvet ? "Rich magenta fabric with soft folds and a fine velvet nap."
                    : "Keep the source artwork's original surface.")
                    { Text = () => ((item.HasValue ? draft.Materials.Any(x => x.Item == item.Value && x.Kind == kind) : draft.Material == kind) ? "* " : "") + MaterialBaker.Name(kind) });
            }
            Show("MATERIAL", rows, item.HasValue ? "Material for " + ItemName(item.Value) + ". Changes are immediate."
                : "Default for the character and all items. Individual overrides take priority.");
            page.PreviewItem = item ?? NativeAppearance.BaseItem;
        }
        private void FitOptions(int item)
        {
            fitItem = item;
            var rows = new List<Row> {
                new Row("Move", BeginFit, "Each step changes the outfit. Back keeps the position; Undo reverses it."),
                new Row(allPoses ? "Scope: all poses" : "Scope: this pose", () => { allPoses = !allPoses; Back(); FitOptions(item); }),
                new Row("Select pose", Poses),
                new Row("Flip facing direction", () => preview.Flipped = !preview.Flipped),
                new Row("Reset this adjustment", () => { var fit = CurrentFit(); draft.Fits.RemoveAll(x => x.BaseId == fit.BaseId && x.SourceId == fit.SourceId && x.Item == fit.Item && x.Group == fit.Group && x.Frame == fit.Frame); Changed(); }),
                new Row("Reset all fits for this pair", () => {
                    if (preview.Prepared == null) return;
                    string baseId = preview.Prepared.Resolved[NativeAppearance.BaseItem].Id, source = preview.Prepared.Resolved[item].Id;
                    draft.Fits.RemoveAll(x => x.BaseId == baseId && x.SourceId == source && x.Item == item);
                    Changed();
                }) };
            Show("POSITION FITTING", rows, "Offsets are saved for this body + item source. Range: +/-32 px.");
            page.PreviewItem = item;
        }
        private void BeginFit()
        {
            if (previewDirty) { preview.FollowActive(); previewDirty = false; }
            if (preview.Prepared == null || preview.Error.Length > 0) throw new InvalidOperationException("Preview is unavailable: " + preview.Error);
            fitGesture = Guid.NewGuid().ToString("N");
            fitting = true;
        }
        private FitAdjustment CurrentFit()
        {
            if (preview.Prepared == null) throw new InvalidOperationException("Preview is unavailable");
            var pose = preview.CurrentPose(false);
            string baseId = preview.Prepared.Resolved[NativeAppearance.BaseItem].Id, source = preview.Prepared.Resolved[fitItem].Id;
            int group = allPoses ? -1 : pose.Group, frame = allPoses ? -1 : pose.Frame;
            var fit = draft.Fit(baseId, source, fitItem, group, frame).Copy(); fit.Group = group; fit.Frame = frame;
            return fit;
        }
        private UiSound? UpdateFit(UiInput input)
        {
            if (input.Cancel || input.Confirm) { Controller.EndGesture(); fitting = false; tryOnItem = NativeAppearance.BaseItem; return input.Cancel ? UiSound.Back : UiSound.Confirm; }
            if (input.Secondary) { preview.Flipped = !preview.Flipped; return UiSound.Change; }
            if (!input.Left && !input.Right && !input.Up && !input.Down && input.Action != UiAction.Reset)
            { Controller.EndGesture(); fitGesture = Guid.NewGuid().ToString("N"); return null; }
            var fit = CurrentFit();
            Point point = input.Action == UiAction.Reset ? Point.Zero : FitBaker.Nudge(new Point(fit.X, fit.Y), input.Right ? 1 : input.Left ? -1 : 0,
                input.Down ? 1 : input.Up ? -1 : 0, preview.Flipped);
            if (fit.X == point.X && fit.Y == point.Y) return null;
            fit.X = point.X; fit.Y = point.Y; draft.SetFit(fit); Changed(); return UiSound.Change;
        }
        private void Poses()
        {
            Show("PREVIEW POSE", preview.Poses.Select((pose, i) => { int index = i; return new Row(pose.Name, () => { preview.PoseIndex = index; Back(); }); }).ToList(),
                "Includes movement and every native ending frame.");
        }
        private void PreviewControls()
        {
            Show("PREVIEW", new List<Row> {
                new Row("Choose pose", Poses), new Row("Flip facing", () => preview.Flipped = !preview.Flipped),
                new Row("Toggle checkerboard", () => checkerboard = !checkerboard),
                SettingRow("Animate", () => Controller.Data.Animate, data => data.Animate = !data.Animate, false),
                new Row("Undo last change", Controller.Undo)
            }, "Pose, facing and background affect the preview only.");
        }
        private void Tools()
        {
            Show("MORE", new List<Row> {
                new Row("Outfit material", () => Materials(null), "Choose a material for the character and all clothing.") { Text = () => "Outfit material: " + MaterialBaker.Name(draft.Material) },
                new Row("Restore normal appearance", Controller.RestoreOriginal, "Use native artwork. Your saved outfits are kept; editing a look enables it again."),
                new Row("Undo", Controller.Undo) { Text = () => Controller.CanUndo ? "Undo" : "Undo (empty)" },
                new Row("Redo", Controller.Redo) { Text = () => Controller.CanRedo ? "Redo" : "Redo (empty)" },
                new Row("Preview options", PreviewControls),
                new Row("Use collection as-is", () => { draft.Choices.Clear(); Changed(); }),
                new Row("Randomize from favorites", () => { draft = Resolver.Randomize(draft, Controller.Catalog,
                    new[] { NativeAppearance.BaseItem }.Concat(EquipmentService.Settings().skins.Select(x => (int)x.item)), Controller.Data.Favorites, new Random()); Changed(); }),
                new Row("Import outfit recipe", Recipes),
                new Row("Export current look", () => { Controller.Flush(); Controller.Store.Export(Controller.Snapshot()); Controller.Status = "Recipe saved in Content/WardrobePlus/Recipes"; }),
                new Row("Clear this map's preference", () => Controller.Request(data => data.Maps.RemoveAll(x => x.MapId == NativeAppearance.MapId()), false, "Map preference cleared")),
                new Row("Refresh installed sources", () => { Controller.RefreshCatalog(); Controller.ContentChanged(); Controller.Status = "Sources refreshed"; }),
                new Row("Write diagnostics", Diagnostics),
                SettingRow("Keep item choices on collection change", () => Controller.Data.KeepCustomizations, data => data.KeepCustomizations = !data.KeepCustomizations, false)
            }, "Optional tools. Select an item on the main screen to change its look.");
        }
        private void Recipes()
        {
            string folder = Path.Combine(Controller.Store.DirectoryPath, "Recipes"); Directory.CreateDirectory(folder);
            var rows = Directory.GetFiles(folder, "*.xml").OrderBy(x => x).Select(file => new Row(Path.GetFileNameWithoutExtension(file), () => {
                Controller.LoadPreset(Controller.Store.Import(file)); history.Clear(); Root(false);
            })).ToList();
            if (rows.Count == 0) rows.Add(new Row("No recipes found", delegate { }));
            Show("IMPORT RECIPE", rows, "Place recipe XML files in Content/WardrobePlus/Recipes. No automatic subscriptions.");
        }
        private void Diagnostics()
        {
            Directory.CreateDirectory(Controller.Store.DirectoryPath);
            var lines = new List<string> { "Wardrobe+ " + typeof(WardrobePage).Assembly.GetName().Version, "Hooks: " + Hooks.Installed, "Error: " + Controller.LastError, "Map: " + NativeAppearance.MapId(), "Revision: " + AppearanceEvents.Revision };
            if (preview.Prepared != null) lines.AddRange(preview.Prepared.Resolved.Select(x => ((Items)x.Key) + ": " + x.Value.Label + " | " + x.Value.Warning));
            lines.AddRange(Controller.Catalog.Problems);
            File.WriteAllLines(Path.Combine(Controller.Store.DirectoryPath, "diagnostics.txt"), lines);
            Controller.Status = "Diagnostics saved";
        }
        private static Row SettingRow(string label, Func<bool> value, Action<WardrobeData> change, bool appearance)
        { return new Row(label, () => Controller.Request(change, appearance, label + " updated")) { Text = () => label + ": " + OnOff(value()) }; }
        private static string OnOff(bool value) { return value ? "On" : "Off"; }
        private void Name(string title, string initial, Action<string> accept, bool allowEmpty = false)
        {
            CloseText();
            textEntry = new UiTextEntryPage(title, initial, accept, 64, allowEmpty);
            textEntry.OnOpen();
        }
        private void CloseText()
        { if (textEntry != null) textEntry.OnClose(); textEntry = null; }
        private static bool Matches(string text, string query) { return string.IsNullOrWhiteSpace(query) || (text ?? "").IndexOf(query,StringComparison.OrdinalIgnoreCase) >= 0; }
        private Row SearchRow()
        {
            return new Row("Search", () => { var target = page; Name("SEARCH",target.Query,value => { target.Query = value; target.Refresh(); },true); })
                { Text = () => string.IsNullOrEmpty(page.Query) ? "Search..." : "Search: " + page.Query };
        }
        private bool HistoryShortcut()
        {
            var keys = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            bool ctrl = keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) || keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl);
            if (!ctrl) return false;
            if (keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Z) && !shortcutKeys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Z)) { Controller.EndGesture(); Controller.Undo(); return true; }
            if (keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Y) && !shortcutKeys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Y)) { Controller.EndGesture(); Controller.Redo(); return true; }
            return false;
        }
        public void Update(UiInput input, float delta)
        {
            preview.Tick(delta);
            UiSound? feedback = null;
            try
            {
                if (textEntry != null) { textEntry.Update(input, delta); if (textEntry.WantsClose) CloseText(); }
                else if (HistoryShortcut()) { feedback = UiSound.Change; }
                else if (fitting) feedback = UpdateFit(input);
                else if (input.Cancel) { Back(); feedback = UiSound.Back; }
                else if (page.Main && input.Left) { Presets(); feedback = UiSound.Confirm; }
                else if (page.Main && input.Right) { Tools(); feedback = UiSound.Confirm; }
                else if (page.Main && input.Secondary) { SavePreset(); feedback = UiSound.Confirm; }
                else if (input.Up && page.Rows.Count > 0) { SelectRow((page.Index + page.Rows.Count - 1) % page.Rows.Count); page.Viewport.FollowSelection(page.Index, page.Rows.Count, VisibleRows); }
                else if (input.Down && page.Rows.Count > 0) { SelectRow((page.Index + 1) % page.Rows.Count); page.Viewport.FollowSelection(page.Index, page.Rows.Count, VisibleRows); }
                else if (input.Confirm && page.Rows.Count > 0) { page.Rows[page.Index].Select(); feedback = UiSound.Confirm; }
                else if (input.Secondary && page.Rows.Count > 0)
                {
                    string id = page.Rows[page.Index].SourceId;
                    if (!string.IsNullOrEmpty(id) && !id.StartsWith("preset:")) { Controller.Request(data => { if (!data.Favorites.Remove(id)) data.Favorites.Add(id); }, false, "Source favorite updated"); feedback = UiSound.Change; }
                }
            }
            catch (Exception error) { Controller.Status = error.GetBaseException().Message; feedback = UiSound.Error; }
            shortcutKeys = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            Controller.Pump();
            if (feedback.HasValue) UiSounds.Play(Controller.LastError.Length > 0 ? UiSound.Error : feedback.Value);
            draft = Controller.Data.Current.Copy();
            if (!ReferenceEquals(observedData, Controller.Data))
            {
                observedData = Controller.Data;
                page.Refresh(); foreach (var previous in history) previous.Refresh();
                previewDirty = true;
            }
            int nextTryOn = NativeAppearance.BaseItem;
            if (nextTryOn != tryOnItem) { tryOnItem = nextTryOn; previewDirty = true; }
            page.Refresh();
            preview.FollowActive(); previewDirty = false;
        }
        private void SelectRow(int next)
        { if (page.Index == next) return; page.Index = next; UiSounds.Play(UiSound.Move); }
    }
}
