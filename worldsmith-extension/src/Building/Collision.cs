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
        internal static int SourceScreens(string root)
        {
            var declared = Layout.DeclaredPixelLayout(root);
            if (declared != null) return declared.Screens;
            string source = Layout.CollisionSource(root);
            string path = Path.Combine(root, source == "atlas" ? "level.png" : "visual_level.png");
            if (!File.Exists(path)) return 0;
            using (var image = Read(path))
                return source == "atlas" ? (image.Width / 60) * (image.Height / 45) : image.Height / 45;
        }
        internal static void ValidatePixel(Bitmap image,Layout layout)
        {
            var format=layout.PixelLayout();format.ValidatePixel();format.ValidateAtlas(image.Width,image.Height);
            var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try
            {
                byte[] row=new byte[image.Width*4];
                for(int y=0;y<image.Height;y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);
                    for(int x=3;x<row.Length;x+=4)if(row[x]!=0&&row[x]!=255)
                        throw new InvalidDataException("level+.png contains partial alpha at "+(x/4)+","+y+". Use opaque colours or full transparency; turn off anti-aliasing.");
                }
                format.ValidateSampling((x,y)=>MegaMappingExpansion.CollisionXnb.Colour(unchecked((uint)Marshal.ReadInt32(IntPtr.Add(data.Scan0,y*data.Stride),x*4))));
            }
            finally{image.UnlockBits(data);}
        }
        internal static void ValidatePixelXnb(string path,Layout layout)
        {
            MegaMappingExpansion.CollisionXnb.Read(path,layout.PixelLayout(),delegate(BinaryReader reader,int count){
                for(int i=0;i<count;i++)MegaMappingExpansion.CollisionXnb.Colour(reader.ReadUInt32());
            });
        }
        internal static void Scaffold(string path,int side=13)
        {using(var empty=new Bitmap(side*60,side*45,PixelFormat.Format32bppArgb))Xnb(empty,path);}
        internal static bool LegacyLayout(string root,Layout layout,bool infer=true)
        {
            string path=Path.Combine(root,"level+.png");if(layout.Pixel||!File.Exists(path))return false;
            using(var image=Read(path))
            {
                if(image.Width%60!=0||image.Height%45!=0||image.Width/60!=image.Height/45)
                    throw new InvalidDataException("Legacy More Block Sizes needs a square 60x45-cell level+ atlas. Use MapLayout version 2 for a rectangular or full-resolution grid.");
                layout.Side=image.Width/60;
                if(infer&&!File.Exists(Layout.Metadata(root)))layout.Screens=layout.Side*layout.Side;
                layout.Validate();
                string sizes=Path.Combine(root,"sizes.png");
                if(File.Exists(sizes))using(var value=Read(sizes))if(value.Size!=image.Size)throw new InvalidDataException("More Block Sizes: sizes.png must match level+.png dimensions.");
            }
            return true;
        }
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
            if (atlas.Width % 60 != 0 || atlas.Height % 45 != 0 || (long)(atlas.Width / 60) * (atlas.Height / 45) < layout.Screens)
                throw new InvalidDataException("Collision atlas must contain whole 60x45 cells and enough cells for every authored screen.");
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
            // keep the source's column order when packing the game's square grid
            int rows = source.Height / 45;
            var atlas = new Bitmap(layout.Side * 60, layout.Side * 45, PixelFormat.Format32bppArgb);
            for (int i = 0; i < layout.Screens; i++)
                Copy(source, new Rectangle((i / rows) * 60, (i % rows) * 45, 60, 45), atlas, (i / layout.Side) * 60, (i % layout.Side) * 45);
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
            WriteXnb(image, path, false);
        }

        internal static void PixelXnb(string input, string output, Layout layout)
        {
            // this file only stays open for compilation; previews still own detached bitmaps
            using (var image = new Bitmap(input))
            {
                ValidatePixel(image, layout);
                WriteXnb(image, output, true);
            }
        }

        static void WriteXnb(Bitmap image, string path, bool keyed)
        {
            Files.Atomic(path, stream =>
            {
                using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
                    {
                        w.Write(new byte[]{88, 78, 66, 119, 5, 0});
                        w.Write(0);
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
                                    // match the native color key and premultiplication for validated 0/255 alpha
                                    if (keyed && (row[x + 3] == 0 || (row[x] == 255 && row[x + 1] == 0 && row[x + 2] == 255)))
                                        row[x] = row[x + 1] = row[x + 2] = row[x + 3] = 0;
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
                        int length = checked((int)stream.Position);
                        stream.Position = 6;
                        w.Write(length);
                        w.Flush();
                    }
            });
        }
    }
}
