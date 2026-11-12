using System;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MoreItems
{
    public interface IConsumableWorldObject : IDisposable
    {
        bool IsAlive { get; }
    }

    public sealed class ConsumableDispenserDefinition
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public string ItemId { get; private set; }
        public int Count { get; private set; }
        public int Screen { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 EjectionOffset { get; private set; }
        public Vector2 EjectionVelocity { get; private set; }
        public float Cooldown { get; private set; }
        public int Priority { get; private set; }

        public ConsumableDispenserDefinition(
            string id,
            string label,
            string itemId,
            int count,
            Vector2 position,
            Vector2 ejectionOffset,
            Vector2 ejectionVelocity,
            float cooldown = 0.45f,
            int priority = 150)
            : this(id, label, itemId, count, 0, position, ejectionOffset, ejectionVelocity, cooldown, priority)
        {
        }

        public ConsumableDispenserDefinition(
            string id,
            string label,
            string itemId,
            int count,
            int screen,
            Vector2 position,
            Vector2 ejectionOffset,
            Vector2 ejectionVelocity,
            float cooldown = 0.45f,
            int priority = 150)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Dispenser id is required", "id");
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("Dispenser item is required", "itemId");
            if (count <= 0) throw new ArgumentOutOfRangeException("count");
            Id = id.Trim();
            Label = string.IsNullOrWhiteSpace(label) ? "Dispense" : label;
            ItemId = itemId.Trim();
            Count = count;
            Screen = Math.Max(0, screen);
            Position = position;
            EjectionOffset = ejectionOffset;
            EjectionVelocity = ejectionVelocity;
            Cooldown = Math.Max(0f, cooldown);
            Priority = priority;
        }
    }

    internal sealed class LooseConsumablePickup : Entity, IConsumableWorldObject
    {
        private readonly ConsumableDefinition definition;
        private readonly int count;
        private readonly BodyComp body;
        private float lifetime, retryAt;
        private ItemInventory.PendingCollection pending;

        internal LooseConsumablePickup(ConsumableDefinition item, int quantity, Vector2 position, Vector2 velocity)
        {
            definition = item;
            count = quantity;
            body = new BodyComp(position, 10, 10);
            body.Velocity = velocity;
            AddComponents(body);
        }

        protected override void Update(float delta)
        {
            lifetime += delta;
            if (pending != null)
            {
                ItemInventory.PollCollection(pending);
                if (!pending.Finished) return;
                bool saved = pending.Succeeded; pending = null;
                if (!saved) { retryAt = lifetime + 1f; return; }
                Destroy();
                try { Game1.instance.contentManager.audio.menu.CursorMove.Play(); }
                catch (Exception error) { Console.WriteLine("[More Items] Pickup sound unavailable: " + error.Message); }
                return;
            }
            if (!definition.IsEnabled() || lifetime >= 90f)
            {
                Destroy();
                return;
            }
            if (body.IsOnGround)
            {
                body.Velocity.X *= (float)Math.Pow(0.06, delta);
                if (Math.Abs(body.Velocity.X) < 0.025f) body.Velocity.X = 0f;
            }
            PlayerEntity player = JKRuntime.UI.WorldInteractionContext.Player;
            if (player == null || lifetime < 0.12f || lifetime < retryAt || !player.m_body.GetHitbox().Intersects(body.GetHitbox())) return;
            pending = ItemInventory.BeginCollect(definition.Id, count, null);
        }

        public override void Draw()
        {
            if (!definition.IsEnabled()) return;
            Rectangle hitbox = body.GetHitbox();
            Point center = Camera.TransformVector2(hitbox.Center.ToVector2()).ToPoint();
            definition.DrawIcon(new Rectangle(center.X - 8, center.Y - 8, 16, 16));
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

    internal sealed class ConsumableDispenser : Entity, IConsumableWorldObject
    {
        private readonly ConsumableDispenserDefinition definition;
        private readonly ConsumableDefinition item;
        private readonly string interactionId;
        private float cooldown;

        internal ConsumableDispenser(ConsumableDispenserDefinition dispenser, ConsumableDefinition consumable)
        {
            definition = dispenser;
            item = consumable;
            interactionId = "more-items.dispenser." + dispenser.Id;
            JKRuntime.UI.WorldInteraction interaction = dispenser.Screen > 0
                ? JKRuntime.UI.WorldInteraction.ScreenAction(
                    interactionId,
                    dispenser.Label,
                    dispenser.Priority,
                    dispenser.Screen,
                    IsAvailable,
                    Dispense)
                : JKRuntime.UI.WorldInteraction.Action(
                    interactionId,
                    dispenser.Label,
                    dispenser.Priority,
                    IsAvailable,
                    Dispense);
            JKRuntime.UI.UIApi.RegisterInteraction(interaction);
        }

        protected override void Update(float delta)
        {
            cooldown = Math.Max(0f, cooldown - delta);
        }

        private bool IsAvailable()
        {
            if (!IsAlive || cooldown > 0f || !item.IsEnabled()) return false;
            if (definition.Screen > 0)
            {
                Rectangle localArea = new Rectangle(
                    (int)definition.Position.X - 12,
                    (int)definition.Position.Y - 12,
                    48,
                    48);
                return JKRuntime.UI.WorldInteractionContext.PlayerIntersectsScreenArea(
                    definition.Screen,
                    localArea);
            }
            PlayerEntity player = JKRuntime.UI.WorldInteractionContext.Player;
            if (player == null) return false;
            Rectangle area = new Rectangle((int)definition.Position.X - 12, (int)definition.Position.Y - 12, 48, 48);
            return player.m_body.GetHitbox().Intersects(area);
        }

        private void Dispense()
        {
            if (!IsAvailable()) return;
            MoreItemsApi.SpawnLoosePickup(
                item.Id,
                definition.Count,
                GetWorldPosition() + definition.EjectionOffset,
                definition.EjectionVelocity);
            cooldown = definition.Cooldown;
            Game1.instance.contentManager.audio.menu.OnSelect();
        }

        private Vector2 GetWorldPosition()
        {
            return definition.Screen <= 0
                ? definition.Position
                : JKRuntime.UI.WorldScreen.ToWorldPosition(
                    definition.Screen,
                    definition.Position);
        }

        protected override void OnDestroy()
        {
            JKRuntime.UI.UIApi.UnregisterInteraction(interactionId);
            MoreItemsApi.ReleaseWorldObject(this);
        }

        public void Dispose()
        {
            if (IsAlive) Destroy();
        }
    }
}
