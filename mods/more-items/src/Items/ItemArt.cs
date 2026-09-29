using System;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MoreItems
{
    public static class ConsumableArt
    {
        public static void DrawDefault(Rectangle destination, Color color)
        {
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            Rectangle inner = Inset(destination, 2);
            Game1.spriteBatch.Draw(pixel, destination, new Color(20, 29, 34));
            Game1.spriteBatch.Draw(pixel, inner, color);
            Game1.spriteBatch.Draw(pixel, new Rectangle(inner.X + 2, inner.Y + 2, Math.Max(1, inner.Width - 4), 2), Color.White);
        }

        public static void DrawRewinder(Rectangle destination)
        {
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            Color dark = new Color(14, 50, 67);
            Color edge = new Color(40, 151, 186);
            Color bright = new Color(74, 231, 255);
            Color light = new Color(224, 255, 255);
            int unit = Math.Max(1, Math.Min(destination.Width, destination.Height) / 8);
            int size = unit * 8;
            int x = destination.Center.X - size / 2;
            int y = destination.Center.Y - size / 2;
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit, y, unit * 6, unit), dark);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x, y + unit, unit, unit * 6), dark);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit * 7, y + unit, unit, unit * 6), dark);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit, y + unit * 7, unit * 6, unit), dark);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit, y + unit, unit * 6, unit * 6), edge);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit * 2, y + unit * 2, unit * 4, unit * 4), new Color(8, 22, 31));
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit * 2, y + unit * 2, unit * 3, unit), bright);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit * 2, y + unit * 2, unit, unit * 3), bright);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit, y + unit * 3, unit * 2, unit), bright);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit * 2, y + unit * 2, unit, unit), light);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit * 4, y + unit * 4, unit * 2, unit), bright);
            Game1.spriteBatch.Draw(pixel, new Rectangle(x + unit * 5, y + unit * 3, unit, unit * 2), bright);
        }

        private static Rectangle Inset(Rectangle rectangle, int amount)
        {
            return new Rectangle(rectangle.X + amount, rectangle.Y + amount,
                Math.Max(1, rectangle.Width - amount * 2), Math.Max(1, rectangle.Height - amount * 2));
        }
    }
}
