using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Factory;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    public static class BallKingMapPixels
    {
        public static readonly Color RestrictedScreen =
            new Color(160, 64, 255, 255);
        public static readonly Color RestrictedNoDoubleJump =
            new Color(255, 96, 64, 255);
        public static readonly Color RestrictedForceDoubleJump =
            new Color(64, 128, 255, 255);
        public static readonly Color StickySurface =
            new Color(255, 64, 160, 255);
    }

    internal enum BallKingScreenRestriction
    {
        None,
        JumpAndSticky,
        NoDoubleJump,
        ForceDoubleJump
    }

    internal interface IBallStickySurface
    {
    }

    internal sealed class BallRestrictedBlock : BoxBlock, IBlockDebugColor
    {
        private readonly Color debugColor;

        internal BallRestrictedBlock(Rectangle rectangle, Color color)
            : base(rectangle)
        {
            debugColor = color;
        }

        public Color DebugColor
        {
            get { return debugColor; }
        }
    }

    internal sealed class BallStickyBoxBlock : BoxBlock,
        IBallStickySurface,
        IBlockDebugColor
    {
        internal BallStickyBoxBlock(Rectangle rectangle)
            : base(rectangle)
        {
        }

        public Color DebugColor
        {
            get { return BallKingMapPixels.StickySurface; }
        }
    }

    internal sealed class BallStickySlopeBlock : SlopeBlock,
        IBallStickySurface,
        IBlockDebugColor
    {
        internal BallStickySlopeBlock(
            Rectangle rectangle,
            SlopeType slopeType)
            : base(rectangle, slopeType)
        {
        }

        public Color DebugColor
        {
            get { return BallKingMapPixels.StickySurface; }
        }
    }

    internal static class BallKingMapRules
    {
        internal const float RestrictedJumpHeightScale = 0.75f;
        internal const float RestrictedDoubleJumpHeightScale = 0.35f;

        private static readonly Dictionary<int, BallKingScreenRestriction>
            Restrictions =
                new Dictionary<int, BallKingScreenRestriction>();

        internal static void Clear()
        {
            Restrictions.Clear();
        }

        internal static void RestrictScreen(
            int screen,
            BallKingScreenRestriction restriction)
        {
            BallKingScreenRestriction existing;
            if (Restrictions.TryGetValue(screen, out existing)
                && existing != restriction)
            {
                throw new InvalidOperationException(
                    "A Ball King screen cannot contain multiple restriction colours");
            }
            Restrictions[screen] = restriction;
        }

        internal static BallKingScreenRestriction RestrictionFor(int screen)
        {
            BallKingScreenRestriction restriction;
            return Restrictions.TryGetValue(screen, out restriction)
                ? restriction
                : BallKingScreenRestriction.None;
        }

        internal static float JumpHeightScale(int screen, bool doubleJump)
        {
            if (RestrictionFor(screen) == BallKingScreenRestriction.None)
            {
                return 1f;
            }
            return doubleJump
                ? RestrictedDoubleJumpHeightScale
                : RestrictedJumpHeightScale;
        }

        internal static bool DisablesSticky(int screen)
        {
            return RestrictionFor(screen) != BallKingScreenRestriction.None;
        }

        internal static bool OverridesDoubleJump(int screen)
        {
            BallKingScreenRestriction restriction = RestrictionFor(screen);
            return restriction == BallKingScreenRestriction.NoDoubleJump
                || restriction == BallKingScreenRestriction.ForceDoubleJump;
        }

        internal static bool DoubleJumpEnabled(
            int screen,
            bool configuredValue)
        {
            BallKingScreenRestriction restriction = RestrictionFor(screen);
            if (restriction == BallKingScreenRestriction.NoDoubleJump)
            {
                return false;
            }
            if (restriction == BallKingScreenRestriction.ForceDoubleJump)
            {
                return true;
            }
            return configuredValue;
        }

        internal static bool IsStickySurface(IBlock block)
        {
            return block is IBallStickySurface;
        }
    }

    public sealed class BallKingBlockFactory : IBlockFactory
    {
        private static readonly MethodInfo GetSlopeTypeMethod =
            typeof(BaseBlockFactory).GetMethod(
                "GetSlopeType",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly BaseBlockFactory baseFactory =
            new BaseBlockFactory();

        internal static void ValidateContract()
        {
            if (GetSlopeTypeMethod == null
                || GetSlopeTypeMethod.ReturnType != typeof(SlopeType))
            {
                throw new InvalidOperationException(
                    "Jump King slope-topology contract is unavailable");
            }
        }

        public bool CanMakeBlock(
            Color blockCode,
            JumpKing.Workshop.Level level)
        {
            return IsRestrictionColor(blockCode)
                || blockCode == BallKingMapPixels.StickySurface;
        }

        public bool IsSolidBlock(Color blockCode)
        {
            return IsRestrictionColor(blockCode)
                || blockCode == BallKingMapPixels.StickySurface;
        }

        public IBlock GetBlock(
            Color blockCode,
            Rectangle blockRect,
            JumpKing.Workshop.Level level,
            LevelTexture textureSrc,
            int currentScreen,
            int x,
            int y)
        {
            BallKingScreenRestriction restriction =
                RestrictionFromColor(blockCode);
            if (restriction != BallKingScreenRestriction.None)
            {
                BallKingMapRules.RestrictScreen(currentScreen, restriction);
                return new BallRestrictedBlock(blockRect, blockCode);
            }
            if (blockCode != BallKingMapPixels.StickySurface)
            {
                throw new InvalidOperationException(
                    "Unsupported Ball King map colour");
            }

            ValidateContract();
            SlopeType slopeType = (SlopeType)GetSlopeTypeMethod.Invoke(
                baseFactory,
                new object[] { textureSrc, currentScreen, x, y });
            return slopeType == SlopeType.None
                ? (IBlock)new BallStickyBoxBlock(blockRect)
                : new BallStickySlopeBlock(blockRect, slopeType);
        }

        private static bool IsRestrictionColor(Color color)
        {
            return RestrictionFromColor(color)
                != BallKingScreenRestriction.None;
        }

        private static BallKingScreenRestriction RestrictionFromColor(
            Color color)
        {
            if (color == BallKingMapPixels.RestrictedScreen)
            {
                return BallKingScreenRestriction.JumpAndSticky;
            }
            if (color == BallKingMapPixels.RestrictedNoDoubleJump)
            {
                return BallKingScreenRestriction.NoDoubleJump;
            }
            if (color == BallKingMapPixels.RestrictedForceDoubleJump)
            {
                return BallKingScreenRestriction.ForceDoubleJump;
            }
            return BallKingScreenRestriction.None;
        }
    }
}
