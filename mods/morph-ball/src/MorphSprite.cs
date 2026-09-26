using System;
using System.Collections;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MorphBallMod
{
    internal sealed class MorphSprite : Sprite
    {
        internal Sprite Source { get; private set; }

        private readonly Texture2D ball;
        private readonly Sprite externalLayerSource;
        private readonly float amount;
        private readonly float angle;
        private readonly bool stickyAttached;
        private readonly Vector2 contactNormal;
        private readonly Vector2 contactOffset;

        internal MorphSprite(
            Sprite source,
            Texture2D ballTexture,
            Sprite accessorySource,
            float morphAmount,
            float rollAngle,
            bool isStickyAttached,
            Vector2 surfaceNormal,
            Vector2 surfaceContactOffset)
        {
            Source = source;
            ball = ballTexture;
            externalLayerSource = accessorySource;
            amount = Math.Max(0f, Math.Min(1f, morphAmount));
            angle = rollAngle;
            stickyAttached = isStickyAttached;
            contactNormal = surfaceNormal;
            contactOffset = surfaceContactOffset;
        }

        public override void Draw(
            Vector2 position,
            SpriteEffects effects = SpriteEffects.None)
        {
            float smooth = amount * amount * (3f - 2f * amount);
            float ballMix = Math.Max(0f, Math.Min(1f, (amount - 0.48f) / 0.32f));
            Vector2 anchor = position + new Vector2(
                0f,
                -17f * smooth);
            anchor += contactOffset * smooth;
            DrawJetpackLayers(
                externalLayerSource,
                position + new Vector2(0f, -4f),
                effects);
            if (ballMix < 1f)
            {
                Vector2 scale = new Vector2(
                    1f - 0.48f * smooth,
                    1f - 0.62f * smooth);
                DrawLayers(
                    Source,
                    anchor,
                    scale,
                    1f - ballMix,
                    effects);
            }
            if (ball != null && ballMix > 0f)
            {
                DrawContactPatch(anchor, ballMix, effects);
                float ballScale = 0.72f + 0.28f * ballMix;
                Game1.spriteBatch.Draw(
                    ball,
                    anchor,
                    null,
                    new Color(ballMix, ballMix, ballMix, ballMix),
                    angle,
                    new Vector2(
                        OutfitTextureBuilder.BallSize / 2f,
                        OutfitTextureBuilder.BallSize / 2f),
                    new Vector2(ballScale, ballScale),
                    effects,
                    0f);
            }
        }

        private void DrawContactPatch(
            Vector2 anchor,
            float ballMix,
            SpriteEffects effects)
        {
            Vector2 normal;
            if (!stickyAttached
                || !SurfaceMotion.TryNormalize(contactNormal, out normal))
            {
                return;
            }
            Vector2 tangent = new Vector2(-normal.Y, normal.X);
            float rotation = (float)Math.Atan2(tangent.Y, tangent.X);
            Vector2 position = anchor
                - normal * (OutfitTextureBuilder.BallSize * 0.46f);
            Vector2 scale = new Vector2(
                0.64f,
                0.25f);
            float alpha = ballMix * 0.95f;
            Game1.spriteBatch.Draw(
                ball,
                position,
                null,
                new Color(alpha, alpha, alpha, alpha),
                rotation,
                new Vector2(
                    OutfitTextureBuilder.BallSize / 2f,
                    OutfitTextureBuilder.BallSize / 2f),
                scale,
                effects,
                0f);
        }

        public override void Draw(
            float x,
            float y,
            SpriteEffects effects = SpriteEffects.None)
        {
            Draw(new Vector2(x, y), effects);
        }

        public override void Draw(
            Point position,
            SpriteEffects effects = SpriteEffects.None)
        {
            Draw(position.ToVector2(), effects);
        }

        public override void Draw(
            Rectangle destination,
            SpriteEffects effects = SpriteEffects.None)
        {
            Draw(destination.Center.ToVector2(), effects);
        }

        private static void DrawLayers(
            Sprite source,
            Vector2 position,
            Vector2 scale,
            float alpha,
            SpriteEffects effects)
        {
            IList layers = SpriteLayerAccess.GetLayersOrSelf(source);
            foreach (object value in layers)
            {
                Sprite layer = value as Sprite;
                if (layer == null
                    || SpriteLayerAccess.IsJetpackLayer(layer)
                    || layer.texture == null)
                {
                    continue;
                }
                Color color = layer.GetColor();
                color = new Color(
                    color.R,
                    color.G,
                    color.B,
                    (int)(color.A * alpha));
                Game1.spriteBatch.Draw(
                    layer.texture,
                    position,
                    layer.source,
                    color,
                    0f,
                    layer.source.Size.ToVector2() * layer.center,
                    scale,
                    effects,
                    0f);
            }
        }

        private static void DrawJetpackLayers(
            Sprite source,
            Vector2 position,
            SpriteEffects effects)
        {
            if (source == null)
            {
                return;
            }
            IList layers = SpriteLayerAccess.GetLayersOrSelf(source);
            foreach (object value in layers)
            {
                Sprite layer = value as Sprite;
                if (SpriteLayerAccess.IsJetpackLayer(layer))
                {
                    layer.Draw(position, effects);
                }
            }
        }
    }
}
