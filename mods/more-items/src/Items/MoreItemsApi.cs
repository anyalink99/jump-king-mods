using System;
using System.Collections.Generic;
using System.IO;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;

namespace MoreItems
{
    public static partial class MoreItemsApi
    {
        private static readonly Dictionary<string, ConsumableDefinition> Definitions =
            new Dictionary<string, ConsumableDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> DefinitionOrder = new List<string>();
        private static readonly Dictionary<string, IConsumableWorldObject> Dispensers =
            new Dictionary<string, IConsumableWorldObject>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<IConsumableWorldObject> WorldObjects =
            new List<IConsumableWorldObject>();
        private static readonly Dictionary<string, List<string>> CurrencyIds =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> OfferIds =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        public static event Action<string, int> InventoryChanged;
        public static event Action DefinitionsChanged;

        public static IDisposable SubscribeInventoryChanged(Action<string, int> listener)
        {
            if (listener == null) throw new ArgumentNullException("listener");
            InventoryChanged += listener;
            return new ConsumableSubscription(
                delegate { InventoryChanged -= listener; });
        }

        public static IDisposable SubscribeDefinitionsChanged(Action listener)
        {
            if (listener == null) throw new ArgumentNullException("listener");
            DefinitionsChanged += listener;
            return new ConsumableSubscription(
                delegate { DefinitionsChanged -= listener; });
        }

        public static void Register(ConsumableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            JKRuntime.UI.UIApi.UnregisterBinding("more-items." + definition.Id);
            JKRuntime.UI.UIApi.UnregisterInputAction("more-items." + definition.Id);
            if (!Definitions.ContainsKey(definition.Id)) DefinitionOrder.Add(definition.Id);
            Definitions[definition.Id] = definition;
            ConsumableDefinition inventoryDefinition = definition;
            JKRuntime.UI.UiInventoryItemDefinition inventoryItem;
            if (inventoryDefinition.IsEquipment)
            {
                inventoryItem = JKRuntime.UI.UiInventoryItemDefinition.Equipment(
                    "more-items." + inventoryDefinition.Id,
                    inventoryDefinition.Name,
                    inventoryDefinition.Description,
                    inventoryDefinition.Color,
                    delegate { return GetCount(inventoryDefinition.Id); },
                    inventoryDefinition.IsEquipped,
                    inventoryDefinition.SetEquipped,
                    delegate
                    {
                        return inventoryDefinition.IsEnabled()
                            && GetCount(inventoryDefinition.Id) > 0;
                    },
                    delegate
                    {
                        return inventoryDefinition.IsEnabled()
                            && GetCount(inventoryDefinition.Id) > 0
                            && inventoryDefinition.CanUse();
                    },
                    inventoryDefinition.PluralName);
            }
            else
            {
                inventoryItem = new JKRuntime.UI.UiInventoryItemDefinition(
                    "more-items." + inventoryDefinition.Id,
                    inventoryDefinition.Name,
                    inventoryDefinition.Description,
                    inventoryDefinition.Color,
                    delegate { return GetCount(inventoryDefinition.Id); },
                    delegate
                    {
                        return inventoryDefinition.IsEnabled()
                            && GetCount(inventoryDefinition.Id) > 0;
                    },
                    delegate
                    {
                        return inventoryDefinition.IsEnabled()
                            && inventoryDefinition.IsUsable
                            && GetCount(inventoryDefinition.Id) > 0
                            && inventoryDefinition.CanUse();
                    },
                    inventoryDefinition.GetActionLabel,
                    delegate { return TryUse(inventoryDefinition.Id); },
                    inventoryDefinition.PluralName);
            }
            JKRuntime.UI.UIApi.RegisterInventoryItem(inventoryItem);
            if (definition.Hotkey != null && definition.IsUsable)
            {
                SettingsStore.EnsureBinding(definition.Id, definition.Hotkey.DefaultChords);
                ConsumableDefinition captured = definition;
                JKRuntime.UI.UIApi.RegisterBinding(new JKRuntime.UI.UiBindingDefinition(
                    "more-items." + captured.Id,
                    "More Items",
                    captured.Hotkey.Label,
                    delegate { return GetChords(captured.Id); },
                    delegate(JKRuntime.UI.UiChord[] chords) { SetChords(captured.Id, chords); },
                    delegate { ResetBindings(captured.Id); }));
                JKRuntime.UI.UIApi.RegisterInputAction(JKRuntime.UI.UiInputActionDefinition.FromChords(
                    "more-items." + captured.Id,
                    captured.Hotkey.Label,
                    100,
                    delegate { return GetChords(captured.Id); },
                    delegate
                    {
                        return captured.IsEnabled()
                            && GetCount(captured.Id) > 0
                            && captured.CanUse();
                    },
                    delegate { TryUse(captured.Id); }));
            }
            NotifyDefinitionsChanged();
        }

