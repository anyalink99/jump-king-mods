using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.Player;

namespace HammerKing
{
    internal sealed class HammerVisual : Component
    {
        private readonly PlayerEntity player;
        private HammerSprite wrapper;
        private HammerPhysics physics;
        private System.IDisposable visual;
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
            if(visual!=null)return;
            visual=JKRuntime.Presentation.PlayerVisuals.Register(player,"more-items.hammer",JKRuntime.Presentation.VisualPhase.Attachment,
                source=>{if(wrapper==null)wrapper=new HammerSprite(source,physics);else wrapper.SetSource(source);return wrapper;});
        }
        protected override void LateUpdate(float delta) { Refresh(); }
        protected override void OnEnable() { Refresh(); }
        // Modal pages suspend components without unequipping their items
        protected override void OnDisable() { RestoreSprite(); }
        protected override void OnOwnerDestroy() { Detach(); }
        private void RestoreSprite()
        {
            if(visual!=null)visual.Dispose();visual=null;
            wrapper = null;
        }
    }
}
