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
                int fallback = Collision.SourceScreens(authoring);
                if (fallback == 0)
                {
                    if (!File.Exists(Path.Combine(authoring, "level.xnb")))
                        throw new InvalidDataException("Map has no collision source or compiled collision.");
                }
                else
                {
                    layout = Layout.Read(authoring, fallback);
                    bool legacyPlus=Collision.LegacyLayout(authoring,layout);
                    if(legacyPlus)Collision.Scaffold(Path.Combine(output,"level.xnb"),layout.Side);
                    else if(layout.Pixel)
                    {
                        Collision.PixelXnb(Path.Combine(authoring,"level+.png"),Path.Combine(output,"level+.xnb"),layout);
                        Collision.Scaffold(Path.Combine(output,"level.xnb"));
                    }
                    else using (var source = Collision.Read(layout.Source == "strip" ? strip : atlas))
                        using (var value = Collision.Compile(source, layout))
                        {
                            if (value.Width != layout.Side * 60 || value.Height != layout.Side * 45)
                                throw new InvalidDataException("Atlas dimensions do not match map.xml.");
                            Collision.Xnb(value, Path.Combine(output, "level.xnb"));
                        }

                    string scene = Path.Combine(authoring, "mapping_settings.xml");
                    if(!File.Exists(scene))scene=Path.Combine(authoring, "props", "mega-mapping-expansion", "scene.xml");
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
                if (level && (rel.Equals("visual_level.png", StringComparison.OrdinalIgnoreCase) || rel.Equals("visual_level.xnb", StringComparison.OrdinalIgnoreCase) || rel.Equals("level.png", StringComparison.OrdinalIgnoreCase) || (layout != null && rel.Equals("level.xnb", StringComparison.OrdinalIgnoreCase))))
                    continue;
                string destination = Path.Combine(output, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (Files.CompiledHasSource(file, authoring))
                    continue;
                if (ext == ".png" || ext == ".bmp")
                {
                    // keep source PNGs as well: scene includes can reference them anywhere in the map
                    File.Copy(file, destination, true);
                    if (!(layout != null && layout.Pixel && rel.Equals("level+.png", StringComparison.OrdinalIgnoreCase)) && !rel.StartsWith("props\\mega-mapping-expansion\\", StringComparison.OrdinalIgnoreCase))
                        NativeCompiler.NativeTexture(file, Path.ChangeExtension(destination, ".xnb"));
                }
                else if (ext == ".wav" || ext == ".mp3")
                    NativeCompiler.NativeSound(file, Path.ChangeExtension(destination, ".xnb"), ext);
                else
                    File.Copy(file, destination, true);
            }

            if(layout!=null)layout.SaveMetadata(output);
            ValidateMapping(output, status);
            if(layout!=null&&layout.Pixel)Collision.ValidatePixelXnb(Path.Combine(output,"level+.xnb"),layout);
            if (level)
            {
                foreach (string required in new[]{"level.xnb", "level_settings.xml", "gui/location_settings.xml", "gui/earthquake_settings.xml", "props/textures/prop_settings.xml", "particles/weather.xml"})
                    if (!File.Exists(Path.Combine(output, required)))
                        throw new InvalidDataException("Missing required map file: " + required);
                ProjectFormat.PrepareMapDirectories(output);
            }

            BuildReceipt.Save(root, output, revision);
            status("Build ready: " + output);
        }

        static void ValidateMapping(string root, Action<string> status)
        {
            if (!MegaMappingExpansion.MappingFiles.Exists(root))
                return;
            status("Validating MegaMapping XML");
            string compiler = Path.Combine(Engine.Bundle, "mapping", "SceneCacheCompiler.exe");
            if (!File.Exists(compiler))
                throw new FileNotFoundException("The portable bundle is missing its MegaMapping compiler.", compiler);
            string steam = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
            string game = SteamInstallations.Game(Engine.Bundle, Engine.Host, steam);
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs e)
            {
                string name = new AssemblyName(e.Name).Name;
                string path = Path.Combine(game, name + (name == "JumpKing" ? ".exe" : ".dll"));
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            var a = Assembly.LoadFrom(compiler);
            int result = (int)a.EntryPoint.Invoke(null, new object[]{new[]{"--validate", root}});
            if (result != 0)
                throw new InvalidDataException("MegaMapping scene validation failed. See the build log.");
        }
    }
}
