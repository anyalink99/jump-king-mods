using System;
using System.Collections;
using System.IO;
using System.Linq;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.Workshop;
using JKRuntime.UI;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void ReloadAndMapTests(Game1 game, WorkshopManager workshop, Collection set, Reskin single, string fixture, string king)
    {
        var saved = Controller.Data.Current.Copy();
        var previousTexture = NativeAppearance.Frames(Controller.Active.Items[Items.Cap].regular)[0].texture;
        workshop.reskins.Remove(single); Controller.ContentChanged(); Controller.Pump();
        Check(Controller.Active.Resolved[(int)Items.Cap].Warning.Contains("Missing") && Controller.Data.Current.Choice((int)Items.Cap).SourceId == saved.Choice((int)Items.Cap).SourceId,
            "A subscription disappearing falls back without erasing its saved reference");
        File.SetLastWriteTimeUtc(Path.Combine(single.Root, single.Info.name + ".xnb"), DateTime.UtcNow.AddSeconds(2));
        workshop.reskins.Add(single); Controller.ContentChanged(); Controller.Pump();
        Check(Controller.Active.Resolved[(int)Items.Cap].Id == saved.Choice((int)Items.Cap).SourceId && !ReferenceEquals(previousTexture, NativeAppearance.Frames(Controller.Active.Items[Items.Cap].regular)[0].texture),
            "Returning or refreshed Workshop content reloads the requested texture");
        var wrapper = game.contentManager.playerSprites.idle;
        var foreign = Sprite.CreateSprite(Controller.Active.BaseTexture, new Microsoft.Xna.Framework.Rectangle(0, 0, 1, 1));
        NativeAppearance.Layers(wrapper).Add(foreign);
        Controller.Apply(saved); Controller.Pump();
        Check(NativeAppearance.Layers(wrapper).Contains(foreign), "Applying an outfit preserves foreign overlay layers in the current world");

        string mapRoot = Path.Combine(fixture, "custom-map"), mapKing = Path.Combine(mapRoot, "king"); Directory.CreateDirectory(mapKing);
        File.Copy(Path.Combine(king, "skin_settings.xml"), Path.Combine(mapKing, "skin_settings.xml"));
        File.Copy(Path.Combine(king, "base.xnb"), Path.Combine(mapKing, "base.xnb"));
        File.Copy(Path.Combine(king, "crown.xnb"), Path.Combine(mapKing, "cap.xnb"));
        var mapOutfit = new Outfit { Name = "Map look" };
        mapOutfit.Set(new AppearanceChoice { Item = (int)Items.Cap, Mode = ChoiceMode.LevelDefault });
        Controller.Request(data => { data.Presets.Add(mapOutfit); data.Maps.Add(new MapOutfit { MapId = "local-map:custom-map", OutfitId = mapOutfit.Id }); }, false, "Map fixture"); Controller.Pump();
        game.contentManager.root = mapRoot;
        Controller.Load(true);
        Check(Controller.Data.Current.Id == mapOutfit.Id && Controller.Active.Resolved[(int)Items.Cap].Asset == Path.Combine(mapKing, "cap"), "Map entry selects its preset and resolves local item art");
        Check(!NativeAppearance.Layers(wrapper).Contains(foreign), "Map transitions remove overlays retained from the previous world");
        var original = mapOutfit.Copy(); original.Set(new AppearanceChoice { Item = (int)Items.Cap, Mode = ChoiceMode.OriginalGame });
        Controller.Apply(original); Controller.Pump();
        Check(Controller.Active.Resolved[(int)Items.Cap].Asset == Path.Combine(king, "cap"), "Original game selection bypasses custom-map item art");

        // Deliberately replace one native mutable wrapper with an unsupported renderer.
        // Publication must roll back persistence and leave the previous complete generation.
        var frames = NativeAppearance.Frames(game.contentManager.playerSprites._CurrentSprites.regular);
        var idle = frames[0]; var active = Controller.Active; int revision = AppearanceEvents.Revision;
        frames[0] = foreign;
        Controller.Apply(saved); Controller.Pump();
        Check(ReferenceEquals(Controller.Active, active) && AppearanceEvents.Revision == revision && Controller.Store.Load().Current.Choice((int)Items.Cap).Mode == ChoiceMode.OriginalGame,
            "Unsupported renderer publication restores persisted state and keeps the active generation");
        frames[0] = idle;
        game.contentManager.root = "Content";
        Controller.Load(false); Controller.Apply(saved); Controller.Pump();
        Controller.LastError = ""; Controller.Status = "";
        Controller.AfterDraw();
    }

    private static void PresetPageTests()
    {
        var page = new WardrobePage(); page.OnOpen(); page.Update(new UiInput(), .016f);
        typeof(WardrobePage).GetMethod("Presets", Flags).Invoke(page, null);
        var preset = Controller.Data.Current.Copy(); preset.Id = Guid.NewGuid().ToString("N"); preset.Name = "Before";
        Controller.Request(data => data.Presets.Add(preset), false, "Saved"); Controller.Pump(); page.Update(new UiInput(), .016f);
        object state = typeof(WardrobePage).GetField("page", Flags).GetValue(page);
        var rows = (IList)state.GetType().GetField("Rows", Flags).GetValue(state);
        Check(rows.Cast<object>().Any(row => (string)row.GetType().GetProperty("CurrentLabel", Flags).GetValue(row, null) == "Before"), "Newly saved presets appear in the already open list");
        typeof(WardrobePage).GetMethod("Preset", Flags).Invoke(page, new object[] { preset.Id });
        Controller.Request(data => data.Presets.Find(x => x.Id == preset.Id).Name = "After", false, "Renamed"); Controller.Pump();
        page.Update(new UiInput(), .016f);
        page.Update(new UiInput { Confirm = true, Action = UiAction.Confirm }, .016f);
        var draft = (Outfit)typeof(WardrobePage).GetField("draft", Flags).GetValue(page);
        Check(draft.Name == "After", "Loading from an open preset menu uses the latest renamed state");
        page.OnClose();
    }
    private static object PageState(WardrobePage page)
    { return typeof(WardrobePage).GetField("page", Flags).GetValue(page); }
    private static object[] PageRows(WardrobePage page)
    { return ((IList)PageState(page).GetType().GetField("Rows", Flags).GetValue(PageState(page))).Cast<object>().ToArray(); }
    private static string RowLabel(object row)
    { return (string)row.GetType().GetProperty("CurrentLabel", Flags).GetValue(row, null); }
    private static void SelectRow(WardrobePage page, Func<object, bool> match)
    {
        var rows = PageRows(page); int index = Array.FindIndex(rows, row => match(row));
        if (index < 0) throw new Exception("Expected UI action is missing");
        PageState(page).GetType().GetField("Index", Flags).SetValue(PageState(page), index);
        page.Update(new UiInput { Confirm = true, Action = UiAction.Confirm }, .016f);
    }
    private static void SimplifiedPageTests()
    {
        int item = (int)Items.Cap;
        var saved = Controller.Data.Current.Copy();
        var source = Controller.Catalog.ForItem(item).First(x => !x.Collection);
        Controller.RestoreOriginal(); Controller.Pump();
        var page = new WardrobePage(); page.OnOpen(); page.Update(new UiInput(), .016f);
        var root = PageState(page);
        SelectRow(page, row => (int)row.GetType().GetField("PreviewItem", Flags).GetValue(row) == item);
        Check(PageRows(page).Any(row => (string)row.GetType().GetField("SourceId", Flags).GetValue(row) == source.Id),
            "Opening an item immediately offers its standalone skins");
        Check((int)typeof(WardrobePage).GetField("tryOnItem", Flags).GetValue(page) == NativeAppearance.BaseItem,
            "Browsing an item keeps the mannequin on the actually equipped outfit");
        Check(!Controller.Data.Enabled, "Browsing while disabled does not enable the outfit");
        SelectRow(page, row => (string)row.GetType().GetField("SourceId", Flags).GetValue(row) == source.Id);
        Check(!ReferenceEquals(PageState(page), root), "Selecting a reskin stays in its list for rapid comparison");
        Check(Controller.Data.Enabled && Controller.Active.Resolved[item].Id == source.Id, "Source selection immediately enables and publishes the appearance");
        page.Update(new UiInput { Cancel = true, Action = UiAction.Cancel }, .016f);
        var summaryRow = PageRows(page).Single(row => (int)row.GetType().GetField("PreviewItem", Flags).GetValue(row) == item);
        Check(((Func<string>)summaryRow.GetType().GetField("Summary", Flags).GetValue(summaryRow))() == source.Name,
            "Main item summary immediately shows the chosen skin");
        Check(Controller.Data.Enabled && Controller.Store.Load().Enabled && Controller.Active.Resolved[item].Id == source.Id,
            "Source selection persists the selected outfit without Apply");
        Check(Controller.Data.Current.ParentId == saved.ParentId, "Applying one item preserves its parent collection");

        SelectRow(page, row => (int)row.GetType().GetField("PreviewItem", Flags).GetValue(row) == item);
        SelectRow(page, row => RowLabel(row) == "Move");
        Check((bool)typeof(WardrobePage).GetField("fitting", Flags).GetValue(page)
            && (bool)typeof(WardrobePage).GetField("allPoses", Flags).GetValue(page), "Move immediately starts all-pose positioning");
        page.Update(new UiInput { Right = true, Action = UiAction.Right }, .016f);
        page.Update(new UiInput { Cancel = true, Action = UiAction.Cancel }, .016f);
        page.Update(new UiInput { Cancel = true, Action = UiAction.Cancel }, .016f);
        Check(ReferenceEquals(PageState(page), root), "Fitting cancel and Back return to the same main list");
        page.Update(new UiInput { Left = true, Action = UiAction.Left }, .016f);
        Check(PageRows(page).Any(row => RowLabel(row) == "Save current look"), "Outfits shortcut opens saving without scrolling the item list");
        page.Update(new UiInput { Cancel = true, Action = UiAction.Cancel }, .016f);
        page.Update(new UiInput { Right = true, Action = UiAction.Right }, .016f);
        SelectRow(page, row => RowLabel(row) == "Restore normal appearance"); Controller.Pump();
        Check(!Controller.Data.Enabled && Controller.Data.Current.Choice(item).SourceId == source.Id,
            "Restore normal appearance disables overrides without erasing the selected outfit");
        Controller.Store.ReadOnly = true;
        Controller.Apply(saved); Controller.Pump(); Controller.Store.ReadOnly = false;
        Check(!Controller.Data.Enabled && !Controller.Store.Load().Enabled, "Failed Apply never partly enables a disabled outfit");
        page.OnClose();
        Controller.Apply(saved); Controller.Pump(); Controller.Status = ""; Controller.LastError = "";
    }
}
