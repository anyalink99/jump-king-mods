using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class CollisionTests
    {
        internal static void Run(string dir, Action<bool, string> Assert, Action<Action, string> Fails)
        {
            foreach (int n in new[]{1, 169, 170, 255, 256, 512})
            {
                var layout = new Layout{Screens = n};
                layout.Validate();
                using (var strip = new Bitmap(60, 45 * n))
                {
                    for (int i = 0; i < n; i++)
                        strip.SetPixel(0, (n - i - 1) * 45, Color.FromArgb(127, i % 256, i / 256, 253));
                    strip.SetPixel(1, 0, Color.Magenta);
                    using (var atlas = Collision.Atlas(strip, layout))
                        using (var back = Collision.Strip(atlas, layout))
                        {
                            for (int i = 0; i < n; i++)
                                Assert(strip.GetPixel(0, (n - i - 1) * 45).ToArgb() == back.GetPixel(0, (n - i - 1) * 45).ToArgb(), "screen marker " + i);
                            Assert(back.GetPixel(1, 0).ToArgb() == Color.Magenta.ToArgb(), "magenta is data");
                            string xnb = Path.Combine(dir, "collision.xnb");
                            Collision.Xnb(atlas, xnb);
                            using (var r = new BinaryReader(File.OpenRead(xnb)))
                            {
                                Assert(new string (r.ReadChars(3)) == "XNB", "XNB magic");
                                r.ReadBytes(3);
                                Assert(r.ReadInt32() == r.BaseStream.Length, "XNB length");
                                Assert(r.ReadByte() == 1, "reader count");
                                r.ReadString();
                                r.ReadInt32();
                                r.ReadByte();
                                r.ReadByte();
                                Assert(r.ReadInt32() == 0, "RGBA format");
                                Assert(r.ReadInt32() == layout.Side * 60, "width");
                                Assert(r.ReadInt32() == layout.Side * 45, "height");
                                r.ReadInt32();
                                r.ReadInt32();
                                for (int y = 0; y < atlas.Height; y++)
                                    for (int x = 0; x < atlas.Width; x++)
                                    {
                                        var c = atlas.GetPixel(x, y);
                                        if (r.ReadByte() != c.R || r.ReadByte() != c.G || r.ReadByte() != c.B || r.ReadByte() != c.A)
                                            throw new Exception("Pixel corruption");
                                    }

                                Assert(r.BaseStream.Position == r.BaseStream.Length, "XNB payload");
                            }
                        }
                }
            }

            RightGrowing(dir, Assert, Fails);
        }

        static void RightGrowing(string dir, Action<bool, string> Assert, Action<Action, string> Fails)
        {
            foreach (int n in new[] { 169, 170, 200, 256, 512, 4096 })
            {
                var layout = new Layout { Screens = n, Source = "atlas" };
                layout.Validate();
                using (var source = new Bitmap(Math.Max(13, (n + 12) / 13) * 60, 585))
                {
                    for (int i = 0; i < n; i++)
                        source.SetPixel((i / 13) * 60, (i % 13) * 45, Color.FromArgb(127, i % 256, i / 256, 253));
                    source.SetPixel(((n - 1) / 13) * 60 + 59, ((n - 1) % 13) * 45 + 44, Color.Magenta);
                    using (var compiled = Collision.Compile(source, layout))
                    using (var preview = Collision.SourceStrip(source, layout))
                    using (var runtimeStrip = Collision.Strip(compiled, layout))
                    {
                        Assert(compiled.Width == layout.Side * 60 && compiled.Height == layout.Side * 45, "runtime dimensions " + n);
                        for (int i = 0; i < n; i++)
                        {
                            int expected = Color.FromArgb(127, i % 256, i / 256, 253).ToArgb();
                            Assert(preview.GetPixel(0, (n - i - 1) * 45).ToArgb() == expected, "preview screen order and alpha " + i);
                            Assert(runtimeStrip.GetPixel(0, (n - i - 1) * 45).ToArgb() == expected, "runtime screen order and alpha " + i);
                        }
                        Assert(runtimeStrip.GetPixel(59, 44).ToArgb() == Color.Magenta.ToArgb(), "last collision pixel survives repacking " + n);
                    }
                    if (n == 200)
                    {
                        string root = Path.Combine(dir, "right-growing"), recovered = Path.Combine(dir, "right-growing-recovered");
                        layout.Save(root);
                        Collision.Png(source, Path.Combine(root, "level.png"));
                        string before = Files.Hash(Path.Combine(root, "level.png"));
                        ProjectFormat.Recover(root, recovered, text => {});
                        Assert(Layout.Read(recovered, 1).Source == "atlas", "recovery retains ordinary atlas source");
                        Assert(Files.Hash(Path.Combine(recovered, "level.png")) == before, "recovery preserves editable atlas bytes");
                        Assert(Layout.Read(recovered, 1).Side == 15, "metadata retains compiled square dimensions");
                        using (var preview = Collision.Read(Path.Combine(recovered, "visual_level.png")))
                            Assert(preview.Height == 200 * 45 && preview.GetPixel(59, 44).ToArgb() == Color.Magenta.ToArgb(), "recovery reconstructs preview order");
                    }
                }
            }
            var large = new Layout { Screens = 200, Source = "atlas" };
            using (var tooSmall = new Bitmap(780, 585))
                Fails(() => { using (Collision.Compile(tooSmall, large)) {} }, "atlas must contain every authored screen");
            using (var wrongRows = new Bitmap(960, 630))
                Fails(() => { using (Collision.SourceStrip(wrongRows, large)) {} }, "nonstandard rectangles are rejected");
            using (var partialColumn = new Bitmap(961, 585))
                Fails(() => { using (Collision.Compile(partialColumn, large)) {} }, "partial screen columns are rejected");
            var legacy = new Layout { Screens = 512, Source = "atlas" };
            legacy.Validate();
            using (var square = new Bitmap(1380, 1035))
            {
                square.SetPixel(1379, 1034, Color.Blue);
                using (var compiled = Collision.Compile(square, legacy))
                    Assert(compiled.GetPixel(1379, 1034).ToArgb() == Color.Blue.ToArgb(), "legacy square atlas retains unused cells");
            }
        }
    }
}
