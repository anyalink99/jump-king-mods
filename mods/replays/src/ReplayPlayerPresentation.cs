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
        private static readonly FieldInfo SpriteField =
            typeof(PlayerEntity).GetField(
                "m_sprite",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private PlayerEntity player;
        private Sprite originalSprite;
        private ReplayPresentationSprite presentation;
        private bool prepared;

        internal void Prepare()
        {
            if (prepared) return;
            if (SpriteField == null)
                throw new InvalidOperationException(
                    "Jump King player-sprite contract is unavailable");
            player = EntityManager.instance.Find<PlayerEntity>();
            if (player == null)
                throw new InvalidOperationException(
                    "Jump King player is not ready for replay playback");
            originalSprite = SpriteField.GetValue(player) as Sprite;
            presentation = new ReplayPresentationSprite();
            SpriteField.SetValue(player, presentation);
            prepared = true;
        }

        internal void Present(ReplayFrame frame, int[] equippedItems)
        {
            if (!prepared || player == null || !player.IsAlive) return;
            if (!ReferenceEquals(SpriteField.GetValue(player), presentation))
                SpriteField.SetValue(player, presentation);
            presentation.SetFrame(
                ReplayGhostRenderer.ResolveSprite(
                    frame.Pose,
                    equippedItems),
                frame.DrawAnchor,
                (frame.Flags & ReplayFrameFlags.FacingLeft) != 0
                    ? SpriteEffects.FlipHorizontally
                    : SpriteEffects.None);
        }

        public void Dispose()
        {
            if (!prepared) return;
            if (player != null && player.IsAlive)
            {
                SpriteField.SetValue(player, originalSprite);
                if (player.m_body != null)
                    Camera.UpdateCamera(player.m_body.GetHitbox().Center);
            }
            prepared = false;
            player = null;
            originalSprite = null;
            presentation = null;
        }

        private sealed class ReplayPresentationSprite : Sprite
        {
            private Sprite target;
            private Vector2 worldAnchor;
            private SpriteEffects targetEffects;

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
                target.Draw(
                    Camera.TransformVector2(worldAnchor),
                    targetEffects);
            }
        }
    }
}
