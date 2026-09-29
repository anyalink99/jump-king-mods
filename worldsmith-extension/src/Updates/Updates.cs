using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal sealed class UpdateRelease
    {
        public string Version, Url, Digest;
        public long Size;
    }

    internal static class Updates
    {
        internal const string Repository = "https://github.com/anyalink99/jump-king-mods";
        internal static string Current { get { return ((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(typeof(Updates).Assembly, typeof(AssemblyInformationalVersionAttribute))).InformationalVersion; } }
        internal static string Root { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldsmithExtension", "updates"); } }
        internal static readonly string[] Required = {"WorldsmithExtension.exe", "WorldsmithExtension.Worker.exe", "WorldsmithExtension.Engine.dll", "WorldsmithExtension.exe.config", "WorldsmithExtension.Worker.exe.config", "0Harmony.dll", "mapping/SceneCacheCompiler.exe", "mapping/MegaMappingApi.dll", "mapping/JKRuntime.dll", "README.md", "CHANGELOG.md", "THIRD_PARTY_NOTICES.md", "mapping/THIRD_PARTY_NOTICES.md", "docs/index.md", "docs/development.md"};
        const long MaxArchive = 64 * 1024 * 1024, MaxExpanded = 256 * 1024 * 1024;

        internal static bool ValidVersion(string value)
        {
            return value != null && Regex.IsMatch(value, @"^\d{1,5}\.\d{1,5}\.\d{1,5}(?:-preview\.[1-9]\d{0,5})?$");
        }

        internal static int Compare(string left, string right)
        {
            if (!ValidVersion(left) || !ValidVersion(right)) throw new InvalidDataException("Unsupported release version.");
            var a = left.Split('-'); var b = right.Split('-');
            int result = Version.Parse(a[0]).CompareTo(Version.Parse(b[0]));
            if (result != 0) return result;
            if (a.Length != b.Length) return a.Length == 1 ? 1 : -1;
            return a.Length == 1 ? 0 : Int32.Parse(a[1].Substring(8)).CompareTo(Int32.Parse(b[1].Substring(8)));
        }

        internal static bool Automatic
        {
            get
            {
                string path = Path.Combine(Root, "settings.xml");
                return !File.Exists(path) || (bool?)Files.Xml(path).Root.Attribute("automatic") != false;
            }
            set { Files.Text(Path.Combine(Root, "settings.xml"), new XElement("Updates", new XAttribute("automatic", value)).ToString()); }
        }

        internal static UpdateRelease Select(string json, string current)
        {
            var releases = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.DeserializeObject(json) as object[];
            if (releases == null) throw new InvalidDataException("Invalid release response.");
            UpdateRelease best = null;
            foreach (var item in releases.OfType<Dictionary<string, object>>())
            {
                object value;
                if (item.TryGetValue("draft", out value) && Object.Equals(value, true)) continue;
                string tag = item.TryGetValue("tag_name", out value) ? value as string : null;
                const string prefix = "worldsmith-extension-v";
                if (tag == null || !tag.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string version = tag.Substring(prefix.Length);
                if (!ValidVersion(version) || Compare(version, current) <= 0 || (best != null && Compare(version, best.Version) <= 0)) continue;
                if (!current.Contains("-") && version.Contains("-")) continue;
                var assets = item.TryGetValue("assets", out value) ? value as object[] : null;
                if (assets == null) continue;
                string name = "WorldsmithExtension-" + version + "-win-x64.zip";
                foreach (var asset in assets.OfType<Dictionary<string, object>>())
                {
                    if (!asset.TryGetValue("name", out value) || !Object.Equals(value, name)) continue;
                    string url = asset.TryGetValue("browser_download_url", out value) ? value as string : null;
                    string digest = asset.TryGetValue("digest", out value) ? value as string : null;
                    long size;
                    if (url != Repository + "/releases/download/" + tag + "/" + name || digest == null || !Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$") || !asset.TryGetValue("size", out value) || !Int64.TryParse(Convert.ToString(value), out size) || size <= 0 || size > MaxArchive) continue;
                    best = new UpdateRelease { Version = version, Url = url, Digest = digest.Substring(7), Size = size };
                }
            }
            return best;
        }

        static void Fetch(string url, Stream destination, long limit)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "WorldsmithExtension/" + Current;
            request.Accept = "application/vnd.github+json";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var input = response.GetResponseStream())
            {
                if (response.ResponseUri.Scheme != "https" || response.ContentLength > limit) throw new IOException("Invalid update download response.");
                var watch = Stopwatch.StartNew();
                byte[] buffer = new byte[65536];
                long total = 0; int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += count;
                    if (total > limit || watch.Elapsed > TimeSpan.FromMinutes(2)) throw new IOException("Update download exceeded its size or time limit.");
                    destination.Write(buffer, 0, count);
                }
            }
        }

        internal static UpdateRelease Check()
        {
            using (var response = new MemoryStream())
            {
                Fetch("https://api.github.com/repos/anyalink99/jump-king-mods/releases?per_page=100", response, 4 * 1024 * 1024);
                return Select(Encoding.UTF8.GetString(response.ToArray()), Current);
            }
        }

        internal static string Download(UpdateRelease release, string bundle)
        {
            string stage = Path.Combine(Root, "packages", release.Version + "-" + Guid.NewGuid().ToString("N"));
            Files.RequirePhysicalPath(stage);
            Directory.CreateDirectory(stage);
            string zip = Path.Combine(stage, "release.zip");
            using (var output = new FileStream(zip, FileMode.CreateNew)) Fetch(release.Url, output, MaxArchive);
            if (new FileInfo(zip).Length != release.Size) throw new InvalidDataException("Incomplete update download.");
            string candidate = Path.Combine(stage, "app");
            Extract(zip, candidate, release.Digest, release.Version);
            // keep explicit installation overrides, projects and user settings stay in place
            foreach (string name in new[]{"worldsmith-path.txt", "game-path.txt"})
                if (File.Exists(Path.Combine(bundle, name))) File.Copy(Path.Combine(bundle, name), Path.Combine(candidate, name));
            return candidate;
        }

        static bool Allowed(string name)
        {
            return Required.Contains(name, StringComparer.Ordinal) || (name.StartsWith("docs/", StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal));
        }

        internal static void Extract(string zip, string output, string digest, string version)
        {
            if (!ValidVersion(version) || !String.Equals(Files.Hash(zip), digest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum does not match the release.");
            Files.RequirePhysicalPath(output);
            if (Directory.Exists(output)) throw new IOException("Update destination already exists.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var archive = ZipFile.OpenRead(zip))
            {
                long total = 0;
                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    string[] parts = name.Split('/');
                    bool directory = name.EndsWith("/", StringComparison.Ordinal);
                    if (name.StartsWith("/", StringComparison.Ordinal) || name.Contains(":") || parts.Any(p => p == ".." || p == "." || p.EndsWith(" ") || p.EndsWith(".")) || !names.Add(name)) throw new InvalidDataException("Unsafe or duplicate update archive path.");
                    if (directory)
                    {
                        if (name != "mapping/" && name != "docs/") throw new InvalidDataException("Unexpected update folder.");
                        continue;
                    }
                    total = checked(total + entry.Length);
                    if (!Allowed(name) || entry.Length <= 0 || total > MaxExpanded) throw new InvalidDataException("Unexpected update package content.");
                }
                if (Required.Any(p => !names.Contains(p))) throw new InvalidDataException("The update package is incomplete.");
                Directory.CreateDirectory(output);
                foreach (var entry in archive.Entries.Where(e => !e.FullName.EndsWith("/")))
                {
                    string destination = Path.Combine(output, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    Files.Relative(output, destination);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (var input = entry.Open())
                    using (var target = new FileStream(destination, FileMode.CreateNew))
                    {
                        byte[] buffer = new byte[65536];
                        long written = 0; int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            written += count;
                            if (written > entry.Length) throw new InvalidDataException("Update entry exceeds its declared size.");
                            target.Write(buffer, 0, count);
                        }
                        if (written != entry.Length) throw new InvalidDataException("Incomplete update entry.");
                    }
                }
            }
            if (FileVersionInfo.GetVersionInfo(Path.Combine(output, "WorldsmithExtension.exe")).ProductVersion != version) throw new InvalidDataException("Downloaded executable version does not match its release.");
            var manifest = new XElement("Update", new XAttribute("version", version));
            foreach (string path in Directory.GetFiles(output, "*", SearchOption.AllDirectories))
                manifest.Add(new XElement("File", new XAttribute("name", Files.Relative(output, path)), new XAttribute("hash", Files.Hash(path))));
            Files.Text(Path.Combine(output, "verified.xml"), manifest.ToString());
        }

        internal static string VerifyCandidate(string candidate, string updateRoot = null)
        {
            Files.RequirePhysicalPath(candidate);
            Files.Relative(Path.Combine(updateRoot ?? Root, "packages"), candidate);
            var manifest = Files.Xml(Path.Combine(candidate, "verified.xml")).Root;
            string version = (string)manifest.Attribute("version");
            if (!ValidVersion(version)) throw new InvalidDataException("Invalid prepared update version.");
            var verified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.Elements("File"))
            {
                string name = (string)entry.Attribute("name");
                string path = Path.GetFullPath(Path.Combine(candidate, name));
                Files.Relative(candidate, path);
                Files.RequirePhysicalPath(path);
                if (!Allowed(name.Replace('\\', '/')) || !verified.Add(name.Replace('\\', '/')) || Files.Hash(path) != (string)entry.Attribute("hash")) throw new InvalidDataException("The prepared update has changed. Download it again.");
            }
            if (Required.Any(p => !verified.Contains(p))) throw new InvalidDataException("Incomplete prepared update.");
            // reject linked or additional loadable files before starting the prepared app
            Files.ContentFingerprint(candidate);
            foreach (string path in Directory.GetFiles(candidate, "*", SearchOption.AllDirectories))
            {
                string name = Files.Relative(candidate, path).Replace('\\', '/');
                if (!verified.Contains(name) && !new[]{"verified.xml", "worldsmith-path.txt", "game-path.txt"}.Contains(name))
                    throw new InvalidDataException("Unexpected file in the prepared update: " + name);
            }
            if (FileVersionInfo.GetVersionInfo(Path.Combine(candidate, "WorldsmithExtension.exe")).ProductVersion != version)
                throw new InvalidDataException("Prepared executable version mismatch.");
            return version;
        }

        internal static void Activate(string candidate, string updateRoot = null)
        {
            string version = VerifyCandidate(candidate, updateRoot);
            Files.Text(Path.Combine(updateRoot ?? Root, "current.xml"), new XElement("Update", new XAttribute("version", version), new XAttribute("path", candidate)).ToString());
        }

        internal static void RestoreSelection(string candidate, string previous, string updateRoot = null)
        {
            string selection = Path.Combine(updateRoot ?? Root, "current.xml");
            Files.RequirePhysicalPath(selection);
            if (!File.Exists(selection) || !String.Equals((string)Files.Xml(selection).Root.Attribute("path"), candidate, StringComparison.OrdinalIgnoreCase)) return;
            if (previous == null) File.Delete(selection);
            else Files.Text(selection, previous);
        }

        internal static string Redirect(string bundle, string updateRoot = null, string current = null)
        {
            string path = Path.Combine(updateRoot ?? Root, "current.xml");
            if (!File.Exists(path)) return null;
            var pointer = Files.Xml(path).Root;
            string candidate = (string)pointer.Attribute("path");
            string version = (string)pointer.Attribute("version");
            if (!ValidVersion(version) || Compare(version, current ?? Current) <= 0 || String.Equals(Path.GetFullPath(candidate), Path.GetFullPath(bundle), StringComparison.OrdinalIgnoreCase)) return null;
            if (VerifyCandidate(candidate, updateRoot) != version) throw new InvalidDataException("Update version mismatch.");
            return Path.Combine(candidate, "WorldsmithExtension.exe");
        }
    }
}
