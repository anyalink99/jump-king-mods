using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace WorldsmithExtension
{
    internal static class CompilerPatches
    {
        static bool Texture(string originFilePath, string destinationFilePath)
        {
            string root = Engine.ProjectRoot;
            bool top = root != null && String.Equals(Path.GetDirectoryName(Path.GetFullPath(originFilePath)), Path.GetFullPath(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            if (top && new[]{"visual_level.png", "level.png"}.Contains(Path.GetFileName(originFilePath).ToLowerInvariant()))
            {
                string strip = Path.Combine(root, "visual_level.png"), atlas = Path.Combine(root, "level.png");
                int count;
                using (var b = Collision.Read(File.Exists(strip) ? strip : atlas))
                    count = File.Exists(strip) ? b.Height / 45 : (b.Width / 60) * (b.Height / 45);
                var layout = Layout.Read(root, count);
                if (!File.Exists(strip) && !File.Exists(Path.Combine(root, "worldsmith-extension.xml")))
                    layout.Source = "atlas";
                using (var source = Collision.Read(layout.Source == "strip" ? strip : atlas))
                    using (var value = Collision.Compile(source, layout))
                    {
                        if (value.Width != layout.Side * 60 || value.Height != layout.Side * 45)
                            throw new InvalidDataException("Collision atlas does not match map.xml");
                        string output = Path.GetDirectoryName(destinationFilePath);
                        Collision.Xnb(value, Path.Combine(output, "level.xnb"));
                        layout.SaveMetadata(output);
                    }

                return false;
            }

            NativeCompiler.NativeTexture(originFilePath, destinationFilePath);
            return false;
        }

        static bool Sound(string originFilePath, string destinationFilePath, string extension)
        {
            NativeCompiler.NativeSound(originFilePath, destinationFilePath, extension);
            return false;
        }

        static bool WaitFile(FileInfo file, int tries, ref bool __result)
        {
            __result = false;
            if (!File.Exists(file.FullName))
                return false;
            for (int n = 0; n < Math.Min(tries, 10); n++)
                try
                {
                    using (var s = File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        s.ReadByte();
                    }

                    __result = true;
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(500);
                }

            return false;
        }

        static bool CreateOutputFolders(string originalDir, string newDir)
        {
            FileSafety.CheckedPath(originalDir, newDir);
            Directory.CreateDirectory(newDir);
            foreach (string folder in Files.SourceDirectories(originalDir))
                Directory.CreateDirectory(Path.Combine(newDir, Files.Relative(originalDir, folder)));
            return false;
        }

        static bool Scan(object __instance, ref string[] toConvert, ref bool __result)
        {
            string root = (string)Engine.Get(__instance, "Directory");
            toConvert = Files.Sources(root).Where(f =>
            {
                string rel = Files.Relative(root, f);
                string ext = Path.GetExtension(f).ToLowerInvariant();
                string target = Path.Combine(root, "bin", rel.Equals("visual_level.png", StringComparison.OrdinalIgnoreCase) ? "level.xnb" : (new[]{".png", ".bmp", ".wav", ".mp3"}.Contains(ext)) ? Path.ChangeExtension(rel, ".xnb") : rel);
                return !File.Exists(target) || File.GetLastWriteTimeUtc(f) > File.GetLastWriteTimeUtc(target);
            }).ToArray();
            __result = toConvert.Length > 0;
            return false;
        }

    }
}
