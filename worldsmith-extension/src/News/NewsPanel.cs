using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;

namespace WorldsmithExtension
{
    internal static class NewsPanel
    {
        sealed class State
        {
            internal bool Running;
            internal DateTime RetryAt;
        }
        static readonly ConditionalWeakTable<object, State> States = new ConditionalWeakTable<object, State>();

        internal static void Load(object viewModel)
        {
            var state = States.GetValue(viewModel, key => new State());
            lock (state)
            {
                if (state.Running || DateTime.UtcNow < state.RetryAt) return;
                state.Running = true;
            }
            Task.Run(() =>
            {
                try
                {
                    string folder = Path.Combine(Engine.Data, "news");
                    Directory.CreateDirectory(folder);
                    var items = NewsFeed.Sources.ToDictionary(source => source,
                        source => NewsFeed.ReadCache(Path.Combine(folder, source + ".xml"), Engine.Log));
                    SyndicationItem bundled = null;
                    try
                    {
                        var sections = NewsFeed.Sections(File.ReadAllText(Path.Combine(Engine.Bundle, "CHANGELOG.md")));
                        string notes;
                        if (sections.TryGetValue(Updates.Current, out notes))
                        {
                            bundled = NewsFeed.ExtensionPost(Updates.Current, notes,
                                File.GetLastWriteTimeUtc(Path.Combine(Engine.Bundle, "CHANGELOG.md")));
                            if (!items[NewsFeed.Extension].Any(item => item.Id == bundled.Id)) items[NewsFeed.Extension].Add(bundled);
                        }
                    }
                    catch (Exception e) { Engine.Log("Bundled changelog unavailable: " + e.Message); }
                    Publish(viewModel, items.Values.SelectMany(list => list).ToList());
                    Parallel.ForEach(NewsFeed.Sources, source =>
                    {
                        var fresh = NewsFeed.Cached(Path.Combine(folder, source + ".xml"), () =>
                        {
                            if (source == NewsFeed.Extension)
                            {
                                string releases = NewsFeed.Fetch(NewsFeed.ReleasesUrl);
                                return NewsFeed.ParseReleases(releases, NewsFeed.Fetch(NewsFeed.ChangelogAddress(releases)));
                            }
                            string url = source == NewsFeed.Editor ? "https://teamnexile.github.io/jk-workshop-docs/feed.xml" : "https://jump-king.com/feed";
                            return NewsFeed.ParseFeed(NewsFeed.Fetch(url), source);
                        }, Engine.Log);
                        if (source == NewsFeed.Extension && bundled != null && !fresh.Any(item => item.Id == bundled.Id)) fresh.Add(bundled);
                        lock (items)
                        {
                            if (fresh.Count != 0) items[source] = fresh;
                            Publish(viewModel, items.Values.SelectMany(list => list).ToList());
                        }
                    });
                }
                catch (Exception e) { Engine.Log("News loading failed: " + e.Message); }
                finally
                {
                    lock (state)
                    {
                        state.Running = false;
                        state.RetryAt = DateTime.UtcNow.AddSeconds(30);
                    }
                }
            });
        }

        static void Publish(object viewModel, List<SyndicationItem> items)
        {
            Engine.UI(() =>
            {
                // Posts is just an auto-property in the host
                // replace it after binding and the page keeps watching the old empty collection
                var posts = (ObservableCollection<SyndicationItem>)Engine.Get(viewModel, "Posts");
                NewsFeed.Populate(posts, items);
            });
        }
    }
}
