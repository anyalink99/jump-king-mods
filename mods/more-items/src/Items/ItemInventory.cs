using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using JumpKing;
using JumpKing.SaveThread;

namespace MoreItems
{
    [Serializable]
    public sealed class ConsumableStack
    {
        [XmlAttribute("id")]
        public string Id { get; set; }
        [XmlAttribute("count")]
        public int Count { get; set; }
    }

    [Serializable]
    public sealed class ConsumableSave
    {
        [XmlAttribute("key")]
        public string Key { get; set; }
        [XmlArray("Items"), XmlArrayItem("Item")]
        public ConsumableStack[] Items { get; set; }
        [XmlArray("Collected"), XmlArrayItem("Pickup")]
        public string[] Collected { get; set; }
    }

    [Serializable]
    [XmlRoot("MoreItemsInventory")]
    public sealed class ConsumableSaveDatabase : ISaveable<ConsumableSaveDatabase>
    {
        [XmlAttribute("version")]
        public int Version { get; set; }
        [XmlElement("Save")]
        public ConsumableSave[] Saves { get; set; }

        public ConsumableSaveDatabase GetDefault()
        {
            return new ConsumableSaveDatabase
            {
                Version = 1,
                Saves = new ConsumableSave[0]
            };
        }
    }

    internal static class ItemInventory
    {
        private const string SaveFolder = "Saves";
        private const string SaveFile = "more_items_inventory.sav";
        private const string InventoryKey = "inventory";
        private static readonly Dictionary<string, int> Counts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Collected =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static JKRuntime.Settings.SettingsFile<ConsumableSaveDatabase> store;
        private static string storePath;
        private static ConsumableSaveDatabase database;
        private static string currentKey;
        private static bool loaded;
        private static bool available;
        private static ConsumableSaveDatabase preparedDatabase;
        private static string preparedKey;
        private static bool preparedRead;
        private static JKRuntime.Settings.SettingsFile<ConsumableSaveDatabase> preparedStore;
        private static string preparedPath;
        private static readonly JKRuntime.BackgroundWorkQueue writer = new JKRuntime.BackgroundWorkQueue("item-pickup", 1);
        internal sealed class PendingCollection
        {
            internal JKRuntime.BackgroundWork Work;
            internal ConsumableSaveDatabase Candidate;
            internal string Id, Key;
            internal int Count;
            internal bool Finished, Succeeded;
        }
        private static PendingCollection pendingCollection;

        internal static void Prepare(JKRuntime.RuntimeScope scope)
        {
            DrainCollection();
            scope.Defer(ClearPreparation);
            preparedKey = GetCurrentSaveKey();
            preparedPath = JKRuntime.Settings.NativeSaveFiles.GetPath(SaveFolder, SaveFile);
            using (JKRuntime.RuntimeApi.MeasureStartup("more-items.inventory-read")) preparedDatabase = ReadDatabase(preparedPath, out preparedStore);
            preparedRead = true;
        }
        private static void ClearPreparation() { preparedRead = false; preparedDatabase = null; preparedKey = null; preparedStore = null; preparedPath = null; }
        private static ConsumableSaveDatabase ReadDatabase(string path, out JKRuntime.Settings.SettingsFile<ConsumableSaveDatabase> result)
        {
            result = new JKRuntime.Settings.SettingsFile<ConsumableSaveDatabase>(path, delegate { return new ConsumableSaveDatabase().GetDefault(); }, ValidateDatabase);
            if (!result.CanSave) throw new InvalidDataException(result.Error);
            return result.Value;
        }
        private static void ValidateDatabase(ConsumableSaveDatabase value)
        { if (value.Version != 1) throw new InvalidDataException("Unsupported More Items inventory version: " + value.Version); }

        internal static void Reload()
        {
            DrainCollection();
            loaded = false;
            available = false;
            Counts.Clear();
            Collected.Clear();
            currentKey = GetCurrentSaveKey();
            try
            {
                storePath = JKRuntime.Settings.NativeSaveFiles.GetPath(SaveFolder, SaveFile);
                if (preparedRead && preparedKey == currentKey && preparedPath == storePath) { database = preparedDatabase; store = preparedStore; }
                else database = ReadDatabase(storePath, out store);
                ClearPreparation();
                if (database == null) database = new ConsumableSaveDatabase().GetDefault();
                ConsumableSave inventory = FindProfile(InventoryKey);
                ApplyItems(inventory == null ? null : inventory.Items);

                ConsumableSave world = FindProfile(currentKey);
                bool newRun = SaveManager.instance != null && SaveManager.instance.IsNewGame;
                ApplyCollected(GetCollectedForRun(world, newRun));

                if (newRun) Collected.Clear();
                if ((inventory == null || newRun) && !Save())
                    throw new IOException("Could not initialize the More Items inventory");
                available = true;
                loaded = true;
            }
            catch (Exception error)
            {
                ClearPreparation();
                Counts.Clear();
                Collected.Clear();
                database = null;
                loaded = true;
                Console.WriteLine("[More Items] Could not load inventory: " + Unwrap(error).Message);
            }
        }

