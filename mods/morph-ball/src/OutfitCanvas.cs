using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal static class OutfitCanvas
    {
        internal static Rectangle Bounds(Point size, Vector2 center)
        {
            return new Rectangle((-size.ToVector2() * center).ToPoint(), size);
        }
    }
}
