using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.UI
{
    /// <summary>Page-owned native-font renderer with a cached Latin/Cyrillic fallback atlas. Dispose on page close.</summary>
    public sealed class UiTextRenderer : IDisposable
    {
        private SpriteFont unicode;
        private Texture2D texture;
        public SpriteFont Font(string text, bool small)
        {
            var font = small ? Game1.instance.contentManager.font.MenuFontSmall : Game1.instance.contentManager.font.MenuFont;
            if ((text ?? "").All(c => font.Characters.Contains(c))) return font;
            if (unicode != null && !texture.IsDisposed && texture.GraphicsDevice == Game1.spriteBatch.GraphicsDevice) return unicode;
            Dispose();
            var chars = Enumerable.Range(32,95).Concat(Enumerable.Range(0x400,256)).Select(x => (char)x).ToList();
            var bounds = new List<Rectangle>(); var crops = new List<Rectangle>(); var kern = new List<Vector3>();
            const int size = 20, columns = 24; int width = columns*size, height = ((chars.Count+columns-1)/columns)*size;
            using (var bitmap = new System.Drawing.Bitmap(width,height))
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            using (var face = new System.Drawing.Font("Segoe UI",12,System.Drawing.FontStyle.Regular,System.Drawing.GraphicsUnit.Pixel))
            {
                graphics.Clear(System.Drawing.Color.Transparent); graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                for (int i = 0; i < chars.Count; i++)
                {
                    int x = i%columns*size, y = i/columns*size;
                    int advance = Math.Max(3,Math.Min(size,(int)Math.Ceiling(graphics.MeasureString(chars[i].ToString(),face,100,System.Drawing.StringFormat.GenericTypographic).Width)));
                    graphics.DrawString(chars[i].ToString(),face,System.Drawing.Brushes.White,x,y,System.Drawing.StringFormat.GenericTypographic);
                    bounds.Add(new Rectangle(x,y,advance,18)); crops.Add(new Rectangle(0,0,advance,18)); kern.Add(new Vector3(0,advance,0));
                }
                var data = new Color[width*height];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                { var c = bitmap.GetPixel(x,y); data[y*width+x] = new Color(c.A,c.A,c.A,c.A); }
                texture = new Texture2D(Game1.spriteBatch.GraphicsDevice,width,height); texture.SetData(data);
            }
            unicode = new SpriteFont(texture,bounds,crops,chars,18,0,kern,'?'); return unicode;
        }
        public string Fit(string text, int width, bool small)
        {
            text = text ?? ""; var font = Font(text,small);
            if (font.MeasureString(text).X <= width) return text;
            while (text.Length > 0 && font.MeasureString(text + "...").X > width) text = text.Substring(0,text.Length-1);
            return text + "...";
        }
        public void Draw(string text, Vector2 at, Color color, bool small)
        { Game1.spriteBatch.DrawString(Font(text,small),text ?? "",at,color); }
        /// <summary>Measure, truncate and draw with the same font, including when the truncated suffix contains the only Cyrillic characters.</summary>
        public void DrawFitted(string text, int width, Vector2 at, Color color, bool small)
        { var font = Font(text,small); Game1.spriteBatch.DrawString(font,Fit(text,width,small),at,color); }
        public void Dispose() { if (texture != null) texture.Dispose(); texture = null; unicode = null; }
    }
}
