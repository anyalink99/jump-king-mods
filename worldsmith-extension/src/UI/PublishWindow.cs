using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WorldsmithExtension
{
    internal sealed class PublishWindow : Window
    {
        readonly string root, category;
        readonly BuildPlan plan;
        readonly PublishSession session;
        readonly bool package;
        readonly TextBox id, title, description, tags, preview, changes, log, screens, links;
        readonly ComboBox visibility, collision;
        readonly StackPanel form;
        readonly Button build, publish;
        IDisposable buildLease;

        readonly DispatcherTimer progress = new DispatcherTimer{Interval = TimeSpan.FromMilliseconds(250)};

        TextBlock publicationHeading;
        internal ulong ItemId { get { return session.ItemId; } }
        string PublicationTitle { get { return (ItemId == 0 ? "Publish new item" : "Update Workshop item") + " - " + title.Text; } }
        internal bool WorkBusy
        {
            get
            {
                return session.Busy;
            }
        }

        internal string ProjectRoot
        {
            get
            {
                return root;
            }
        }

        string content { get { return session.Content; } }
        internal PublishWindow(string packageRoot = null, string packageCategory = null)
        {
            root = packageRoot ?? Engine.ProjectRoot;
            if (String.IsNullOrEmpty(root) || !Directory.Exists(root))
                throw new InvalidOperationException("Open a project in Worldsmith first.");
            category = packageCategory ?? Engine.Get(Engine.Project, "Type").ToString();
            plan = BuildPlan.Inspect(root, category);
            var format = plan.Format;
            package = plan.Intent == BuildIntent.PreservePackage;
            Title = "Workshop";
            Width = 760;
            Height = 850;
            MinWidth = 600;
            MinHeight = 500;
            EditorUI.Theme(this);
            form = new StackPanel{Margin = new Thickness(20)};
            Content = new ScrollViewer{Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto};
            form.Children.Add(new EditorText{Text = root, TextWrapping = TextWrapping.Wrap});
            form.Children.Add(new EditorText{Text = format.Label + ": " + plan.Description + " " + format.Limits, TextWrapping = TextWrapping.Wrap});
            if (category == "Mod")
                form.Children.Add(new EditorText{Text = ModPackage.IsSourceProject(root) ? "C# mod project: Build compiles Release and prepares a checked package." : "Compiled mod project: Build prepares a checked copy of its DLLs and resources.", TextWrapping = TextWrapping.Wrap});
            ulong nativeId = packageRoot == null ? Convert.ToUInt64(Engine.Get(Engine.Project, "SteamPublishedId")) : 0;
            ulong existing = WorkshopCatalog.IdFor(root, nativeId);
            var linked = WorkshopCatalog.Find(existing);
            WorkshopLibrary.RequireCategory(linked, category);
            var background = BackgroundWork.On(Dispatcher);
            session = new PublishSession(existing, linked, new Publisher(new SteamWorkshop(), background), Operations.Current, background);
            publicationHeading = new EditorText { FontSize = 25, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 4) };
            form.Children.Add(publicationHeading);
            id = Field("Workshop item ID", 0);
            id.IsReadOnly = true;
            id.Text = existing == 0 ? "" : existing.ToString();
            var linking = new WrapPanel();
            linking.Children.Add(EditorUI.Button("Choose existing item...", () =>
            {
                if (WorkBusy) throw new InvalidOperationException("Wait for the current operation before changing the Workshop item.");
                var item = WorkshopUI.ChooseItem(category);
                if (item == null) return;
                WorkshopCatalog.Link(root, item);
                session.Relink(item);
            }));
            linking.Children.Add(EditorUI.Button("Refresh Workshop", WorkshopCatalog.RequestRefresh));
            linking.Children.Add(EditorUI.Button("View Workshop item", () =>
            {
                if (ItemId != 0) Process.Start("steam://url/CommunityFilePage/" + ItemId);
            }));
            form.Children.Add(linking);
            title = Field("Title", 0);
            title.Text = WorkshopLibrary.LocalTitle(root, category);
            description = Field("Description", 70);
            string notes = Path.Combine(root, "WORKSHOP.md");
            if (File.Exists(notes)) description.Text = String.Join(Environment.NewLine, File.ReadLines(notes).SkipWhile(line => String.IsNullOrWhiteSpace(line) || line.StartsWith("# ", StringComparison.Ordinal)));
            tags = Field("Tags (comma separated)", 0);
            tags.Text = category == "Set" ? "Skin, Set" : category == "Skin" ? "Skin, Single" : category;
            visibility = Choice("Visibility", new[]{"Public", "Friends only", "Private", "Unlisted"});
            visibility.SelectedIndex = 2;
            preview = Field("Local preview image (blank = keep existing preview)", 0);
            preview.Text = existing == 0 ? WorkshopLibrary.LocalPreview(root) : "";
            changes = Field("Change notes", 45);
            if (packageRoot == null && existing == 0)
            {
                var prototype = Engine.Get(Engine.Service("JKWorldsmith.ViewModels.Shared.DetailsViewModel"), "Prototype");
                if (prototype != null)
                {
                    title.Text = (string)Engine.Get(prototype, "Title") ?? title.Text;
                    description.Text = (string)Engine.Get(prototype, "Description") ?? description.Text;
                    string[] currentTags = ((IEnumerable)Engine.Get(prototype, "Tags")).Cast<string>().ToArray();
                    if (currentTags.Length != 0) tags.Text = String.Join(", ", currentTags);
                    string image = (string)Engine.Get(prototype, "Preview");
                    if (File.Exists(image)) preview.Text = image;
                }
            }
            ApplyDetails(linked);

            if (!package && category == "Level" && File.Exists(Path.Combine(root, "level_settings.xml")))
            {
                form.Children.Add(new EditorText{Text = "MegaMapping layout", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 18, 0, 0)});
                int count = Collision.SourceScreens(root);
                var layout = Layout.Read(root, count == 0 ? 169 : count);
                Collision.LegacyLayout(root,layout);
                screens = Field("Authored screens (1–4096)", 0);
                screens.Text = layout.Screens.ToString();
                collision = Choice("Authoritative collision image", new[]{"strip", "atlas", "level+ atlas", "level+ strip"});
                collision.SelectedItem = (layout.Pixel?"level+ ":"")+layout.Source;
                var columns=Field("level+ columns (atlas)",0);columns.Text=(layout.Pixel?layout.Columns:13).ToString();
                var rows=Field("level+ rows (atlas)",0);rows.Text=(layout.Pixel?layout.Rows:13).ToString();
                var resolution=Choice("level+ pixels per screen",new[]{"60x45","120x90","240x180","480x360"});
                resolution.SelectedItem=layout.Pixel?layout.CellWidth+"x"+layout.CellHeight:"480x360";
                var overrides=Field("Optional screen resolutions: screen widthxheight (one per line)",45);
                overrides.Text=String.Join(Environment.NewLine,layout.Resolutions.OrderBy(p=>p.Key).Select(p=>(p.Key+1)+" "+p.Value+"x"+(p.Value*3/4)));
                form.Children.Add(new EditorText{Text="Layouts use mapping_settings.xml and transitions.xml. Players need Mega Mapping Expansion 0.11+ and JK Runtime 2.1+. More Block Sizes is optional for version 2; legacy level+ maps still need it. Edit one PNG; atlas screens run down each column. Save keeps the image dimensions.",TextWrapping=TextWrapping.Wrap});
                Func<Layout> readLayout=delegate {
                    int authored=Int32.Parse(screens.Text);string selected=(string)collision.SelectedItem;
                    var value=new Layout{Screens=authored,Side=authored==layout.Screens?layout.Side:Layout.AtlasSide(authored),Source=selected.Replace("level+ ","")};
                    if(selected.StartsWith("level+ ",StringComparison.Ordinal))
                    {
                        string[] size=((string)resolution.SelectedItem).Split('x');
                        value.Version=2;value.CellWidth=Int32.Parse(size[0]);value.CellHeight=Int32.Parse(size[1]);
                        value.Columns=value.Source=="strip"?1:Int32.Parse(columns.Text);value.Rows=value.Source=="strip"?authored:Int32.Parse(rows.Text);
                        foreach(string row in overrides.Text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))
                        {
                            string[] fields=row.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                            if(fields.Length!=2)throw new InvalidDataException("Use: screen widthxheight (for example, 1 60x45).");
                            string[] dimensions=fields[1].Split('x');
                            if(dimensions.Length!=2)throw new InvalidDataException("Use widthxheight for a screen resolution.");
                            int screen=Int32.Parse(fields[0])-1,width=Int32.Parse(dimensions[0]),height=Int32.Parse(dimensions[1]);
                            if(width*3!=height*4||value.Resolutions.ContainsKey(screen))throw new InvalidDataException("Invalid or duplicate screen resolution.");
                            value.Resolutions.Add(screen,width);
                        }
                    }
                    Collision.LegacyLayout(root,value,false);value.Validate();return value;
                };
                form.Children.Add(Button("Create blank level+.png",()=>{
                    if(Operations.Current.Busy||LoadState.Busy)throw new InvalidOperationException("Wait for the current operation.");
                    var value=readLayout();if(!value.Pixel)throw new InvalidDataException("Select level+ atlas or level+ strip first.");
                    string path=Path.Combine(root,"level+.png");if(File.Exists(path))throw new IOException("level+.png already exists. Your collision image was kept.");
                    using(var image=new System.Drawing.Bitmap(value.PixelLayout().ImageWidth,value.PixelLayout().ImageHeight))Collision.Png(image,path);
                    Write("Created an empty level+.png. Paint your collision, then save layout and build.");
                }));
                links = Field("Side links: screen left|right target (one per line)", 55);
                links.Text = String.Join(Environment.NewLine, layout.Links.Select(x => x.Item1 + " " + x.Item2 + " " + x.Item3));
                var save = Button("Save layout", () =>
                {
                    if (Operations.Current.Busy || LoadState.Busy)
                        throw new InvalidOperationException("Wait for the current operation before changing layout.");
                    var v=readLayout();
                    if(v.Pixel)using(var image=Collision.Read(Path.Combine(root,"level+.png")))Collision.ValidatePixel(image,v);
                    foreach (string row in links.Text.Split(new[]{'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var p = row.Split(new[]{' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length != 3)
                            throw new InvalidDataException("Use: screen left|right target");
                        v.Links.Add(Tuple.Create(Int32.Parse(p[0]), p[1], Int32.Parse(p[2])));
                    }

                    v.Save(root);
                    layout = v;
                    session.InvalidateBuild();
                    Write("Layout saved. Build again to apply it.");
                });
                form.Children.Add(save);
            }

            var buttons = new StackPanel{Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 8)};
            build = Button("Build checked copy", StartBuild);
            publish = Button(ItemId == 0 ? "Publish new item" : "Update item", StartPublish);
            publish.IsEnabled = false;
            buttons.Children.Add(build);
            buttons.Children.Add(publish);
            buttons.Children.Add(Button("Open build folder", () =>
            {
                if (content != null)
                    Process.Start("explorer.exe", WorkerProcess.Quote(content));
            }));
            form.Children.Add(buttons);
            log = Field("Build and Steam status", 150);
            log.IsReadOnly = true;
            log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            progress.Tick += delegate
            {
                try
                {
                    WriteStatus(session.Progress());
                }
                catch (Exception e)
                {
                    WriteStatus("Cannot read Steam progress: " + e.Message);
                }
            };
            WorkshopCatalog.Changed += OnWorkshopChanged;
            Closed += (s, e) => { WorkshopCatalog.Changed -= OnWorkshopChanged; if (buildLease != null) buildLease.Dispose(); };
            session.DetailsChanged += ApplyDetails;
            session.Changed += RefreshActions;
            if (!session.DetailsLoaded) Write("Waiting for the linked item's Workshop details. Refresh Workshop before publishing.");
            Closing += (s, e) =>
            {
                if (session.Busy)
                {
                    e.Cancel = true;
                    Write("Wait for the current build or Steam callback before closing this panel.");
                }
            };
        }

        void ApplyDetails(WorkshopEntry item)
        {
            if (item != null)
            {
                WorkshopLibrary.RequireCategory(item, category);
                title.Text = item.Title;
                description.Text = item.Description;
                tags.Text = String.Join(", ", item.Tags);
                visibility.SelectedIndex = item.Visibility;
                preview.Text = "";
            }
            publicationHeading.Text = ItemId == 0 ? "New publication" : "Update " + title.Text;
            Title = PublicationTitle;
            if (publish != null) publish.Content = ItemId == 0 ? "Publish new item" : "Update item";
        }

        void RefreshActions()
        {
            id.Text = ItemId == 0 ? "" : ItemId.ToString();
            build.IsEnabled = !session.Busy;
            publish.IsEnabled = session.CanPublish;
            publish.Content = ItemId == 0 ? "Publish new item" : "Update item";
            publicationHeading.Text = ItemId == 0 ? "New publication" : "Update " + title.Text;
        }

        void OnWorkshopChanged()
        {
            var item = WorkshopCatalog.Find(ItemId);
            WorkshopLibrary.RequireCategory(item, category);
            session.AcceptDetails(item);
        }

        TextBox Field(string label, double height)
        {
            form.Children.Add(new EditorText{Text = label, Margin = new Thickness(0, 10, 0, 3)});
            var t = EditorUI.Control<TextBox>("TextBox");
            t.Padding = new Thickness(5);
            t.TextWrapping = height > 0 ? TextWrapping.Wrap : TextWrapping.NoWrap;
            t.AcceptsReturn = height > 0;
            if (height > 0)
                t.Height = height;
            form.Children.Add(t);
            return t;
        }

        ComboBox Choice(string label, string[] options)
        {
            form.Children.Add(new EditorText{Text = label, Margin = new Thickness(0, 10, 0, 3)});
            var c = new ComboBox{ItemsSource = options};
            form.Children.Add(c);
            return c;
        }

        Button Button(string text, Action action)
        {
            return EditorUI.Button(text, () =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Write(e.GetBaseException().Message);
                    throw;
                }
            });
        }

        void Write(string text)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                log.AppendText(text + Environment.NewLine);
                log.ScrollToEnd();
                Panel.Status(text);
            }));
        }

        string lastStatus;
        void WriteStatus(string text)
        {
            if (lastStatus == text)
                return;
            lastStatus = text;
            Panel.Status(text);
            Title = PublicationTitle + " - " + text;
        }

        void StartBuild()
        {
            if (LoadState.Busy) throw new InvalidOperationException("Wait for project loading to finish.");
            string destination = Path.Combine(Engine.Data, "builds", Guid.NewGuid().ToString("N"), "content");
            Write(plan.Description);
            var candidateLease = GeneratedHistory.Lease(Path.GetDirectoryName(destination));
            try
            {
                session.Build(destination, () =>
                {
                    GeneratedHistory.Prune(Engine.Data, "builds", "", 3, DateTime.UtcNow);
                    BuildJob.Run(root, category, destination, Write);
                }, error =>
                {
                    if (error == null)
                    {
                        if (buildLease != null) buildLease.Dispose();
                        buildLease = candidateLease;
                    }
                    else candidateLease.Dispose();
                    Write(error != null ? error.GetBaseException().Message : session.DetailsLoaded
                        ? "Ready. Review the fields, then choose " + (ItemId == 0 ? "Publish new item." : "Update item.")
                        : "Build ready. Load the linked item details before updating.");
                });
            }
            catch { candidateLease.Dispose(); throw; }
        }

        void StartPublish()
        {
            if (LoadState.Busy) throw new InvalidOperationException("Wait for project loading to finish.");
            if (!session.NeedsPersistence && WorkshopCatalog.IdFor(root) != ItemId)
                throw new InvalidOperationException("The project's Workshop link changed. Reopen the publication panel before uploading.");
            ulong owner = Steamworks.SteamUser.GetSteamID().m_SteamID;
            if (ItemId != 0) WorkshopLibrary.RequireOwner(session.Linked, owner);
            var request = new PublishRequest { Root = root, Title = title.Text, Description = description.Text,
                Tags = tags.Text.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray(),
                Visibility = visibility.SelectedIndex, Preview = preview.Text, Changelog = changes.Text };
            try
            {
                session.Publish(request, owner, (value, draft) => WorkshopPublication.Save(root, draft, owner), (message, ok) =>
                {
                    progress.Stop();
                    Write(message + " Item ID: " + request.Id);
                    Title = PublicationTitle + (ok ? " - completed" : " - failed");
                    if (ok)
                    {
                        try { WorkshopPublication.Save(root, request, owner); WorkshopCatalog.RequestRefresh(); }
                        catch (Exception error) { Write("Upload completed; local Workshop data could not refresh: " + error.GetBaseException().Message); }
                    }
                });
                if (session.Busy) progress.Start();
            }
            catch { progress.Stop(); throw; }
        }
    }
}
