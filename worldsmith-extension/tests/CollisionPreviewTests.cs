using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using MegaMappingExpansion;

namespace WorldsmithExtension
{
    internal static class CollisionPreviewTests
    {
        internal static void Run(Action<bool, string> Assert)
        {
            foreach (int width in new[] { 60, 120, 240, 480 })
            foreach (string source in new[] { "atlas", "strip" })
            {
                var format = new MapLayout { Version=2, Screens=3, Columns=source=="strip"?1:3, Rows=source=="strip"?3:1,
                    CellWidth=width, CellHeight=width*3/4, Source=source };
                format.Resolutions.Add(0,60);
                format.Resolutions.Add(1,Math.Max(60,width/2));
                using (var image = new Bitmap(format.ImageWidth, format.ImageHeight, PixelFormat.Format32bppArgb))
                {
                    var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                    try
                    {
                        int[] palette={Color.Black.ToArgb(),Color.Red.ToArgb(),Color.Cyan.ToArgb(),Color.Magenta.ToArgb(),Color.FromArgb(0,17,29,43).ToArgb(),Color.Yellow.ToArgb()};
                        int[] row=new int[width];
                        for(int screen=0;screen<3;screen++)
                        {
                            int ox,oy;format.Origin(screen,out ox,out oy);
                            int stride=width/format.Width(screen);
                            for(int y=0;y<format.CellHeight;y++)
                            {
                                for(int x=0;x<width;x++)row[x]=palette[(x/stride+3*(y/stride)+screen)%palette.Length];
                                Marshal.Copy(row,0,IntPtr.Add(data.Scan0,(oy+y)*data.Stride+ox*4),row.Length);
                            }
                        }
                    }
                    finally { image.UnlockBits(data); }
                    for(int screen=0;screen<3;screen++)
                    using(var actual=CollisionPreview.Sample(image,format,screen))
                    using(var expected=Reference(image,format,screen))
                    {
                        Equal(actual,expected);
                        Assert(true,"pixel sample matches scalar coordinates, borders and alpha: "+source+" / "+width+" / "+screen);
                        var area=new Rectangle(1,1,actual.Width-2,actual.Height-2);
                        var slopes=CollisionPreview.Slopes(actual,area);
                        var reference=(from y in Enumerable.Range(0,area.Height) from x in Enumerable.Range(0,area.Width)
                            where expected.GetPixel(x+1,y+1).ToArgb()==Color.Red.ToArgb() select new Point(x,y)).ToArray();
                        Assert(slopes.SequenceEqual(reference),"slope scan preserves marker order and cropped coordinates");
                    }
                }
            }
            int strideBytes=60*4,height=45;
            IntPtr storage=Marshal.AllocHGlobal(strideBytes*height);
            try
            {
                using(var image=new Bitmap(60,height,-strideBytes,PixelFormat.Format32bppArgb,IntPtr.Add(storage,strideBytes*(height-1))))
                {
                    using(var graphics=Graphics.FromImage(image))graphics.Clear(Color.Cyan);
                    image.SetPixel(17,21,Color.Red);
                    var format=new MapLayout{Version=2,Screens=1,Columns=1,Rows=1,CellWidth=60,CellHeight=45,Source="atlas"};
                    using(var actual=CollisionPreview.Sample(image,format,0))using(var expected=Reference(image,format,0))Equal(actual,expected);
                    Assert(CollisionPreview.Slopes(image,new Rectangle(3,5,40,30)).Single()==new Point(14,16),"negative bitmap stride and cropped slope scan");
                }
            }
            finally { Marshal.FreeHGlobal(storage); }
        }

        static Bitmap Reference(Bitmap image,MapLayout format,int screen)
        {
            int width=format.Width(screen),height=format.Height(screen);
            var sample=new Bitmap(width+2,height+2);
            for(int y=-1;y<=height;y++)for(int x=-1;x<=width;x++)
            {
                Color colour=Color.Transparent;
                if(x<0||x>=width)colour=Color.Black;
                else
                {
                    int px,py;
                    if(format.Coordinate(screen,x,y,out px,out py))
                    {
                        colour=image.GetPixel(px,py);
                        if(colour.A==0||colour.ToArgb()==Color.Magenta.ToArgb())colour=Color.Transparent;
                    }
                }
                sample.SetPixel(x+1,y+1,colour);
            }
            return sample;
        }
        static void Equal(Bitmap actual,Bitmap expected)
        {
            for(int y=0;y<expected.Height;y++)for(int x=0;x<expected.Width;x++)
                if(actual.GetPixel(x,y).ToArgb()!=expected.GetPixel(x,y).ToArgb())throw new Exception("Pixel sample differs at "+x+","+y);
        }
    }
}
