using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JumpKingJetpack
{
    internal static class JetpackArt
    {
        internal static void DrawBody(Rectangle destination)
        {
            string[] rows = JetpackSpriteData.GetBody();
            float scale = System.Math.Min(
                destination.Width / (float)JetpackSpriteData.Width,
                destination.Height / (float)JetpackSpriteData.Height);
            int width = System.Math.Max(
                1,
                (int)System.Math.Floor(JetpackSpriteData.Width * scale));
            int height = System.Math.Max(
                1,
                (int)System.Math.Floor(JetpackSpriteData.Height * scale));
            int left = destination.Center.X - width / 2;
            int top = destination.Center.Y - height / 2;
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    Color color = PixelColor(rows[y][x]);
                    if (color.A == 0) continue;
                    int x1 = left + x * width / JetpackSpriteData.Width;
                    int x2 = left + (x + 1) * width / JetpackSpriteData.Width;
                    int y1 = top + y * height / JetpackSpriteData.Height;
                    int y2 = top + (y + 1) * height / JetpackSpriteData.Height;
                    Game1.spriteBatch.Draw(pixel, new Rectangle(
                        x1,
                        y1,
                        System.Math.Max(1, x2 - x1),
                        System.Math.Max(1, y2 - y1)), color);
                }
            }
        }

        internal static Color PixelColor(char value)
        {
            switch (value)
            {
                case 'D': return new Color(25, 29, 36);
                case 'S': return new Color(52, 65, 76);
                case 'M': return new Color(82, 103, 113);
                case 'L': return new Color(126, 153, 157);
                case 'H': return new Color(187, 207, 198);
                case 'B': return new Color(91, 35, 39);
                case 'C': return new Color(171, 55, 43);
                case 'r': return new Color(126, 32, 25);
                case 'o': return new Color(220, 74, 28);
                case 'y': return new Color(255, 164, 35);
                case 'w': return new Color(255, 238, 143);
                default: return Color.Transparent;
            }
        }
    }
}
