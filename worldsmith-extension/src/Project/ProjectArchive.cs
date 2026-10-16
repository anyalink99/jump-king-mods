using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace WorldsmithExtension
{
    internal static class ProjectArchive
    {
        internal static string Move(string root)
        {
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            string parent = Path.GetDirectoryName(root);
            if (String.IsNullOrEmpty(parent) || !Directory.Exists(root))
                throw new IOException("Select an existing project folder.");
            string destination = Path.Combine(parent, ".worldsmith-archives", Path.GetFileName(root) + "-" + Guid.NewGuid().ToString("N"));
            FileSafety.CheckedPath(parent, root);
            FileSafety.CheckedPath(parent, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            Directory.Move(root, destination);
            return destination;
        }

        internal static bool Prompt()
        {
            if (LoadState.Busy || Operations.Current.Busy || (bool)Engine.Get(Engine.Project, "IsBusyPacking"))
            {
                Panel.Error(new InvalidOperationException("Wait for loading, compilation or publishing to finish before archiving the project."));
                return false;
            }

            string root = Engine.ProjectRoot;
            object project = Engine.Call(Engine.Project, "ToProject");
            var window = new Window{Title = "Archive local project", Width = 540, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize};
            EditorUI.Theme(window);
            var body = new StackPanel{Margin = new Thickness(20)};
            body.Children.Add(new EditorText{Text = "Move this project into a .worldsmith-archives folder beside it? All local files are retained. The Steam Workshop item stays available.", TextWrapping = TextWrapping.Wrap});
            body.Children.Add(new EditorText{Text = root, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12)});
            body.Children.Add(EditorUI.Button("Archive project", () =>
            {
                if (!String.Equals(root, Engine.ProjectRoot, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("The current project changed. Open the archive action again.");
                Engine.Call(Engine.Type("JKWorldsmith.Models.Projects.CurrentProjectManager"), "Unload");
                string destination;
                try
                {
                    destination = Move(root);
                }
                catch
                {
                    EditorUI.OpenProject(root);
                    throw;
                }

                foreach (string list in new[]{"RecentProjects", "FavoriteProjects"})
                {
                    object instance = Engine.Get(Engine.Type("JKWorldsmith.Models.Projects." + list), "Instance");
                    Engine.ExecuteCommand(Engine.Get(instance, "RemoveProjectCommand"), project);
                }

                Panel.Status("Project archived: " + destination);
                window.Close();
                EditorUI.GoHome();
            }));
            body.Children.Add(EditorUI.Button("Cancel", () => window.Close()));
            window.Content = body;
            window.ShowDialog();
            return false;
        }
    }
}
