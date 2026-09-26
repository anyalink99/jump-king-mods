using System;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private static void DrawFlexible(Texture2D texture, Rectangle source, Vector2 position, Vector2 origin,
            Vector2 scale, float bendRadians, Color tint, SpriteEffects effects, int sliceCount)
        {
            int slices = Math.Max(2, Math.Min(sliceCount, source.Height));
            float left = position.X - origin.X * scale.X;
            float top = position.Y - origin.Y * scale.Y;
            float bend = (float)Math.Tan(bendRadians) * source.Height * scale.Y;
            for (int i = 0; i < slices; i++)
            {
                int sy0 = source.Y + i * source.Height / slices;
                int sy1 = source.Y + (i + 1) * source.Height / slices;
                float normalizedTop = 1f - i / (float)slices;
                float offset = bend * normalizedTop * normalizedTop;
                int dy0 = (int)(top + (sy0 - source.Y) * scale.Y);
                int dy1 = (int)(top + (sy1 - source.Y) * scale.Y) + 1;
                Rectangle src = new Rectangle(source.X, sy0, source.Width, Math.Max(1, sy1 - sy0));
                Rectangle dst = new Rectangle((int)(left + offset), dy0,
                    Math.Max(1, (int)(source.Width * scale.X)), Math.Max(1, dy1 - dy0));
                Game1.spriteBatch.Draw(texture, dst, src, tint, 0f, Vector2.Zero, effects, 0f);
            }
        }

        private void DrawBush(BushData bush)
        {
            PlayerEntity player = GameLoopPlayer();
            float playerPush = 0f;
            if (player != null)
            {
                float px = Camera.TransformVector2(player.m_body.GetHitbox().Center.ToVector2()).X;
                float distance = Math.Abs(px - bush.X);
                if (distance < bush.ReactRadius)
                    playerPush = Math.Sign(bush.X - px) * bush.ReactStrength * (1f - distance / bush.ReactRadius);
            }
            float sway = (float)Math.Sin(time * bush.Speed * MathHelper.TwoPi + StableHash(bush.Id) * 0.001f) * bush.Sway + playerPush;
            Color back = prepared.Color(bush.BackColor);
            Color front = prepared.Color(bush.FrontColor);
            Color high = prepared.Color(bush.HighlightColor);
            Texture2D circle = radial;
            int seed = StableHash(bush.Id);
            for (int pass = 0; pass < 3; pass++)
            {
                int count = 7 + pass * 3;
                Color color = pass == 0 ? back : pass == 1 ? front : high;
                for (int i = 0; i < count; i++)
                {
                    float ratio = (i + 0.5f) / count;
                    float x = bush.X - bush.Width / 2f + ratio * bush.Width;
                    float arch = (float)Math.Sin(ratio * Math.PI);
                    float y = bush.Y - arch * bush.Height * (0.55f + pass * 0.12f);
                    float jitter = ((seed >> (i % 24)) & 3) - 1.5f;
                    int size = Math.Max(6, bush.Height / (4 + pass));
                    Rectangle destination = new Rectangle((int)(x + sway * arch * (0.35f + pass * 0.2f) + jitter - size),
                        (int)(y - size / 2f), size * 2, size);
                    Game1.spriteBatch.Draw(circle, destination, MultiplyAlpha(color, pass == 2 ? 0.75f : 1f));
                }
            }
        }

        private void DrawFog(FogData fog)
        {
            if (fog.Opacity <= 0) return;
            Texture2D circle = radial;
            Color color = MultiplyAlpha(prepared.Color(fog.Color), fog.Opacity);
            for (int i = 0; i < fog.Bands; i++)
            {
                float span = fog.Width + 120f;
                float x = fog.X - 60f + PositiveModulo(time * fog.Speed * (0.65f + i * 0.11f) + i * 83f, span);
                float y = fog.Y + (i + 0.5f) / fog.Bands * fog.Height;
                int width = Math.Max(30, fog.Width / Math.Max(2, fog.Bands - 1));
                int height = Math.Max(8, fog.Height / 3);
                Game1.spriteBatch.Draw(circle, new Rectangle((int)x - width / 2, (int)y - height / 2, width, height), color);
                Game1.spriteBatch.Draw(circle, new Rectangle((int)x - width - (int)span, (int)y - height / 2, width, height), color);
            }
        }

        private void DrawEmitter(EmitterData emitter)
        {
            if (emitter.Opacity <= 0) return;
            SceneTexture asset;
            if (!vectors.TryGetValue(emitter.Asset ?? "", out asset)) return;
            int baseSeed = StableHash(emitter.Id);
            Vector2 parallax = Vector2.Zero;
            PlayerEntity player = GameLoopPlayer();
            if (player != null)
            {
                Vector2 local = Camera.TransformVector2(player.m_body.GetHitbox().Center.ToVector2());
                parallax = new Vector2((local.X - 240f) * (1f - emitter.Depth) * 0.055f,
                    (local.Y - 180f) * (1f - emitter.Depth) * 0.028f);
            }
            Color baseColor = prepared.Color(emitter.Tint);
            Vector2 origin = new Vector2(asset.FrameWidth * 0.5f, asset.FrameHeight * 0.5f);
            for (int i = 0; i < emitter.Count; i++)
            {
                int seed = unchecked(baseSeed + i * 1103515245);
                float lifetime = emitter.Lifetime * (0.72f + Hash01(seed + 17) * 0.56f);
                float age = PositiveModulo(time + Hash01(seed + 29) * lifetime, lifetime) / lifetime;
                float x = emitter.X + Hash01(seed + 43) * emitter.Width + emitter.DriftX * (age - 0.5f);
                float y = emitter.Y + Hash01(seed + 71) * emitter.Height + emitter.DriftY * (age - 0.5f);
                float frequency = 0.55f + Hash01(seed + 97) * 0.85f;
                x += (float)Math.Sin(time * frequency + Hash01(seed + 131) * MathHelper.TwoPi) * emitter.Wander;
                y += (float)Math.Cos(time * frequency * 0.73f + Hash01(seed + 173) * MathHelper.TwoPi) * emitter.Wander * 0.38f;
                x = emitter.X + PositiveModulo(x - emitter.X, emitter.Width);
                y = emitter.Y + PositiveModulo(y - emitter.Y, emitter.Height);
                float lifeFade = (float)Math.Sin(age * Math.PI);
                lifeFade *= lifeFade;
                float twinkle = 0.45f + 0.55f * (0.5f + 0.5f * (float)Math.Sin(time * (2.1f + Hash01(seed + 211) * 3.7f) + seed * 0.001f));
                float scale = MathHelper.Lerp(emitter.ScaleMin, emitter.ScaleMax, Hash01(seed + 251));
                Game1.spriteBatch.Draw(asset.Texture, new Vector2(x, y) + parallax, null,
                    MultiplyAlpha(baseColor, emitter.Opacity * lifeFade * twinkle), 0f, origin, scale,
                    SpriteEffects.None, 0f);
            }
        }
    }
}
