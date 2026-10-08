using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WorldsmithExtension
{
    internal static class Collision
    {
        internal static Bitmap Read(string path)
        {
            using (var input = new Bitmap(path))
                return input.Clone(new Rectangle(0, 0, input.Width, input.Height), PixelFormat.Format32bppArgb);
        }

        internal static Bitmap Atlas(Bitmap strip, Layout layout)
        {
            layout.Validate();
            if (strip.Width != 60 || strip.Height % 45 != 0 || strip.Height / 45 < layout.Screens)
                throw new InvalidDataException("Collision strip must be 60 pixels wide and contain every authored 45-pixel screen.");
            var atlas = new Bitmap(layout.Side * 60, layout.Side * 45, PixelFormat.Format32bppArgb);
            for (int i = 0; i < layout.Screens; i++)
                Copy(strip, new Rectangle(0, strip.Height - (i + 1) * 45, 60, 45), atlas, (i / layout.Side) * 60, (i % layout.Side) * 45);
            return atlas;
        }

        internal static Bitmap Strip(Bitmap atlas, Layout layout)
        {
            if (atlas.Width != layout.Side * 60 || atlas.Height != layout.Side * 45)
                throw new InvalidDataException("Collision atlas dimensions do not match map.xml.");
            var strip = new Bitmap(60, layout.Screens * 45, PixelFormat.Format32bppArgb);
            for (int i = 0; i < layout.Screens; i++)
                Copy(atlas, new Rectangle((i / layout.Side) * 60, (i % layout.Side) * 45, 60, 45), strip, 0, (layout.Screens - i - 1) * 45);
            return strip;
        }

        static void ValidateSourceAtlas(Bitmap atlas, Layout layout)
        {
            layout.Validate();
            if (atlas.Height == 585)
            {
                if (atlas.Height != 585 || atlas.Width < 780 || atlas.Width % 60 != 0 || (atlas.Width / 60) * 13 < layout.Screens)
                    throw new InvalidDataException("Right-growing level.png must be 585 pixels high, at least 780 pixels wide, and contain enough 60-pixel columns for every authored screen.");
            }
            else if (atlas.Width != layout.Side * 60 || atlas.Height != layout.Side * 45)
                throw new InvalidDataException("Collision atlas dimensions do not match map.xml.");
        }

        internal static Bitmap SourceStrip(Bitmap atlas, Layout layout)
        {
            ValidateSourceAtlas(atlas, layout);
            int rows = atlas.Height / 45;
            var strip = new Bitmap(60, layout.Screens * 45, PixelFormat.Format32bppArgb);
            for (int i = 0; i < layout.Screens; i++)
                Copy(atlas, new Rectangle((i / rows) * 60, (i % rows) * 45, 60, 45), strip, 0, (layout.Screens - i - 1) * 45);
            return strip;
        }

        internal static Bitmap Compile(Bitmap source, Layout layout)
        {
            if (layout.Source == "strip") return Atlas(source, layout);
            ValidateSourceAtlas(source, layout);
            if (source.Width == layout.Side * 60 && source.Height == layout.Side * 45)
                return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
            // source atlas can grow sideways, the game's metadata still expects a square grid
            var atlas = new Bitmap(layout.Side * 60, layout.Side * 45, PixelFormat.Format32bppArgb);
            for (int i = 0; i < layout.Screens; i++)
                Copy(source, new Rectangle((i / 13) * 60, (i % 13) * 45, 60, 45), atlas, (i / layout.Side) * 60, (i % layout.Side) * 45);
            return atlas;
        }

        private static void Copy(Bitmap source, Rectangle region, Bitmap target, int x, int y)
        {
            var a = source.LockBits(region, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var b = target.LockBits(new Rectangle(x, y, region.Width, region.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[region.Width * 4];
                for (int r = 0; r < region.Height; r++)
                {
                    Marshal.Copy(IntPtr.Add(a.Scan0, r * a.Stride), row, 0, row.Length);
                    Marshal.Copy(row, 0, IntPtr.Add(b.Scan0, r * b.Stride), row.Length);
                }
            }
            finally
            {
                source.UnlockBits(a);
                target.UnlockBits(b);
            }
        }

        internal static void Png(Bitmap value, string path)
        {
            Files.Atomic(path, s => value.Save(s, ImageFormat.Png));
        }

        internal static void Xnb(Bitmap image, string path)
        {
            // collision data is copied byte-for-byte: no color key, filtering or alpha multiplication
            Files.Atomic(path, stream =>
            {
                using (var body = new MemoryStream())
                    using (var w = new BinaryWriter(body, Encoding.UTF8))
                    {
                        w.Write((byte)1);
                        w.Write("Microsoft.Xna.Framework.Content.Texture2DReader");
                        w.Write(0);
                        w.Write((byte)0);
                        w.Write((byte)1);
                        w.Write(0);
                        w.Write(image.Width);
                        w.Write(image.Height);
                        w.Write(1);
                        w.Write(checked(image.Width * image.Height * 4));
                        var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        try
                        {
                            byte[] row = new byte[image.Width * 4];
                            for (int y = 0; y < image.Height; y++)
                            {
                                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                                for (int x = 0; x < row.Length; x += 4)
                                {
                                    byte swap = row[x];
                                    row[x] = row[x + 2];
                                    row[x + 2] = swap;
                                }

                                w.Write(row);
                            }
                        }
                        finally
                        {
                            image.UnlockBits(data);
                        }

                        w.Flush();
                        var header = new BinaryWriter(stream, Encoding.UTF8, true);
                        header.Write(new byte[]{88, 78, 66, 119, 5, 0});
                        header.Write(checked((int)body.Length + 10));
                        body.Position = 0;
                        body.CopyTo(stream);
                        header.Flush();
                    }
            });
        }
    }
}