        internal static void EnsureLoaded()
        {
            if (!loaded) Reload();
            if (!available)
                throw new InvalidOperationException(
                    "More Items inventory is unavailable; the existing save was left untouched");
        }

        internal static int GetCount(string id)
        {
            EnsureLoaded();
            int count;
            return Counts.TryGetValue(id ?? string.Empty, out count) ? count : 0;
        }

        internal static bool Add(string id, int count)
        {
            EnsureLoaded();
            if (!FinishCollection(true)) return false;
            if (string.IsNullOrWhiteSpace(id) || count <= 0) return false;
            int current = GetCount(id);
            if (current > int.MaxValue - count) return false;
            Counts[id] = current + count;
            if (Save()) return true;
            if (current == 0) Counts.Remove(id); else Counts[id] = current;
            return false;
        }

        internal static bool Remove(string id, int count)
        {
            EnsureLoaded();
            if (!FinishCollection(true)) return false;
            if (string.IsNullOrWhiteSpace(id) || count <= 0) return false;
            int current = GetCount(id);
            if (current < count) return false;
            int next = current - count;
            if (next == 0) Counts.Remove(id); else Counts[id] = next;
            if (Save()) return true;
            Counts[id] = current;
            return false;
        }

        internal static bool IsCollected(string key)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(key) && Collected.Contains(key);
        }

        internal static bool MarkCollected(string key)
        {
            EnsureLoaded();
            if (!FinishCollection(true)) return false;
            if (string.IsNullOrWhiteSpace(key) || !Collected.Add(key)) return false;
            if (Save()) return true;
            Collected.Remove(key);
            return false;
        }

        // Count and world marker are one durable transaction. A failed commit
        // restores both in-memory structures before observers can see success.
        internal static bool TryCollect(string id, int count, string key)
        {
            EnsureLoaded();
            if (!FinishCollection(true)) return false;
            if (string.IsNullOrWhiteSpace(id) || count <= 0 || string.IsNullOrWhiteSpace(key) || Collected.Contains(key)) return false;
            int current = GetCount(id);
            if (current > int.MaxValue - count) return false;
            Counts[id] = current + count;
            Collected.Add(key);
            if (Save()) return true;
            Collected.Remove(key);
            if (current == 0) Counts.Remove(id); else Counts[id] = current;
            return false;
        }

        internal static PendingCollection BeginCollect(string id, int count, string key)
        {
            EnsureLoaded();
            if (!FinishCollection(true) || store == null || string.IsNullOrWhiteSpace(id) || count <= 0
                || (key != null && Collected.Contains(key)) || GetCount(id) > int.MaxValue - count) return null;
            ClearPreparation();
            var request = new PendingCollection { Id = id, Count = count, Key = key, Candidate = CreateCandidate(id, count, key) };
            var destination = store;
            string path = storePath;
            JKRuntime.BackgroundWork work;
            if (!writer.TryEnqueue(delegate
            {
                if (!Directory.Exists(Path.GetDirectoryName(path))) throw new IOException("Native save context disappeared");
                destination.Save(request.Candidate);
            }, out work)) return null;
            request.Work = work;
            pendingCollection = request;
            return request;
        }

        internal static void PollCollection(PendingCollection request)
        { if (ReferenceEquals(request, pendingCollection)) FinishCollection(true); }

        private static bool FinishCollection(bool notify)
        {
            var request = pendingCollection;
            if (request == null) return true;
            if (!request.Work.IsCompleted) return false;
            pendingCollection = null;
            request.Finished = true;
            request.Succeeded = request.Work.State == JKRuntime.BackgroundWorkState.Succeeded;
            if (request.Succeeded)
            {
                Counts[request.Id] = GetCount(request.Id) + request.Count;
                if (request.Key != null) Collected.Add(request.Key);
                database = request.Candidate;
            }
            request.Candidate = null;
            if (request.Succeeded && notify) MoreItemsApi.NotifyInventoryChanged(request.Id);
            return true;
        }

