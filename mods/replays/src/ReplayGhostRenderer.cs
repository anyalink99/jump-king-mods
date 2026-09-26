using System;
using System.Collections;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Replays
{
    internal static class ReplayGhostRenderer
    {
        private static readonly BindingFlags LayerFlags =
            BindingFlags.Instance | BindingFlags.Public;

        internal static void Draw(
            ReplayFrame frame,
            int[] equippedItems,
            Color tint,
            float alpha,
            bool requireCurrentScreen)
        {
            if (requireCurrentScreen
                && frame.Screen != JumpKing.Camera.CurrentScreenIndex1)
            {
                return;
            }
            Sprite sprite = ResolveSprite(frame.Pose, equippedItems);
            if (sprite == null) return;
            Vector2 anchor = JumpKing.Camera.TransformVector2(
                frame.DrawAnchor);
            SpriteEffects effects =
                (frame.Flags & ReplayFrameFlags.FacingLeft) != 0
                    ? SpriteEffects.FlipHorizontally
                    : SpriteEffects.None;
            if (tint == Color.White && alpha >= 0.999f)
            {
                sprite.Draw(anchor, effects);
                return;
            }
            DrawLayers(sprite, anchor, effects, tint, alpha);
        }

        internal static Sprite ResolveSprite(
            ReplayPose pose,
            int[] equippedItems)
        {
            return ReplayAppearanceSprites.Resolve(pose, equippedItems);
        }

        internal static Sprite ResolveActiveSprite(ReplayPose pose)
        {
            JKContentManager.PlayerSprites sprites =
                Game1.instance.contentManager.playerSprites;
            switch (pose)
            {
                case ReplayPose.WalkOne: return sprites.walk_one;
                case ReplayPose.WalkSmear: return sprites.walk_smear;
                case ReplayPose.WalkTwo: return sprites.walk_two;
                case ReplayPose.Charge: return sprites.jump_charge;
                case ReplayPose.JumpUp: return sprites.jump_up;
                case ReplayPose.JumpFall: return sprites.jump_fall;
                case ReplayPose.Bounce: return sprites.jump_bounce;
                case ReplayPose.Splat: return sprites.splat;
                case ReplayPose.LookUp: return sprites.look_up;
                case ReplayPose.StretchOne: return sprites.stretch_one;
                case ReplayPose.StretchSmear: return sprites.stretch_smear;
                case ReplayPose.StretchTwo: return sprites.stretch_two;
                default: return sprites.idle;
            }
        }

        private static void DrawLayers(
            Sprite sprite,
            Vector2 position,
            SpriteEffects effects,
            Color tint,
            float alpha)
        {
            PropertyInfo property = sprite.GetType().GetProperty(
                "Sprites",
                LayerFlags);
            IList layers = property == null
                ? null
                : property.GetValue(sprite, null) as IList;
            if (layers != null)
            {
                foreach (object value in layers)
                {
                    Sprite layer = value as Sprite;
                    if (layer != null)
                        DrawLayers(layer, position, effects, tint, alpha);
                }
                return;
            }
            if (sprite.texture == null) return;
            Color source = sprite.GetColor();
            float opacity = Math.Max(0f, Math.Min(1f, alpha));
            Color color = new Color(
                (int)(source.R * tint.R / 255f * opacity),
                (int)(source.G * tint.G / 255f * opacity),
                (int)(source.B * tint.B / 255f * opacity),
                (int)(source.A * opacity));
            Vector2 topLeft = SnappedTopLeft(sprite, position);
            Game1.spriteBatch.Draw(
                sprite.texture,
                topLeft,
                sprite.source,
                color,
                0f,
                Vector2.Zero,
                1f,
                effects,
                0f);
        }

        internal static Vector2 SnappedTopLeft(
            Sprite sprite,
            Vector2 anchor)
        {
            if (sprite == null) return anchor;
            Vector2 origin = sprite.source.Size.ToVector2() * sprite.center;
            return (anchor - origin).ToPoint().ToVector2();
        }
    }
}
