using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class PackageTests
    {
        internal static void Run(string dir, Action<bool, string> Assert, Action<Action, string> Fails)
        {
            string modRoot = Path.Combine(dir, "mod"), modOutput = Path.Combine(dir, "mod-build", "content");
            Directory.CreateDirectory(modRoot);
            File.Copy(typeof(Tests).Assembly.Location, Path.Combine(modRoot, "Example.dll"));
            Directory.CreateDirectory(Path.Combine(modRoot, "resources"));
            Files.Text(Path.Combine(modRoot, "resources", "data.txt"), "resource");
            Directory.CreateDirectory(Path.Combine(modRoot, "resources", "empty"));
            Files.Text(Path.Combine(modRoot, "source.cs"), "private source");
            Fails(() => ModPackage.Validate(modRoot), "source folders cannot be uploaded as compiled mods");
            File.Delete(Path.Combine(modRoot, "source.cs"));
            Files.Text(Path.Combine(modRoot, ".worldsmith-extension", "workshop.xml"), "<Workshop id=\"123\"/>");
            ModPackage.Build(modRoot, modOutput, s =>
            {
            });
            Assert(File.Exists(Path.Combine(modOutput, "Example.dll")) && File.ReadAllText(Path.Combine(modOutput, "resources", "data.txt")) == "resource", "mod DLL and nested resources preserved");
            Assert(Directory.Exists(Path.Combine(modOutput, "resources", "empty")), "empty mod resource directories preserved");
            Assert(!Directory.Exists(Path.Combine(modOutput, ".worldsmith-extension")), "local receipt not published");
            var modRequest = new PublishRequest{Root = modRoot, Content = modOutput, Title = "Example mod", Tags = new[]{"Mod"}, Visibility = 2};
            modRequest.Validate();
            Files.Text(Path.Combine(modRoot, "resources", "data.txt"), "changed");
            Fails(modRequest.Validate, "changed mod resources block upload");
            string packed = Path.Combine(dir, "packed"), recovered = Path.Combine(dir, "recovered");
            Directory.CreateDirectory(packed);
            string[] emptyFolders = {"props/textures/old_man/lines", "props/textures/old_man/merchant", "resources/empty/nested"};
            foreach (string folder in emptyFolders)
                Directory.CreateDirectory(Path.Combine(packed, folder));
            string[] excludedFolders = {"bin", "obj", "temp", ".git", ".vs", ".worldsmith-extension", "saves", "savesperma"};
            foreach (string folder in excludedFolders)
                Directory.CreateDirectory(Path.Combine(packed, "resources", folder, "ignored-empty"));
            using (var pixels = new Bitmap(780, 585))
            {
                pixels.SetPixel(0, 0, Color.FromArgb(127, 17, 39, 255));
                pixels.SetPixel(1, 0, Color.Magenta);
                Collision.Xnb(pixels, Path.Combine(packed, "level.xnb"));
            }

            Files.Text(Path.Combine(packed, "level_settings.xml"), "<LevelSettings/>");
            Assert(ProjectFormat.Inspect(packed, "Level").Label == "Compiled resource package", "compiled map classification");
            Assert(!ProjectFormat.CanEdit(packed, "Level"), "compiled-only map needs package browser");
            string packedBefore = Files.Fingerprint(packed);
            ProjectFormat.Recover(packed, recovered, s =>
            {
            });
            Assert(Files.Fingerprint(packed) == packedBefore, "recovery does not change the compiled source");
            using (var pixels = Collision.Read(Path.Combine(recovered, "level.png")))
                Assert(pixels.GetPixel(0, 0).ToArgb() == Color.FromArgb(127, 17, 39, 255).ToArgb() && pixels.GetPixel(1, 0).ToArgb() == Color.Magenta.ToArgb(), "recovered collision preserves exact RGBA");
            Assert(ProjectFormat.Inspect(recovered, "Level").Label == "Mixed source and compiled assets" && ProjectFormat.CanEdit(recovered, "Level"), "recovered mixed map is editable");
            Assert(File.Exists(Path.Combine(recovered, ".worldsmith-extension", "recovery.txt")), "recovery limitations report exists");
            string hiddenTexture = Path.Combine(recovered, "props", "hidden_walls", "textures", "wall.xnb");
            Directory.CreateDirectory(Path.GetDirectoryName(hiddenTexture));
            File.Copy(Path.Combine(packed, "level.xnb"), hiddenTexture);
            Assert(!ProjectFormat.CanEdit(recovered, "Level"), "compiled hidden wall textures require recovery");
            using (var image = new Bitmap(2, 2)) image.Save(Path.ChangeExtension(hiddenTexture, ".png"));
            Assert(ProjectFormat.CanEdit(recovered, "Level"), "mixed map with paired image sources opens in editor");

            Files.Text(Path.Combine(recovered, "worldsmith-extension.xml"), "<WorldsmithExtension collisionSource=\"atlas\"/>");
            string recoveredAgain = Path.Combine(dir, "recovered-again");
            ProjectFormat.Recover(recovered, recoveredAgain, s =>
            {
            });
            Assert(Layout.Read(recoveredAgain, 169).Source == "atlas", "working copies retain the author's collision source choice");
            string publishCopy = Path.Combine(dir, "publish-copy", "content");
            ProjectFormat.Snapshot(recovered, publishCopy, s =>
            {
            });
            foreach (string copy in new[]{recovered, recoveredAgain, publishCopy})
            {
                foreach (string folder in emptyFolders)
                    Assert(Directory.Exists(Path.Combine(copy, folder)), "empty directory survives recovery and packaging: " + folder);
                foreach (string folder in excludedFolders)
                    Assert(!Directory.Exists(Path.Combine(copy, "resources", folder)), "excluded directory stays out of copies: " + folder);
            }
            BuildReceipt.Validate(recovered, publishCopy);
            Directory.Delete(Path.Combine(publishCopy, emptyFolders[0]));
            Fails(() => BuildReceipt.Validate(recovered, publishCopy), "missing empty output directory blocks upload");
            Assert(!File.Exists(Path.Combine(publishCopy, "worldsmith-extension.xml")), "authoring settings stay out of published packages");
            byte[] compressed = File.ReadAllBytes(Path.Combine(packed, "level.xnb"));
            compressed[5] = 128;
            File.WriteAllBytes(Path.Combine(packed, "unsupported.xnb"), compressed);
            Fails(() => PackedAssets.Extract(Path.Combine(packed, "unsupported.xnb"), Path.Combine(dir, "unsupported.xnb"), false), "compressed resources reported instead of corrupting output");
            Assert(!File.Exists(Path.Combine(dir, "unsupported.png")), "unsupported conversion creates no partial output");
        }
    }
}
