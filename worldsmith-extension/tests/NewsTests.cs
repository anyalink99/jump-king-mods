using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;

namespace WorldsmithExtension
{
    internal static class NewsTests
    {
        internal static void Run(string root, Action<bool, string> assert, Action<Action, string> fails)
        {
            string atom = "<feed xmlns='http://www.w3.org/2005/Atom'><title>Editor</title><id>editor</id><updated>2024-01-01T00:00:00Z</updated><entry><id>update</id><title>Editor update</title><published>2024-01-01T00:00:00Z</published><content type='html'>&lt;p&gt;Editor notes&lt;/p&gt;</content></entry></feed>";
            string rss = "<rss version='2.0' xmlns:dc='http://purl.org/dc/elements/1.1/' xmlns:content='http://purl.org/rss/1.0/modules/content/'><channel><title>Game</title><link>https://jump-king.com</link><description>News</description><item><title>Workshop update</title><category>Workshop</category><dc:creator>Nexile</dc:creator><content:encoded><![CDATA[<p>Full post</p>]]></content:encoded><pubDate>Mon, 01 Jan 2024 00:00:00 GMT</pubDate></item><item><title>Other news</title><category>Other</category></item></channel></rss>";
            var editor = NewsFeed.ParseFeed(atom, NewsFeed.Editor);
            var game = NewsFeed.ParseFeed(rss, NewsFeed.Game);
            assert(editor.Single().Categories.Single().Name == NewsFeed.Editor, "editor source label restored");
            assert(((TextSyndicationContent)editor[0].Content).Text == "<p>Editor notes</p>", "Atom article body retained");
            assert(game.Count == 1 && game[0].Authors.Single().Name == "Nexile", "Workshop filtering and WordPress author restored");
            assert(((TextSyndicationContent)game[0].Content).Text == "<p>Full post</p>" && game[0].Categories.Single().Name == NewsFeed.Game, "WordPress full article and source restored");
            fails(() => NewsFeed.ParseFeed("<!DOCTYPE feed [<!ENTITY x SYSTEM 'file:///secret'>]>" + atom, NewsFeed.Editor), "feed external entities rejected");
            const string changelog = "# Release notes\n\n## 0.1.0 Preview 3\n\n- Restore news.\n  Keep authors.\n\n### More\n\nA <script> and & text.\n\n## 0.1.0 Preview 2\n\n- Earlier changes.\n";
            const string releases = "[{\"tag_name\":\"worldsmith-extension-v0.1.0-preview.3\",\"published_at\":\"2026-09-28T12:00:00Z\"},{\"tag_name\":\"another-mod-v1.0.0\",\"published_at\":\"2026-09-28T13:00:00Z\"},{\"tag_name\":\"worldsmith-extension-v0.1.0-preview.2\",\"draft\":true,\"published_at\":\"2026-09-27T12:00:00Z\"},{\"tag_name\":\"worldsmith-extension-v0.1.0-preview.4\",\"published_at\":\"2026-09-29T12:00:00Z\"}]";
            assert(NewsFeed.ChangelogAddress(releases) == "https://raw.githubusercontent.com/anyalink99/jump-king-mods/worldsmith-extension-v0.1.0-preview.4/worldsmith-extension/CHANGELOG.md", "release notes use a versioned URL instead of a stale main-branch cache");
            fails(() => NewsFeed.ChangelogAddress("[{\"tag_name\":\"worldsmith-extension-v../../other\"}]"), "invalid release tags cannot select a changelog path");
            var extension = NewsFeed.ParseReleases(releases, changelog);
            assert(extension.Count == 1 && extension[0].Title.Text == "Worldsmith Extension 0.1.0 Preview 3", "only published extension releases with changelogs appear");
            var published = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
            assert(extension[0].PublishDate == published, "release publication date is available to the native news binding");
            assert(extension[0].Authors.Single().Name == "Niotid", "extension posts credit Niotid");
            string html = ((TextSyndicationContent)extension[0].Content).Text;
            assert(html.Contains("<li>Restore news. Keep authors.</li>") && html.Contains("<h3>More</h3>"), "changelog bullet continuation and headings rendered");
            assert(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("&amp;") && !html.Contains("Earlier changes"), "notes are escaped and kept within their version");
            string cache = Path.Combine(root, "news", "editor.xml");
            var logs = new List<string>();
            NewsFeed.Cached(cache, () => editor, logs.Add);
            var retained = NewsFeed.Cached(cache, () => { throw new IOException("offline"); }, logs.Add);
            assert(retained.Count == 1 && retained[0].Categories[0].Name == NewsFeed.Editor, "offline feed retains cached source");
            assert(((TextSyndicationContent)retained[0].Content).Text == "<p>Editor notes</p>", "cached article body retained");
            retained = NewsFeed.Cached(cache, () => new List<SyndicationItem>(), logs.Add);
            assert(retained.Count == 1, "empty remote response does not erase cache");
            File.WriteAllText(cache, "broken");
            assert(NewsFeed.ReadCache(cache, logs.Add).Count == 0, "corrupt cache handled");
            string extensionCache = Path.Combine(root, "news", "extension.xml");
            NewsFeed.Cached(extensionCache, () => extension, logs.Add);
            var cachedExtension = NewsFeed.ReadCache(extensionCache, logs.Add).Single();
            assert(cachedExtension.PublishDate == published && cachedExtension.Authors.Single().Name == "Niotid", "extension date and author survive cache round trip");
            var oldPost = extension[0].Clone();
            oldPost.PublishDate = DateTimeOffset.MinValue;
            oldPost.Authors.Clear();
            oldPost.Authors.Add(new SyndicationPerson { Name = "Worldsmith Extension" });
            NewsFeed.Cached(extensionCache, () => new List<SyndicationItem> { oldPost }, logs.Add);
            var repaired = NewsFeed.Cached(extensionCache, () => { throw new IOException("offline"); }, logs.Add).Single();
            assert(repaired.PublishDate == published && repaired.Authors.Single().Name == "Niotid", "old extension cache corrected without a network connection");
            var posts = new ObservableCollection<SyndicationItem>();
            int changed = 0;
            posts.CollectionChanged += (s, e) => changed++;
            var items = editor.Concat(game).Concat(extension).ToList();
            items.Add(NewsFeed.ExtensionPost("0.1.0-preview.2", "Earlier", published.AddDays(-1)));
            NewsFeed.Populate(posts, items);
            assert(posts.Count == 3 && changed > 0, "bound collection notifies item updates");
            assert(NewsFeed.Sources.All(source => posts.Any(item => item.Categories[0].Name == source)), "recent extension posts cannot crowd out developer posts");
            assert(posts[0].PublishDate >= posts[1].PublishDate && posts[1].PublishDate >= posts[2].PublishDate, "visible posts sorted by publication date");
            assert(posts[0].Id == extension[0].Id && posts[0].PublishDate == published, "new extension release sorts ahead of older developer posts");
            NewsFeed.Populate(posts, editor);
            assert(posts.Count == 1, "refresh removes stale rows");
        }
    }
}
