using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace WorldsmithExtension
{
    internal static class TestBuild
    {
        internal static string ProjectDirectory(string data, string root, string category)
        {
            string key = Path.GetFullPath(root).TrimEnd('\\', '/').ToUpperInvariant() + "\n" + category;
            using (var hash = SHA256.Create())
                return Path.Combine(data, "tests", BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").Substring(0, 32).ToLowerInvariant());
        }

        internal static string Prepare(string data, string root, string category, Action<string> build, Func<bool> running, Action<string> status)
        {
            string project = ProjectDirectory(data, root, category);
            Files.RequirePhysicalPath(project);
            Directory.CreateDirectory(project);
            // don't let two editors replace this test folder at once
            using (var lease = new FileStream(Path.Combine(project, "prepare.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                string current = Path.Combine(project, "current"), content = Path.Combine(current, "content");
                string engineHash = Files.Hash(typeof(TestBuild).Assembly.Location);
                if (running()) throw new IOException("Close Jump King before preparing a test build.");
                // a mod's references can live outside its folder
                // hashing this folder won't catch changes to those DLLs
                if (category != "Mod" && Directory.Exists(content))
                {
                    try
                    {
                        if (File.ReadAllText(Path.Combine(current, "test-engine.sha256")) != engineHash)
                            throw new InvalidDataException("The test build was prepared by another extension build.");
                        BuildReceipt.Validate(root, content);
                        status("Using the unchanged test build.");
                        return content;
                    }
                    catch (IOException) { }
                    catch (InvalidDataException) { }
                    catch (XmlException) { }
                }
                string stage = Path.Combine(data, "tests", "build-" + Guid.NewGuid().ToString("N"));
                string stagedContent = Path.Combine(stage, "content");
                build(stagedContent);
                BuildReceipt.Validate(root, stagedContent);
                File.WriteAllText(Path.Combine(stage, "test-engine.sha256"), engineHash);
                if (running()) throw new IOException("Jump King started during preparation. Close it and try again.");
                string previous = Path.Combine(data, "tests", "previous-" + Guid.NewGuid().ToString("N"));
                Files.RequirePhysicalPath(current);
                Files.RequirePhysicalPath(stage);
                bool hadPrevious = Directory.Exists(current);
                if (hadPrevious) Directory.Move(current, previous);
                try { Directory.Move(stage, current); }
                catch
                {
                    if (hadPrevious) Directory.Move(previous, current);
                    throw;
                }
                return content;
            }
        }

        internal static ProcessStartInfo Launch(string steam, string content, string category)
        {
            if (!File.Exists(steam)) throw new FileNotFoundException("Steam installation was not found.", steam);
            var info = new ProcessStartInfo(steam, "-applaunch 1061090" + (category == "Mod" ? "" : " -debug " + WorkerProcess.Quote(content)))
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(steam) };
            // don't pass Worldsmith's AppID to the game
            info.EnvironmentVariables.Remove("SteamAppId");
            info.EnvironmentVariables.Remove("SteamGameId");
            return info;
        }
    }
}
