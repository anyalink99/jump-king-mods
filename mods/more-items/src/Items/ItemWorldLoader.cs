using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MoreItems
{
    [Serializable]
    public sealed class ConsumablePickupData
    {
        public ConsumablePickupData() { Persistent = true; }
        [XmlAttribute("id")]
        public string Id { get; set; }
        [XmlAttribute("item")]
        public string Item { get; set; }
        [XmlAttribute("count")]
        public int Count { get; set; }
        [XmlAttribute("screen")]
        public int Screen { get; set; }
        [XmlAttribute("x")]
        public int X { get; set; }
        [XmlAttribute("y")]
        public int Y { get; set; }
        [XmlAttribute("persistent")]
        [DefaultValue(true)]
        public bool Persistent { get; set; }
    }

    [Serializable]
    [XmlRoot("MoreItems")]
    public sealed class ConsumablePickupFile
    {
        [XmlAttribute("version")]
        public int Version { get; set; }
        [XmlElement("Pickup")]
        public ConsumablePickupData[] Pickups { get; set; }
        [XmlElement("Dispenser")]
        public ConsumableDispenserData[] Dispensers { get; set; }
    }

    [Serializable]
    public sealed class ConsumableDispenserData
    {
        public ConsumableDispenserData()
        {
            Count = 1;
            Screen = 1;
            Cooldown = 0.45f;
            Priority = 150;
        }

        [XmlAttribute("id")]
        public string Id { get; set; }
        [XmlAttribute("label")]
        public string Label { get; set; }
        [XmlAttribute("item")]
        public string Item { get; set; }
        [XmlAttribute("count")]
        [DefaultValue(1)]
        public int Count { get; set; }
        [XmlAttribute("screen")]
        [DefaultValue(1)]
        public int Screen { get; set; }
        [XmlAttribute("x")]
        public float X { get; set; }
        [XmlAttribute("y")]
        public float Y { get; set; }
        [XmlAttribute("eject_x")]
        public float EjectX { get; set; }
        [XmlAttribute("eject_y")]
        public float EjectY { get; set; }
        [XmlAttribute("velocity_x")]
        public float VelocityX { get; set; }
        [XmlAttribute("velocity_y")]
        public float VelocityY { get; set; }
        [XmlAttribute("cooldown")]
        [DefaultValue(0.45f)]
        public float Cooldown { get; set; }
        [XmlAttribute("priority")]
        [DefaultValue(150)]
        public int Priority { get; set; }
    }

    internal sealed class ConsumablePickup : Entity, IConsumableWorldObject
    {
        private readonly ConsumablePickupData data;
        private readonly ConsumableDefinition definition;
        private readonly Vector2 position;
        private readonly string saveKey;
        private float time;
        private float retryAt;
        private bool collected;
        private ItemInventory.PendingCollection pending;

        internal ConsumablePickup(ConsumablePickupData pickup, ConsumableDefinition item, string key)
        {
            data = pickup;
            definition = item;
            saveKey = key;
            position = pickup.Screen == 0
                ? new Vector2(pickup.X, pickup.Y)
                : JKRuntime.UI.WorldScreen.ToWorldPosition(
                    pickup.Screen,
                    new Vector2(pickup.X, pickup.Y));
        }

        protected override void Update(float delta)
        {
            time += delta;
            if (pending != null)
            {
                ItemInventory.PollCollection(pending);
                if (!pending.Finished) return;
                bool saved = pending.Succeeded;
                pending = null;
                if (!saved) { retryAt = time + 1f; return; }
                collected = true;
                Destroy();
                try { Game1.instance.contentManager.audio.menu.CursorMove.Play(); }
                catch (Exception error) { Console.WriteLine("[More Items] Pickup sound unavailable: " + error.Message); }
                return;
            }
            if (collected || time < retryAt || !definition.IsEnabled()
                || (data.Screen > 0 && Camera.CurrentScreenIndex1 != data.Screen)) return;
            PlayerEntity player = JKRuntime.UI.WorldInteractionContext.Player;
            if (player == null) return;
            Rectangle hitbox = new Rectangle((int)position.X - 7, (int)position.Y - 10, 14, 18);
            if (!player.m_body.GetHitbox().Intersects(hitbox)) return;
            if (data.Persistent && ItemInventory.IsCollected(saveKey)) { collected = true; Destroy(); return; }
            pending = ItemInventory.BeginCollect(definition.Id, Math.Max(1, data.Count), data.Persistent ? saveKey : null);
        }

        public override void Draw()
        {
            if (!definition.IsEnabled()
                || (data.Screen > 0 && Camera.CurrentScreenIndex1 != data.Screen)) return;
            Vector2 center = Camera.TransformVector2(position + new Vector2(0f, (float)Math.Sin(time * 3.2f) * 2f));
            definition.DrawIcon(new Rectangle((int)center.X - 8, (int)center.Y - 8, 16, 16));
        }

        public void Dispose()
        {
            if (IsAlive) Destroy();
        }

        protected override void OnDestroy()
        {
            MoreItemsApi.ReleaseWorldObject(this);
        }
    }

    internal static class ItemWorldLoader
    {
        private static string preparedRoot;
        private static ConsumablePickupFile prepared;
        private static bool hasPrepared;
        internal static void Prepare(JKRuntime.RuntimeScope scope, string root)
        {
            scope.Defer(delegate { preparedRoot = null; prepared = null; hasPrepared = false; });
            try { using (JKRuntime.RuntimeApi.MeasureStartup("more-items.world-items-read")) prepared = Read(root); }
            catch (Exception error) { prepared = new ConsumablePickupFile(); Console.WriteLine("[More Items] World items unavailable: " + error.Message); }
            preparedRoot = Path.GetFullPath(root); hasPrepared = true;
        }
        internal static ConsumablePickupFile Read(string root)
        {
            string path = Path.Combine(root, "props", "more-items", "items.xml");
            if (!File.Exists(path)) return new ConsumablePickupFile();
            ConsumablePickupFile file;
            using (var stream = File.OpenRead(path))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024 }))
                file = (ConsumablePickupFile)new XmlSerializer(typeof(ConsumablePickupFile)).Deserialize(reader);
            if (file == null || (file.Version != 0 && file.Version != 1))
                throw new InvalidDataException("Unsupported More Items XML version");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            foreach (var pickup in file.Pickups ?? new ConsumablePickupData[0])
            {
                string id = pickup == null || string.IsNullOrWhiteSpace(pickup.Id) ? "pickup-" + index : pickup.Id;
                ValidatePickup(pickup, id); pickup.Id = id; index++;
                if (!ids.Add(id)) throw new InvalidDataException("Duplicate More Items object id: " + id);
            }
            foreach (var dispenser in file.Dispensers ?? new ConsumableDispenserData[0])
            {
                ValidateDispenser(dispenser);
                if (!ids.Add(dispenser.Id)) throw new InvalidDataException("Duplicate More Items object id: " + dispenser.Id);
            }
            return file;
        }
        internal static IList<IConsumableWorldObject> Load()
        {
            var result = new List<IConsumableWorldObject>();
            var rollback = new JKRuntime.RuntimeScope();
            try
            {
                string root = Path.GetFullPath(Game1.instance.contentManager.root);
                var file = hasPrepared && string.Equals(root, preparedRoot, StringComparison.OrdinalIgnoreCase) ? prepared : Read(root);
                var pickups = file.Pickups ?? new ConsumablePickupData[0];
                var dispensers = file.Dispensers ?? new ConsumableDispenserData[0];
                // Definitions may register after preparation. Resolve all before creating any entity.
                foreach (var pickup in pickups) RequireItem(pickup.Item);
                foreach (var dispenser in dispensers) RequireItem(dispenser.Item);
                foreach (var pickup in pickups)
                {
                    string key = root.ToLowerInvariant() + "|" + pickup.Id;
                    if (pickup.Persistent && ItemInventory.IsCollected(key)) continue;
                    var worldPickup = rollback.Own(new ConsumablePickup(pickup, RequireItem(pickup.Item), key));
                    MoreItemsApi.TrackWorldObject(worldPickup); result.Add(worldPickup);
                }
                foreach (var dispenser in dispensers)
                {
                    var worldObject = MoreItemsApi.SpawnDispenser(new ConsumableDispenserDefinition(
                        dispenser.Id, dispenser.Label, dispenser.Item, dispenser.Count, dispenser.Screen,
                        new Vector2(dispenser.X, dispenser.Y), new Vector2(dispenser.EjectX, dispenser.EjectY),
                        new Vector2(dispenser.VelocityX, dispenser.VelocityY), dispenser.Cooldown, dispenser.Priority));
                    if (worldObject != null) { rollback.Own(worldObject); result.Add(worldObject); }
                }
            }
            catch (Exception failure)
            {
                try { rollback.Dispose(); } catch (Exception cleanup) { throw new AggregateException("World items and rollback failed", failure, cleanup); }
                result.Clear();
                Console.WriteLine("[More Items] Could not load world items: " + failure.Message);
            }
            return result;
        }
        private static ConsumableDefinition RequireItem(string id)
        {
            ConsumableDefinition definition;
            if (!MoreItemsApi.TryGet(id, out definition)) throw new InvalidDataException("Unknown item: " + id);
            return definition;
        }

        private static void ValidatePickup(ConsumablePickupData pickup, string id)
        {
            if (pickup == null) throw new InvalidDataException("More Items pickup is null");
            if (pickup.Screen < 1) throw new InvalidDataException("Pickup screen must be one-based: " + id);
            if (pickup.Count <= 0) throw new InvalidDataException("Pickup count must be positive: " + id);
            if (pickup.X < 0 || pickup.X >= JumpGame.GAME_RECT.Width
                || pickup.Y < 0 || pickup.Y >= JumpGame.GAME_RECT.Height)
                throw new InvalidDataException("Pickup position must fit inside its screen: " + id);
        }

        private static void ValidateDispenser(ConsumableDispenserData dispenser)
        {
            if (dispenser == null || string.IsNullOrWhiteSpace(dispenser.Id))
                throw new InvalidDataException("Dispenser id is required");
            foreach (float value in new[] { dispenser.X, dispenser.Y, dispenser.EjectX, dispenser.EjectY, dispenser.VelocityX, dispenser.VelocityY, dispenser.Cooldown })
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Dispenser values must be finite: " + dispenser.Id);
            if (dispenser.Cooldown < 0) throw new InvalidDataException("Dispenser cooldown cannot be negative: " + dispenser.Id);
            if (dispenser.Screen < 1)
                throw new InvalidDataException("Dispenser screen must be one-based: " + dispenser.Id);
            if (dispenser.Count <= 0)
                throw new InvalidDataException("Dispenser count must be positive: " + dispenser.Id);
            if (dispenser.X < 0 || dispenser.X >= JumpGame.GAME_RECT.Width
                || dispenser.Y < 0 || dispenser.Y >= JumpGame.GAME_RECT.Height)
                throw new InvalidDataException("Dispenser position must fit inside its screen: " + dispenser.Id);
        }
    }
}