        private static void DrainCollection()
        {
            if (pendingCollection == null) return;
            using (JKRuntime.RuntimeApi.MeasureStartup("more-items.pending-pickup"))
                if (!writer.Drain(30000)) throw new IOException("Previous pickup write is still pending; retry loading after storage recovers");
            FinishCollection(false);
        }

        internal static string[] GetCollectedForRun(ConsumableSave world, bool newRun)
        {
            return world == null || newRun
                ? new string[0]
                : world.Collected ?? new string[0];
        }

        private static void ApplyItems(ConsumableStack[] items)
        {
            foreach (ConsumableStack stack in items ?? new ConsumableStack[0])
            {
                if (stack == null
                    || string.IsNullOrWhiteSpace(stack.Id)
                    || stack.Count <= 0) continue;
                int current;
                Counts.TryGetValue(stack.Id, out current);
                long combined = (long)current + stack.Count;
                Counts[stack.Id] = combined > int.MaxValue ? int.MaxValue : (int)combined;
            }
        }

        private static void ApplyCollected(string[] collected)
        {
            foreach (string key in collected ?? new string[0])
                if (!string.IsNullOrWhiteSpace(key)) Collected.Add(key);
        }

        private static bool Save()
        {
            // A menu/API mutation after preparation invalidates the older read.
            ClearPreparation();
            using (JKRuntime.RuntimeApi.MeasureStartup("more-items.inventory-save"))
            {
            try
            {
                string path = JKRuntime.Settings.NativeSaveFiles.GetPath(SaveFolder, SaveFile);
                if (!Directory.Exists(Path.GetDirectoryName(path))) throw new IOException("Native save directory is unavailable; refusing to recreate a reset context");
                if (store != null && storePath != path) throw new IOException("Inventory context changed; reload before saving");
                if (store == null) { ReadDatabase(path, out store); storePath = path; }
                var candidate = CreateCandidate(null, 0, null);
                using (JKRuntime.RuntimeApi.MeasureStartup("more-items.inventory-write")) store.Save(candidate);
                database = candidate;
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine("[More Items] Could not save inventory: " + Unwrap(error).Message);
                return false;
            }
            }
        }

        private static ConsumableSaveDatabase CreateCandidate(string item, int additional, string pickup)
        {
                var previous = database ?? new ConsumableSaveDatabase().GetDefault();
                ValidateDatabase(previous);
                List<ConsumableSave> saves = new List<ConsumableSave>(
                    (previous.Saves ?? new ConsumableSave[0])
                        .Where(value => value != null && !string.IsNullOrWhiteSpace(value.Key)
                            && !string.Equals(value.Key, InventoryKey, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(value.Key, currentKey, StringComparison.OrdinalIgnoreCase))
                        .GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(group => new ConsumableSave
                        {
                            Key = group.First().Key,
                            Items = new ConsumableStack[0],
                            Collected = group
                                .SelectMany(value => value.Collected ?? new string[0])
                                .Where(value => !string.IsNullOrWhiteSpace(value))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                                .ToArray()
                        }));
                var items = Counts.Select(pair => new ConsumableStack { Id = pair.Key, Count = pair.Value
                    + (string.Equals(pair.Key, item, StringComparison.OrdinalIgnoreCase) ? additional : 0) }).ToList();
                if (item != null && !Counts.ContainsKey(item)) items.Add(new ConsumableStack { Id = item, Count = additional });
                saves.Add(new ConsumableSave
                {
                    Key = InventoryKey,
                    Items = items.OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase).ToArray(),
                    Collected = new string[0]
                });
                saves.Add(new ConsumableSave
                {
                    Key = currentKey,
                    Items = new ConsumableStack[0],
                    Collected = (pickup == null ? Collected : Collected.Concat(new[] { pickup }))
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                });
                return new ConsumableSaveDatabase { Version = 1, Saves = saves.OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase).ToArray() };
        }

        private static ConsumableSave FindProfile(string key)
        {
            foreach (ConsumableSave save in database.Saves ?? new ConsumableSave[0])
                if (save != null && string.Equals(save.Key, key, StringComparison.OrdinalIgnoreCase)) return save;
            return null;
        }

        private static string GetCurrentSaveKey()
        {
            if (Game1.instance == null || Game1.instance.contentManager == null)
                throw new InvalidOperationException("Jump King content is unavailable");
            if (Game1.instance.contentManager.level == null) return "vanilla";
            string root = Path.GetFullPath(Game1.instance.contentManager.root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToLowerInvariant();
            return "level:" + root;
        }

        private static Exception Unwrap(Exception error)
        {
            while (error is TargetInvocationException && error.InnerException != null)
                error = error.InnerException;
            return error;
        }
    }
}
