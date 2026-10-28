using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using MoreItems;

namespace JumpKingJetpack
{
    internal sealed class JetpackAirSpriteComponent : Component
    {
        private static readonly FieldInfo SpriteField =
            typeof(PlayerEntity).GetField(
                "m_sprite",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly PlayerEntity player;
        private readonly JetpackAirAnimation animation =
            new JetpackAirAnimation();

        internal JetpackAirSpriteComponent(PlayerEntity playerEntity)
        {
            player = playerEntity;
        }

        protected override void LateUpdate(float delta)
        {
            BodyComp body = player.m_body;
            bool supported = body.IsOnGround
                || (body.IsOnBlock(typeof(SandBlock))
                    && body.Velocity.Y >= 0f);
            if (!SettingsStore.Current.JetpackEquipped
                || JKRuntime.Gameplay.GameFeatures.IsMorphed
                || BallKingOwnsSprite()
                || supported)
            {
                return;
            }

            JetpackAirSprite sprite = animation.Select(body.Velocity.Y);
            player.SetSprite(
                sprite == JetpackAirSprite.Up
                    ? Game1.instance.contentManager.playerSprites.jump_up
                    : Game1.instance.contentManager.playerSprites.jump_fall);
        }

        private bool BallKingOwnsSprite()
        {
            object sprite = SpriteField == null
                ? null
                : SpriteField.GetValue(player);
            return JKRuntime.Gameplay.GameFeatures.FormOwnsSprite(sprite);
        }
    }
}
