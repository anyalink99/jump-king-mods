using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.Workshop;

namespace WardrobePlus
{
    public static class AppearanceEvents
    {
        public static int Revision { get; internal set; }
        public static event Action Changed;
        internal static void Notify()
        {
            Revision++;
            foreach (Action callback in Changed == null ? new Delegate[0] : Changed.GetInvocationList())
                try { callback(); } catch (Exception error) { Console.WriteLine("[Wardrobe+] Appearance observer: " + error); }
        }
    }
    internal static partial class Controller
    {
        internal static Store Store;
        internal static WardrobeData Data;
        internal static Catalog Catalog;
        internal static PreparedAppearance Active;
        internal static string LastError = "";
        internal static string Status = "";
        private static readonly Queue<Action> pending = new Queue<Action>();
        private static bool initialized;
        private static bool catalogDirty;
        private static bool catalogReady;
        private static string map = "";
        private static string materialEquipmentAttempt;
        internal static string ActiveMap { get { return map; } }
        private static readonly List<PreparedAppearance> retired = new List<PreparedAppearance>();
        internal static bool Enabled { get { return initialized && Data.Enabled && Hooks.Installed; } }
        internal static bool Pending { get { return pending.Count != 0 || liveChange != null; } }
        internal static void Ensure()
        {
            if (initialized && Hooks.Installed) return;
            if (!initialized)
            {
            Store = new Store(Path.Combine(Path.GetDirectoryName(typeof(Game1).Assembly.Location), "Content", "WardrobePlus"));
            Data = Store.Load(); LastError = Store.Warning;
            Catalog = Catalog.Discover();
            NativeAppearance.Validate();
            initialized = true;
            }
            Hooks.Install();
            if (!Hooks.Installed) LastError = Hooks.Error;
            else LastError = Store.Warning;
            Menus.Sync();
            // Native assets precede mod discovery on the first process load. Apply now,
            // before JumpGame constructs behavior-tree nodes from the loaded sprites.
            if (Enabled && Game1.instance != null && Game1.instance.contentManager.playerSprites._CurrentSprites != null) Load(false);
        }
        internal static void RefreshCatalog() { Catalog = Catalog.Discover(); }
        internal static void ContentChanged() { if (initialized) catalogDirty = true; }
        internal static void ContentParsed() { catalogReady = true; ContentChanged(); }
        internal static Outfit NativeOutfit()
        {
            var result = new Outfit { Name = "Imported native appearance" };
            if (WorkshopManager.instance == null) return result;
            var sets = WorkshopManager.instance.collections.Where(x => x.Info.enabled).ToList();
            var singles = WorkshopManager.instance.reskins.Where(x => x.Info.enabled).ToList();
            var first = sets.FirstOrDefault();
            if (first != null) { result.ParentId = Catalog.PackageId(first); result.ParentName = ((IUGC)first).Name ?? result.ParentId; }
            // Preserve the actual native precedence, including unusual pre-existing mixed flags.
            foreach (int target in new[] { NativeAppearance.BaseItem }.Concat(NativeAppearance.Settings().skins.Select(x => (int)x.item)))
            {
                var single = singles.Find(x => (int)x.Info.skin == target);
                var set = sets.Find(x => x.Contains((Items)target));
                IUGC source = target == NativeAppearance.BaseItem ? (IUGC)single ?? set : (IUGC)set ?? single;
                if (source == null) continue;
                // Parent entries must remain inherited so a subsequent collection
                // change can replace them without clearing genuine overrides.
                if (ReferenceEquals(source, first)) continue;
                string id = Catalog.SourceId(source, target, source is Collection);
                var entry = Catalog.Find(id, target);
                if (entry != null) result.Set(new AppearanceChoice { Item = target, Mode = ChoiceMode.Source, SourceId = id, Label = entry.Name });
            }
            return result;
        }
        internal static bool Load(bool reload)
        {
            Ensure();
            if (!Enabled) return false;
            RefreshCatalog();
            // Steam's initial subscription query finishes after mod discovery. Import only
            // after that result (or already populated offline sources), never an empty interim list.
            if (!Data.ImportedNative && !catalogReady && Catalog.Sources.Count == 0) return false;
            var candidate = Data.Copy();
            if (!candidate.ImportedNative)
            {
                candidate.Current = NativeOutfit(); candidate.ImportedNative = true;
            }
            string currentMap = NativeAppearance.MapId();
            EquipmentSelection equipment = null;
            if (map != currentMap)
            {
                var assignment = candidate.Maps.Find(x => x.MapId == currentMap);
                var preset = assignment == null ? null : candidate.Presets.Find(x => x.Id == assignment.OutfitId);
                if (preset != null) { candidate.Current = preset.Copy(); equipment = preset.Equipment; }
            }
            var prepared = PreparedAppearance.Build(candidate.Current, Catalog, reload);
            bool saved = false;
            var oldOptions = EquipmentService.SnapshotOptions();
            var oldEquipment = equipment == null ? null : EquipmentService.Capture();
            try
            {
                if (!Store.ReadOnly && (!Data.ImportedNative || map != currentMap)) { Store.Save(candidate); saved = true; }
                if (equipment != null) Status = EquipmentService.Restore(equipment);
                NativeAppearance.Publish(prepared);
            }
            catch (Exception failure)
            {
                var failures = new List<Exception> { failure };
                try { prepared.Dispose(); } catch (Exception error) { failures.Add(error); }
                if (equipment != null)
                {
                    try { EquipmentService.Restore(oldEquipment); } catch (Exception error) { failures.Add(error); }
                    try { EquipmentService.RestoreOptions(oldOptions); } catch (Exception error) { failures.Add(error); }
                }
                try { if (saved) Store.Save(Data); } catch (Exception error) { failures.Add(error); }
                if (failures.Count == 1) throw;
                throw new AggregateException("Appearance load failed; some rollback operations failed", failures);
            }
            if (map != currentMap) { undo.Clear(); redo.Clear(); historyKey = null; if (map.Length > 0) historyInitialized = true; }
            ReplaceActive(prepared); Data = candidate; map = currentMap;
            catalogDirty = false;
            AppearanceEvents.Notify();
            return true;
        }
        internal static void Request(Action<WardrobeData> change, bool appearance, string message)
        {
            QueueLive();
            pending.Enqueue(() => Commit(change, appearance, message, null, null, appearance));
        }
        internal static void Apply(Outfit outfit)
        {
            Edit(outfit);
        }
        internal static void RestoreOriginal()
        { Request(data => data.Enabled = false, true, "Normal appearance restored. Your outfits are saved."); }
        internal static void Undo()
        {
            NavigateHistory(false);
        }
        internal static void Pump()
        {
            if (!initialized) return;
            QueueLive();
            if (!Pending && Enabled && Active != null && Active.HasRefraction)
            {
                var worn = NativeAppearance.Worn();
                string key = string.Join(",", worn);
                // A failed rebuild must not retry/log every frame. A different
                // equipment selection or an explicit source refresh can retry.
                if (!Active.MaterialEquipment.SequenceEqual(worn) && key != materialEquipmentAttempt)
                { materialEquipmentAttempt = key; catalogDirty = true; }
            }
            if (!Pending && catalogDirty)
            {
                catalogDirty = false;
                try { RefreshCatalog(); if (Enabled) Load(false); }
                catch (Exception error) { LastError = error.GetBaseException().Message; Status = LastError; }
            }
            while (Pending)
            {
                var action = pending.Dequeue();
                try { action(); }
                catch (Exception error) { LastError = error.GetBaseException().Message; Status = LastError; Console.WriteLine("[Wardrobe+] " + error); }
            }
            if (Unsaved && System.Diagnostics.Stopwatch.GetTimestamp() >= saveAfter) FlushSave();
        }
        private static void ReplaceActive(PreparedAppearance prepared)
        {
            if (Active != null) retired.Add(Active);
            Active = prepared;
            materialEquipmentAttempt = null;
        }
        internal static void AfterDraw()
        {
            CosmicRenderer.EndFrame();
            // Keep one retired generation until consumers have observed the new sprite identity.
            while (retired.Count > 1) { retired[0].Dispose(); retired.RemoveAt(0); }
        }
        internal static void Release()
        {
            Flush();
            CrystalRenderer.Release();
            CosmicRenderer.Release();
            pending.Clear(); liveChange = null;
            foreach (var item in retired) item.Dispose(); retired.Clear();
            // Active sprites may still be referenced by the title/ending screen; retire on replacement.
        }
        internal static List<AppearanceChoice> Choices(int item, Outfit outfit = null)
        {
            var values = new List<AppearanceChoice> {
                new AppearanceChoice { Item = item, Label = "Inherit" },
                new AppearanceChoice { Item = item, Mode = ChoiceMode.LevelDefault, Label = "Level default" },
                new AppearanceChoice { Item = item, Mode = ChoiceMode.OriginalGame, Label = "Original game" } };
            values.AddRange(Catalog.ForItem(item).OrderByDescending(x => Data.Favorites.Contains(x.Id)).ThenBy(x => x.Collection).ThenBy(x => x.Name)
                .Select(x => new AppearanceChoice { Item = item, Mode = ChoiceMode.Source, SourceId = x.Id, Label = x.Name }));
            var choice = (outfit ?? Data.Current).Choice(item);
            if (choice.Mode == ChoiceMode.Source && values.All(x => x.SourceId != choice.SourceId)) { var missing = choice.Copy(); missing.Label = "Missing: " + missing.Label; values.Add(missing); }
            return values;
        }
        internal static string CollectionStatus(Collection set)
        {
            string id = Catalog.PackageId(set);
            int count = Data.Current.Choices.Count(x => x.Mode == ChoiceMode.Source && x.SourceId.StartsWith(id + ":set:", StringComparison.Ordinal));
            return Data.Current.ParentId == id ? "Parent collection" : count > 0 ? "Used for " + count + " items" : "Use as parent";
        }
    }
}
