using System;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private void UpdateWaterSurfaces(int currentScreen)
        {
            PlayerEntity player = GameLoopPlayer();
            Rectangle hitbox = player == null ? Rectangle.Empty : Camera.TransformRect(player.m_body.GetHitbox());
            Vector2 velocity = player == null ? Vector2.Zero : player.m_body.Velocity;
            foreach (WaterData water in work.Waters.At(currentScreen))
            {
                if (!water.Interactive || water.Screen != currentScreen) continue;
                WaterSurfaceState state = GetWaterState(water);
                state.Surface.Configure(water.SurfaceTension, water.WaveSpread, water.WaveDamping);
                bool horizontal = hitbox.Right > water.X && hitbox.Left < water.X + water.Width;
                bool inside = player != null && horizontal && hitbox.Bottom > water.Y
                    && hitbox.Top < water.Y + water.Height;
                float contactX = player == null ? water.X + water.Width * 0.5f
                    : MathHelper.Clamp(hitbox.Center.X, water.X, water.X + water.Width);
                float normalizedX = (contactX - water.X) / Math.Max(1f, water.Width);
                if (state.ContactKnown && inside != state.WasInside)
                {
                    float direction = inside ? 1f : -1f;
                    float strength = water.SplashStrength * MathHelper.Clamp(4.2f + Math.Abs(velocity.Y) * 2.4f, 4.2f, 24f);
                    state.Surface.Impulse(normalizedX, direction * strength, 0.07f);
                    SpawnSplash(state, water, contactX, velocity, inside, strength);
                }
                if (inside)
                {
                    state.WakeClock -= frameDelta;
                    if (state.WakeClock <= 0f && (Math.Abs(velocity.X) > 0.12f || Math.Abs(velocity.Y) > 0.2f))
                    {
                        float wake = water.WakeStrength * MathHelper.Clamp(1.2f + Math.Abs(velocity.X) + Math.Abs(velocity.Y) * 0.35f, 1.2f, 7f);
                        state.Surface.Impulse(normalizedX, wake, 0.035f);
                        state.WakeClock = 0.075f;
                    }
                }
                state.ContactKnown = true;
                state.WasInside = inside;
                state.Surface.Step(frameDelta);
                UpdateSplashes(state);
            }
        }

        private WaterSurfaceState GetWaterState(WaterData water)
        {
            WaterSurfaceState state;
            if (!waterSurfaces.TryGetValue(water.Id, out state))
            { state = new WaterSurfaceState(water); waterSurfaces.Add(water.Id, state); }
            return state;
        }

        private void SpawnSplash(WaterSurfaceState state, WaterData water, float x, Vector2 playerVelocity,
            bool entering, float strength)
        {
            int count = Math.Max(7, Math.Min(24, (int)(strength * 0.9f)));
            int seed = StableHash(water.Id) ^ (int)(time * 1000f) ^ (entering ? 0x4512 : 0x2871);
            for (int i = 0; i < count; i++)
            {
                float side = Hash01(seed + i * 71) * 2f - 1f;
                float lift = 0.65f + Hash01(seed + i * 97) * 0.85f;
                state.Splashes.Add(new SplashParticle {
                    X = x + side * (4f + strength * 0.3f), Y = water.Y,
                    VelocityX = side * (13f + strength * 1.1f) + playerVelocity.X * 4f,
                    VelocityY = -(24f + strength * 2.2f) * lift - Math.Abs(playerVelocity.Y) * 1.8f,
                    Life = 0f, Lifetime = 0.38f + Hash01(seed + i * 131) * 0.46f,
                    Size = 0.7f + Hash01(seed + i * 173) * 1.7f
                });
            }
        }

        private void UpdateSplashes(WaterSurfaceState state)
        {
            for (int i = state.Splashes.Count - 1; i >= 0; i--)
            {
                SplashParticle particle = state.Splashes[i];
                particle.Life += frameDelta;
                if (particle.Life >= particle.Lifetime) { state.Splashes.RemoveAt(i); continue; }
                particle.VelocityY += 68f * frameDelta;
                particle.VelocityX *= (float)Math.Exp(-1.7f * frameDelta);
                particle.X += particle.VelocityX * frameDelta;
                particle.Y += particle.VelocityY * frameDelta;
            }
        }

        private void DrawWater(WaterData water)
        {
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            Color near = prepared.Color(water.Color);
            Color deep = prepared.Color(water.Color2);
            // A water plane is composited in optical order.  The first pass is only
            // the dark body below the surface; reflected planes remain readable on
            // top of it, then a depth glaze and the specular ripples attenuate them.
            for (int row = 0; row < water.Height; row += 2)
            {
                float depth = row / (float)Math.Max(1, water.Height - 1);
                Color color = MultiplyAlpha(Color.Lerp(near, deep, depth * depth),
                    water.Opacity * (0.42f + depth * 0.26f));
                Game1.spriteBatch.Draw(pixel, new Rectangle(water.X, water.Y + row, water.Width, Math.Min(2, water.Height - row)), color);
            }
            if (!water.CompositeReflection)
            {
                DrawSceneReflection(water);
                if (water.Reflection) DrawReflection(water);
            }

            for (int row = 0; row < water.Height; row += 2)
            {
                float depth = row / (float)Math.Max(1, water.Height - 1);
                Color glaze = Color.Lerp(near, deep, 0.35f + depth * 0.65f);
                Game1.spriteBatch.Draw(pixel,
                    new Rectangle(water.X, water.Y + row, water.Width, Math.Min(2, water.Height - row)),
                    MultiplyAlpha(glaze, water.Opacity * (0.10f + depth * 0.42f)));
            }

            Color highlight = prepared.Color(water.Highlight);
            DrawLine(new Vector2(water.X, water.Y + 0.5f),
                new Vector2(water.X + water.Width, water.Y + 0.5f),
                MultiplyAlpha(highlight, 0.17f), 0.72f);
            int lines = Math.Max(3, water.Height / 38);
            for (int i = 0; i < lines; i++)
            {
                float depth = (i + 1f) / (lines + 1f);
                float y = water.Y + depth * depth * water.Height;
                float wavelength = 34f + depth * 54f;
                float amplitude = water.Ripple * (0.25f + depth * 0.9f);
                float phase = time * (0.55f + i * 0.035f) + i * 1.73f;
                Vector2 previous = new Vector2(water.X, y + (float)Math.Sin(phase) * amplitude);
                for (float x = water.X + 5f; x <= water.X + water.Width; x += 5f)
                {
                    Vector2 next = new Vector2(Math.Min(x, water.X + water.Width), y + (float)Math.Sin(phase + x / wavelength * MathHelper.TwoPi) * amplitude);
                    float shimmer = 0.018f + 0.032f * (0.5f + 0.5f * (float)Math.Sin(i * 4.7f + x * 0.071f));
                    DrawLine(previous, next, MultiplyAlpha(highlight, shimmer), 0.55f + depth * 0.45f);
                    previous = next;
                }
            }
            if (water.GlintX >= water.X && water.GlintX <= water.X + water.Width) DrawMoonGlint(water, highlight);
        }

        private void DrawMoonGlint(WaterData water, Color highlight)
        {
            int bands = Math.Max(6, water.Height / 16);
            for (int i = 0; i < bands; i++)
            {
                float depth = (i + 1f) / (bands + 1f);
                float y = water.Y + depth * water.Height;
                float spread = 3f + depth * depth * 31f;
                float center = water.GlintX + (float)Math.Sin(time * 0.7f + i * 2.31f) * spread * 0.35f;
                float width = 2f + depth * 10f * (0.55f + 0.45f * (float)Math.Sin(time * 1.1f + i * 1.7f));
                DrawLine(new Vector2(center - width, y), new Vector2(center + width, y),
                    MultiplyAlpha(highlight, 0.045f + (1f - depth) * 0.085f), 0.65f);
            }
        }

        private void DrawSceneReflection(WaterData water)
        {
            string[] ids = (water.ReflectionAssets ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in ids)
            {
                SceneTexture asset;
                if (!vectors.TryGetValue(raw.Trim(), out asset) || asset.Texture.Width < water.X + water.Width) continue;
                for (int destinationY = water.Y; destinationY < water.Y + water.Height; destinationY += 2)
                {
                    float depth = destinationY - water.Y;
                    int sourceY = (int)(water.Y - depth / water.ReflectionScaleY);
                    int sourceHeight = Math.Max(1, (int)Math.Ceiling(2f / water.ReflectionScaleY));
                    if (sourceY < 0) break;
                    if (sourceY + sourceHeight > asset.Texture.Height) sourceHeight = asset.Texture.Height - sourceY;
                    if (sourceHeight <= 0) continue;
                    float ripple = (float)Math.Sin(time * 1.35f + destinationY * 0.19f) * water.Ripple
                        * (0.15f + depth / Math.Max(1f, water.Height));
                    float normalizedDepth = depth / Math.Max(1f, water.Height);
                    float fresnel = 0.98f - normalizedDepth * 0.58f;
                    int perspectiveExpansion = (int)(normalizedDepth * normalizedDepth * water.Width * 0.035f);
                    Color tint = MultiplyAlpha(new Color(178, 204, 211),
                        water.SceneReflectionOpacity * fresnel);
                    Game1.spriteBatch.Draw(asset.Texture,
                        new Rectangle(water.X - perspectiveExpansion / 2 + (int)ripple, destinationY,
                            water.Width + perspectiveExpansion, Math.Min(2, water.Y + water.Height - destinationY)),
                        new Rectangle(water.X, sourceY, water.Width, sourceHeight), tint);
                }
            }
            foreach (PropData node in scene.Nodes ?? new PropData[0])
                if (node.Screen == Camera.CurrentScreenIndex1 && node.Reflect) DrawNodeReflection(node, water);
        }

        private void DrawNodeReflection(PropData node, WaterData water)
        {
            SceneTexture asset;
            if (!vectors.TryGetValue(node.Asset ?? "", out asset)) return;
            PropPose pose = ResolvePropPose(node);
            if (!pose.Visible) return;
            float scaleX = pose.Scale * pose.ScaleX, scaleY = pose.Scale * pose.ScaleY;
            float originX = node.OriginX < 0f ? asset.FrameWidth / 2f : node.OriginX;
            float originY = node.OriginY < 0f ? asset.FrameHeight / 2f : node.OriginY;
            float left = pose.Position.X - originX * scaleX;
            Color tint = MultiplyAlpha(new Color(182, 207, 210), water.SceneReflectionOpacity * pose.Opacity);
            for (int sy = 0; sy < asset.FrameHeight; sy += 3)
            {
                float worldY = pose.Position.Y + (sy - originY) * scaleY;
                if (worldY > water.Y) continue;
                int dy = (int)(water.Y + (water.Y - worldY) * water.ReflectionScaleY);
                if (dy < water.Y || dy >= water.Y + water.Height) continue;
                float ripple = (float)Math.Sin(time * 1.6f + dy * 0.23f) * water.Ripple;
                Rectangle source = new Rectangle(0, sy, asset.FrameWidth, Math.Min(3, asset.FrameHeight - sy));
                Rectangle destination = new Rectangle((int)(left + ripple), dy,
                    Math.Max(1, (int)(asset.FrameWidth * scaleX)), Math.Max(1, (int)(3 * scaleY * water.ReflectionScaleY) + 1));
                Game1.spriteBatch.Draw(asset.Texture, destination, source, tint);
            }
        }

        private void DrawReflection(WaterData water)
        {
            PlayerEntity player = GameLoopPlayer();
            Sprite sprite; Texture2D texture; Rectangle source; SpriteEffects flip;
            if (!TryPlayerSprite(player, out sprite, out texture, out source, out flip)) return;
            Rectangle hitbox = Camera.TransformRect(player.m_body.GetHitbox());
            if (hitbox.Right < water.X - 20 || hitbox.Left > water.X + water.Width + 20 || hitbox.Bottom > water.Y + 3) return;
            float distance = Math.Max(0f, water.Y - hitbox.Bottom);
            int reflectedHeight = Math.Max(7, (int)(source.Height * water.ReflectionScaleY));
            int reflectedWidth = Math.Max(8, (int)(source.Width * (1.02f + water.ReflectionScaleY * 0.2f)));
            int top = water.Y + 1 + (int)(distance * water.ReflectionScaleY * 0.92f);
            int left = hitbox.Center.X - reflectedWidth / 2;
            flip ^= SpriteEffects.FlipVertically;
            Color tint = MultiplyAlpha(Color.Lerp(new Color(61, 97, 119), sprite.GetColor(), 0.72f), water.ReflectionOpacity);
            int slices = Math.Min(source.Height, 9);
            for (int i = 0; i < slices; i++)
            {
                int sourceTop = source.Y + i * source.Height / slices;
                int sourceBottom = source.Y + (i + 1) * source.Height / slices;
                int destinationTop = top + i * reflectedHeight / slices;
                int destinationBottom = top + (i + 1) * reflectedHeight / slices;
                float ripple = (float)Math.Sin(time * 2.1f + i * 0.93f) * water.Ripple * (0.35f + i / (float)slices);
                Rectangle src = new Rectangle(source.X, sourceTop, source.Width, Math.Max(1, sourceBottom - sourceTop));
                Rectangle dst = new Rectangle(left + (int)ripple, destinationTop, reflectedWidth, Math.Max(1, destinationBottom - destinationTop));
                Game1.spriteBatch.Draw(texture, dst, src, MultiplyAlpha(tint, 1f - i / (float)(slices + 3)), 0f, Vector2.Zero, flip, 0f);
            }
        }

        private void DrawCompositeReflection(RenderTarget2D frame, WaterData water)
        {
            if (water.ReflectionOpacity <= 0) return;
            WaterSurfaceState state = water.Interactive ? GetWaterState(water) : null;
            int bottom = Math.Min(frame.Height, water.Y + water.Height);
            for (int destinationY = Math.Max(0, water.Y); destinationY < bottom; destinationY += 2)
            {
                float depth = destinationY - water.Y;
                float normalizedDepth = depth / Math.Max(1f, water.Height);
                int sourceY = (int)(water.Y - depth / water.ReflectionScaleY);
                int sourceHeight = Math.Max(1, (int)Math.Ceiling(2f / water.ReflectionScaleY));
                if (sourceY < 0) break;
                if (sourceY + sourceHeight > frame.Height) sourceHeight = frame.Height - sourceY;
                if (sourceHeight <= 0) continue;
                int perspectiveExpansion = (int)(normalizedDepth * normalizedDepth * water.Width * 0.045f);
                float fresnel = 1f - normalizedDepth * 0.62f;
                Color tint = MultiplyAlpha(new Color(193, 216, 220), water.ReflectionOpacity * fresnel);
                int meshColumns = Math.Max(12, Math.Min(64, water.Width / 10));
                for (int column = 0; column < meshColumns; column++)
                {
                    float nx0 = column / (float)meshColumns;
                    float nx1 = (column + 1f) / meshColumns;
                    int sx0 = water.X + (int)(nx0 * water.Width);
                    int sx1 = water.X + (int)(nx1 * water.Width);
                    if (sx0 >= frame.Width) break;
                    sx1 = Math.Min(sx1, frame.Width);
                    float lateralPhase = nx0 * MathHelper.TwoPi * 2.7f;
                    float nearWave = (float)Math.Sin(time * 1.17f + destinationY * 0.21f + lateralPhase);
                    float crossWave = (float)Math.Sin(time * -0.73f + destinationY * 0.071f - lateralPhase * 0.37f);
                    float ripple = (nearWave * 0.68f + crossWave * 0.32f) * water.Ripple
                        * (0.28f + normalizedDepth * 1.25f);
                    if (state != null)
                        ripple += state.Surface.Sample((nx0 + nx1) * 0.5f) * (1f - normalizedDepth * 0.72f);
                    int expandedWidth = water.Width + perspectiveExpansion;
                    int dx0 = water.X - perspectiveExpansion / 2 + (int)(nx0 * expandedWidth + ripple);
                    int dx1 = water.X - perspectiveExpansion / 2 + (int)(nx1 * expandedWidth + ripple);
                    Rectangle src = new Rectangle(sx0, sourceY, Math.Max(1, sx1 - sx0), sourceHeight);
                    Rectangle dst = new Rectangle(dx0, destinationY, Math.Max(1, dx1 - dx0 + 1),
                        Math.Min(2, bottom - destinationY));
                    Game1.spriteBatch.Draw(frame, dst, src, tint);
                }
            }
        }

        private void DrawCompositeHighlights(WaterData water)
        {
            Color highlight = prepared.Color(water.Highlight);
            WaterSurfaceState state = water.Interactive ? GetWaterState(water) : null;
            if (state == null)
                DrawLine(new Vector2(water.X, water.Y + 0.5f), new Vector2(water.X + water.Width, water.Y + 0.5f),
                    MultiplyAlpha(highlight, 0.22f), 0.65f);
            else
            {
                int samples = Math.Max(24, Math.Min(160, state.Surface.SegmentCount));
                Vector2 previous = new Vector2(water.X, water.Y + state.Surface.Sample(0f));
                for (int i = 1; i <= samples; i++)
                {
                    float nx = i / (float)samples;
                    Vector2 next = new Vector2(water.X + nx * water.Width, water.Y + state.Surface.Sample(nx));
                    DrawLine(previous, next, MultiplyAlpha(highlight, 0.34f), 0.9f);
                    DrawLine(previous + new Vector2(0f, 1.2f), next + new Vector2(0f, 1.2f),
                        MultiplyAlpha(new Color(24, 83, 102), 0.24f), 1.1f);
                    previous = next;
                }
                DrawSplashParticles(state, water, highlight);
            }
            int bands = Math.Max(5, water.Height / 16);
            for (int i = 0; i < bands; i++)
            {
                float depth = (i + 1f) / (bands + 1f);
                float y = water.Y + depth * depth * water.Height;
                float center = water.GlintX >= water.X ? water.GlintX : water.X + water.Width * 0.5f;
                center += (float)Math.Sin(time * 0.83f + i * 1.91f) * (2f + depth * 9f);
                float half = 2f + depth * depth * 14f;
                DrawLine(new Vector2(center - half, y), new Vector2(center + half, y),
                    MultiplyAlpha(highlight, 0.045f + (1f - depth) * 0.09f), 0.7f);
            }
        }

        private void DrawSplashParticles(WaterSurfaceState state, WaterData water, Color highlight)
        {
            foreach (SplashParticle particle in state.Splashes)
            {
                float life = particle.Life / particle.Lifetime;
                float opacity = (1f - life) * (1f - life);
                int width = Math.Max(1, (int)(particle.Size * (1f + life)));
                int height = Math.Max(1, (int)(particle.Size * (2.2f - life)));
                Game1.spriteBatch.Draw(radial, new Rectangle((int)particle.X - width / 2,
                    (int)particle.Y - height / 2, width, height), MultiplyAlpha(highlight, opacity * 0.72f));
            }
        }
    }
}
