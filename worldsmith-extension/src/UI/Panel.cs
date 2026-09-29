using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class Panel
    {
        static TextBlock barStatus;
        static Window panel;
        internal static bool WorkBusy
        {
            get
            {
                return Operations.Current.Busy;
            }
        }

        internal static void Attach(Window main)
        {
            if (barStatus != null)
                return;
            var host = Engine.Get(main, "RootContent") as FrameworkElement;
            if (host == null)
                throw new InvalidOperationException("The editor page container is unavailable.");
            var bar = new DockPanel{Background = main.Background, LastChildFill = true};
            barStatus = new EditorText{Text = "Worldsmith Extension ready", Foreground = main.Foreground, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(10, 0, 8, 4)};
            bar.Children.Add(barStatus);
            EditorUI.ReserveFooter(host, bar);
            var frame = Engine.Get(main, "RootFrame") as Frame;
            if (frame != null)
            {
                Stopwatch navigation = null;
                frame.Navigating += (sender, args) =>
                {
                    navigation = Stopwatch.StartNew();
                };
                frame.LoadCompleted += (sender, args) =>
                {
                    var measured = navigation;
                    if (measured == null)
                        return;
                    main.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                    {
                        Engine.Log("Navigation ready: " + (args.Content == null ? "unknown" : args.Content.GetType().Name) + " in " + measured.ElapsedMilliseconds + " ms");
                    }));
                };
            }

            Engine.Log("Extension toolbar placed below the native page container");
            main.AllowDrop = true;
            main.Drop += delegate (object sender, DragEventArgs e)
            {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null && paths.Length == 1 && Directory.Exists(paths[0]))
                {
                    e.Handled = true;
                    try
                    {
                        EditorUI.OpenProject(paths[0]);
                    }
                    catch (Exception error)
                    {
                        Error(error);
                    }
                }
            };
            main.Closing += (sender, args) =>
            {
                if (WorkBusy || LoadState.Busy)
                {
                    args.Cancel = true;
                    Status("Wait for the current build, test or Steam operation before closing Worldsmith.");
                }
            };
        }

        internal static void Status(string text)
        {
            Engine.Log(text);
            Engine.UI(() =>
            {
                if (barStatus != null)
                    barStatus.Text = text;
            });
        }

        internal static void Error(Exception e)
        {
            Engine.Log(e.ToString());
            Engine.UI(() => Engine.Type("JKWorldsmith.Models.DialogManager").GetMethod("ShowError", new[]{typeof(string), typeof(string), typeof(string)}).Invoke(null, new object[]{ErrorDetails.Message(e), "Worldsmith Extension", e.ToString()}));
        }

        internal static void OpenPackage(string root, string category)
        {
            if (panel != null)
            {
                if (String.Equals(((PublishWindow)panel).ProjectRoot, root, StringComparison.OrdinalIgnoreCase) && ((PublishWindow)panel).ItemId == WorkshopCatalog.IdFor(root))
                {
                    panel.Activate();
                    return;
                }

                if (WorkBusy)
                    throw new InvalidOperationException("Finish the current Workshop operation before opening another project panel.");
                panel.Close();
            }

            panel = new PublishWindow(root, category);
            panel.Closed += delegate
            {
                panel = null;
            };
            panel.Show();
        }

        internal static void Open()
        {
            if (panel != null)
            {
                if (String.Equals(((PublishWindow)panel).ProjectRoot, Engine.ProjectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    panel.Activate();
                    return;
                }

                if (WorkBusy)
                    throw new InvalidOperationException("Finish the current Workshop operation before opening another project panel.");
                panel.Close();
            }

            if (String.IsNullOrEmpty(Engine.ProjectRoot) || !Directory.Exists(Engine.ProjectRoot))
            {
                EditorUI.BrowseProject();
                return;
            }

            try
            {
                panel = new PublishWindow();
                panel.Closed += delegate
                {
                    panel = null;
                };
                panel.Show();
            }
            catch (Exception e)
            {
                Error(e);
            }
        }
    }
}
