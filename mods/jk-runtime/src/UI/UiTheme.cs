using System;
using JumpKing;
using JumpKing.PauseMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.UI
{
    public sealed class UiFrame
    {
        private readonly GuiFrame frame;
        private Rectangle bounds;

        public UiFrame(Rectangle rectangle)
        {
            bounds = rectangle;
            frame = new GuiFrame(rectangle);
        }

        public Rectangle Bounds
        {
            get { return bounds; }
            set
            {
                if (bounds == value) return;
                bounds = value;
                frame.SetBounds(value);
            }
        }

        public void Draw()
        {
            frame.Draw();
        }
    }

    public struct UiCommand
    {
        public string Key;
        public string Label;
        internal UiAction PointerAction;

        public UiCommand(string key, string label)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
            PointerAction = UiAction.None;
        }
    }

    public static class UiTheme
    {
        public static readonly Color Ink = Color.Black;
        public static readonly Color PanelFill = Color.Black;
        // Native Content/gui/frame.xnb uses this neutral gray for its visible border.
        public static readonly Color Border = new Color(114, 114, 114);
        public static readonly Color Text = Color.White;
        /// <summary>Readable secondary copy. Never use this role to indicate an unavailable action.</summary>
        public static readonly Color Muted = new Color(208, 208, 208);
        /// <summary>Native unavailable-option gray; normal labels remain white even when unselected.</summary>
        public static readonly Color Disabled = Color.Gray;
        public static readonly Color Gold = new Color(229, 184, 68);
        public static readonly Color Cyan = new Color(88, 218, 235);
        public static readonly Color Red = new Color(190, 66, 70);
        private static readonly GuiFrame sharedFrame = new GuiFrame(Rectangle.Empty);

        /// <summary>Draw the loaded native ornamental frame, without allocating a frame per cell.</summary>
        public static void NativeFrame(Rectangle bounds)
        { sharedFrame.SetBounds(bounds); sharedFrame.Draw(); }

        public static void Panel(Rectangle rectangle, Color fill, Color border)
        {
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            Game1.spriteBatch.Draw(pixel, rectangle, fill);
            Game1.spriteBatch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 2), border);
            Game1.spriteBatch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Bottom - 2, rectangle.Width, 2), border);
            Game1.spriteBatch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, 2, rectangle.Height), border);
            Game1.spriteBatch.Draw(pixel, new Rectangle(rectangle.Right - 2, rectangle.Y, 2, rectangle.Height), border);
        }

        public static void Tab(string text, Rectangle bounds, bool selected)
        {
            SpriteFont font = Game1.instance.contentManager.font.MenuFontSmall;
            string label = FitText((text ?? string.Empty).ToUpperInvariant(), bounds.Width, true);
            float width = font.MeasureString(label).X;
            TextLine(
                label,
                new Vector2(bounds.X + (bounds.Width - width) / 2f, bounds.Y),
                selected ? Gold : Text,
                true);
            if (!selected) return;
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            Game1.spriteBatch.Draw(
                pixel,
                new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2),
                Gold);
        }

        public static void TextLine(string text, Vector2 position, Color color, bool small)
        {
            SpriteFont font = small
                ? Game1.instance.contentManager.font.MenuFontSmall
                : Game1.instance.contentManager.font.MenuFont;
            DrawText(font, text, position, color);
        }

        /// <summary>Draw pixel-font text on the game's integer 480x360 pixel grid.</summary>
        public static void DrawText(SpriteFont font, string text, Vector2 position, Color color)
        {
            Game1.spriteBatch.DrawString(font, text ?? string.Empty, SnapTextPosition(position), color);
        }

        public static Vector2 SnapTextPosition(Vector2 position)
        { return new Vector2((float)Math.Floor(position.X), (float)Math.Floor(position.Y)); }

        public static string FitText(string text, int maxWidth, bool small)
        {
            string value = text ?? string.Empty;
            SpriteFont font = small
                ? Game1.instance.contentManager.font.MenuFontSmall
                : Game1.instance.contentManager.font.MenuFont;
            if (maxWidth <= 0) return string.Empty;
            if (font.MeasureString(value).X <= maxWidth) return value;
            const string suffix = "...";
            if (font.MeasureString(suffix).X > maxWidth)
            {
                string dot = ".";
                return font.MeasureString(dot).X <= maxWidth ? dot : string.Empty;
            }
            while (value.Length > 0 && font.MeasureString(value + suffix).X > maxWidth)
                value = value.Substring(0, value.Length - 1);
            return value + suffix;
        }

        public static string NormalizeKey(string value)
        {
            string key = string.IsNullOrWhiteSpace(value) ? "-" : value.ToUpperInvariant();
            if (key.Contains("+") || key.Contains("/"))
            {
                string[] alternatives = key.Split('/');
                for (int i = 0; i < alternatives.Length; i++)
                {
                    string[] chord = alternatives[i].Split('+');
                    for (int j = 0; j < chord.Length; j++) chord[j] = NormalizeKey(chord[j]);
                    alternatives[i] = string.Join("+", chord);
                }
                return string.Join("/", alternatives);
            }
            switch (key)
            {
                case "RIGHTSHIFT": return "RSHIFT";
                case "LEFTSHIFT": return "LSHIFT";
                case "RIGHTCONTROL": return "RCTRL";
                case "LEFTCONTROL": return "LCTRL";
                case "RETURN": return "ENTER";
                case "BACKSPACE": return "BACK";
                default: return key;
            }
        }

        public static void Keycap(string value, Rectangle bounds, bool selected)
        {
            SpriteFont font = Game1.instance.contentManager.font.LocationFont;
            string key = NormalizeKey(value);
            key = FitWithFont(key, font, Math.Max(1, bounds.Width - 10));
            Rectangle badge = bounds;
            Panel(
                badge,
                selected ? new Color(15, 42, 47) : new Color(18, 23, 25),
                selected ? Cyan : Border);
            float textWidth = font.MeasureString(key).X;
            DrawText(
                font,
                key,
                new Vector2(badge.X + (badge.Width - textWidth) / 2f, badge.Y + (badge.Height - font.MeasureString(key).Y) / 2f),
                selected ? Cyan : Gold);
        }

        /// <summary>Reserve a footer row inside the owning frame, with 16 px edge padding and 4 px between rows.</summary>
        public static Rectangle FooterRow(Rectangle frameBounds, int rowFromBottom = 0)
        {
            if (rowFromBottom < 0) throw new ArgumentOutOfRangeException("rowFromBottom");
            const int inset = 16, height = 20, stride = 24;
            long top = (long)frameBounds.Bottom - inset - height - (long)rowFromBottom * stride;
            if (frameBounds.Width < inset * 2 || top < (long)frameBounds.Top + inset)
                throw new ArgumentException("The frame has no room for this footer row", "frameBounds");
            return new Rectangle(frameBounds.X + inset, (int)top, frameBounds.Width - inset * 2, height);
        }

        public static void CommandBar(Rectangle bounds, params UiCommand[] commands)
        {
            if (commands == null || commands.Length == 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
            SpriteFont keyFont = Game1.instance.contentManager.font.LocationFont;
            SpriteFont labelFont = Game1.instance.contentManager.font.MenuFontSmall;
            var widths = new int[commands.Length];
            for (int i = 0; i < commands.Length; i++)
                widths[i] = Math.Max(18, (int)Math.Ceiling(keyFont.MeasureString(NormalizeKey(commands[i].Key)).X) + 10)
                    + 6 + (int)Math.Ceiling(labelFont.MeasureString((commands[i].Label ?? "").ToUpperInvariant()).X);
            Rectangle[] slots = CommandSlots(bounds, widths);
            for (int i = 0; i < commands.Length; i++)
            {
                Rectangle slot = slots[i];
                if (slot.Width < 18) continue;
                string key = NormalizeKey(commands[i].Key);
                string label = (commands[i].Label ?? "").ToUpperInvariant();
                int keyWidth = Math.Min(Math.Max(18, slot.Width - 6 - (int)Math.Ceiling(labelFont.MeasureString(label).X)),
                    Math.Max(18, (int)Math.Ceiling(keyFont.MeasureString(key).X) + 10));
                Keycap(key, new Rectangle(slot.X, slot.Y, keyWidth, slot.Height), false);
                label = FitText(label, Math.Max(0, slot.Width - keyWidth - 6), true);
                TextLine(label, new Vector2(slot.X + keyWidth + 6, slot.Y + (slot.Height - labelFont.MeasureString(label).Y) / 2f), Muted, true);
                UiPointer.ActionRegion(slot, commands[i].PointerAction);
            }
        }

        // Content-sized groups, one shared baseline, and a fixed gap instead of equal columns.
        internal static Rectangle[] CommandSlots(Rectangle bounds, int[] widths)
        {
            var result = new Rectangle[widths.Length];
            int gap = widths.Length <= 1 ? 0 : Math.Min(16, Math.Max(0, bounds.Width / widths.Length - 18));
            int available = Math.Max(0, bounds.Width - gap * Math.Max(0, widths.Length - 1));
            int total = 0; foreach (int width in widths) total += Math.Max(0, width);
            int x = bounds.X, assigned = 0;
            int height = Math.Min(20, bounds.Height);
            for (int i = 0; i < widths.Length; i++)
            {
                int width = total <= available ? Math.Max(0, widths[i])
                    : (int)((long)available * Math.Max(0, widths[i]) / Math.Max(1, total));
                if (total > available && i == widths.Length - 1) width = available - assigned;
                result[i] = new Rectangle(x, bounds.Y + (bounds.Height - height) / 2, width, height);
                assigned += width; x += width + gap;
            }
            return result;
        }

        public static void WrappedText(string text, Rectangle bounds, Color color)
        {
            int y = bounds.Y;
            foreach (string line in WrapLines(text, bounds.Width, s => (int)Math.Ceiling(Game1.instance.contentManager.font.MenuFontSmall.MeasureString(s).X)))
            {
                if (y + 13 > bounds.Bottom) break;
                TextLine(FitText(line, bounds.Width, true), new Vector2(bounds.X, y), color, true); y += 15;
            }
        }

        internal static string[] WrapLines(string text, int width, Func<string, int> measure)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (width <= 0) return lines.ToArray();
            foreach (string paragraph in (text ?? "").Replace("\r", "").Split('\n'))
            {
                string line = "";
                foreach (string word in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (measure(candidate) <= width) { line = candidate; continue; }
                    if (line.Length > 0) { lines.Add(line); line = ""; }
                    var elements = System.Globalization.StringInfo.GetTextElementEnumerator(word);
                    while (elements.MoveNext())
                    {
                        string element = elements.GetTextElement();
                        if (line.Length > 0 && measure(line + element) > width) { lines.Add(line); line = ""; }
                        line += element;
                    }
                }
                lines.Add(line);
            }
            return lines.ToArray();
        }

        private static string FitWithFont(string text, SpriteFont font, int maxWidth)
        {
            if (maxWidth <= 0 || font.MeasureString(".").X > maxWidth) return string.Empty;
            if (font.MeasureString(text).X <= maxWidth) return text;
            string value = text;
            while (value.Length > 1 && font.MeasureString(value + ".").X > maxWidth)
                value = value.Substring(0, value.Length - 1);
            return value + ".";
        }
    }
}
