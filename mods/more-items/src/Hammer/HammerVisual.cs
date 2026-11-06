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
            physics = value; Enabled = true;JKRuntime.Presentation.ActorVisuals.Changed(player); Refresh();
        }
        internal void Detach()
        {
            JKRuntime.Presentation.ActorVisuals.Changed(player);
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
        internal byte[] Capture()
        {
            if(physics==null||!physics.Ready)return null;
            var offset=physics.Head-JKRuntime.Presentation.AppearanceGeometry.Resolve(player).Anchor(player.m_body.Position);
            var state=new byte[9];System.Buffer.BlockCopy(System.BitConverter.GetBytes(offset.X),0,state,0,4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(offset.Y),0,state,4,4);state[8]=(byte)(physics.Contact?1:0);return state;
        }
        protected override void LateUpdate(float delta) { if(physics!=null)JKRuntime.Presentation.ActorVisuals.Changed(player);Refresh(); }
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
