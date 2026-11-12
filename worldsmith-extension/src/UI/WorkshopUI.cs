using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WorldsmithExtension
{
    internal static class WorkshopUI
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page, object> Attached = new System.Runtime.CompilerServices.ConditionalWeakTable<Page, object>();

        internal static void PublishNew()
        {
            if (Operations.Current.Busy || LoadState.Busy) throw new InvalidOperationException("Finish the current operation before opening a publication.");
            string root = EditorUI.SelectFolder("Choose the project or release folder to publish");
            if (root == null) return;
            string category = WorkshopCatalog.CategoryFor(root);
            Panel.OpenPackage(root, category);
        }

        internal static WorkshopEntry ChooseItem(string category)
        {
            var window = new Window { Title = "Choose your Workshop item", Width = 700, Height = 530, MinWidth = 500, MinHeight = 350 };
            EditorUI.Theme(window);
            var body = new DockPanel { Margin = new Thickness(20) };
            var heading = new EditorText { Text = "Choose an existing " + category.ToLowerInvariant() + " to update", FontSize = 22, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(heading, Dock.Top); body.Children.Add(heading);
            var list = new ListBox { Background = window.Background, Foreground = window.Foreground };
            WorkshopEntry selected = null;
            var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            var choose = EditorUI.Button("Use selected item", () =>
            {
                selected = list.SelectedItem as WorkshopEntry;
                if (selected != null) window.DialogResult = true;
            });
            actions.Children.Add(choose);
            actions.Children.Add(EditorUI.Button("Refresh", WorkshopCatalog.RequestRefresh));
            DockPanel.SetDock(actions, Dock.Bottom); body.Children.Add(actions); body.Children.Add(list);
            Action update = () =>
            {
                var previous = list.SelectedItem as WorkshopEntry;
                var items = WorkshopCatalog.Items().Where(item => item.Category == category).OrderBy(item => item.Title).ToList();
                list.ItemsSource = items;
                list.SelectedItem = previous == null ? null : items.FirstOrDefault(item => item.Id == previous.Id);
                choose.IsEnabled = list.SelectedItem != null;
            };
            list.SelectionChanged += (sender, args) => choose.IsEnabled = list.SelectedItem != null;
            update();
            WorkshopCatalog.Changed += update;
            window.Closed += (sender, args) => WorkshopCatalog.Changed -= update;
            window.Content = body;
            window.ShowDialog();
            return selected;
        }

        internal static string ChooseFolder(WorkshopEntry entry)
        {
            if (Operations.Current.Busy || LoadState.Busy) throw new InvalidOperationException("Finish the current operation before changing the project folder.");
            string root = EditorUI.SelectFolder("Choose the project or release folder for " + entry.Title);
            if (root != null) WorkshopCatalog.Link(root, entry);
            return root;
        }

        internal static void OpenUpdate(WorkshopEntry entry)
        {
            if (Operations.Current.Busy || LoadState.Busy) throw new InvalidOperationException("Finish the current operation before opening an update.");
            string root = WorkshopCatalog.FolderFor(entry.Id);
            if (!Directory.Exists(root)) root = ChooseFolder(entry);
            if (root == null) return;
            WorkshopCatalog.Link(root, entry);
            Panel.OpenPackage(root, entry.Category);
        }

        internal static void Attach(Page page)
        {
            var list = Engine.Get(page, "Items") as ListView;
            if (list == null) throw new InvalidOperationException("Workshop item list is unavailable.");
            Wrap(page, list);
        }

        internal static void Wrap(Page page, ListView list)
        {
            object marker;
            if (Attached.TryGetValue(page, out marker)) return;
            var original = page.Content as UIElement;
            if (original == null) throw new InvalidOperationException("Workshop page content is unavailable.");
            page.Content = null;
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 4, 16, 0) };
            header.Children.Add(EditorUI.Button("Publish new...", PublishNew));
            layout.Children.Add(header);
            Grid.SetRow(original, 1); layout.Children.Add(original);
            var selected = new StackPanel { Margin = new Thickness(20, 8, 20, 12) };
            var title = new EditorText { Text = "Select an item to open its project or update its files.", FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
            var folder = new EditorText { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 4, 4, 4) };
            var actions = new WrapPanel();
            selected.Children.Add(title); selected.Children.Add(folder); selected.Children.Add(actions);
            Func<WorkshopEntry> current = () => list.SelectedItem == null ? null : NativeWorkshop.FromNative(list.SelectedItem);
            Action refresh = () =>
            {
                var item = current();
                actions.Children.Clear();
                if (item == null) { title.Text = "Select an item to open its project or update its files."; folder.Text = ""; return; }
                string root = WorkshopCatalog.FolderFor(item.Id);
                title.Text = item.Title;
                folder.Text = String.IsNullOrEmpty(root) ? "No project folder linked." : (Directory.Exists(root) ? "Project folder: " : "Folder not found: ") + root;
                actions.Children.Add(EditorUI.Button("Open project", () =>
                {
                    string path = Directory.Exists(root) ? root : ChooseFolder(item);
                    if (path != null) { WorkshopCatalog.Link(path, item); EditorUI.OpenProject(path); }
                }));
                var openFolder = EditorUI.Button("Open folder", () => Process.Start("explorer.exe", WorkerProcess.Quote(root)));
                openFolder.IsEnabled = Directory.Exists(root); actions.Children.Add(openFolder);
                actions.Children.Add(EditorUI.Button("Update...", () => OpenUpdate(item)));
                actions.Children.Add(EditorUI.Button("Choose folder...", () => ChooseFolder(item)));
                string installed = WorkshopCatalog.InstalledFolder(item.Id);
                if (installed != null && !WorkshopLibrary.SameFolder(root, installed))
                    actions.Children.Add(EditorUI.Button("Open installed copy", () => EditorUI.OpenProject(installed, false, item.Id)));
            };
            list.SelectionChanged += (sender, args) => refresh();
            page.Loaded += (sender, args) => { WorkshopCatalog.Initialize(); WorkshopCatalog.Changed -= refresh; WorkshopCatalog.Changed += refresh; refresh(); };
            page.Unloaded += (sender, args) => WorkshopCatalog.Changed -= refresh;
            Grid.SetRow(selected, 2); layout.Children.Add(selected);
            page.Content = layout;
            Attached.Add(page, new object());
        }

        internal static void AttachSummary(Page page)
        {
            var original = page.Content as UIElement;
            page.Content = null;
            var layout = new DockPanel();
            var actions = new WrapPanel { Margin = new Thickness(20, 12, 20, 8) };
            actions.Children.Add(EditorUI.Button("Publish new...", PublishNew));
            actions.Children.Add(new EditorText { Text = "Select a Workshop category to open or update your existing items.", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), TextWrapping = TextWrapping.Wrap });
            DockPanel.SetDock(actions, Dock.Top); layout.Children.Add(actions);
            if (original != null) layout.Children.Add(original);
            page.Content = layout;
        }
    }
}
