using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

namespace WorldsmithExtension
{
    internal static class NewsFeed
    {
        internal const string Editor = "Worldsmith Changelogs", Game = "Jump King News", Extension = "Worldsmith Extension";
        internal const string ReleasesUrl = "https://api.github.com/repos/anyalink99/jump-king-mods/releases?per_page=100";
        internal static readonly string[] Sources = { Editor, Game, Extension };

        internal static string Fetch(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "WorldsmithExtension/" + Updates.Current;
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            using (var response = request.GetResponse())
            using (var input = response.GetResponseStream())
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int count;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (output.Length + count > 4 * 1024 * 1024 || watch.Elapsed.TotalSeconds > 15)
                        throw new IOException("News response exceeded its size or time limit.");
                    output.Write(buffer, 0, count);
                }
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        internal static List<SyndicationItem> ParseFeed(string text, string source)
        {
            using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 }))
            {
                var items = SyndicationFeed.Load(reader).Items.ToList();
                if (source == Game)
                    items = items.Where(item => item.Categories.Any(c => c.Name.Contains("Workshop"))).ToList();
                foreach (var item in items)
                {
                    foreach (var extension in item.ElementExtensions)
                    {
                        if (extension.OuterName == "creator" && item.Authors.Count == 0)
                            item.Authors.Add(new SyndicationPerson { Name = extension.GetObject<string>() });
                        if (extension.OuterName == "encoded")
                            item.Content = new TextSyndicationContent(extension.GetObject<string>(), TextSyndicationContentKind.Html);
                    }
                    if (!(item.Content is TextSyndicationContent))
                        item.Content = item.Summary ?? new TextSyndicationContent("");
                    item.Categories.Clear();
                    item.Categories.Add(new SyndicationCategory(source));
                }
                return items.OrderByDescending(item => item.PublishDate).Take(10).ToList();
            }
        }

        internal static Dictionary<string, string> Sections(string markdown)
        {
            var sections = new Dictionary<string, string>(StringComparer.Ordinal);
            string version = null;
            var body = new StringBuilder();
            foreach (string line in markdown.Replace("\r", "").Split('\n'))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    if (version != null) sections[version] = body.ToString().Trim();
                    body.Clear();
                    version = line.Substring(3).Trim().Replace(" Preview ", "-preview.");
                    if (!Updates.ValidVersion(version)) version = null;
                }
                else if (version != null) body.AppendLine(line);
            }
            if (version != null) sections[version] = body.ToString().Trim();
            return sections;
        }

        // changelogs only need headings, paragraphs and bullets here
        // encode the text before feeding the editor's HTML reader, don't let raw HTML through
        internal static string RenderNotes(string markdown)
        {
            var html = new StringBuilder();
            bool list = false, paragraph = false;
            foreach (string raw in markdown.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("- ", StringComparison.Ordinal))
                {
                    if (paragraph) { html.Append("</p>"); paragraph = false; }
                    if (!list) { html.Append("<ul>"); list = true; }
                    else html.Append("</li>");
                    html.Append("<li>").Append(WebUtility.HtmlEncode(line.Substring(2)));
                }
                else if (line.Length == 0 || line.StartsWith("### ", StringComparison.Ordinal))
                {
                    if (list) { html.Append("</li></ul>"); list = false; }
                    if (paragraph) { html.Append("</p>"); paragraph = false; }
                    if (line.Length != 0) html.Append("<h3>").Append(WebUtility.HtmlEncode(line.Substring(4))).Append("</h3>");
                }
                else if (list) html.Append(" ").Append(WebUtility.HtmlEncode(line));
                else
                {
                    if (!paragraph) { html.Append("<p>"); paragraph = true; }
                    else html.Append(" ");
                    html.Append(WebUtility.HtmlEncode(line));
                }
            }
            if (list) html.Append("</li></ul>");
            if (paragraph) html.Append("</p>");
            return html.ToString();
        }

        internal static SyndicationItem ExtensionPost(string version, string notes, DateTimeOffset published)
        {
            string title = "Worldsmith Extension " + version.Replace("-preview.", " Preview ");
            var item = new SyndicationItem(title, "", new Uri(Updates.Repository + "/releases/tag/worldsmith-extension-v" + version), "worldsmith-extension-v" + version, published);
            item.PublishDate = published;
            item.Content = new TextSyndicationContent("<h2>" + WebUtility.HtmlEncode(title) + "</h2>" + RenderNotes(notes), TextSyndicationContentKind.Html);
            item.Categories.Add(new SyndicationCategory(Extension));
            item.Authors.Add(new SyndicationPerson { Name = "Niotid" });
            return item;
        }

        internal static string ChangelogAddress(string json)
        {
            var releases = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.DeserializeObject(json) as object[];
            if (releases == null) throw new InvalidDataException("Invalid news release response.");
            string newest = null;
            foreach (var release in releases.OfType<Dictionary<string, object>>())
            {
                object value;
                if (release.TryGetValue("draft", out value) && Object.Equals(value, true)) continue;
                string tag = release.TryGetValue("tag_name", out value) ? value as string : null;
                const string prefix = "worldsmith-extension-v";
                if (tag == null || !tag.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string version = tag.Substring(prefix.Length);
                if (Updates.ValidVersion(version) && (newest == null || Updates.Compare(version, newest) > 0)) newest = version;
            }
            if (newest == null) throw new InvalidDataException("No extension releases found.");
            return "https://raw.githubusercontent.com/anyalink99/jump-king-mods/worldsmith-extension-v" + newest + "/worldsmith-extension/CHANGELOG.md";
        }

        internal static List<SyndicationItem> ParseReleases(string json, string changelog)
        {
            var releases = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.DeserializeObject(json) as object[];
            if (releases == null) throw new InvalidDataException("Invalid news release response.");
            var sections = Sections(changelog);
            var result = new List<SyndicationItem>();
            foreach (var release in releases.OfType<Dictionary<string, object>>())
            {
                object value;
                if (release.TryGetValue("draft", out value) && Object.Equals(value, true)) continue;
                string tag = release.TryGetValue("tag_name", out value) ? value as string : null;
                const string prefix = "worldsmith-extension-v";
                if (tag == null || !tag.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string version = tag.Substring(prefix.Length), notes;
                DateTimeOffset published;
                if (!Updates.ValidVersion(version) || !sections.TryGetValue(version, out notes) ||
                    !release.TryGetValue("published_at", out value) || !DateTimeOffset.TryParse(Convert.ToString(value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out published)) continue;
                result.Add(ExtensionPost(version, notes, published));
            }
            return result.OrderByDescending(item => item.PublishDate).Take(10).ToList();
        }

        internal static List<SyndicationItem> Cached(string path, Func<List<SyndicationItem>> fetch, Action<string> log)
        {
            try
            {
                var items = fetch();
                if (items.Count == 0) throw new InvalidDataException("News feed is empty.");
                try
                {
                    Files.Atomic(path, stream =>
                    {
                        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = Encoding.UTF8, CloseOutput = false }))
                            new SyndicationFeed(items).SaveAsAtom10(writer);
                    });
                }
                catch (Exception e) { log("News cache write failed: " + e.Message); }
                return items;
            }
            catch (Exception e) { log("News unavailable: " + e.Message); }
            return ReadCache(path, log);
        }

        internal static List<SyndicationItem> ReadCache(string path, Action<string> log)
        {
            try
            {
                if (!File.Exists(path)) return new List<SyndicationItem>();
                using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 }))
                {
                    var items = SyndicationFeed.Load(reader).Items.ToList();
                    foreach (var item in items.Where(post => post.Categories.Any(category => category.Name == Extension)))
                    {
                        // early extension feeds stored the release date only in updated
                        if (item.PublishDate == DateTimeOffset.MinValue) item.PublishDate = item.LastUpdatedTime;
                        item.Authors.Clear();
                        item.Authors.Add(new SyndicationPerson { Name = "Niotid" });
                    }
                    return items;
                }
            }
            catch (Exception e) { log("News cache unavailable: " + e.Message); return new List<SyndicationItem>(); }
        }

        internal static void Populate(ObservableCollection<SyndicationItem> posts, IEnumerable<SyndicationItem> items)
        {
            var sorted = items.OrderByDescending(item => item.PublishDate).ToList();
            // keep each publisher visible, even when its latest post is older
            var selected = Sources.Select(source => sorted.FirstOrDefault(item => item.Categories.Any(c => c.Name == source)))
                .Where(item => item != null).ToList();
            selected.AddRange(sorted.Except(selected).Take(3 - selected.Count));
            posts.Clear();
            foreach (var item in selected.OrderByDescending(item => item.PublishDate)) posts.Add(item);
        }
    }
}
