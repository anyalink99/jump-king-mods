using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal sealed class ProjectFormat
    {
        internal string Label, Limits;
        internal bool Package, HasSources, Editable;
        internal static ProjectFormat Inspect(string root, string category)
        {
            if (category == "Mod")
            {
                bool source = ModPackage.IsSourceProject(root);
                return new ProjectFormat{Label = source ? "C# source project" : "Compiled mod package", Package = !source, HasSources = source, Editable = true, Limits = source ? "Build requires a compatible .NET SDK and the project's dependencies." : "DLLs and resources can be tested and published. Original C# sources cannot be recovered; edit resources or open the source project to change code."};
            }

            string[] files = Files.Sources(root).ToArray();
            var paths = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            bool compiled = files.Any(p => Path.GetExtension(p).Equals(".xnb", StringComparison.OrdinalIgnoreCase));
            bool sourceAssets = files.Any(p => new[]{".png", ".bmp", ".wav", ".mp3"}.Contains(Path.GetExtension(p).ToLowerInvariant()) && !new[] { "workshop-preview.png", "preview.png", "workshop.png" }.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase));
            bool editable = category != "Level" || File.Exists(Path.Combine(root, "level.png")) || File.Exists(Path.Combine(root, "visual_level.png"));
            editable = editable && files.Where(p => Path.GetExtension(p).Equals(".xnb", StringComparison.OrdinalIgnoreCase)).All(file =>
            {
                string relative = Files.Relative(root, file).Replace('\\', '/').ToLowerInvariant();
                return !new[] { "screens/", "king/", "props/", "gui/" }.Any(relative.StartsWith) || paths.Contains(Path.ChangeExtension(file, ".png"));
            });
            return new ProjectFormat{Label = compiled ? (sourceAssets ? "Mixed source and compiled assets" : "Compiled resource package") : "Editable source project", Package = compiled, HasSources = sourceAssets, Editable = editable, Limits = compiled ? "Compiled files are present. Create a separate working copy to recover supported PNG/WAV assets. Compressed textures, effects and original layered artwork may remain unavailable for editing." : "Editable resources are available. A checked build is required before testing or publishing."};
        }

        internal static void Snapshot(string root, string output, Action<string> status, bool receipt = true, bool authoringSettings = false)
        {
            root = Path.GetFullPath(root);
            output = Path.GetFullPath(output);
            Files.PrepareOutput(root, output);
            string revision = Files.Fingerprint(root);
            Directory.CreateDirectory(output);
            foreach (string source in Files.Sources(root))
            {
                string relative = Files.Relative(root, source), destination = Path.Combine(output, relative);
                status("Copying " + relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                string hash = Files.Hash(source);
                File.Copy(source, destination, false);
                if (Files.Hash(destination) != hash)
                    throw new IOException("File changed while copying: " + relative);
            }

            string config = Path.Combine(root, "worldsmith-extension.xml");
            if (authoringSettings && File.Exists(config))
                File.Copy(config, Path.Combine(output, "worldsmith-extension.xml"), false);
            Files.CopyDirectories(root, output);
            if (Files.Fingerprint(root) != revision)
                throw new IOException("The package changed while copying. Try again.");
            if (receipt)
                BuildReceipt.Save(root, output, revision);
        }

        internal static bool CanEdit(string root, string category)
        {
            return Inspect(root, category).Editable;
        }

        internal static string Recover(string root, string output, Action<string> status)
        {
            Snapshot(root, output, status, false, true);
            var report = new StringBuilder("Recovered working copy. Original compiled files are retained.\r\n\r\n");
            foreach (string source in Files.Sources(output).Where(p => Path.GetExtension(p).Equals(".xnb", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                string relative = Files.Relative(output, source);
                if (new[]{".png", ".wav", ".mp3", ".bmp"}.Any(ext => File.Exists(Path.ChangeExtension(source, ext))))
                    continue;
                try
                {
                    string result = PackedAssets.Extract(source, source, relative.Equals("level.xnb", StringComparison.OrdinalIgnoreCase));
                    report.AppendLine(relative + ": " + result);
                }
                catch (Exception error)
                {
                    report.AppendLine(relative + ": " + error.GetBaseException().Message);
                }

                status("Inspecting " + relative);
            }

            string atlas = Path.Combine(output, "level.png");
            if (File.Exists(atlas) && !File.Exists(Path.Combine(output, "visual_level.png")))
            {
                using (var bitmap = Collision.Read(atlas))
                {
                    var layout = Layout.Read(output, (bitmap.Width / 60) * (bitmap.Height / 45));
                    layout.Source = "atlas";
                    if (bitmap.Height != 585)
                        layout.Side = bitmap.Width / 60;
                    layout.Validate();
                    using (var strip = Collision.SourceStrip(bitmap, layout))
                        Collision.Png(strip, Path.Combine(output, "visual_level.png"));
                    bool inferred = !File.Exists(Layout.Metadata(output));
                    layout.Save(output);
                    if (inferred)
                        report.AppendLine("Screen count was inferred from atlas capacity. Review padding and the authored screen count.");
                }
            }

            string text = report.ToString();
            Files.Text(Path.Combine(output, ".worldsmith-extension", "recovery.txt"), text);
            return text;
        }
    }
}