        public static void Unregister(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            Definitions.Remove(id);
            DefinitionOrder.RemoveAll(
                delegate(string value)
                {
                    return string.Equals(value, id, StringComparison.OrdinalIgnoreCase);
                });
            JKRuntime.UI.UIApi.UnregisterBinding("more-items." + id);
            JKRuntime.UI.UIApi.UnregisterInputAction("more-items." + id);
            JKRuntime.UI.UIApi.UnregisterInventoryItem("more-items." + id);
            UnregisterOwnedIds(CurrencyIds, id, JKRuntime.UI.UIApi.UnregisterCurrency);
            UnregisterOwnedIds(OfferIds, id, JKRuntime.UI.UIApi.UnregisterMerchantOffer);
            NotifyDefinitionsChanged();
        }

        public static bool TryGet(string id, out ConsumableDefinition definition)
        {
            return Definitions.TryGetValue(id ?? string.Empty, out definition);
        }

        public static IList<ConsumableDefinition> GetDefinitions()
        {
            List<ConsumableDefinition> result = new List<ConsumableDefinition>();
            foreach (string id in DefinitionOrder) result.Add(Definitions[id]);
            return result;
        }

        public static int GetCount(string id) { return ItemInventory.GetCount(id); }
        public static void Add(string id, int count)
        {
            ConsumableDefinition definition;
            if (!TryGet(id, out definition))
                throw new ArgumentException("Item is not registered", "id");
            if (count <= 0) throw new ArgumentOutOfRangeException("count");
            if (!ItemInventory.Add(id, count))
                throw new InvalidOperationException("Item count exceeds the supported range");
            NotifyInventoryChanged(definition.Id);
        }

        internal static bool TryCollect(string id, int count, string persistentKey)
        {
            ConsumableDefinition definition;
            if (!TryGet(id, out definition) || !definition.IsEnabled()) return false;
            bool saved = persistentKey == null ? ItemInventory.Add(id, count) : ItemInventory.TryCollect(id, count, persistentKey);
            if (saved) NotifyInventoryChanged(definition.Id);
            return saved;
        }

        public static bool SpawnPickup(string pickupId, string itemId, int count, int screen, int x, int y)
        {
            ConsumableDefinition definition;
            if (string.IsNullOrWhiteSpace(pickupId)
                || count <= 0
                || screen < 1
                || x < 0
                || x >= JumpGame.GAME_RECT.Width
                || y < 0
                || y >= JumpGame.GAME_RECT.Height
                || !TryGet(itemId, out definition)
                || !definition.IsEnabled()) return false;
            string root = Game1.instance.contentManager.root;
            string key = Path.GetFullPath(root).ToLowerInvariant() + "|" + pickupId;
            if (ItemInventory.IsCollected(key)) return false;
            ConsumablePickup pickup = new ConsumablePickup(new ConsumablePickupData
            {
                Id = pickupId,
                Item = itemId,
                Count = count,
                Screen = screen,
                X = x,
                Y = y,
                Persistent = true
            }, definition, key);
            TrackWorldObject(pickup);
            return true;
        }

        public static bool TryUse(string id)
        {
            ConsumableDefinition definition;
            if (!TryGet(id, out definition)
                || !definition.IsEnabled()
                || !definition.IsUsable
                || GetCount(id) <= 0
                || !definition.CanUse()) return false;
            if (definition.ConsumesOnUse && !ItemInventory.Remove(id, 1)) return false;
            bool used;
            try { used = definition.Use(); }
            catch
            {
                if (definition.ConsumesOnUse && !ItemInventory.Add(id, 1))
                    throw new InvalidOperationException(
                        "Item use failed and its inventory reservation could not be restored");
                throw;
            }
            if (!used)
            {
                if (definition.ConsumesOnUse && !ItemInventory.Add(id, 1))
                    throw new InvalidOperationException(
                        "Item use was rejected and its inventory reservation could not be restored");
                return false;
            }
            NotifyInventoryChanged(id);
            return true;
        }

        public static bool TryTake(string id, int count)
        {
            ConsumableDefinition definition;
            if (count <= 0
                || !TryGet(id, out definition)
                || !definition.IsEnabled()
                || !ItemInventory.Remove(id, count)) return false;
            NotifyInventoryChanged(id);
            return true;
        }

