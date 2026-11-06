using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorldsmithExtension
{
    internal static class EditorUI
    {
        internal static TextBlock FormatSummary;
        static string inspectedRoot;
        static ProjectFormat inspectedFormat;
        internal static void RefreshFormat()
        {
            if (FormatSummary == null || String.IsNullOrEmpty(Engine.ProjectRoot))
                return;
            string root = Engine.ProjectRoot, category = Engine.Get(Engine.Project, "Type").ToString();
            var format = WorkshopLibrary.SameFolder(root, inspectedRoot) ? inspectedFormat : null;
            inspectedRoot = null;
            inspectedFormat = null;
            if (format != null)
            {
                FormatSummary.Text = format.Label + Environment.NewLine + format.Limits;
                return;
            }
            // creating a project doesn't go through OpenProject
            // scan off the UI thread here too
            BackgroundWork.On(Application.Current.Dispatcher)(() => format = ProjectFormat.Inspect(root, category), error =>
            {
                if (!WorkshopLibrary.SameFolder(root, Engine.ProjectRoot)) return;
                if (error != null) { Engine.Log("Project format unavailable: " + error); return; }
                FormatSummary.Text = format.Label + Environment.NewLine + format.Limits;
            });
        }

        internal static Button Button(string text, Action action)
        {
            var button = (Button)Activator.CreateInstance(Engine.Type("JKWorldsmith.Controls.Button"));
            button.Content = text;
            button.Margin = new Thickness(4);
            button.Padding = new Thickness(12, 5, 12, 5);
            button.Click += delegate
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Panel.Error(e);
                }
            };
            return button;
        }

        internal static T Control<T>(string name)
            where T : Control
        {
            var type = Assembly.Load("Wpf.Ui").GetType("Wpf.Ui.Controls." + name, false);
            return type == null ? (T)Activator.CreateInstance(typeof(T)) : (T)Activator.CreateInstance(type);
        }

        internal static void Theme(Window window)
        {
            var owner = Application.Current.MainWindow;
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // system font for the window chrome, editor text style for the body
            var body = new EditorText();
            body.BeginInit();
            body.EndInit();
            window.FontFamily = body.FontFamily;
            window.FontSize = body.FontSize;
            window.FontWeight = body.FontWeight;
            window.Foreground = owner.Foreground;
            var host = Engine.Get(owner, "RootContent") as Control;
            window.Background = (host == null ? null : host.Background) ?? owner.Background;
            var solid = window.Background as SolidColorBrush;
            if (window.Background == null || (solid != null && solid.Color.A == 0))
                window.Background = new SolidColorBrush(Color.FromRgb(29, 26, 31));
        }

        internal static void ReserveFooter(FrameworkElement host, FrameworkElement footer)
        {
            var parent = host.Parent as Grid;
            if (parent == null)
                throw new InvalidOperationException("Unsupported editor page layout.");
            var wrapper = new Grid{HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch};
            Grid.SetRow(wrapper, Grid.GetRow(host));
            Grid.SetColumn(wrapper, Grid.GetColumn(host));
            Grid.SetRowSpan(wrapper, Grid.GetRowSpan(host));
            Grid.SetColumnSpan(wrapper, Grid.GetColumnSpan(host));
            wrapper.RowDefinitions.Add(new RowDefinition{Height = new GridLength(1, GridUnitType.Star)});
            wrapper.RowDefinitions.Add(new RowDefinition{Height = GridLength.Auto});
            int index = parent.Children.IndexOf(host);
            parent.Children.RemoveAt(index);
            parent.Children.Insert(index, wrapper);
            Grid.SetRow(host, 0);
            Grid.SetColumn(host, 0);
            Grid.SetRowSpan(host, 1);
            Grid.SetColumnSpan(host, 1);
            Grid.SetRow(footer, 1);
            wrapper.Children.Add(host);
            wrapper.Children.Add(footer);
        }

        internal static string SelectFolder(string description)
        {
            var type = Assembly.Load("Ookii.Dialogs.Wpf").GetType("Ookii.Dialogs.Wpf.VistaFolderBrowserDialog", true);
            object dialog = Activator.CreateInstance(type);
            Engine.Set(dialog, "Description", description);
            Engine.Set(dialog, "UseDescriptionForTitle", true);
            object result = type.GetMethod("ShowDialog", Type.EmptyTypes).Invoke(dialog, null);
            return result is bool && (bool)result ? (string)Engine.Get(dialog, "SelectedPath") : null;
        }

        internal static void BrowseProject()
        {
            string root = SelectFolder("Open a Worldsmith project folder");
            if (root != null)
                OpenProject(root);
        }

        internal static void OpenProject(string root, bool sourceView = false, ulong workshopId = 0)
        {
            if (LoadState.Busy || Operations.Current.Busy)
                throw new InvalidOperationException("The current project is still loading. Wait for it to finish before opening another project.");
            root = Path.GetFullPath(root);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException(root);
            object category = NativeProjects.Category(root);
            var lease = Operations.Current.Enter("project inspection");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Panel.Status("Opening project: " + Path.GetFileName(root));
            ProjectFormat format = null;
            BackgroundWork.On(Application.Current.Dispatcher)(() => format = ProjectFormat.Inspect(root, category.ToString()), error =>
            {
                try
                {
                    Engine.Log("Project inspection: " + root + " in " + watch.ElapsedMilliseconds + " ms");
                    if (error != null) throw error;
                    OpenInspectedProject(root, category, format, sourceView, workshopId);
                }
                catch (Exception failure)
                {
                    inspectedRoot = null;
                    inspectedFormat = null;
                    Panel.Status("Could not open project: " + Path.GetFileName(root));
                    Panel.Error(failure);
                }
                finally { lease.Dispose(); }
            });
        }

        static void OpenInspectedProject(string root, object category, ProjectFormat format, bool sourceView, ulong workshopId)
        {
            object project = NativeProjects.Find(root);
            ulong linkedId = workshopId != 0 ? workshopId : WorkshopCatalog.IdFor(root, project == null ? 0 : Convert.ToUInt64(Engine.Get(project, "SteamPublishedId")));
            string name = WorkshopCatalog.NameFor(root, category.ToString(), linkedId, project == null ? null : (string)Engine.Get(project, "Name"));
            if (project == null)
                project = Activator.CreateInstance(Engine.Type("JKWorldsmith.Models.Projects.Project"), new[]{(object)name, root, category, DateTime.Now, linkedId});
            else
            {
                Engine.Set(project, "AutoCreateFolder", false);
                if (!String.Equals((string)Engine.Get(project, "Name"), name, StringComparison.Ordinal))
                    Engine.Set(project, "Name", name);
                Engine.Set(project, "SteamPublishedId", linkedId);
            }
            if (category.ToString() != "Mod" && format.Package && !sourceView && !format.Editable)
            {
                NativeProjects.Remember(project);
                new PackageWindow(root, category.ToString(), format, name).Show();
                return;
            }

            Panel.Status("Loading project: " + name);
            inspectedRoot = root;
            inspectedFormat = format;
            NativeProjects.Load(project);
        }

        internal static void GoHome()
        {
            object navigation = Engine.Get(Application.Current.MainWindow, "RootNavigation");
            var method = navigation.GetType().GetMethods().Where(m => m.Name == "Navigate").FirstOrDefault(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length > 0 && parameters[0].ParameterType == typeof(string) && parameters.Skip(1).All(p => p.IsOptional);
            });
            if (method == null)
            {
                Engine.Log("Choose the home page to leave the archived project.");
                return;
            }

            object[] arguments = method.GetParameters().Select((p, i) => i == 0 ? (object)"dashboard" : Type.Missing).ToArray();
            method.Invoke(navigation, arguments);
        }
    }
}
