using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.SaveThread;
using JumpKing.SaveThread.SaveComponents;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void EquipmentDataTests()
    {
        var store = new Store(Path.Combine(output,"equipment-data"));
        var legacy = new Outfit { Name = "Legacy" };
        var empty = new Outfit { Name = "Empty", Equipment = new EquipmentSelection() };
        var full = new Outfit { Name = "Космический король", Equipment = new EquipmentSelection { Items = new List<int> { (int)Items.Cap } } };
        store.Save(new WardrobeData { Presets = new List<Outfit> { legacy,empty,full } });
        var data = store.Load();
        Check(data.Presets[0].Equipment == null && data.Presets[1].Equipment.Items.Count == 0 && data.Presets[2].Equipment.Items.SequenceEqual(full.Equipment.Items),
            "Storage distinguishes legacy, empty and populated equipment snapshots");
        Check(store.Import(store.Export(full)).Name == full.Name && store.Import(store.Export(full)).Equipment.Items.Contains((int)Items.Cap),
            "Recipes preserve Unicode names and equipment");
        var copy = data.Copy(); copy.Presets[2].Equipment.Items.Clear();
        Check(data.Presets[2].Equipment.Items.Count == 1,"Copied preset equipment cannot alias the saved selection");
        data.Current.Equipment = new EquipmentSelection { Items = new List<int> { 1,1 } };
        Reject(() => Store.Validate(data),"Duplicate equipment is rejected before restoration");
    }
    private static void LiveEquipmentTests()
    {
        var saved = Controller.Snapshot(); var options = EquipmentService.SnapshotOptions();
        int cap = (int)Items.Cap, shoes = (int)Items.Shoes;
        if (!NativeAppearance.Worn().Contains(cap)) { Controller.ToggleEquipment(cap); Controller.Pump(); }
        var active = Controller.Active;
        Controller.ToggleEquipment(cap); Controller.ToggleEquipment(shoes); Controller.ToggleEquipment(shoes); Controller.Pump();
        Check(!NativeAppearance.Worn().Contains(cap) && !ItemEquipOptions.IsItemEnabled(Items.Cap),"Menu unequip updates native layers and equipment settings");
        Check(ReferenceEquals(active,Controller.Active),"Equipment toggles reuse prepared material textures");
        Controller.ToggleEquipment(cap); Controller.Pump();
        Check(NativeAppearance.Worn().Contains(cap) && ItemEquipOptions.IsItemEnabled(Items.Cap),"Menu equip follows native item enable logic");
        Controller.Undo(); Controller.Pump(); Check(!NativeAppearance.Worn().Contains(cap),"Undo restores removed equipment");
        Controller.Redo(); Controller.Pump(); Check(NativeAppearance.Worn().Contains(cap),"Redo restores equipped items");
        var cache = (Dictionary<string,object>)typeof(Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube",true).GetField("loaded_objects",Flags).GetValue(null);
        var inventory = (Inventory)cache["SavesPermainventory.inv"];
        inventory.items.Add(new InventoryItem { item = Items.Crown, count = 1 });
        Controller.ToggleEquipment((int)Items.Crown); Controller.Pump();
        Check(NativeAppearance.Worn().Contains((int)Items.Crown) && !NativeAppearance.Worn().Contains(cap),"Equipping a competing hat follows native slot conflicts");
        Controller.Undo(); Controller.Pump();
        Check(NativeAppearance.Worn().Contains(cap) && !NativeAppearance.Worn().Contains((int)Items.Crown),"Undo restores the previous occupant of a conflicted slot");
        inventory.items.RemoveAll(i => i.item == Items.Crown);
        var full = Controller.Snapshot(); full.Name = "Full preset"; full.Material = MaterialKind.Gold;
        var empty = full.Copy(); empty.Equipment.Items.Clear();
        Controller.LoadPreset(empty); Controller.Pump();
        Check(NativeAppearance.Worn().Count == 0 && Controller.Data.Current.Material == MaterialKind.Gold,"Empty preset removes all clothing and restores its appearance");
        Controller.LoadPreset(full); Controller.Pump();
        Check(NativeAppearance.Worn().Contains(cap),"Complete preset restores the saved equipment");
        var legacy = full.Copy(); legacy.Equipment = null; legacy.Material = MaterialKind.Original;
        Controller.LoadPreset(legacy); Controller.Pump();
        Check(NativeAppearance.Worn().Contains(cap),"Legacy preset leaves native equipment unchanged");
        int unavailable = NativeAppearance.Settings().skins.Where(s => !InventoryManager.HasItem(s.item)).Select(s => (int)s.item).First();
        var missing = full.Copy(); missing.Equipment.Items.Add(unavailable);
        Controller.LoadPreset(missing); Controller.Pump();
        Check(!InventoryManager.HasItem((Items)unavailable) && !NativeAppearance.Worn().Contains(unavailable) && Controller.Status.Contains("Unavailable"),
            "Unavailable preset items are reported without granting inventory");
        Controller.Store.ReadOnly = true; var before = EquipmentService.Capture();
        Controller.ToggleEquipment(cap); Controller.Pump(); Controller.Store.ReadOnly = false;
        Check(EquipmentService.Capture().Items.SequenceEqual(before.Items),"Persistence failure leaves native equipment unchanged");
        var next = Controller.Data.Current.Copy(); next.Material = MaterialKind.Original;
        Controller.Edit(next); Controller.Request(d => d.KeepCustomizations = false,false,"Metadata saved");
        Controller.ToggleEquipment(cap); Controller.ToggleEquipment(cap); Controller.Pump();
        Check(Controller.Data.Current.Material == MaterialKind.Original && !Controller.Data.KeepCustomizations && NativeAppearance.Worn().Contains(cap),
            "Rapid appearance, metadata and equipment actions are processed in order without loss");
        using (var preview = new Preview())
        {
            preview.FollowActive(); Check(ReferenceEquals(preview.Prepared,Controller.Active),"Live preview borrows the published prepared generation");
        }
        Check(!Controller.Active.BaseTexture.IsDisposed,"Closing a borrowing preview keeps live textures alive");
        // Named baseline save and preparation stages make persistence cost visible.
        var watch = Stopwatch.StartNew(); for (int i=0;i<20;i++) Controller.Store.Save(Controller.Data); watch.Stop();
        double saveMs = watch.Elapsed.TotalMilliseconds/20;
        watch.Restart();
        var fitOutfit = Controller.Data.Current.Copy();
        for (int i=0;i<20;i++) {
            fitOutfit.SetFit(new FitAdjustment { BaseId=Controller.Active.Resolved[NativeAppearance.BaseItem].Id,
                SourceId=Controller.Active.Resolved[cap].Id,Item=cap,X=i%10 });
            Controller.Edit(fitOutfit,"performance-gesture");
        }
        Controller.Pump(); watch.Stop();
        Check(Controller.Unsaved && Controller.Data.Current.Fits.Any(f => f.Item == cap && f.X == 9),"Coalesced fit publishes the final position and marks pending durability");
        Controller.EndGesture(); Check(!Controller.Unsaved,"Gesture end flushes the final state");
        Controller.Edit(fitOutfit,"failed-save-gesture"); Controller.Pump(); Controller.Store.ReadOnly = true;
        Check(!Controller.Flush() && Controller.Unsaved && Controller.Status.Contains("Not saved"),"Deferred save failure retains live state with a visible durability error");
        Controller.Store.ReadOnly = false; Check(Controller.Flush() && !Controller.Unsaved,"Retry saves retained live changes after a transient failure");
        var resourceOutfit = new Outfit(); resourceOutfit.SetMaterial(cap,MaterialKind.Gold);
        var resourceSource = PreparedAppearance.Build(resourceOutfit,Controller.Catalog,false);
        var materialTexture = NativeAppearance.Frames(resourceSource.PreviewItems[Items.Cap].regular)[0].texture;
        resourceOutfit.SetFit(new FitAdjustment { BaseId=resourceSource.Resolved[NativeAppearance.BaseItem].Id,SourceId=resourceSource.Resolved[cap].Id,Item=cap,X=2 });
        var firstFit = resourceSource.Refit(resourceOutfit); var secondFit = firstFit.Refit(resourceOutfit);
        resourceSource.Dispose(); firstFit.Dispose();
        Check(!materialTexture.IsDisposed && ReferenceEquals(secondFit.PreviewItems[Items.Cap],resourceSource.PreviewItems[Items.Cap]),"Refitting retains shared materials after earlier generations retire");
        secondFit.Dispose(); Check(materialTexture.IsDisposed,"Last fitted generation releases its shared material resources");
        File.WriteAllText(Path.Combine(output,"live-edit-timing.txt"),"Store.Save mean (20): " + saveMs.ToString("F3") + " ms\n20 queued fits + one prepare/publish: " + watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms\n");
        var mapPreset = empty.Copy(); mapPreset.Id = Guid.NewGuid().ToString("N");
        Controller.Request(d => { d.Presets.Add(mapPreset); d.Maps.RemoveAll(m => m.MapId == "local-map:custom-map"); d.Maps.Add(new MapOutfit { MapId="local-map:custom-map",OutfitId=mapPreset.Id }); },false,"Map assigned"); Controller.Pump();
        string nativeRoot = Game1.instance.contentManager.root;
        Game1.instance.contentManager.root = Path.Combine(Directory.GetCurrentDirectory(),"custom-map");
        Controller.Load(true); Check(NativeAppearance.Worn().Count == 0,"Map assignment restores preset equipment on entry");
        Game1.instance.contentManager.root = nativeRoot; Controller.Load(true);
        Controller.LoadPreset(saved); Controller.Pump(); EquipmentService.RestoreOptions(options); NativeAppearance.Publish(Controller.Active);
        Controller.LastError = Controller.Status = "";
    }
}
