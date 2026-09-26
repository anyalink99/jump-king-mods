using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.Player;

namespace HammerKing
{
    internal sealed class HammerVisual : Component
    {
        private static readonly FieldInfo SpriteField = typeof(PlayerEntity).GetField("m_sprite", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly PlayerEntity player;
        private HammerSprite wrapper;
        private HammerPhysics physics;
        internal HammerVisual(PlayerEntity value) { player = value; }
        internal void Attach(HammerPhysics value)
        {
            if (value == null) throw new System.ArgumentNullException("value");
            Detach();
            physics = value; Enabled = true; Refresh();
        }
        internal void Detach()
        {
            Enabled = false;
            RestoreSprite();
            physics = null;
        }
        internal void Refresh()
        {
            if (physics == null) return;
            var current = (Sprite)SpriteField.GetValue(player);
            if (current == null || ReferenceEquals(current, wrapper)) return;
            if (wrapper == null) wrapper = new HammerSprite(current, physics);
            else wrapper.SetSource(current);
            player.SetSprite(wrapper);
        }
        protected override void LateUpdate(float delta) { Refresh(); }
        protected override void OnEnable() { Refresh(); }
        // Modal pages suspend components without unequipping their items.
        protected override void OnDisable() { RestoreSprite(); }
        protected override void OnOwnerDestroy() { Detach(); }
        private void RestoreSprite()
        {
            if (wrapper != null && ReferenceEquals(SpriteField.GetValue(player), wrapper)) player.SetSprite(wrapper.Source);
            wrapper = null;
        }
    }
}
