using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    public sealed class GimmickSearchPreferences
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public string Colour { get; set; }
        public string[] Providers { get; set; }
        public string[] Maps { get; set; }
        public string[] Regions { get; set; }
        public string[] Families { get; set; }
        public string[] Geometry { get; set; }
        public string[] Readiness { get; set; }
        public string[] Usage { get; set; }
        public string Sort { get; set; }
    }
    internal sealed class GimmickSearchRecord
    {
        internal GimmickEntry Entry;
        internal string Text, Readiness;
        internal bool Enabled;
    }
    internal static class GimmickSearch
    {
        internal static int[] ScreenSelection(string text)
        {
            var result = new SortedSet<int>();
            foreach (string token in (text ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) {
                var ends = token.Trim().Split('-'); int first, last;
                if (ends.Length > 2 || !int.TryParse(ends[0], out first) || first < 1 || first > 10000) throw new InvalidOperationException("Use screen numbers or ranges, for example 1,3-7,12.");
                if (ends.Length == 1) last = first;
                else if (!int.TryParse(ends[1], out last) || last < first || last > 10000) throw new InvalidOperationException("Screen ranges must ascend and stay between 1 and 10000.");
                for (int i = first; i <= last; i++) result.Add(i);
            }
            return result.ToArray();
        }
        internal static GimmickSearchPreferences Copy(GimmickSearchPreferences q)
        {
            Func<string[], string[]> copy = a => a == null ? null : (string[])a.Clone();
            return new GimmickSearchPreferences { Name = q.Name, Text = q.Text, Colour = q.Colour, Sort = q.Sort,
                Providers = copy(q.Providers), Maps = copy(q.Maps), Regions = copy(q.Regions), Families = copy(q.Families),
                Geometry = copy(q.Geometry), Readiness = copy(q.Readiness), Usage = copy(q.Usage) };
        }
        internal static bool Colour(string text, out Color? colour)
        {
            colour = null; if (string.IsNullOrWhiteSpace(text)) return true;
            text = text.Trim(); uint hex;
            if (Regex.IsMatch(text, "^#?[0-9a-fA-F]{6}$") && uint.TryParse(text.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hex))
            { colour = new Color((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex); return true; }
            var match = Regex.Match(text, @"^(?:rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)|(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3}))$", RegexOptions.IgnoreCase);
            if (!match.Success) return false;
            int at = match.Groups[1].Success ? 1 : 4;
            int[] values = Enumerable.Range(at, 3).Select(i => int.Parse(match.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray();
            if (values.Any(v => v > 255)) return false;
            colour = new Color(values[0], values[1], values[2]); return true;
        }
        internal static string Hex(Color colour) { return "#" + colour.R.ToString("X2") + colour.G.ToString("X2") + colour.B.ToString("X2"); }
        internal static string Ready(GimmickEntry entry)
        { return entry.Slot != null && entry.Slot.ReadOnlyReason != null ? "Read-only" : entry.Error != null ? "Unavailable" : entry.Kind == "Colour" ? "Unknown" : entry.Kind == "Block" && entry.Template == null ? "Needs preparation" : "Ready"; }
        internal static bool Selected(string[] options, string value) { return options == null || options.Length == 0 || options.Contains(value); }
        internal static string[] Toggle(string[] values, string value)
        { var result = (values ?? new string[0]).ToList(); if (!result.Remove(value)) result.Add(value); return result.ToArray(); }
        internal static GimmickSearchRecord[] Snapshot()
        {
            return Gimmicks.Entries.Values.Select(e => new GimmickSearchRecord { Entry = e, Readiness = Ready(e), Enabled = Gimmicks.Enabled(e),
                Text = (e.Label + " " + e.Owner + " " + e.Id + " " + e.Family + " " + (e.Kind == "Wind" ? "wind gust breeze environment" : "")).ToLowerInvariant() }).ToArray();
        }
        internal static GimmickSearchRecord[] Filter(GimmickSearchRecord[] snapshot, GimmickSearchPreferences query, GimmickMap[] maps, out string error)
        {
            error = null; Color? colour;
            if (!Colour(query.Colour, out colour)) { error = "Use #RRGGBB or R,G,B (0-255)."; return new GimmickSearchRecord[0]; }
            var words = (query.Text ?? "").ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<GimmickSearchRecord> result = snapshot.Where(r => words.All(w => r.Text.Contains(w))
                && Selected(query.Providers, r.Entry.Owner) && Selected(query.Families, r.Entry.Family) && Selected(query.Geometry, r.Entry.Geometry)
                && Selected(query.Readiness, r.Readiness) && (!colour.HasValue || r.Entry.Colour == colour || (r.Entry.Kind == "Wind" && GimmickWind.SourceColour(colour.Value)))
                && (query.Usage == null || query.Usage.Length == 0 || (query.Usage.Contains("Pinned") && Gimmicks.Pins.Contains(r.Entry.Id)) || (query.Usage.Contains("Enabled") && r.Enabled)));
            if ((query.Maps ?? new string[0]).Length != 0 || (query.Regions ?? new string[0]).Length != 0)
                result = result.Where(r => maps.Any(m => Selected(query.Maps, m.Id) && GimmickMaps.Occurs(m, null, r.Entry)
                    && ((query.Regions ?? new string[0]).Length == 0 || m.Regions.Any(region => query.Regions.Contains(m.Id + "|" + region.Number) && GimmickMaps.Occurs(m, region, r.Entry)))));
            if (query.Sort == "Provider") return result.OrderBy(r => r.Entry.Owner).ThenBy(r => r.Entry.Label).ThenBy(r => r.Entry.Id, StringComparer.Ordinal).ToArray();
            if (query.Sort == "Readiness") return result.OrderBy(r => r.Readiness != "Ready").ThenBy(r => r.Entry.Label).ThenBy(r => r.Entry.Id, StringComparer.Ordinal).ToArray();
            return result.OrderByDescending(r => words.Length != 0 && r.Entry.Label.StartsWith(query.Text ?? "", StringComparison.OrdinalIgnoreCase)).ThenBy(r => r.Entry.Label).ThenBy(r => r.Entry.Id, StringComparer.Ordinal).ToArray();
        }
    }
}
