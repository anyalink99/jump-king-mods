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
        private IDisposable visual;
        private JKRuntime.Presentation.AppearanceImage transitionImage;
        private long appearanceRevision;

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
            float amount = controller.MorphAmount;
            if (amount <= 0f) { RestoreSprite();previousAmount=0;return; }
            Sprite outfit = Game1.instance.contentManager
                .playerSprites.jump_charge;
            int outfitSignature = SpriteLayerAccess.GetSignature(outfit);
            if (ballTexture == null
                || previousAmount <= 0f
                || !object.ReferenceEquals(capturedOutfit, outfit)
                || capturedOutfitSignature != outfitSignature
                || appearanceRevision != JKRuntime.Presentation.PlayerAppearance.Revision)
            {
                try {RebuildBall(outfit, outfitSignature);}
                catch(Exception error){System.Diagnostics.Debug.WriteLine("Ball appearance: "+error.Message);}
            }
            if(transitionImage!=null){transitionImage.Dispose();transitionImage=null;}
            if(amount<.8f)
            {
                try {
                    var sample=JKRuntime.Presentation.PlayerAppearance.Resolve(player,JKRuntime.Presentation.AppearanceStage.Outfit);
                    var filtered=JKRuntime.Presentation.PlayerAppearance.Filter(sample.Sprite,JKRuntime.Presentation.AppearanceLayerRole.ExcludeFromForm|JKRuntime.Presentation.AppearanceLayerRole.WorldEffect);
                    transitionImage=JKRuntime.Presentation.AppearanceCapture.Freeze(JKRuntime.Presentation.PlayerAppearance.FromSprite(filtered,sample.WorldAnchor,sample.Facing));
                } catch(System.Exception error) {System.Diagnostics.Debug.WriteLine("Ball transition capture: "+error.Message);}
            }
            if(visual==null) visual=JKRuntime.Presentation.PlayerVisuals.Register(player,"morph-ball",
                JKRuntime.Presentation.VisualPhase.Form,source=>new MorphSprite(source,ballTexture,source,
                    controller.MorphAmount,controller.RollAngle,controller.StickyAttached,controller.ContactNormal,
                    controller.GetVisualContactOffset(OutfitTextureBuilder.BallSize/2f),transitionImage),()=>controller.MorphAmount>0);
            previousAmount = amount;
        }

        protected override void OnDisable()
        {
            RestoreSprite();
            DisposeTexture();
        }

        protected override void OnOwnerDestroy()
        {
            RestoreSprite();
            DisposeTexture();
        }

        private void RebuildBall(Sprite outfit, int signature)
        {
            var next = OutfitTextureBuilder.Build(outfit);
            DisposeTexture();
            ballTexture = next;
            capturedOutfit = outfit;
            capturedOutfitSignature = signature;
            appearanceRevision=JKRuntime.Presentation.PlayerAppearance.Revision;
        }

        private void RestoreSprite()
        { if(visual!=null)visual.Dispose();visual=null;if(transitionImage!=null)transitionImage.Dispose();transitionImage=null; }

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
