using System;
using System.Collections;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MorphBallMod
{
    internal static class OutfitTextureBuilder
    {
        internal const int BallSize = 18;

        internal static Texture2D Build(Sprite layeredSprite)
        {
            var filtered=JKRuntime.Presentation.PlayerAppearance.Filter(layeredSprite,JKRuntime.Presentation.AppearanceLayerRole.ExcludeFromForm|JKRuntime.Presentation.AppearanceLayerRole.WorldEffect);
            var sample=JKRuntime.Presentation.PlayerAppearance.FromSprite(filtered,Microsoft.Xna.Framework.Vector2.Zero,SpriteEffects.None,"charge");
            var pixels=JKRuntime.Presentation.AppearanceCapture.Read(sample,512,true);
            int width=pixels.Width,height=pixels.Height;
            Color[] composite=pixels.Pixels;
            Rectangle bounds = FindBounds(composite, width, height);
            Color[] ball = BuildBall(composite, width, height, bounds);
            Texture2D texture = new Texture2D(
                Game1.instance.GraphicsDevice,
                BallSize,
                BallSize,
                false,
                SurfaceFormat.Color);
            texture.SetData(ball);
            return texture;
        }

        private static Rectangle FindBounds(
            Color[] pixels,
            int width,
            int height)
        {
            int left = width;
            int top = height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].A <= 8)
                    {
                        continue;
                    }
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
            if (right < left || bottom < top)
            {
                return new Rectangle(0, 0, width, height);
            }
            return new Rectangle(
                left,
                top,
                right - left + 1,
                bottom - top + 1);
        }

        private static Color[] BuildBall(
            Color[] source,
            int sourceWidth,
            int sourceHeight,
            Rectangle bounds)
        {
            Color[] result = new Color[BallSize * BallSize];
            int square = Math.Max(bounds.Width, bounds.Height) + 2;
            float centerX = bounds.X + (bounds.Width - 1) / 2f;
            float centerY = bounds.Y + (bounds.Height - 1) / 2f;
            float center = (BallSize - 1) / 2f;
            float radius = BallSize / 2f;
            for (int y = 0; y < BallSize; y++)
            {
                for (int x = 0; x < BallSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (distance > radius)
                    {
                        continue;
                    }
                    float normalizedX = dx / radius;
                    float normalizedY = dy / radius;
                    int sourceX = (int)Math.Round(
                        centerX + normalizedX * (square - 1) / 2f);
                    int sourceY = (int)Math.Round(
                        centerY + normalizedY * (square - 1) / 2f);
                    Color color = FindNearestOpaque(
                        source,
                        sourceWidth,
                        sourceHeight,
                        sourceX,
                        sourceY);
                    float edge = Math.Max(0f, (distance / radius - 0.68f) / 0.32f);
                    float shade = 1f - edge * 0.38f;
                    result[y * BallSize + x] = new Color(
                        (int)(color.R * shade),
                        (int)(color.G * shade),
                        (int)(color.B * shade),
                        color.A);
                }
            }
            return result;
        }

        private static Color FindNearestOpaque(
            Color[] source,
            int width,
            int height,
            int x,
            int y)
        {
            x = Math.Max(0, Math.Min(width - 1, x));
            y = Math.Max(0, Math.Min(height - 1, y));
            Color direct = source[y * width + x];
            if (direct.A > 8)
            {
                return direct;
            }
            for (int radius = 1; radius <= 10; radius++)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    for (int offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        if (Math.Abs(offsetX) != radius
                            && Math.Abs(offsetY) != radius)
                        {
                            continue;
                        }
                        int sampleX = x + offsetX;
                        int sampleY = y + offsetY;
                        if (sampleX < 0 || sampleX >= width
                            || sampleY < 0 || sampleY >= height)
                        {
                            continue;
                        }
                        Color sample = source[sampleY * width + sampleX];
                        if (sample.A > 8)
                        {
                            return sample;
                        }
                    }
                }
            }
            return Color.Transparent;
        }
    }
}
