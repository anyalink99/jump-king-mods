using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WorldsmithExtension
{
    internal static class EditorPatches
    {
        static bool OpenRecentProject(object possible_project)
        {
            Engine.Type("JKWorldsmith.Models.DialogManager").GetMethod("Hide", Type.EmptyTypes).Invoke(null, null);
            try
            {
                EditorUI.OpenProject((string)Engine.Get(possible_project, "Directory"));
            }
            catch (Exception error)
            {
                Panel.Error(error);
            }

            return false;
        }

        static bool ProjectAction(object __instance, object sender)
        {
            if (!Object.ReferenceEquals(sender, Engine.Get(__instance, "DeletePermaButton")))
                return true;
            return ProjectArchive.Prompt();
        }

        static bool BrowseFromHome()
        {
            try
            {
                EditorUI.BrowseProject();
            }
            catch (Exception e)
            {
                Panel.Error(e);
            }

            return false;
        }

        static bool IsModProject(string folder, ref bool __result)
        {
            if (ModPackage.IsSourceProject(folder))
            {
                __result = true;
                return false;
            }

            try
            {
                ModPackage.Validate(folder);
                __result = true;
            }
            catch (Exception)
            {
                __result = false;
            }

            return false;
        }

        static void DashboardReady(object __instance)
        {
            var button = Engine.Get(__instance, "LoadProjectButton") as System.Windows.Controls.Button;
            if (button != null)
            {
                button.Content = "Open project...";
                button.ToolTip = "Open a map, skin or mod folder";
                UpdateUI.Attach(button);
            }
        }

        static void DetailsReady(object __instance)
        {
            var archive = Engine.Get(__instance, "DeletePermaButton") as System.Windows.Controls.Button;
            if (archive != null)
                archive.Content = "Archive local project";
            var sidebar = Engine.Get(__instance, "Sidebar") as System.Windows.Controls.Border;
            if (sidebar == null)
                throw new InvalidOperationException("Project actions container is missing.");
            var body = sidebar.Child;
            sidebar.Child = null;
            var actions = new System.Windows.Controls.DockPanel();
            var button = EditorUI.Button("Build / Workshop", Panel.Open);
            System.Windows.Controls.DockPanel.SetDock(button, System.Windows.Controls.Dock.Top);
            var summary = new System.Windows.Controls.StackPanel();
            EditorUI.FormatSummary = new EditorText{TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 8, 4, 8)};
            summary.Children.Add(EditorUI.FormatSummary);
            summary.Children.Add(button);
            System.Windows.Controls.DockPanel.SetDock(summary, System.Windows.Controls.Dock.Top);
            actions.Children.Add(summary);
            if (body != null)
                actions.Children.Add(body);
            sidebar.Child = actions;
        }

        static void PageStarting(string resourceName, out System.Diagnostics.Stopwatch __state)
        {
            __state = System.Diagnostics.Stopwatch.StartNew();
            Engine.Log("Loading page " + resourceName);
        }

        static void PageReady(string resourceName, System.Diagnostics.Stopwatch __state)
        {
            Engine.Log("Loaded page " + resourceName + " in " + __state.ElapsedMilliseconds + " ms");
        }

        static bool NativeArguments()
        {
            var args = Environment.GetCommandLineArgs();
            return args.Length > 1 && new[]{"--open", "-o", "--workshop", "-ws"}.Contains(args[1]);
        }

        static void LoadStarting(MethodBase __originalMethod, out System.Diagnostics.Stopwatch __state)
        {
            __state = System.Diagnostics.Stopwatch.StartNew();
            Engine.Log("Project load: " + __originalMethod.DeclaringType.Name);
        }

        static Exception LoadFinished(MethodBase __originalMethod, System.Diagnostics.Stopwatch __state, Exception __exception)
        {
            Engine.Log("Project load: " + __originalMethod.DeclaringType.Name + " finished in " + __state.ElapsedMilliseconds + " ms" + (__exception == null ? "" : "; failed: " + __exception));
            return __exception;
        }

        static readonly Dictionary<string, object> Icons = new Dictionary<string, object>();
        static bool EditorIcon(object parameter, ref object __result)
        {
            string name = Convert.ToString(parameter);
            lock (Icons)
            {
                if (Icons.TryGetValue(name, out __result))
                    return false;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var paths = new List<string>();
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (name == "code")
                {
                    paths.Add(Path.Combine(local, "Programs", "Microsoft VS Code", "Code.exe"));
                    paths.Add(Path.Combine(programs, "Microsoft VS Code", "Code.exe"));
                }

                if (name == "devenv")
                    foreach (string year in new[]{"2022", "2019", "2017"})
                        foreach (string edition in new[]{"Community", "Professional", "Enterprise"})
                            paths.Add(Path.Combine(programs, "Microsoft Visual Studio", year, edition, "Common7", "IDE", "devenv.exe"));
                if (name == "code" || name == "devenv")
                {
                    string registered = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + name + ".exe", null, null) as string;
                    if (!String.IsNullOrEmpty(registered) && !registered.StartsWith(@"\\"))
                        paths.Add(registered);
                }

                __result = null;
                foreach (string path in paths)
                    if (File.Exists(path))
                        try
                        {
                            using (var icon = Icon.ExtractAssociatedIcon(path))
                            {
                                if (icon == null)
                                    continue;
                                var image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, new Int32Rect(0, 0, icon.Width, icon.Height), BitmapSizeOptions.FromEmptyOptions());
                                image.Freeze();
                                __result = image;
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            Engine.Log("Editor icon unavailable: " + e.Message);
                        }

                Icons[name] = __result;
                Engine.Log("Editor icon " + name + " resolved in " + watch.ElapsedMilliseconds + " ms");
                return false;
            }
        }

        static void RefreshNews(object __instance)
        {
            NewsPanel.Load(__instance);
        }

        static bool LoadNews(object __instance)
        {
            NewsPanel.Load(__instance);
            return false;
        }

        static void WindowStarting()
        {
            Engine.Log("Constructing main window");
        }

        static Exception WindowFailure(Exception __exception)
        {
            if (__exception != null)
                Engine.Log("Main window resource failure: " + __exception);
            return __exception;
        }

        static void WindowCreated(object __instance)
        {
            Engine.Log("Main window constructed");
            ((Window)__instance).Loaded += delegate
            {
                Engine.Log("Main window loaded");
                Panel.Attach((Window)__instance);
            };
        }

    }
}