        public static void RegisterMerchantOffer(
            string offerId,
            string itemId,
            int quantity,
            Items currency,
            int price)
        {
            ConsumableDefinition definition;
            if (!TryGet(itemId, out definition))
                throw new ArgumentException("Item is not registered", "itemId");
            if (quantity <= 0) throw new ArgumentOutOfRangeException("quantity");
            ConsumableDefinition captured = definition;
            string registeredId = "more-items." + offerId;
            JKRuntime.UI.UIApi.RegisterMerchantOffer(new JKRuntime.UI.MerchantOfferDefinition(
                registeredId,
                quantity + " " + (quantity == 1 ? captured.Name : captured.PluralName),
                "Adds " + quantity + " " + (quantity == 1 ? captured.Name : captured.PluralName) + " to your inventory.",
                currency,
                price,
                captured.Color,
                delegate { Add(captured.Id, quantity); },
                captured.IsEnabled,
                captured.DrawIcon));
            TrackOwnedId(OfferIds, captured.Id, registeredId);
        }

        public static void RegisterMerchantOffer(
            string offerId,
            string itemId,
            int quantity,
            string currencyId,
            int price)
        {
            ConsumableDefinition definition;
            if (!TryGet(itemId, out definition))
                throw new ArgumentException("Item is not registered", "itemId");
            if (quantity <= 0) throw new ArgumentOutOfRangeException("quantity");
            ConsumableDefinition captured = definition;
            string registeredId = "more-items." + offerId;
            JKRuntime.UI.UIApi.RegisterMerchantOffer(new JKRuntime.UI.MerchantOfferDefinition(
                registeredId,
                quantity + " " + (quantity == 1 ? captured.Name : captured.PluralName),
                "Adds " + quantity + " " + (quantity == 1 ? captured.Name : captured.PluralName) + " to your inventory.",
                currencyId,
                price,
                captured.Color,
                delegate { Add(captured.Id, quantity); },
                captured.IsEnabled,
                captured.DrawIcon));
            TrackOwnedId(OfferIds, captured.Id, registeredId);
        }

        public static void RegisterCurrency(string currencyId, string itemId, int exchangeUnit)
        {
            ConsumableDefinition definition;
            if (!TryGet(itemId, out definition))
                throw new ArgumentException("Item is not registered", "itemId");
            ConsumableDefinition captured = definition;
            JKRuntime.UI.UIApi.RegisterCurrency(new JKRuntime.UI.UiCurrencyDefinition(
                currencyId,
                captured.Name,
                captured.PluralName,
                exchangeUnit,
                delegate { return GetCount(captured.Id); },
                delegate(int count) { return TryTake(captured.Id, count); },
                delegate(int count) { Add(captured.Id, count); },
                captured.DrawIcon));
            TrackOwnedId(CurrencyIds, captured.Id, currencyId);
        }

        public static IConsumableWorldObject SpawnLoosePickup(
            string itemId,
            int count,
            Vector2 position,
            Vector2 velocity)
        {
            ConsumableDefinition definition;
            if (count <= 0
                || !TryGet(itemId, out definition)
                || !definition.IsEnabled()) return null;
            PruneWorldObjects();
            int looseCount = 0;
            foreach (IConsumableWorldObject value in WorldObjects)
                if (value is LooseConsumablePickup) looseCount++;
            if (looseCount >= 64) return null;
            LooseConsumablePickup pickup = new LooseConsumablePickup(definition, count, position, velocity);
            TrackWorldObject(pickup);
            return pickup;
        }

        public static IConsumableWorldObject SpawnDispenser(ConsumableDispenserDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            ConsumableDefinition item;
            if (!TryGet(definition.ItemId, out item))
                throw new ArgumentException("Dispenser item is not registered", "definition");
            if (!item.IsEnabled()) return null;
            IConsumableWorldObject previous;
            if (Dispensers.TryGetValue(definition.Id, out previous) && previous != null) previous.Dispose();
            IConsumableWorldObject dispenser = new ConsumableDispenser(definition, item);
            Dispensers[definition.Id] = dispenser;
            TrackWorldObject(dispenser);
            return dispenser;
        }

        public static int[] GetBindings(string id)
        {
            return JKRuntime.UI.UiChord.ToAlternatives(GetChords(id));
        }

