using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WorldsmithExtension
{
    internal static class Launcher
    {
        internal const string Supported = "691B8DE520DF66EEA262EBC4AF7D5E03F8786B5AEB73A7EAF9B9FC1779FD042C";
        [STAThread]
        static int Main(string[] args)
        {
            InterfaceLanguage.Initialize();
            bool selectedUpdate = false;
            string previousSelection = null;
            string bundle = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            try
            {
                if (args.Any(x => x == "--build" || x == "--package-mod" || x == "--package-content" || x == "--recover"))
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
                bool activate = args.Length >= 3 && args[0] == "--wait-for-exit";
                if (activate)
                {
                    int id = Int32.Parse(args[1]);
                    long ticks = Int64.Parse(args[2]);
                    try
                    {
                        using (var previous = Process.GetProcessById(id))
                            if (previous.StartTime.ToUniversalTime().Ticks == ticks && !previous.WaitForExit(30000))
                                throw new IOException("The previous Worldsmith session is still running.");
                    }
                    catch (ArgumentException) { /* the previous process has already exited */ }
                    Updates.VerifyCandidate(bundle);
                }
                else if (Path.GetFileName(Assembly.GetExecutingAssembly().Location) == "WorldsmithExtension.exe" && !args.Any(x => new[]{"--ignore-updates", "--check", "--build", "--package-content", "--package-mod", "--recover"}.Contains(x)))
                {
                    try
                    {
                        string updated = Updates.Redirect(bundle);
                        if (updated != null)
                        {
                            Process.Start(new ProcessStartInfo(updated, String.Join(" ", args.Select(WorkerProcess.Quote))) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(updated) });
                            return 0;
                        }
                    }
                    catch (Exception error)
                    {
                        Console.Error.WriteLine("Update unavailable; starting this version: " + error.Message);
                    }
                }
                string host = FindHost(args, bundle);
                if (host == null)
                {
                    using (var picker = new OpenFileDialog{Title = "Select Worldsmith", Filter = "Worldsmith|JKWorldsmith.exe"})
                    {
                        if (picker.ShowDialog() != DialogResult.OK)
                            return 2;
                        host = Path.GetDirectoryName(picker.FileName);
                    }
                }

                string executable = Path.Combine(host, "JKWorldsmith.exe");
                if (Files.Hash(executable) != Supported)
                    throw new InvalidOperationException("This Worldsmith build is not supported. Select the executable specified in the installation guide.");
                try
                {
                    Files.Text(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldsmithExtension", "host.txt"), host);
                }
                catch (IOException)
                { /* discovery cache is optional */
                }

                // CefSharp's unmanaged callbacks enter the default AppDomain
                // keep the host and its assembly resolver in that same domain
                if (activate)
                {
                    string selection = Path.Combine(Updates.Root, "current.xml");
                    previousSelection = File.Exists(selection) ? File.ReadAllText(selection) : null;
                    Updates.Activate(bundle);
                    selectedUpdate = true;
                }
                var runner = new Runner();
                int result = runner.Run(host, bundle, args);
                if (selectedUpdate && result != 0) Updates.RestoreSelection(bundle, previousSelection);
                return result;
            }
            catch (Exception e)
            {
                if (selectedUpdate)
                {
                    try { Updates.RestoreSelection(bundle, previousSelection); }
                    catch (Exception rollback) { Console.Error.WriteLine("Could not restore update selection: " + rollback.Message); }
                }
                string text = e.ToString();
                Console.Error.WriteLine(text);
                if (!args.Any(x => x.StartsWith("--")) || args.Contains("--wait-for-exit"))
                    MessageBox.Show(text, "Worldsmith Extension", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        static string FindHost(string[] args, string bundle)
        {
            int i = Array.IndexOf(args, "--worldsmith");
            if (i >= 0 && i + 1 < args.Length)
                return Path.GetFullPath(args[i + 1]);
            string config = Path.Combine(bundle, "worldsmith-path.txt");
            if (File.Exists(config))
                return Path.GetFullPath(File.ReadAllText(config).Trim());
            string remembered = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldsmithExtension", "host.txt");
            if (File.Exists(remembered))
            {
                string path = File.ReadAllText(remembered).Trim();
                string executable = Path.Combine(path, "JKWorldsmith.exe");
                if (File.Exists(executable) && Files.Hash(executable) == Supported)
                    return path;
            }

            string steam = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
            string[] options = {bundle, steam == null ? "" : Path.Combine(steam, "steamapps", "common", "Jump King Workshop")};
            return options.FirstOrDefault(p => File.Exists(Path.Combine(p, "JKWorldsmith.exe")) && Files.Hash(Path.Combine(p, "JKWorldsmith.exe")) == Supported);
        }
    }

    public sealed class Runner : MarshalByRefObject
    {
        public override object InitializeLifetimeService()
        {
            return null;
        }

        public int Run(string host, string bundle, string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs e)
            {
                string name = new AssemblyName(e.Name).Name + ".dll";
                foreach (string folder in new[]{bundle, Path.Combine(bundle, "mapping"), host})
                {
                    string p = Path.Combine(folder, name);
                    if (File.Exists(p))
                        return Assembly.LoadFrom(p);
                }

                return null;
            };
            var engine = Assembly.LoadFrom(Path.Combine(bundle, "WorldsmithExtension.Engine.dll"));
            return (int)engine.GetType("WorldsmithExtension.Engine").GetMethod("Run").Invoke(null, new object[]{host, bundle, args});
        }
    }
}
