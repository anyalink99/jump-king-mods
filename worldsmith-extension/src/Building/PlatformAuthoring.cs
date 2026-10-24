using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MegaMappingExpansion;

namespace WorldsmithExtension
{
    internal static class PlatformAuthoring
    {
        internal const string Example="<MovingPlatforms version=\"1\">\n  <Motion id=\"lift\" axis=\"vertical\" distance=\"-96\" speed=\"40\" wait=\"0.5\" />\n  <Profile id=\"lift\" motion=\"lift\" />\n  <Paint color=\"#60A0D0\" profile=\"lift\" />\n  <Platform id=\"lift-1\" profile=\"lift\" screen=\"1\" rect=\"120,280,48,8\" />\n</MovingPlatforms>";
        internal static MovingPlatformPlan Validate(string root,string text,int screens,bool sample=true)
        {
            var result=MovingPlatformPlan.Parse(text,screens);
            if(sample)result.AddPainted(Cells(root,result,screens));
            PlatformImageScan.ValidateBounds(result,screens);
            foreach(var profile in result.Profiles.Values.Where(p=>p.Texture!=""&&p.Texture!="none"))
            {
                string path=Path.Combine(root,profile.Texture);Files.Relative(root,path);Files.RequirePhysicalPath(path);
                if(!File.Exists(path))throw new FileNotFoundException("Platform texture is missing",path);
                int width=0,height=0;
                if(Path.GetExtension(path).Equals(".xnb",StringComparison.OrdinalIgnoreCase))CollisionXnb.ReadTexture(path,(reader,w,h)=>{width=w;height=h;reader.BaseStream.Seek((long)w*h*4,SeekOrigin.Current);});
                else using(var image=Image.FromFile(path)){width=image.Width;height=image.Height;}
                if(width<1||height<1||width>16384||height>16384||width%profile.Columns!=0||height%profile.Rows!=0)throw new InvalidDataException(profile.Id+": texture dimensions do not match the animation grid");
            }
            return result;
        }
        internal static List<PlatformCell> Cells(string root,MovingPlatformPlan plan,int screens)
        {
            var layout=Layout.Read(root,screens);string source=Path.Combine(root,layout.Pixel?"level+.png":layout.Source=="atlas"?"level.png":"visual_level.png");
            if(!File.Exists(source))return new List<PlatformCell>();
            using(var image=Collision.Read(source))
            {
                MapLayout format=layout.Pixel?layout.PixelLayout():new MapLayout{Screens=screens,Source=layout.Source,Columns=layout.Source=="atlas"?image.Width/60:1,Rows=layout.Source=="atlas"?image.Height/45:screens,CellWidth=60,CellHeight=45};
                if(layout.Pixel&&image.Width!=format.ImageWidth||image.Height<format.ImageHeight)throw new InvalidDataException("Platform collision image dimensions do not match the layout");
                var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                try
                {
                    var row=new int[image.Width];int previous=-1;
                    return PlatformImageScan.Collect(plan,format,(x,y)=>{if(y!=previous){Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);previous=y;}return unchecked((uint)row[x]);});
                }
                finally{image.UnlockBits(data);}
            }
        }
        internal static void Save(string root,string text,int screens)
        {
            if(Operations.Current.Busy)throw new InvalidOperationException("Wait for the current build before saving platforms");
            Validate(root,text,screens);Files.Text(Path.Combine(root,MovingPlatformPlan.PathName),text);
        }
    }
}
