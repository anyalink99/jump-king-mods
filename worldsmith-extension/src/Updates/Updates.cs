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
        internal static readonly string[] Required = {"WorldsmithExtension.exe", "WorldsmithExtension.Worker.exe", "WorldsmithExtension.Engine.dll", "WorldsmithExtension.LegacyCollision.dll", "WorldsmithExtension.exe.config", "WorldsmithExtension.Worker.exe.config", "0Harmony.dll", "mapping/SceneCacheCompiler.exe", "mapping/MegaMappingApi.dll", "mapping/JKRuntime.dll", "assets/platforms/default.png", "README.md", "CHANGELOG.md", "THIRD_PARTY_NOTICES.md", "mapping/THIRD_PARTY_NOTICES.md", "docs/index.md", "docs/development.md"};
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

        internal static string Download(UpdateRelease release, string bundle, string updateRoot = null, Action<string, Stream, long> fetch = null)
        {
            if (release == null || !ValidVersion(release.Version) || release.Size <= 0 || release.Size > MaxArchive || release.Digest == null || !Regex.IsMatch(release.Digest, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Invalid update download.");
            string stage = Path.Combine(updateRoot ?? Root, "packages", release.Version + "-" + Guid.NewGuid().ToString("N"));
            Files.RequirePhysicalPath(stage);
            Directory.CreateDirectory(stage);
            try {
            string zip = Path.Combine(stage, "release.zip");
            using (var output = new FileStream(zip, FileMode.CreateNew)) (fetch ?? Fetch)(release.Url, output, release.Size);
            if (new FileInfo(zip).Length != release.Size) throw new InvalidDataException("Incomplete update download.");
            string candidate = Path.Combine(stage, "app");
            Extract(zip, candidate, release.Digest, release.Version);
            // keep explicit installation overrides, projects and user settings stay in place
            foreach (string name in new[]{"worldsmith-path.txt", "game-path.txt"})
                if (File.Exists(Path.Combine(bundle, name))) File.Copy(Path.Combine(bundle, name), Path.Combine(candidate, name));
            File.Delete(zip);
            return candidate;
            } catch { Files.RequirePhysicalPath(stage); Directory.Delete(stage, true); throw; }
        }

        internal static void Extract(string zip, string output, string digest, string version)
        {
            if (!ValidVersion(version) || !String.Equals(Files.Hash(zip), digest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum does not match the release.");
            Files.RequirePhysicalPath(output);
            if (Directory.Exists(output)) throw new IOException("Update destination already exists.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var archive = ZipFile.OpenRead(zip))
            {
                if (archive.Entries.Count > 8192) throw new InvalidDataException("Too many update archive entries.");
                var packageEntry = archive.GetEntry(PackageManifest.Name);
                if (packageEntry == null || packageEntry.Length > 1024 * 1024) throw new InvalidDataException("Missing or oversized package manifest.");
                Dictionary<string, PackageFile> package;
                using (var input = packageEntry.Open()) package = PackageManifest.Read(input, version);
                long total = 0;
                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName;
                    bool directory = name.EndsWith("/", StringComparison.Ordinal);
                    PackageManifest.CheckName(directory ? name.TrimEnd('/') : name);
                    if (!names.Add(name) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000 || (entry.ExternalAttributes & 0x400) != 0) throw new InvalidDataException("Duplicate or linked update archive entry.");
                    if (directory)
                    {
                        if (!package.Keys.Any(p => p.StartsWith(name, StringComparison.Ordinal))) throw new InvalidDataException("Unexpected update folder.");
                        continue;
                    }
                    total = checked(total + entry.Length);
                    PackageFile expected;
                    if (total > MaxExpanded || (name != PackageManifest.Name && (!package.TryGetValue(name, out expected) || expected.Size != entry.Length))) throw new InvalidDataException("Unexpected or incomplete update package content.");
                }
                if (package.Keys.Any(p => !names.Contains(p))) throw new InvalidDataException("The update package is incomplete.");
                Directory.CreateDirectory(output);
                try {
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
                    if (entry.FullName != PackageManifest.Name && !String.Equals(Files.Hash(destination), package[entry.FullName].Hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package file checksum mismatch: " + entry.FullName);
                }
                if (FileVersionInfo.GetVersionInfo(Path.Combine(output, "WorldsmithExtension.exe")).ProductVersion != version) throw new InvalidDataException("Downloaded executable version does not match its release.");
                } catch { Files.RequirePhysicalPath(output); Directory.Delete(output, true); throw; }
            }
            var manifest = new XElement("Update", new XAttribute("version", version));
            foreach (string path in Directory.GetFiles(output, "*", SearchOption.AllDirectories))
                manifest.Add(new XElement("File", new XAttribute("name", Files.Relative(output, path).Replace('\\', '/')), new XAttribute("hash", Files.Hash(path))));
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
                PackageManifest.CheckName(name);
                if (!verified.Add(name) || Files.Hash(path) != (string)entry.Attribute("hash")) throw new InvalidDataException("The prepared update has changed. Download it again.");
            }
            if (!verified.Contains(PackageManifest.Name) || Required.Any(p => !verified.Contains(p))) throw new InvalidDataException("Incomplete prepared update.");
            Dictionary<string, PackageFile> package;
            using (var input = File.OpenRead(Path.Combine(candidate, PackageManifest.Name))) package = PackageManifest.Read(input, version);
            if (verified.Count != package.Count + 1 || package.Keys.Any(p => !verified.Contains(p))) throw new InvalidDataException("Prepared files don't match the package manifest.");
            foreach (var entry in package)
            {
                string path = Path.Combine(candidate, entry.Key);
                if (new FileInfo(path).Length != entry.Value.Size || !String.Equals(Files.Hash(path), entry.Value.Hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Prepared package checksum mismatch: " + entry.Key);
            }
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
