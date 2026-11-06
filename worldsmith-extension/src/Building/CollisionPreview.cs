using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using MegaMappingExpansion;

namespace WorldsmithExtension
{
    internal static class CollisionPreview
    {
        internal static Bitmap Sample(Bitmap image, MapLayout format, int screen)
        {
            int width = format.Width(screen), height = format.Height(screen);
            var sample = new Bitmap(width + 2, height + 2, PixelFormat.Format32bppArgb);
            try
            {
                var input = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var output = sample.LockBits(new Rectangle(0, 0, sample.Width, sample.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        int[] row = new int[sample.Width];
                        // neighboring screens can use another scale; map their columns once
                        int[] upper = Columns(format, screen, width, -1), middle = Columns(format, screen, width, 0), lower = Columns(format, screen, width, height);
                        for (int y = -1; y <= height; y++)
                        {
                            row[0] = row[row.Length - 1] = Color.Black.ToArgb();
                            int px, py;
                            bool available = format.Coordinate(screen, 0, y, out px, out py);
                            var origin = available ? IntPtr.Add(input.Scan0, py * input.Stride) : IntPtr.Zero;
                            int[] columns = y < 0 ? upper : y == height ? lower : middle;
                            for (int x = 0; x < width; x++)
                            {
                                int colour = Color.Transparent.ToArgb();
                                if (available && columns[x] >= 0)
                                {
                                    colour = Marshal.ReadInt32(origin, columns[x]);
                                    if ((colour & unchecked((int)0xff000000)) == 0 || colour == Color.Magenta.ToArgb()) colour = Color.Transparent.ToArgb();
                                }
                                row[x + 1] = colour;
                            }
                            Marshal.Copy(row, 0, IntPtr.Add(output.Scan0, (y + 1) * output.Stride), row.Length);
                        }
                    }
                    finally { sample.UnlockBits(output); }
                }
                finally { image.UnlockBits(input); }
                return sample;
            }
            catch { sample.Dispose(); throw; }
        }

        static int[] Columns(MapLayout format, int screen, int width, int y)
        {
            int[] columns = new int[width];
            for (int x = 0; x < width; x++)
            {
                int px, py;
                columns[x] = format.Coordinate(screen, x, y, out px, out py) ? px * 4 : -1;
            }
            return columns;
        }

        internal static List<Point> Slopes(Bitmap sample, Rectangle area)
        {
            var points = new List<Point>();
            var pixels = sample.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] row = new int[area.Width];
                for (int y = 0; y < area.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(pixels.Scan0, y * pixels.Stride), row, 0, row.Length);
                    for (int x = 0; x < row.Length; x++)
                        if (row[x] == Color.Red.ToArgb()) points.Add(new Point(x, y));
                }
            }
            finally { sample.UnlockBits(pixels); }
            return points;
        }
    }
}
