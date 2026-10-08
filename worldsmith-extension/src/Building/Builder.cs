using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class Builder
    {
        internal static void Build(string root, string output, Action<string> status)
        {
            root = Path.GetFullPath(root);
            output = Path.GetFullPath(output);
            Files.PrepareOutput(root, output);
            bool level = File.Exists(Path.Combine(root, "level_settings.xml"));
            string revision = Files.Fingerprint(root);
            string authoring = Path.Combine(Path.GetDirectoryName(output), "input");
            Directory.CreateDirectory(authoring);
            Directory.CreateDirectory(output);
            string[] sources = Files.Sources(root).ToArray();
            Files.CopyDirectories(root, authoring);
            Files.CopyDirectories(authoring, output);
            foreach (string file in sources)
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked files are not supported: " + file);
                string target = Path.Combine(authoring, Files.Relative(root, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                string hash = Files.Hash(file);
                File.Copy(file, target, false);
                if (Files.Hash(target) != hash)
                    throw new IOException("Source changed during snapshot: " + file);
            }

            string config = Path.Combine(root, "worldsmith-extension.xml");
            if (File.Exists(config))
                File.Copy(config, Path.Combine(authoring, "worldsmith-extension.xml"));
            NativeCompiler.Configure(authoring, output);
            Layout layout = null;
            if (level)
            {
                string strip = Path.Combine(authoring, "visual_level.png"), atlas = Path.Combine(authoring, "level.png");
                int fallback = 0;
                if (File.Exists(strip))
                    using (var b = Collision.Read(strip))
                        fallback = b.Height / 45;
                else if (File.Exists(atlas))
                    using (var b = Collision.Read(atlas))
                        fallback = (b.Width / 60) * (b.Height / 45);
                if (fallback == 0)
                {
                    if (!File.Exists(Path.Combine(authoring, "level.xnb")))
                        throw new InvalidDataException("Map has no collision source or compiled collision.");
                }
                else
                {
                    layout = Layout.Read(authoring, fallback);
                    if (!File.Exists(strip) && !File.Exists(config))
                        layout.Source = "atlas";
                    using (var source = Collision.Read(layout.Source == "strip" ? strip : atlas))
                        using (var value = Collision.Compile(source, layout))
                        {
                            if (value.Width != layout.Side * 60 || value.Height != layout.Side * 45)
                                throw new InvalidDataException("Atlas dimensions do not match map.xml.");
                            Collision.Xnb(value, Path.Combine(output, "level.xnb"));
                        }

                    layout.SaveMetadata(output);
                    string scene = Path.Combine(authoring, "props", "mega-mapping-expansion", "scene.xml");
                    if (File.Exists(scene))
                    {
                        var options = Files.Xml(scene).Root.Element("Options");
                        int expected = options == null ? 0 : (int? )options.Attribute("expectedScreens") ?? 0;
                        if (expected != 0 && expected != layout.Screens)
                            throw new InvalidDataException("scene.xml expectedScreens does not match map.xml.");
                    }
                }
            }

            var inputs = Files.Sources(authoring).ToArray();
            int done = 0;
            foreach (string file in inputs)
            {
                string rel = Files.Relative(authoring, file), ext = Path.GetExtension(file).ToLowerInvariant();
                status("Building " + (++done) + "/" + inputs.Length + ": " + rel);
                if (level && (rel.Equals("visual_level.png", StringComparison.OrdinalIgnoreCase) || rel.Equals("visual_level.xnb", StringComparison.OrdinalIgnoreCase) || rel.Equals("level.png", StringComparison.OrdinalIgnoreCase) || (layout != null && (rel.Equals("level.xnb", StringComparison.OrdinalIgnoreCase) || rel.Equals("props\\mega-mapping-expansion\\map.xml", StringComparison.OrdinalIgnoreCase)))))
                    continue;
                string destination = Path.Combine(output, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (ext == ".xnb" && (File.Exists(Path.ChangeExtension(file, ".png")) || File.Exists(Path.ChangeExtension(file, ".bmp")) || File.Exists(Path.ChangeExtension(file, ".wav")) || File.Exists(Path.ChangeExtension(file, ".mp3"))))
                    continue;
                if (ext == ".png" || ext == ".bmp")
                {
                    // keep source PNGs as well: scene includes can reference them anywhere in the map
                    File.Copy(file, destination, true);
                    if (!rel.StartsWith("props\\mega-mapping-expansion\\", StringComparison.OrdinalIgnoreCase))
                        NativeCompiler.NativeTexture(file, Path.ChangeExtension(destination, ".xnb"));
                }
                else if (ext == ".wav" || ext == ".mp3")
                    NativeCompiler.NativeSound(file, Path.ChangeExtension(destination, ".xnb"), ext);
                else
                    File.Copy(file, destination, true);
            }

            ValidateMapping(output, status);
            if (level)
            {
                foreach (string required in new[]{"level.xnb", "level_settings.xml", "gui/location_settings.xml", "gui/earthquake_settings.xml", "props/textures/prop_settings.xml", "particles/weather.xml"})
                    if (!File.Exists(Path.Combine(output, required)))
                        throw new InvalidDataException("Missing required map file: " + required);
                foreach (string folder in new[]{"audio", "ending", "king", "particles", "props", "screens"})
                {
                    string target = Path.Combine(output, folder);
                    if (!Directory.Exists(Path.Combine(authoring, folder)))
                        throw new InvalidDataException("Missing map directory: " + folder);
                    Directory.CreateDirectory(target);
                }
            }

            BuildReceipt.Save(root, output, revision);
            status("Build ready: " + output);
        }

        static void ValidateMapping(string root, Action<string> status)
        {
            string scene = Path.Combine(root, "props", "mega-mapping-expansion", "scene.xml");
            if (!File.Exists(scene))
                return;
            status("Validating and compiling MegaMapping scene");
            string compiler = Path.Combine(Engine.Bundle, "mapping", "SceneCacheCompiler.exe");
            if (!File.Exists(compiler))
                throw new FileNotFoundException("The portable bundle is missing its MegaMapping compiler.", compiler);
            string steam = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
            string game = Path.Combine(steam ?? "", "steamapps", "common", "Jump King");
            string overrideFile = Path.Combine(Engine.Bundle, "game-path.txt");
            if (File.Exists(overrideFile))
                game = File.ReadAllText(overrideFile).Trim();
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs e)
            {
                string name = new AssemblyName(e.Name).Name;
                string path = Path.Combine(game, name + (name == "JumpKing" ? ".exe" : ".dll"));
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            var a = Assembly.LoadFrom(compiler);
            int result = (int)a.EntryPoint.Invoke(null, new object[]{new[]{root, Path.Combine(root, "props", "mega-mapping-expansion", "scene.mmgfx")}});
            if (result != 0)
                throw new InvalidDataException("MegaMapping scene compilation failed. See the build log.");
        }
    }
}
