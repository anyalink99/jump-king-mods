using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WorldsmithExtension
{
    internal static class UpdateUI
    {
        internal static bool Busy;
        static bool started;
        static UpdateRelease available;
        static Button homeButton;
        static Window window;

        internal static void Attach(FrameworkElement anchor)
        {
            var row = anchor.Parent as Grid;
            var parent = row == null ? null : row.Parent as StackPanel;
            if (parent == null) throw new InvalidOperationException("Unsupported home page update layout.");
            var updates = new WrapPanel { Margin = new Thickness(0, 16, 0, 4) };
            updates.Children.Add(new EditorText { Text = "Worldsmith Extension " + Updates.Current, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) });
            homeButton = EditorUI.Button("Extension updates", Show);
            updates.Children.Add(homeButton);
            parent.Children.Insert(parent.Children.IndexOf(row) + 1, updates);
            if (started) return;
            started = true;
            try
            {
                if (Updates.Automatic)
                    Task.Run(() =>
                    {
                        try
                        {
                            var release = Updates.Check();
                            Engine.UI(() => { available = release; Refresh(); });
                        }
                        catch (Exception error) { Engine.Log("Update check: " + error.GetBaseException().Message); }
                    });
            }
            catch (Exception error) { Engine.Log("Update settings: " + error.GetBaseException().Message); }
        }

        static void Refresh()
        {
            if (homeButton != null)
                homeButton.Content = available == null ? "Extension updates" : "Update to " + available.Version;
        }

        internal static void Show()
        {
            if (window != null) { window.Activate(); return; }
            var dialog = new Window { Title = "Worldsmith Extension updates", Width = 580, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
            EditorUI.Theme(dialog);
            var body = new StackPanel { Margin = new Thickness(20) };
            body.Children.Add(new EditorText { Text = "Worldsmith Extension " + Updates.Current, FontSize = 22, FontWeight = FontWeights.Bold });
            var automatic = new CheckBox { Content = "Check for updates when Worldsmith starts", IsChecked = Updates.Automatic, Margin = new Thickness(4, 16, 4, 12) };
            automatic.Click += delegate { try { Updates.Automatic = automatic.IsChecked == true; } catch (Exception error) { Panel.Error(error); } };
            body.Children.Add(automatic);
            var status = new EditorText { Text = available == null ? "Check GitHub for a newer version." : "Version " + available.Version + " is available.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 8, 4, 12) };
            body.Children.Add(status);
            body.Children.Add(new EditorText { Text = "Download an update, then restart when you are ready. The previous version is kept.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) });
            var actions = new WrapPanel();
            Button download = null;
            string candidate = null;
            var check = EditorUI.Button("Check for updates", () =>
            {
                if (Busy) return;
                Busy = true;
                actions.IsEnabled = false;
                status.Text = "Checking GitHub...";
                Task.Run(() =>
                {
                    try
                    {
                        var release = Updates.Check();
                        Engine.UI(() =>
                        {
                            available = release;
                            Refresh();
                            status.Text = release == null ? "This is the latest version on this release channel." : "Version " + release.Version + " is available.";
                            download.IsEnabled = release != null;
                        });
                    }
                    catch (Exception error) { Engine.UI(() => status.Text = "Could not check for updates: " + error.GetBaseException().Message); }
                    finally { Engine.UI(() => { Busy = false; actions.IsEnabled = true; }); }
                });
            });
            download = EditorUI.Button("Download update", () =>
            {
                if (Operations.Current.Busy || LoadState.Busy) throw new InvalidOperationException("Finish the current operation before updating.");
                if (candidate != null)
                {
                    Updates.VerifyCandidate(candidate);
                    var main = Application.Current.MainWindow;
                    var process = Process.GetCurrentProcess();
                    string arguments = "--wait-for-exit " + process.Id + " " + process.StartTime.ToUniversalTime().Ticks + " --worldsmith " + WorkerProcess.Quote(Engine.Host);
                    EventHandler restart = null;
                    restart = (sender, args) =>
                    {
                        main.Closed -= restart;
                        try
                        {
                            Process.Start(new ProcessStartInfo(Path.Combine(candidate, "WorldsmithExtension.exe"), arguments) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = candidate });
                        }
                        catch (Exception error)
                        {
                            Engine.Log("Update restart failed: " + error);
                            MessageBox.Show("Could not restart Worldsmith: " + error.Message + "\nStart your previous launcher to continue.", "Worldsmith Extension");
                        }
                    };
                    main.Closed += restart;
                    main.Close();
                    // A native unsaved-work prompt may cancel closing
                    main.Closed -= restart;
                    return;
                }
                if (available == null) return;
                var release = available;
                var operation = Operations.Current.Enter("the extension download");
                Busy = true;
                actions.IsEnabled = false;
                status.Text = "Downloading " + release.Version + "...";
                Task.Run(() =>
                {
                    try
                    {
                        string prepared = Updates.Download(release, Engine.Bundle);
                        Engine.UI(() => { candidate = prepared; check.IsEnabled = false; download.Content = "Restart in updated version"; status.Text = "Version " + release.Version + " is ready. Finish editing before restarting."; });
                    }
                    catch (Exception error) { Engine.UI(() => status.Text = "Update failed: " + error.GetBaseException().Message); }
                    finally { operation.Dispose(); Engine.UI(() => { Busy = false; actions.IsEnabled = true; }); }
                });
            });
            download.IsEnabled = available != null;
            actions.Children.Add(check);
            actions.Children.Add(download);
            body.Children.Add(actions);
            dialog.Content = body;
            window = dialog;
            dialog.Closing += (sender, args) => { if (Busy) args.Cancel = true; };
            dialog.Closed += delegate { window = null; };
            dialog.Show();
        }
    }
}
