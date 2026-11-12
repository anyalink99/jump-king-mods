using System;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Replays
{
    internal sealed class ReplayPlayerPresentation : IDisposable
    {
        private PlayerEntity player;
        private ReplayPresentationSprite presentation;
        private bool prepared;
        private IDisposable visual;
        internal ReplayCosmetics Cosmetics;
        private ReplayVisualActor actor;
        internal ReplayData Replay;

        internal void Prepare()
        {
            if (prepared) return;
            player = EntityManager.instance.Find<PlayerEntity>();
            if (player == null)
                throw new InvalidOperationException(
                    "Jump King player is not ready for replay playback");
            presentation = new ReplayPresentationSprite();
            if(Replay!=null)actor=new ReplayVisualActor(Replay);
            presentation.Cosmetics = Cosmetics;
            visual=JKRuntime.Presentation.PlayerVisuals.Register(player,"replays",JKRuntime.Presentation.VisualPhase.Replacement,source=>presentation);
            prepared = true;
        }

        internal void Present(ReplayFrame frame, int[] equippedItems,byte[] visualState=null,int index=-1,bool cut=false)
        {
            if (!prepared || player == null || !player.IsAlive) return;
            Sprite sprite;
            using(Cosmetics==null?null:Cosmetics.BeginDraw(Color.White))
                sprite=actor!=null&&index>=0?actor.Present(index,cut):ReplayGhostRenderer.ResolveSprite(frame.Pose,equippedItems);
            presentation.SetFrame(
                sprite,
                frame.DrawAnchor,
                (frame.Flags & ReplayFrameFlags.FacingLeft) != 0
                    ? SpriteEffects.FlipHorizontally
                    : SpriteEffects.None);
        }

        public void Dispose()
        {
            if(actor!=null)actor.Dispose();actor=null;
            if (!prepared) return;
            if(visual!=null)visual.Dispose();visual=null;
            if (player != null && player.IsAlive)
            {
                // The registry restores the latest native pose, including outfit changes during playback.
                if (player.m_body != null)
                    Camera.UpdateCamera(player.m_body.GetHitbox().Center);
            }
            prepared = false;
            player = null;
            presentation = null;
        }

        private sealed class ReplayPresentationSprite : Sprite
        {
            private Sprite target;
            private Vector2 worldAnchor;
            private SpriteEffects targetEffects;
            internal ReplayCosmetics Cosmetics;

            internal void SetFrame(
                Sprite sprite,
                Vector2 anchor,
                SpriteEffects effects)
            {
                target = sprite;
                worldAnchor = anchor;
                targetEffects = effects;
            }

            public override void Draw(
                Vector2 position,
                SpriteEffects effects = SpriteEffects.None)
            {
                DrawTarget();
            }

            public override void Draw(
                float x,
                float y,
                SpriteEffects effects = SpriteEffects.None)
            {
                DrawTarget();
            }

            public override void Draw(
                Point position,
                SpriteEffects effects = SpriteEffects.None)
            {
                DrawTarget();
            }

            public override void Draw(
                Rectangle destination,
                SpriteEffects effects = SpriteEffects.None)
            {
                DrawTarget();
            }

            private void DrawTarget()
            {
                if (target == null) return;
                using (Cosmetics == null ? null : Cosmetics.BeginDraw(Color.White)) target.Draw(
                    Camera.TransformVector2(worldAnchor),
                    targetEffects);
            }
        }
    }
}
