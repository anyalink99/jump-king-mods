using System;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework.Graphics;

namespace MorphBallMod
{
    internal sealed class MorphVisualComponent : Component
    {
        private static readonly FieldInfo SpriteField =
            typeof(PlayerEntity).GetField(
                "m_sprite",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly PlayerEntity player;
        private MorphController controller;
        private Texture2D ballTexture;
        private Sprite capturedOutfit;
        private int capturedOutfitSignature;
        private float previousAmount;

        internal static void ValidateContract()
        {
            if (SpriteField == null)
            {
                throw new InvalidOperationException(
                    "Jump King player sprite is unavailable");
            }
        }

        internal MorphVisualComponent(
            PlayerEntity playerEntity,
            MorphController morphController)
        {
            ValidateContract();
            player = playerEntity;
            controller = morphController;
        }

        internal void AttachController(MorphController morphController)
        {
            if (morphController == null)
            {
                throw new ArgumentNullException("morphController");
            }
            controller = morphController;
            previousAmount = 0f;
        }

        protected override void LateUpdate(float delta)
        {
            Sprite current = SpriteField.GetValue(player) as Sprite;
            MorphSprite wrapper = current as MorphSprite;
            Sprite source = wrapper == null ? current : wrapper.Source;
            float amount = controller.MorphAmount;
            if (amount <= 0f)
            {
                if (wrapper != null)
                {
                    player.SetSprite(source);
                }
                previousAmount = 0f;
                return;
            }

            Sprite outfit = Game1.instance.contentManager
                .playerSprites.jump_charge;
            int outfitSignature = SpriteLayerAccess.GetSignature(outfit);
            if (ballTexture == null
                || previousAmount <= 0f
                || !object.ReferenceEquals(capturedOutfit, outfit)
                || capturedOutfitSignature != outfitSignature)
            {
                RebuildBall(outfit, outfitSignature);
            }
            player.SetSprite(new MorphSprite(
                source,
                ballTexture,
                outfit,
                amount,
                controller.RollAngle,
                controller.StickyAttached,
                controller.ContactNormal,
                controller.GetVisualContactOffset(
                    OutfitTextureBuilder.BallSize / 2f)));
            previousAmount = amount;
        }

        protected override void OnDisable()
        {
            RestoreSprite();
            DisposeTexture();
        }

        protected override void OnOwnerDestroy()
        {
            DisposeTexture();
        }

        private void RebuildBall(Sprite outfit, int signature)
        {
            DisposeTexture();
            ballTexture = OutfitTextureBuilder.Build(outfit);
            capturedOutfit = outfit;
            capturedOutfitSignature = signature;
        }

        private void RestoreSprite()
        {
            Sprite current = SpriteField.GetValue(player) as Sprite;
            MorphSprite wrapper = current as MorphSprite;
            if (wrapper != null)
            {
                player.SetSprite(wrapper.Source);
            }
        }

        private void DisposeTexture()
        {
            if (ballTexture != null)
            {
                ballTexture.Dispose();
            }
            ballTexture = null;
            capturedOutfit = null;
            capturedOutfitSignature = 0;
        }

    }
}
