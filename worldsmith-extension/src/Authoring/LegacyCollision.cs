using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("WorldsmithExtension.Engine")]

namespace WorldsmithExtension
{
    internal static class LegacyCollision
    {
        internal struct LegacyCell
        {
            internal Color Colour;
            internal Rectangle Bounds;
            internal int X,Y;
        }
        internal static List<LegacyCell> LegacyCells(Bitmap sample,Bitmap sizes,int originX,int originY)
        {
            if(sample.Width!=62||sample.Height!=47||originX<0||originY<0||originX>sizes.Width-60||originY>sizes.Height-45)
                throw new InvalidDataException("Legacy collision preview needs one 60x45 cell and its neighbour border.");
            int[] colours=Argb(sample,new Rectangle(1,1,60,45)),dimensions=Argb(sizes,new Rectangle(originX,originY,60,45));
            var cells=new List<LegacyCell>();
            for(int y=0;y<45;y++)for(int x=0;x<60;x++)
            {
                Color colour=Color.FromArgb(colours[y*60+x]),size=Color.FromArgb(dimensions[y*60+x]);
                if(colour.A==0||colour.ToArgb()==Color.Magenta.ToArgb()||size.R>63||size.G==0||size.B==0)continue;
                int dx=size.R%8,dy=size.R/8;
                // MBS packs the offset in red, then clips width and height to the cell
                cells.Add(new LegacyCell{Colour=colour,X=x,Y=y,Bounds=new Rectangle(x*8+dx,y*8+dy,Math.Min(size.G,8-dx),Math.Min(size.B,8-dy))});
            }
            return cells;
        }
        private static int[] Argb(Bitmap image,Rectangle region)
        {
            var data=image.LockBits(region,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try
            {
                var pixels=new int[checked(region.Width*region.Height)];
                // copy one bounded screen, not thousands of GDI calls for its pixels
                for(int y=0;y<region.Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,checked(y*data.Stride)),pixels,y*region.Width,region.Width);
                return pixels;
            }
            finally{image.UnlockBits(data);}
        }
    }
}
