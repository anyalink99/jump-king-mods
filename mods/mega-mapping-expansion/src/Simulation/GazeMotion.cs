using System;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static class GazeMotion
    {
        internal static Vector2 Target(Vector2 eye, Vector2 king, float rangeX, float rangeY)
        {
            Vector2 delta = king - eye;
            // A soft near field avoids a direction snap as the King crosses the eye.
            float length = (float)Math.Sqrt(delta.LengthSquared() + 24f * 24f);
            return new Vector2(delta.X / length * rangeX, delta.Y / length * rangeY);
        }

        internal static Vector2 Advance(Vector2 current, Vector2 target, float response, float delta)
        { return Vector2.Lerp(current, target, 1f - (float)Math.Exp(-response * Math.Max(0f, delta))); }
    }
}