        public static JKRuntime.UI.UiChord[] GetChords(string id)
        {
            SettingsStore.EnsureLoaded();
            JKRuntime.UI.UiChord[] chords;
            if (!SettingsStore.Current.KeyBindings.TryGetValue(id ?? string.Empty, out chords))
                return new JKRuntime.UI.UiChord[0];
            return CloneChords(chords);
        }

        public static void SetBindings(string id, params int[] bindings)
        {
            SetChords(id, JKRuntime.UI.UiChord.FromAlternatives(bindings));
        }

        public static void SetChords(string id, params JKRuntime.UI.UiChord[] chords)
        {
            ConsumableDefinition definition;
            if (!TryGet(id, out definition) || definition.Hotkey == null)
                throw new ArgumentException("Item has no registered hotkey", "id");
            SettingsStore.EnsureLoaded();
            SettingsStore.Current.KeyBindings[id] = CloneChords(chords);
            SettingsStore.Save();
        }

        public static void ResetBindings(string id)
        {
            SettingsStore.ResetBinding(id);
            SettingsStore.Save();
        }

        internal static void NotifyInventoryChanged(string id)
        {
            int count = GetCount(id);
            try { JKRuntime.UI.UIApi.NotifyInventoryItemChanged("more-items." + id); }
            catch (Exception error) { Console.WriteLine("[More Items] Inventory UI observer failed after commit: " + error); }
            Action<string, int> handler = InventoryChanged;
            if (handler == null) return;
            foreach (Action<string, int> callback in handler.GetInvocationList())
                try { callback(id, count); }
                catch (Exception error) { Console.WriteLine("[More Items] Inventory observer " + callback.Method.DeclaringType + " failed after commit: " + error); }
        }

        private static JKRuntime.UI.UiChord[] CloneChords(JKRuntime.UI.UiChord[] chords)
        {
            List<JKRuntime.UI.UiChord> result = new List<JKRuntime.UI.UiChord>();
            foreach (JKRuntime.UI.UiChord chord in chords ?? new JKRuntime.UI.UiChord[0])
            {
                if (chord == null || chord.IsEmpty) continue;
                result.Add(new JKRuntime.UI.UiChord(chord.Buttons));
                if (result.Count == 2) break;
            }
            return result.ToArray();
        }

        internal static void TrackWorldObject(IConsumableWorldObject value)
        {
            if (value == null || WorldObjects.Contains(value)) return;
            WorldObjects.Add(value);
        }

        internal static void ReleaseWorldObject(IConsumableWorldObject value)
        {
            if (value == null) return;
            WorldObjects.Remove(value);
            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, IConsumableWorldObject> pair in Dispensers)
                if (ReferenceEquals(pair.Value, value)) remove.Add(pair.Key);
            foreach (string id in remove) Dispensers.Remove(id);
        }

        internal static void ClearWorldObjects()
        {
            IConsumableWorldObject[] values = WorldObjects.ToArray();
            WorldObjects.Clear();
            Dispensers.Clear();
            foreach (IConsumableWorldObject value in values)
                if (value != null) value.Dispose();
        }

        private static void PruneWorldObjects()
        {
            WorldObjects.RemoveAll(
                delegate(IConsumableWorldObject value)
                {
                    return value == null || !value.IsAlive;
                });
        }

        private static void NotifyDefinitionsChanged()
        {
            Action handler = DefinitionsChanged;
            if (handler == null) return;
            foreach (Action callback in handler.GetInvocationList())
                try { callback(); }
                catch (Exception error) { Console.WriteLine("[More Items] Definition observer " + callback.Method.DeclaringType + " failed: " + error); }
        }

        private static void TrackOwnedId(
            Dictionary<string, List<string>> ownership,
            string itemId,
            string registeredId)
        {
            List<string> ids;
            if (!ownership.TryGetValue(itemId, out ids))
            {
                ids = new List<string>();
                ownership[itemId] = ids;
            }
            if (!ids.Contains(registeredId)) ids.Add(registeredId);
        }

        private static void UnregisterOwnedIds(
            Dictionary<string, List<string>> ownership,
            string itemId,
            Action<string> unregister)
        {
            List<string> ids;
            if (!ownership.TryGetValue(itemId, out ids)) return;
            foreach (string registeredId in ids) unregister(registeredId);
            ownership.Remove(itemId);
        }
    }

    internal sealed class ConsumableSubscription : IDisposable
    {
        private Action unsubscribe;

        internal ConsumableSubscription(Action action)
        {
            unsubscribe = action;
        }

        public void Dispose()
        {
            Action action = unsubscribe;
            unsubscribe = null;
            if (action != null) action();
        }
    }
}
