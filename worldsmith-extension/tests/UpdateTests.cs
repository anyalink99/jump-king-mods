using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml.Linq;
using System.Web.Script.Serialization;

namespace WorldsmithExtension
{
    internal static class UpdateTests
    {
        static string Release(string version, string url = null, bool draft = false)
        {
            string name = "WorldsmithExtension-" + version + "-win-x64.zip";
            return new JavaScriptSerializer().Serialize(new { tag_name = "worldsmith-extension-v" + version, draft = draft, assets = new[]{new { name = name, size = 123, digest = "sha256:" + new string('a',64), browser_download_url = url ?? Updates.Repository + "/releases/download/worldsmith-extension-v" + version + "/" + name }} });
        }

        static void Archive(string path, string extra = null, bool omit = false, bool declared = false)
        {
            var manifest = new XElement("Package", new XAttribute("version", "1"), new XAttribute("release", Updates.Current));
            using (var file = File.Create(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                foreach (string name in Updates.Required.Where(n => !omit || n != "0Harmony.dll").Concat(extra == null ? new string[0] : new[]{extra}))
                    using (var output = zip.CreateEntry(name).Open())
                    {
                        byte[] data = name == "WorldsmithExtension.exe" ? File.ReadAllBytes(typeof(UpdateTests).Assembly.Location) : Encoding.UTF8.GetBytes("fixture");
                        output.Write(data, 0, data.Length);
                        if (extra != name || declared)
                            using (var sha = SHA256.Create()) manifest.Add(new XElement("File", new XAttribute("name", name), new XAttribute("size", data.Length), new XAttribute("sha256", BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant())));
                    }
                using (var writer = new StreamWriter(zip.CreateEntry(PackageManifest.Name).Open())) writer.Write(manifest);
            }
        }

        internal static void Run(string root, Action<bool, string> assert, Action<Action, string> fails)
        {
            assert(Updates.Compare("0.1.0-preview.10", "0.1.0-preview.2") > 0, "preview ordering is numeric");
            assert(Updates.Compare("0.1.0", "0.1.0-preview.20") > 0, "stable release follows previews");
            assert(Updates.Compare("0.2.0-preview.1", "0.1.0") > 0, "release major/minor ordering");
            string data = "[" + Release("0.1.0-preview.3") + "," + Release("0.1.0-preview.10") + "," + Release("0.1.0-preview.11", null, true) + "," + Release("0.2.0", "https://example.com/evil.zip") + "]";
            assert(Updates.Select(data, "0.1.0-preview.2").Version == "0.1.0-preview.10", "newest published official asset selected");
            assert(Updates.Select(data, "0.1.0") == null, "stable channel excludes preview releases");
            assert(Updates.Select("[" + Release("0.1.0-preview.1") + "]", "0.1.0-preview.2") == null, "older versions excluded");
            assert(Updates.Select("[{\"tag_name\":\"some-mod-v99.0.0\",\"assets\":[]}]", Updates.Current) == null, "unrelated mod releases excluded");
            string latestOnly = "[" + Release("0.1.0-preview.9") + "]";
            foreach (string installed in new[] { "0.1.0-preview.2", "0.1.0-preview.3", "0.1.0-preview.7", "0.1.0-preview.8" })
                assert(Updates.Select(latestOnly, installed).Version == "0.1.0-preview.9", "direct update with intermediate releases removed: " + installed);
            assert(Updates.Select(latestOnly, "0.1.0-preview.9") == null, "latest-only feed does not offer the installed version");
            string packages = Path.Combine(root, "updates", "packages");
            Directory.CreateDirectory(packages);
            string zip = Path.Combine(root, "update.zip");
            Archive(zip);
            string hash = Files.Hash(zip);
            fails(() => Updates.Extract(zip, Path.Combine(packages, "bad-hash"), new string('0',64), Updates.Current), "wrong checksum rejected");
            assert(!Directory.Exists(Path.Combine(packages, "bad-hash")), "checksum failure does not install files");
            string candidate = Path.Combine(packages, "ready");
            Updates.Extract(zip, candidate, hash, Updates.Current);
            assert(Updates.VerifyCandidate(candidate, Path.Combine(root, "updates")) == Updates.Current, "complete extracted candidate verified");
            string updateRoot = Path.Combine(root, "updates");
            Updates.Activate(candidate, updateRoot);
            assert(Updates.Redirect(root, updateRoot, "0.1.0-preview.1") == Path.Combine(candidate, "WorldsmithExtension.exe"), "old launcher follows selected update");
            assert(Updates.Redirect(root, updateRoot, Updates.Current) == null, "equal version is not redirected");
            assert(Updates.Redirect(root, updateRoot, "99.0.0") == null, "newer launcher cannot be downgraded");
            assert(Updates.Redirect(candidate, updateRoot, "0.1.0-preview.1") == null, "updated launcher cannot redirect to itself");
            Updates.RestoreSelection(candidate, null, updateRoot);
            assert(Updates.Redirect(root, updateRoot, "0.1.0-preview.1") == null, "failed first activation restores original launcher");
            Updates.Activate(candidate, updateRoot);
            string selection = File.ReadAllText(Path.Combine(updateRoot, "current.xml"));
            Updates.RestoreSelection("another candidate", null, updateRoot);
            assert(File.ReadAllText(Path.Combine(updateRoot, "current.xml")) == selection, "rollback cannot overwrite another selection");
            string extraAssembly = Path.Combine(candidate, "Unlisted.dll");
            File.WriteAllText(extraAssembly, "unlisted");
            fails(() => Updates.VerifyCandidate(candidate, updateRoot), "unlisted prepared dependency rejected");
            File.Delete(extraAssembly);
            File.WriteAllText(Path.Combine(candidate, "WorldsmithExtension.Engine.dll"), "tampered");
            fails(() => Updates.Redirect(root, updateRoot, "0.1.0-preview.1"), "old launcher rejects changed update before starting it");

            fails(() => Updates.VerifyCandidate(candidate, Path.Combine(root, "updates")), "modified prepared assembly rejected");
            fails(() => Updates.Extract(zip, candidate, hash, Updates.Current), "existing installation is never overwritten");
            fails(() => Updates.Extract(zip, Path.Combine(packages, "wrong-version"), hash, "99.0.0"), "mismatched executable release version rejected");
            int i = 0;
            foreach (string extra in new[]{"../outside.txt", "docs/../../outside.txt", "docs/a.md:ads", "extra.dll", "worldsmithextension.exe", "WorldsmithExtension.exe", "worldsmith-path.txt"})
            {
                string bad = Path.Combine(root, "bad-update-" + (++i) + ".zip");
                Archive(bad, extra);
                string output = Path.Combine(packages, "bad-" + i);
                fails(() => Updates.Extract(bad, output, Files.Hash(bad), Updates.Current), "unsafe archive rejected: " + extra);
                assert(!Directory.Exists(output), "invalid archive rejected before extraction");
            }
            string missing = Path.Combine(root, "incomplete-update.zip");
            Archive(missing, null, true);
            fails(() => Updates.Extract(missing, Path.Combine(packages, "missing"), Files.Hash(missing), Updates.Current), "incomplete release rejected");
            foreach (string resource in new[]{"assets/new/theme/font.bin", "plugins/future/Renderer.dll", "docs/media/platform.png"})
            {
                string future = Path.Combine(root, "future.zip");
                Archive(future, resource, false, true);
                string output = Path.Combine(packages, "future-" + (++i));
                Updates.Extract(future, output, Files.Hash(future), Updates.Current);
                assert(File.ReadAllText(Path.Combine(output, resource)) == "fixture", "new declared package resources don't need an updater allowlist: " + resource);
                assert(Updates.VerifyCandidate(output, updateRoot) == Updates.Current, "new declared resources survive activation verification");
            }
            foreach (string unsafeName in new[]{"assets/CON.png", "assets/a./file", "assets//file", "/rooted", "assets/a\\b.png", "verified.xml", "game-path.txt"})
                fails(() => PackageManifest.CheckName(unsafeName), "Windows aliases and local state cannot be packaged: " + unsafeName);
            string corrupt = Path.Combine(root, "corrupt-file.zip"); Archive(corrupt);
            using (var archive = ZipFile.Open(corrupt, ZipArchiveMode.Update))
            {var entry = archive.GetEntry("0Harmony.dll"); entry.Delete(); using (var writer = new StreamWriter(archive.CreateEntry("0Harmony.dll").Open())) writer.Write("changed");}
            string corruptOutput = Path.Combine(packages, "corrupt-file");
            fails(() => Updates.Extract(corrupt, corruptOutput, Files.Hash(corrupt), Updates.Current), "per-file checksum rejects changed payload with a matching archive digest");
            assert(!Directory.Exists(corruptOutput), "failed checksum extraction removes its incomplete candidate");
            string bundle = Path.Combine(root, "original-bundle"); Directory.CreateDirectory(bundle);
            File.WriteAllText(Path.Combine(bundle, "game-path.txt"), "original game path");
            var release = new UpdateRelease { Version = Updates.Current, Size = new FileInfo(zip).Length, Digest = Files.Hash(zip), Url = "fixture" };
            string downloaded = Updates.Download(release, bundle, updateRoot, (url, target, limit) => {
                assert(limit == release.Size, "download stream is bounded by the selected asset size");
                using (var input = File.OpenRead(zip)) input.CopyTo(target);
            });
            assert(Updates.VerifyCandidate(downloaded, updateRoot) == Updates.Current && File.ReadAllText(Path.Combine(downloaded, "game-path.txt")) == "original game path", "download keeps installation overrides and verifies the whole package");
            assert(!File.Exists(Path.Combine(Path.GetDirectoryName(downloaded), "release.zip")), "successful preparation doesn't retain a duplicate archive");
            int preparedCount = Directory.GetDirectories(packages).Length;
            fails(() => Updates.Download(release, bundle, updateRoot, (url, target, limit) => { target.WriteByte(1); throw new IOException("interrupted fixture download"); }), "interrupted downloads report their failure");
            assert(Directory.GetDirectories(packages).Length == preparedCount, "failed downloads remove only their incomplete stage");
            assert(File.ReadAllText(Path.Combine(bundle, "game-path.txt")) == "original game path", "failed download doesn't change the original installation");
        }
    }
}
