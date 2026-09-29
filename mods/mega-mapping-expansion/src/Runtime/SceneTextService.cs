using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed class SceneTextService
    {
        private readonly SceneFile scene;
        private readonly Dictionary<string, string> strings = new Dictionary<string, string>(StringComparer.Ordinal);
        internal SceneTextService(SceneFile scene, string culture)
        {
            this.scene = scene;
            foreach (MapString item in scene.Strings)
            {
                string value = item.Value;
                if (item.Translations.Length > 0)
                {
                    Translation translation = item.Translations.SingleOrDefault(t => string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));
                    if (translation == null) throw new InvalidDataException(item.Id + ": missing translation for active culture '" + culture + "'");
                    value = translation.Value;
                }
                strings.Add(item.Id, value);
            }
        }
        internal string Resolve(string id, Func<string, string> flag)
        { string value = strings[id]; return value.IndexOf("{flag:", StringComparison.Ordinal) < 0 ? value : NarrativeValidation.FlagToken.Replace(value, m => flag(m.Groups[1].Value)); }
        internal string Initial(string id) { return Resolve(id, name => scene.Flags.Single(f => f.Id == name).Value); }
        internal static SpriteFont Font(string name)
        {
            var fonts = Game1.instance.contentManager.font;
            switch (name) { case "menu": return fonts.MenuFont; case "small": return fonts.MenuFontSmall; case "style": return fonts.StyleFont; case "location": return fonts.LocationFont; case "gargoyle": return fonts.GargoyleFont; default: throw new InvalidDataException("Unknown font " + name); }
        }
        internal static string[] Wrap(string text, float width, Func<string, float> measure)
        {
            var result = new List<string>();
            foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = "";
                foreach (string word in paragraph.Split(' '))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (measure(candidate) <= width) { line = candidate; continue; }
                    if (line.Length != 0) { result.Add(line); line = ""; }
                    foreach (char c in word)
                    {
                        if (measure(line + c) > width) { if (line.Length == 0) throw new InvalidDataException("A glyph exceeds the text width"); result.Add(line); line = ""; }
                        line += c;
                    }
                }
                result.Add(line);
            }
            if (result.Count > 128) throw new InvalidDataException("Text exceeds 128 wrapped lines");
            return result.ToArray();
        }
        internal static string[] Layout(SpriteFont font, string value, float width)
        {
            foreach (char c in value) if (c != '\n' && c != '\r' && !font.Characters.Contains(c)) throw new InvalidDataException("Font has no glyph U+" + ((int)c).ToString("X4"));
            return Wrap(value, width, s => font.MeasureString(s).X);
        }
        internal static void Draw(SpriteFont font, string[] lines, Vector2 position, float width, string align, Color color)
        {
            foreach (string line in lines)
            {
                float offset = align == "left" ? 0 : (width - font.MeasureString(line).X) * (align == "center" ? .5f : 1f);
                Game1.spriteBatch.DrawString(font, line, position + new Vector2(offset, 0), color); position.Y += font.LineSpacing;
            }
        }
    }
    internal sealed partial class SceneHost
    {
        internal SceneTextService Narrative { get; private set; }
        internal Color NarrativeColor(string color) { return prepared.Color(color); }
        private sealed class TextLayout { internal SpriteFont Font; internal string Value; internal string[] Lines; }
        private readonly Dictionary<SceneText, TextLayout> textLayouts = new Dictionary<SceneText, TextLayout>();
        private void PrepareTexts()
        {
            Narrative = new SceneTextService(scene, (LanguageJK.language.Culture ?? CultureInfo.CurrentUICulture).Name);
            foreach (SceneText text in scene.Texts)
            {
                var layout = new TextLayout { Font = SceneTextService.Font(text.Font), Value = Narrative.Initial(text.String) };
                layout.Lines = SceneTextService.Layout(layout.Font, layout.Value, text.Width); textLayouts.Add(text, layout);
            }
            NativeNarrative.ValidateResources(this);
        }
        private void DrawText(SceneText text)
        {
            if (!text.Visible || text.Opacity <= 0) return;
            TextLayout layout = textLayouts[text]; string value = Narrative.Resolve(text.String, behaviors.GetFlag);
            if (value != layout.Value) { layout.Lines = SceneTextService.Layout(layout.Font, value, text.Width); layout.Value = value; }
            SceneTextService.Draw(layout.Font, layout.Lines, new Vector2(text.X, text.Y), text.Width, text.Align, prepared.Color(text.Color) * text.Opacity);
        }
        internal void DrawScreenTexts()
        {
            foreach (SceneText text in scene.Texts) if (text.Space == "screen" && text.Screen == Camera.CurrentScreenIndex1) DrawText(text);
        }
    }
}
