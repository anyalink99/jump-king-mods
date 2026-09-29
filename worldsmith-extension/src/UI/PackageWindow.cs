using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WorldsmithExtension
{
    internal sealed class PackageWindow : Window
    {
        internal PackageWindow(string root, string category, ProjectFormat format, string name = null)
        {
            Title = (name ?? WorkshopCatalog.NameFor(root, category, WorkshopCatalog.IdFor(root))) + " - " + format.Label;
            Width = 820;
            Height = 620;
            MinWidth = 540;
            MinHeight = 400;
            EditorUI.Theme(this);
            var body = new DockPanel{Margin = new Thickness(20)};
            var heading = new StackPanel();
            heading.Children.Add(new EditorText{Text = (name ?? Path.GetFileName(root)) + " / " + format.Label, FontSize = 22, FontWeight = FontWeights.Bold});
            heading.Children.Add(new EditorText{Text = root, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8)});
            heading.Children.Add(new EditorText{Text = format.Limits, TextWrapping = TextWrapping.Wrap});
            var actions = new WrapPanel{Margin = new Thickness(0, 12, 0, 12)};

            var recover = EditorUI.Button("Create editable working copy...", () =>
            {
            });
            actions.Children.Add(recover);
            heading.Children.Add(actions);
            var status = EditorUI.Control<TextBox>("TextBox");
            status.IsReadOnly = true;
            status.TextWrapping = TextWrapping.Wrap;
            status.Height = 100;
            status.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            heading.Children.Add(status);
            actions.Children.Add(EditorUI.Button("Test in-game", () => GameTest.RunProject(root, category,
                message => Dispatcher.BeginInvoke(new Action(() => { status.Text = message; Panel.Status(message); })))));
            actions.Children.Add(EditorUI.Button("Build / Workshop", () => Panel.OpenPackage(root, category)));
            if (ProjectFormat.CanEdit(root, category))
                actions.Children.Add(EditorUI.Button("Open in editor", () => { EditorUI.OpenProject(root, true); Close(); }));
            DockPanel.SetDock(heading, Dock.Top);
            body.Children.Add(heading);
            body.Children.Add(new ListBox{Background = Background, Foreground = Foreground, ItemsSource = Files.Sources(root).Select(p => Files.Relative(root, p)).ToArray()});
            Content = body;
            bool busy = false;
            Closing += (sender, args) =>
            {
                if (busy)
                    args.Cancel = true;
            };
            recover.Click += delegate
            {
                if (Operations.Current.Busy || LoadState.Busy)
                {
                    status.Text = "Wait for the current operation before creating a working copy.";
                    return;
                }

                string parent = EditorUI.SelectFolder("Choose a parent folder for the separate working copy");
                if (parent == null)
                    return;
                string destination = Path.Combine(parent, Path.GetFileName(root) + "-editable-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                var operation = Operations.Current.Enter("asset recovery");
                busy = true;
                actions.IsEnabled = false;
                Task.Run(() =>
                {
                    try
                    {
                        string report = ProjectFormat.Recover(root, destination, message => Dispatcher.BeginInvoke(new Action(() => status.Text = message)));
                        Dispatcher.Invoke(() =>
                        {
                            status.Text = report + Environment.NewLine + "Working copy: " + destination;
                            actions.Children.Add(EditorUI.Button("Open working folder", () => System.Diagnostics.Process.Start("explorer.exe", WorkerProcess.Quote(destination))));
                            if (ProjectFormat.CanEdit(destination, category))
                                actions.Children.Add(EditorUI.Button("Open recovered project", () =>
                                {
                                    EditorUI.OpenProject(destination, true);
                                    Close();
                                }));
                            else
                                status.AppendText(Environment.NewLine + "Some required image sources remain unavailable. This copy can still be inspected and packaged.");
                        });
                    }
                    catch (Exception error)
                    {
                        Dispatcher.Invoke(() => status.Text = error.GetBaseException().Message);
                    }
                    finally
                    {
                        operation.Dispose();
                        Dispatcher.Invoke(() =>
                        {
                            busy = false;
                            actions.IsEnabled = true;
                        });
                    }
                });
            };
        }
    }
}
