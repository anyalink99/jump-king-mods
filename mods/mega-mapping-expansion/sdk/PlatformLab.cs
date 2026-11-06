using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MegaMappingExpansion
{
    // ordinary Worldsmith sources; the stock template supplies native settings
    internal static class PlatformLab
    {
        private static readonly int[][] Stone={
            new[]{0,0,43,60,2},new[]{0,0,0,60,1},new[]{0,0,1,1,42},
            new[]{0,36,22,10,1},
            new[]{1,0,43,60,2},new[]{1,0,0,60,1},new[]{1,59,1,1,42}};
        private static int Main(string[] args)
        {
            try
            {
                if(args.Length!=3)throw new ArgumentException("Usage: PlatformLab <Worldsmith folder> <example folder> <new map folder>");
                string root=Path.GetFullPath(args[2]);if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any())throw new IOException("Choose an empty map folder");
                Copy(Path.Combine(args[0],"Resources/Templates/Level"),root);Copy(args[1],root);
                var settings=XDocument.Load(Path.Combine(root,"level_settings.xml"));var about=settings.Root.Element("About");about.Element("title").Value="Moving Platform Lab";
                // the third room is sealed, with no links from either test room
                about.Element("ending_screen").Value="3";about.Element("StartData").ReplaceWith(new XElement("StartData",new XElement("Position",new XElement("X",240),new XElement("Y",316)),new XElement("Velocity",new XElement("X",0),new XElement("Y",0))));settings.Save(Path.Combine(root,"level_settings.xml"));
                using(var collision=new Bitmap(180,45,PixelFormat.Format32bppArgb))
                using(var background=new Bitmap(960,360,PixelFormat.Format32bppArgb))
                using(var terrain=new Bitmap(960,360,PixelFormat.Format32bppArgb))
                {
                    foreach(var r in Stone)for(int y=r[2];y<r[2]+r[4];y++)for(int x=r[1];x<r[1]+r[3];x++)collision.SetPixel(r[0]*60+x,y,Color.Black);
                    for(int y=0;y<45;y++)for(int x=120;x<180;x++)collision.SetPixel(x,y,Color.Black);
                    for(int x=6;x<12;x++)collision.SetPixel(x,29,Color.FromArgb(208,96,160));for(int x=12;x<24;x++)collision.SetPixel(x,29,Color.FromArgb(208,97,160));
                    PaintBackground(background);PaintTerrain(terrain,collision);
                    using(var ink=Graphics.FromImage(background))
                    {
                        Ornament(ink,132,164,Color.FromArgb(49,66,80));Ornament(ink,720,168,Color.FromArgb(62,53,82));
                        using(var rail=new Pen(Color.FromArgb(37,67,73)))
                        {ink.DrawLine(rail,48,224,48,320);ink.DrawLine(rail,324,144,324,240);ink.DrawLine(rail,444,128,444,320);}
                        using(var rail=new Pen(Color.FromArgb(44,72,78))){rail.DashPattern=new[]{2f,5f};ink.DrawLine(rail,212,304,276,240);}
                        using(var font=new Font("Consolas",9,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(Color.FromArgb(147,168,182)))
                        {ink.DrawString("01 / LIFTS",font,brush,20,20);ink.DrawString("02 / COMPOUND & CIRCUIT",font,brush,500,20);}
                    }
                    using(var ink=Graphics.FromImage(terrain))
                    {
                        using(var ceiling=Image.FromFile(Path.Combine(root,"props/platform-lab/ceiling-static.png")))
                        {
                            if(ceiling.Width!=80||ceiling.Height!=8)throw new InvalidDataException("ceiling-static.png must be 80x8 to match its collision");
                            ink.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;ink.DrawImageUnscaled(ceiling,288,176);ink.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceOver;
                        }
                        Crystal(ink,384,116,Color.FromArgb(131,219,208));Crystal(ink,704,112,Color.FromArgb(178,154,218));
                        ink.FillRectangle(Brushes.Goldenrod,88,340,32,4);
                        ink.FillRectangle(Brushes.Goldenrod,760,340,32,4);
                        using(var font=new Font("Consolas",8,FontStyle.Regular,GraphicsUnit.Pixel))
                        {ink.DrawString("REVERSE CIRCUIT",font,Brushes.Goldenrod,80,348);ink.DrawString("REVERSE CIRCUIT",font,Brushes.Goldenrod,736,348);}
                        using(var pen=new Pen(Color.FromArgb(219,180,143)))
                        {ink.DrawLines(pen,new[]{new Point(453,324),new Point(460,328),new Point(453,332)});ink.DrawLines(pen,new[]{new Point(507,324),new Point(500,328),new Point(507,332)});}
                    }
                    collision.Save(Path.Combine(root,"level+.png"),ImageFormat.Png);
                    SaveRooms(root,"background",background);SaveRooms(root,"midground",terrain);
                    using(var preview=(Bitmap)background.Clone())using(var ink=Graphics.FromImage(preview))
                    {ink.DrawImageUnscaled(terrain,0,0);preview.Save(Path.Combine(root,"platform-lab-preview.png"),ImageFormat.Png);}
                }
                AddOcclusion(root);
                Console.WriteLine("[OK] Worldsmith source map: "+root);return 0;
            }
            catch(Exception error){Console.Error.WriteLine(error);return 1;}
        }
        private static void PaintBackground(Bitmap image)
        {
            for(int y=0;y<360;y++)for(int x=0;x<960;x++)
            {
                double u=(x%480-240)/260.0,v=(y-155)/220.0;int band=(int)Math.Max(0,Math.Floor((1-u*u-v*v)*8));
                var color=Color.FromArgb(10+band,14+band,25+band*2);
                int diagonal=(x-(int)(y*.46)+960)%190;
                if(diagonal<38)color=Color.FromArgb(19,27,43);else if(diagonal==38)color=Color.FromArgb(27,37,55);
                if(Hash(x,y)%1973==0)color=Color.FromArgb(47,60,76);
                image.SetPixel(x,y,color);
            }
        }
        private static void PaintTerrain(Bitmap image,Bitmap collision)
        {
            Func<int,int,bool> solid=(x,y)=>x>=0&&x<960&&y>=0&&y<360&&collision.GetPixel(x/8,y/8).ToArgb()==Color.Black.ToArgb();
            for(int y=0;y<360;y++)for(int x=0;x<960;x++)
            {
                if(!solid(x,y))continue;
                int band=(int)Math.Floor((Math.Sin(x*.016+y*.009)+1)*3),grain=Hash(x,y)%5-2;
                bool vein=Math.Sin(x*.041+y*.067+Math.Sin(x*.018)*1.5)>.91;
                int facet=(x+y*2)%137<27?5:0;
                var color=Color.FromArgb(32+band*3+grain+facet,39+band*3+grain+(vein?5:0),56+band*4+grain+(vein?8:0)+facet);
                if(y%24==0||(x+(y/24%2)*40-y%24/2)%80==0)color=Color.FromArgb(22,29,44);
                if(y>0&&!solid(x,y-1)||x>0&&!solid(x-1,y))color=Color.FromArgb(111,136,151);
                else if(y<359&&!solid(x,y+1))color=Color.FromArgb(26,33,49);
                image.SetPixel(x,y,color);
            }
        }
        private static int Hash(int x,int y){unchecked{return (int)((uint)(x*374761393+y*668265263)^((uint)x*1274126177U)>>13)&0x7fffffff;}}
        private static void Ornament(Graphics ink,int x,int y,Color color)
        {
            using(var pen=new Pen(color))
            {
                foreach(int radius in new[]{62,70,96})ink.DrawArc(pen,x-radius,y-radius,radius*2,radius*2,25,275);
                for(int n=0;n<24;n++){double angle=n*Math.PI/12;int px=x+(int)Math.Round(Math.Cos(angle)*91),py=y+(int)Math.Round(Math.Sin(angle)*91);ink.DrawLine(pen,px,py,px+2,py-2);}
                ink.DrawPolygon(pen,new[]{new Point(x,y-19),new Point(x+12,y),new Point(x,y+19),new Point(x-12,y)});
            }
        }
        private static void Crystal(Graphics ink,int x,int y,Color color)
        {
            using(var chain=new Pen(Color.FromArgb(59,73,89)))ink.DrawLine(chain,x,8,x,y-22);
            using(var face=new SolidBrush(Color.FromArgb(color.R/2,color.G/2,color.B/2)))ink.FillPolygon(face,new[]{new Point(x,y-18),new Point(x+10,y),new Point(x,y+18),new Point(x-10,y)});
            using(var edge=new Pen(color)){ink.DrawPolygon(edge,new[]{new Point(x,y-18),new Point(x+10,y),new Point(x,y+18),new Point(x-10,y)});ink.DrawLines(edge,new[]{new Point(x,y-18),new Point(x+2,y),new Point(x,y+18)});}
        }
        private static void SaveRooms(string root,string layer,Bitmap scene)
        {
            string target=Path.Combine(root,"screens/"+layer);Directory.CreateDirectory(target);
            string prefix=layer=="background"?"bg":"";
            for(int i=0;i<2;i++)using(var image=scene.Clone(new Rectangle(i*480,0,480,360),PixelFormat.Format32bppArgb))image.Save(Path.Combine(target,prefix+(i+1)+".png"),ImageFormat.Png);
            using(var hidden=new Bitmap(480,360))using(var ink=Graphics.FromImage(hidden)){ink.Clear(Color.FromArgb(16,20,32));hidden.Save(Path.Combine(target,prefix+"3.png"),ImageFormat.Png);}
        }
        private static XElement Value(string name,string value){return new XElement("Value",new XAttribute("name",name),new XAttribute("value",value));}
        private static void AddOcclusion(string root)
        {
            var path=Path.Combine(root,"mapping_settings.xml");var scene=XDocument.Load(path);var components=scene.Root.Element("Components");
            for(int i=0;i<Stone.Length;i++)
            {
                var r=Stone[i];string id="stone-"+i,w=(r[3]*8).ToString(),h=(r[4]*8).ToString();
                components.Add(new XElement("Component",new XAttribute("id",id+"-position"),new XAttribute("kind","transform"),Value("position",r[1]*8+","+r[2]*8),Value("screen",(r[0]+1).ToString())));
                components.Add(new XElement("Component",new XAttribute("id",id),new XAttribute("kind","receiver"),new XAttribute("tags","lab-stone"),Value("transform",id+"-position"),Value("mode","occlusion"),Value("outline","0,0;"+w+",0;"+w+","+h+";0,"+h)));
            }
            scene.Save(path);
        }
        private static void Copy(string source,string target)
        {
            Directory.CreateDirectory(target);foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
            foreach(string folder in Directory.GetDirectories(source))Copy(folder,Path.Combine(target,Path.GetFileName(folder)));
        }
    }
}
