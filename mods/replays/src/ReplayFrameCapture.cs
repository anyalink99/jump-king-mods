using System;
using System.Reflection;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Replays
{
    internal static class ReplayFrameCapture
    {
        private static readonly FieldInfo FlipField =
            typeof(PlayerEntity).GetField(
                "m_flip",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SpriteField =
            typeof(PlayerEntity).GetField(
                "m_sprite",
                BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void ValidateContract()
        {
            if (FlipField == null || SpriteField == null)
                throw new InvalidOperationException(
                    "Jump King player-presentation contract is unavailable");
        }

        internal static ReplayFrame Capture(
            PlayerEntity player,
            int frameIndex)
        {
            ValidateContract();
            BodyComp body = player.m_body;
            PadState state = ControllerManager.instance.GetPadState();
            Sprite actualSprite = ReplayAppearanceSprites.BaseLayer(
                SpriteField.GetValue(player) as Sprite);
            ReplayPose pose = SelectPose(
                actualSprite,
                body,
                state,
                frameIndex);
            ReplayFrameFlags flags = ReplayFrameFlags.None;
            SpriteEffects effects = (SpriteEffects)FlipField.GetValue(player);
            if ((effects & SpriteEffects.FlipHorizontally) != 0)
                flags |= ReplayFrameFlags.FacingLeft;

            return new ReplayFrame
            {
                Position = body.Position,
                VisualOffset = VisualOffset(actualSprite, pose),
                Screen = JumpKing.Camera.CurrentScreenIndex1,
                Flags = flags,
                Pose = pose
            };
        }

        private static Vector2 VisualOffset(
            Sprite actual,
            ReplayPose pose)
        {
            Sprite expected = ReplayAppearanceSprites.BaseLayer(
                ReplayGhostRenderer.ResolveActiveSprite(pose));
            if (actual == null || expected == null) return Vector2.Zero;
            return Origin(expected) - Origin(actual);
        }

        private static Vector2 Origin(Sprite sprite)
        {
            return sprite.source.Size.ToVector2() * sprite.center;
        }

        private static ReplayPose SelectPose(
            Sprite current,
            BodyComp body,
            PadState state,
            int frameIndex)
        {
            ReplayPose pose;
            if (TryReadPose(current, out pose)) return pose;
            if (!body.IsOnGround)
                return body.Velocity.Y < 0f
                    ? ReplayPose.JumpUp
                    : ReplayPose.JumpFall;
            if (state.jump) return ReplayPose.Charge;
            if (Math.Abs(body.Velocity.X) < 0.05f) return ReplayPose.Idle;
            switch ((frameIndex / 5) % 4)
            {
                case 0: return ReplayPose.WalkOne;
                case 1: return ReplayPose.WalkSmear;
                case 2: return ReplayPose.WalkTwo;
                default: return ReplayPose.WalkSmear;
            }
        }

        private static bool TryReadPose(Sprite current, out ReplayPose pose)
        {
            pose = ReplayPose.Idle;
            if (current == null
                || Game1.instance == null
                || Game1.instance.contentManager == null
                || Game1.instance.contentManager.playerSprites == null)
            {
                return false;
            }
            JKContentManager.PlayerSprites sprites =
                Game1.instance.contentManager.playerSprites;
            if (SamePoseVisual(current, sprites.idle)) pose = ReplayPose.Idle;
            else if (SamePoseVisual(current, sprites.walk_one)) pose = ReplayPose.WalkOne;
            else if (SamePoseVisual(current, sprites.walk_smear)) pose = ReplayPose.WalkSmear;
            else if (SamePoseVisual(current, sprites.walk_two)) pose = ReplayPose.WalkTwo;
            else if (SamePoseVisual(current, sprites.jump_charge)) pose = ReplayPose.Charge;
            else if (SamePoseVisual(current, sprites.jump_up)) pose = ReplayPose.JumpUp;
            else if (SamePoseVisual(current, sprites.jump_fall)) pose = ReplayPose.JumpFall;
            else if (SamePoseVisual(current, sprites.jump_bounce)) pose = ReplayPose.Bounce;
            else if (SamePoseVisual(current, sprites.splat)) pose = ReplayPose.Splat;
            else if (SamePoseVisual(current, sprites.look_up)) pose = ReplayPose.LookUp;
            else if (SamePoseVisual(current, sprites.stretch_one)) pose = ReplayPose.StretchOne;
            else if (SamePoseVisual(current, sprites.stretch_smear)) pose = ReplayPose.StretchSmear;
            else if (SamePoseVisual(current, sprites.stretch_two)) pose = ReplayPose.StretchTwo;
            else return false;
            return true;
        }

        internal static bool SamePoseVisual(Sprite current, Sprite expected)
        {
            return SameVisual(
                ReplayAppearanceSprites.BaseLayer(current),
                ReplayAppearanceSprites.BaseLayer(expected));
        }

        internal static bool SameVisual(Sprite left, Sprite right)
        {
            if (ReferenceEquals(left, right)) return true;
            return left != null
                && right != null
                && ReferenceEquals(left.texture, right.texture)
                && left.source == right.source
                && left.center == right.center;
        }

    }
}
