using System;
using System.Collections;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JumpKingJetpack
{
    internal sealed class JetpackFlameLayer : IDisposable
    {
        private readonly Texture2D[] ignitionTextures;
        private readonly Texture2D[] sustainedTextures;
        private readonly Texture2D[] shutdownTextures;
        private readonly IList[] layers;
        private readonly Sprite[] sprites;
        private readonly JetpackFireTrail trail;
        private readonly JetpackFlameAnimation animation =
            new JetpackFlameAnimation();

        private bool visible = true;
        private bool disposed;
        private readonly JKRuntime.RuntimeScope resources = new JKRuntime.RuntimeScope();

        internal JetpackFlameLayer(
            Sprite[] targetSprites,
            JetpackFireTrail fireTrail)
        {
            if (targetSprites == null)
            {
                throw new ArgumentNullException("targetSprites");
            }
            if (targetSprites.Length != JetpackAtlasData.RegularPoseCount)
            {
                throw new ArgumentException(
                    "Invalid regular sprite count",
                    "targetSprites");
            }
            if (fireTrail == null)
            {
                throw new ArgumentNullException("fireTrail");
            }

            trail = fireTrail;

            ignitionTextures = new Texture2D[
                JetpackFlameData.IgnitionFrameCount];
            sustainedTextures = new Texture2D[JetpackFlameData.FrameCount];
            shutdownTextures = new Texture2D[
                JetpackFlameData.ShutdownFrameCount];
            layers = new IList[targetSprites.Length];
            sprites = new Sprite[targetSprites.Length];
            try
            {
                for (int frame = 0; frame < ignitionTextures.Length; frame++)
                {
                    ignitionTextures[frame] = resources.Own(CreateTexture(
                        JetpackFlameData.GetIgnitionFrame(frame)));
                }
                for (int frame = 0; frame < sustainedTextures.Length; frame++)
                {
                    sustainedTextures[frame] = resources.Own(CreateTexture(
                        JetpackFlameData.GetFrame(frame)));
                }
                for (int frame = 0; frame < shutdownTextures.Length; frame++)
                {
                    shutdownTextures[frame] = resources.Own(CreateTexture(
                        JetpackFlameData.GetShutdownFrame(frame)));
                }
                for (int pose = 0; pose < targetSprites.Length; pose++)
                {
                    int nozzleX;
                    int nozzleY;
                    bool rotated;
                    JetpackAtlasData.GetNozzle(
                        pose,
                        out nozzleX,
                        out nozzleY,
                        out rotated);

                    layers[pose] = SpriteLayerAccess.GetLayers(
                        targetSprites[pose]);
                    sprites[pose] = new FlameSprite(
                        this,
                        nozzleX,
                        nozzleY,
                        rotated);
                    var layer = layers[pose]; var sprite = sprites[pose];
                    resources.Defer(delegate { layer.Remove(sprite); });
                    layer.Insert(0, sprite);
                }
            }
            catch (Exception failure)
            {
                try { Dispose(); } catch (Exception cleanup) { throw new AggregateException("Jetpack flame initialization and cleanup failed", failure, cleanup); }
                throw;
            }
        }

        internal void SetActive(bool value)
        {
            if (disposed)
            {
                return;
            }
            animation.Update(value);
        }

        internal void SetVisible(bool value)
        {
            visible = value;
        }

        public void Dispose()
        {
            disposed = true;
            visible = false;
            resources.Dispose();
        }

        private void Draw(
            int nozzleX,
            int nozzleY,
            bool rotated,
            Vector2 position,
            SpriteEffects effects)
        {
            trail.Draw();
            if (!visible || !animation.Visible || disposed)
            {
                return;
            }
            if (effects == SpriteEffects.FlipHorizontally)
            {
                nozzleX = 47 - nozzleX;
            }

            Vector2 nozzle = position
                - new Vector2(24f, 48f)
                + new Vector2(nozzleX, nozzleY);
            float rotation = rotated
                ? (effects == SpriteEffects.FlipHorizontally
                    ? -MathHelper.PiOver2
                    : MathHelper.PiOver2)
                : 0f;

            Game1.spriteBatch.Draw(
                CurrentTexture(),
                nozzle,
                null,
                Color.White,
                rotation,
                new Vector2(JetpackFlameData.Width / 2, 0f),
                1f,
                effects,
                0f);
        }

        private Texture2D CurrentTexture()
        {
            switch (animation.Phase)
            {
                case JetpackFlamePhase.Ignition:
                    return ignitionTextures[animation.Frame];
                case JetpackFlamePhase.Sustained:
                    return sustainedTextures[animation.Frame];
                case JetpackFlamePhase.Shutdown:
                    return shutdownTextures[animation.Frame];
                default:
                    throw new InvalidOperationException(
                        "Jetpack flame is not visible");
            }
        }

        private static Texture2D CreateTexture(string[] rows)
        {
            Texture2D texture = new Texture2D(
                Game1.instance.GraphicsDevice,
                JetpackFlameData.Width,
                JetpackFlameData.Height,
                false,
                SurfaceFormat.Color);
            Color[] pixels = new Color[
                JetpackFlameData.Width * JetpackFlameData.Height];
            for (int y = 0; y < JetpackFlameData.Height; y++)
            {
                for (int x = 0; x < JetpackFlameData.Width; x++)
                {
                    pixels[y * JetpackFlameData.Width + x] =
                        PixelColor(rows[y][x]);
                }
            }
            try { texture.SetData(pixels); return texture; }
            catch { texture.Dispose(); throw; }
        }

        private static Color PixelColor(char value)
        {
            switch (value)
            {
                case 'r': return new Color(126, 32, 25);
                case 'o': return new Color(220, 74, 28);
                case 'y': return new Color(255, 164, 35);
                case 'w': return new Color(255, 238, 143);
                default: return Color.Transparent;
            }
        }

        private sealed class FlameSprite : Sprite
        {
            private readonly JetpackFlameLayer owner;
            private readonly int nozzleX;
            private readonly int nozzleY;
            private readonly bool rotated;

            internal FlameSprite(
                JetpackFlameLayer layer,
                int x,
                int y,
                bool isRotated)
            {
                owner = layer;
                nozzleX = x;
                nozzleY = y;
                rotated = isRotated;
            }

            public override void Draw(
                Vector2 position,
                SpriteEffects effects = SpriteEffects.None)
            {
                owner.Draw(
                    nozzleX,
                    nozzleY,
                    rotated,
                    position,
                    effects);
            }
        }
    }
}
