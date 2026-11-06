using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Steamworks;

namespace WorldsmithExtension
{
    internal static class GameTest
    {
        internal static bool Start()
        {
            try
            {
                string root = Engine.ProjectRoot;
                if (String.IsNullOrEmpty(root))
                    throw new InvalidOperationException("Open a project first.");
                RunProject(root, Engine.Get(Engine.Project, "Type").ToString(), Panel.Status);
            }
            catch (Exception error)
            {
                Panel.Error(error);
            }
            return false;
        }

        internal static void RunProject(string root, string category, Action<string> status)
        {
            if (LoadState.Busy || Operations.Current.Busy)
                throw new InvalidOperationException("Wait for the current operation before testing.");
            if (Running())
                throw new InvalidOperationException("Close Jump King before preparing a new test build.");
            Files.RequirePhysicalPath(root);
            string steam = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null);
            if (String.IsNullOrEmpty(steam) || !File.Exists(steam))
                throw new FileNotFoundException("Steam installation was not found.");
            var operation = Operations.Current.Enter("the game test");
            Task.Run(() =>
            {
                try
                {
                    string content = TestBuild.Prepare(Engine.Data, root, category,
                        destination => BuildJob.Run(root, category, destination, status), Running, status);
                    if (Running())
                        throw new IOException("Jump King started during preparation. Close it and try again.");
                    if (category == "Mod")
                    {
                        string game;
                        SteamApps.GetAppInstallDir(new AppId_t(1061090), out game, 4096);
                        if (!Directory.Exists(game))
                            throw new DirectoryNotFoundException("Jump King installation was not found.");
                        string target = ModInstallation.SelectTarget(game, content);
                        string backup = Path.Combine(Engine.Data, "test-backups", Guid.NewGuid().ToString("N"));
                        ModInstallation.Install(content, target, backup, status);
                        status("Test mod installed: " + target + "; rollback files: " + backup);
                    }
                    var launch = TestBuild.Launch(steam, content, category);
                    Engine.UI(() =>
                    {
                        try
                        {
                            using (var process = Process.Start(launch)) { }
                            status("Launching Jump King: " + Path.GetFileName(root));
                        }
                        catch (Exception error) { Panel.Error(error); }
                        finally { operation.Dispose(); }
                    });
                }
                catch (Exception error)
                {
                    operation.Dispose();
                    status("Test failed: " + error.GetBaseException().Message);
                    Panel.Error(error);
                }
            });
        }

        static bool Running()
        {
            foreach (var process in Process.GetProcessesByName("JumpKing"))
            {
                process.Dispose();
                return true;
            }

            return false;
        }

    }
}
