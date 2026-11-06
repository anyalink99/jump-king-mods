using System;
using JumpKing;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    public static class WorldScreen
    {
        public static bool IsCurrent(int screen)
        {
            return screen > 0 && Camera.CurrentScreenIndex1 == screen;
        }

        public static Vector2 ToWorldPosition(int screen, Vector2 localPosition)
        {
            if (screen < 1) throw new ArgumentOutOfRangeException("screen");
            return new Vector2(
                localPosition.X,
                localPosition.Y - (screen - 1) * JumpGame.GAME_RECT.Height);
        }

        public static bool PlayerIntersects(int screen, Rectangle localArea)
        {
            if (screen < 1) throw new ArgumentOutOfRangeException("screen");
            return WorldInteractionContext.PlayerIntersectsScreenArea(screen, localArea);
        }

        public static void ValidatePoint(int screen, float x, float y, string id)
        {
            if (screen < 1) throw new ArgumentOutOfRangeException("screen");
            if (x < 0f || x >= JumpGame.GAME_RECT.Width
                || y < 0f || y >= JumpGame.GAME_RECT.Height)
                throw new ArgumentOutOfRangeException("Point must fit inside its screen: " + (id ?? string.Empty));
        }
    }
}
