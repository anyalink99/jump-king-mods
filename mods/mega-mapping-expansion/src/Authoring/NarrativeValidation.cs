using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MegaMappingExpansion
{
    internal static class NarrativeValidation
    {
        internal static readonly Regex FlagToken = new Regex(@"\{flag:([^{}]+)\}");
        internal static void FlagValue(FlagData flag, string value)
        {
            if (flag.Type == "string") return;
            int number;
            if (flag.Type != "integer" || !int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number))
                throw new InvalidDataException(flag.Id + ": expected an integer flag value");
        }
        internal static void Validate(SceneFile scene, int screens)
        {
            if (scene.Strings.Length > 2048 || scene.Texts.Length > 512 || scene.NativeActors.Length > 128 || scene.IntroPages.Length > 16 || scene.ResultPages.Length > 8)
                throw new InvalidDataException("Narrative budget: 2048 strings, 512 texts, 128 actors, 16 intro pages, 8 result pages");
            var strings = new HashSet<string>(StringComparer.Ordinal);
            foreach (MapString item in scene.Strings)
            {
                if (string.IsNullOrWhiteSpace(item.Id) || !strings.Add(item.Id)) throw new InvalidDataException("Duplicate/empty string ID");
                if ((item.Value != null) == (item.Translations.Length > 0)) throw new InvalidDataException(item.Id + ": supply value OR explicit translations");
                var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Translation translation in item.Translations)
                {
                    if (string.IsNullOrEmpty(translation.Culture) || !cultures.Add(CultureInfo.GetCultureInfo(translation.Culture).Name)) throw new InvalidDataException(item.Id + ": duplicate/empty culture");
                }
                foreach (string text in item.Translations.Length == 0 ? new[] { item.Value } : item.Translations.Select(t => t.Value))
                {
                    if (text == null || text.Length > 4096) throw new InvalidDataException(item.Id + ": text limit is 4096 characters");
                    foreach (Match token in FlagToken.Matches(text)) if (!scene.Flags.Any(f => f.Id == token.Groups[1].Value)) throw new InvalidDataException(item.Id + ": unknown flag " + token.Value);
                }
            }
            Action<string> textRef = id => { if (id == null || !strings.Contains(id)) throw new InvalidDataException("Unknown string: " + id); };
            Action<string> flagRef = id => { if (!string.IsNullOrEmpty(id) && !scene.Flags.Any(f => f.Id == id)) throw new InvalidDataException("Unknown flag: " + id); };
            var ids = new HashSet<string>(scene.Props.Concat(scene.Nodes).Select(p => p.Id).Concat(scene.Lights.Select(p => p.Id)).Concat(scene.Waters.Select(p => p.Id)).Concat(scene.Fogs.Select(p => p.Id)).Concat(scene.Rains.Select(p => p.Id)).Concat(scene.Emitters.Select(p => p.Id)).Concat(scene.Bushes.Select(p => p.Id)).Concat(scene.Puddles.Select(p => p.Id)).Concat(scene.Planets.Select(p => p.Id)).Concat(scene.Surfs.Select(p => p.Id)).Concat(scene.ShadowSurfaces.Select(p => p.Id)), StringComparer.Ordinal);
            Action<string> objectId = id => { if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id == "options" || id.StartsWith("@") || id.Contains(":") || id.Contains("/") || !ids.Add(id)) throw new InvalidDataException("Invalid/duplicate narrative object ID: " + id); };
            foreach (SceneText text in scene.Texts)
            {
                objectId(text.Id); textRef(text.String);
                if (text.Screen < 1 || text.Screen > screens || text.Width < 8 || text.Width > 480 || text.Opacity < 0 || text.Opacity > 1 || Math.Abs(text.X) > 4096 || Math.Abs(text.Y) > 4096) throw new InvalidDataException(text.Id + ": invalid text bounds/opacity");
                Choice(text.Font, "menu", "small", "style", "location", "gargoyle"); Choice(text.Align, "left", "center", "right"); Choice(text.Space, "world", "screen"); Choice(text.Layer, "background", "world", "foreground"); SceneValidation.ParseColor(text.Color, text.Id);
            }
            if (scene.IntroPages.Length > 0 && !string.IsNullOrEmpty(scene.Options.IntroText)) throw new InvalidDataException("Use Intro pages or introText, not both");
            foreach (IntroPage page in scene.IntroPages)
            {
                textRef(page.String); SceneValidation.ParseColor(page.Color, "Intro");
                if (page.Stay < 0 || page.Stay > 60 || page.FadeIn < 0 || page.FadeIn > 10 || page.FadeOut < 0 || page.FadeOut > 10 || page.Stay + page.FadeIn + page.FadeOut <= 0) throw new InvalidDataException("Invalid intro page timing");
                if (scene.Strings.Single(s => s.Id == page.String).Translations.Select(t => t.Value).Concat(new[] { scene.Strings.Single(s => s.Id == page.String).Value }).Any(v => v != null && FlagToken.IsMatch(v))) throw new InvalidDataException("Intro strings cannot depend on gameplay flags");
            }
            foreach (ResultPage page in scene.ResultPages)
            {
                textRef(page.Title); SceneValidation.ParseColor(page.Color, "Results"); SceneValidation.ParseColor(page.Background, "Results");
                if (page.Rows.Length > 10) throw new InvalidDataException("Result page limit is 10 rows");
                foreach (ResultRow row in page.Rows) { textRef(row.String); flagRef(row.RequiresFlag); flagRef(row.Counter); if (string.IsNullOrEmpty(row.RequiresFlag) != (row.EqualsValue == null)) throw new InvalidDataException("Result conditions need both requiresFlag and equals"); if (!string.IsNullOrEmpty(row.Counter) && !scene.Flags.Any(f => f.Id == row.Counter && f.Type == "integer")) throw new InvalidDataException("Result counter must name an integer flag"); }
            }
            var actors = new HashSet<string>(StringComparer.Ordinal);
            foreach (NativeActorData actor in scene.NativeActors)
            {
                objectId(actor.Id); Choice(actor.Kind, "oldman", "merchant");
                if (string.IsNullOrWhiteSpace(actor.Name) || !actors.Add(actor.Kind + "/" + actor.Name) || actor.Quotes.Length > 64 || actor.Opacity < 0 || actor.Opacity > 1 || Math.Abs(actor.OffsetX) > 4096 || Math.Abs(actor.OffsetY) > 4096) throw new InvalidDataException(actor.Id + ": invalid actor binding");
                SceneValidation.ParseColor(actor.Tint, actor.Id);
                if (actor.Kind == "merchant" && actor.Quotes.Length > 0) throw new InvalidDataException(actor.Id + ": merchant trading dialogue remains native; extra quotes require oldman");
                foreach (ActorQuote quote in actor.Quotes) { textRef(quote.String); flagRef(quote.RequiresFlag); if (string.IsNullOrEmpty(quote.RequiresFlag) || quote.EqualsValue == null) throw new InvalidDataException(actor.Id + ": extra quote requires explicit requiresFlag and equals"); }
            }
            foreach (FlagData flag in scene.Flags) FlagValue(flag, flag.Value);
            foreach (RuleData rule in scene.Rules)
            {
                FlagData assignment = scene.Flags.FirstOrDefault(f => f.Id == rule.SetFlag);
                if (assignment != null) FlagValue(assignment, rule.Value);
                if (string.IsNullOrEmpty(rule.Increment) && rule.Amount != 0) throw new InvalidDataException(rule.Id + ": amount requires increment");
                if (!string.IsNullOrEmpty(rule.Increment))
                {
                    if (!scene.Flags.Any(f => f.Id == rule.Increment && f.Type == "integer") || rule.Amount == 0) throw new InvalidDataException(rule.Id + ": increment requires an integer flag and nonzero amount");
                    if (!string.IsNullOrEmpty(rule.SetFlag)) throw new InvalidDataException(rule.Id + ": choose increment or setFlag");
                }
            }
        }
        private static void Choice(string value, params string[] values) { if (!values.Contains(value)) throw new InvalidDataException("Expected " + string.Join("/", values) + ", got " + value); }
    }
}
