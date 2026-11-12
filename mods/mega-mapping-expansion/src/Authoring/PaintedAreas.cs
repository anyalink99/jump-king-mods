using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MegaMappingExpansion.Construction
{
    // expand geometry before recipes and cache hashing, so edits to pixels invalidate both
    internal static class PaintedAreas
    {
        private static readonly Regex Token=new Regex(@"\$\{paint\.([a-zA-Z]+)\}");
        internal sealed class Area {internal int Screen,X,Y,Width,Height;}
        internal static uint Color(string value)
        {
            uint rgb;if(value==null||value.Length!=7||value[0]!='#'||!uint.TryParse(value.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb))throw new InvalidDataException("Paint color requires #RRGGBB");
            return 0xff000000U|rgb;
        }
        internal static HashSet<uint> Palette(XDocument document)
        {
            var result=new HashSet<uint>();if(document==null)return result;
            foreach(var paint in document.Descendants("Paint").Where(p=>p.HasElements))
            {
                var consume=(string)paint.Attribute("consume")??"true";
                if(consume!="true"&&consume!="false")throw new InvalidDataException("Paint consume requires true or false");
                uint color=Color((string)paint.Attribute("color"));if(consume=="true")result.Add(color);
            }
            return result;
        }
        internal static XDocument ExpandText(string text,string root)
        {
            using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000}))
                return Expand(XDocument.Load(reader),root);
        }
        internal static XDocument Expand(XDocument source,string root,MapLayout suppliedLayout=null)
        {
            var document=new XDocument(source);var paints=document.Descendants("Paint").Where(p=>p.HasElements).ToArray();
            if(paints.Length==0)return document;
            var layout=suppliedLayout??MapLayout.ReadForScreens(root,169,false);
            if(layout==null||!layout.Pixel)throw new InvalidDataException("Paint templates require MapLayout version 2 with atlas or strip source");
            if(paints.Length>128)throw new InvalidDataException("At most 128 Paint templates per document");
            uint[] pixels=ReadPixels(root,layout);var ids=new HashSet<string>(StringComparer.Ordinal);int total=0;long nodes=0;
            var regions=new Dictionary<string,List<Area>>(StringComparer.Ordinal);
            foreach(var paint in paints)
            {
                if(paint.Ancestors("Paint").Any()||paint.Ancestors("Recipe").Any())throw new InvalidDataException("Paint templates belong outside recipes and cannot nest");
                foreach(var a in paint.Attributes())if(!new[]{"id","color","mode","consume"}.Contains(a.Name.ToString()))throw new InvalidDataException("Unknown Paint attribute: "+a.Name);
                string id=(string)paint.Attribute("id"),mode=(string)paint.Attribute("mode")??"rectangles";
                if(string.IsNullOrWhiteSpace(id)||id.Length>64||!ids.Add(id)||!Regex.IsMatch(id,@"^[a-zA-Z0-9_.-]+$"))throw new InvalidDataException("Paint requires a unique id of 1..64 letters, digits, dots, hyphens or underscores");
                Palette(new XDocument(new XElement("Root",new XElement(paint))));
                uint color=Color((string)paint.Attribute("color"));string cacheKey=color+":"+mode;List<Area> areas;
                if(!regions.TryGetValue(cacheKey,out areas))regions.Add(cacheKey,areas=Scan(layout,pixels,color,mode));
                if((total+=areas.Count)>1024)throw new InvalidDataException("Paint expansion exceeds 1024 areas");
                if((nodes+=(long)areas.Count*paint.Descendants().Count())>65536)throw new InvalidDataException("Paint expansion exceeds 65536 XML elements");
                foreach(var area in areas)
                {
                    string key=id+".s"+area.Screen+".x"+area.X+".y"+area.Y;
                    var values=new Dictionary<string,string>{
                        {"id",key},{"screen",S(area.Screen)},{"x",S(area.X)},{"y",S(area.Y)},
                        {"width",S(area.Width)},{"height",S(area.Height)},
                        {"position",S(area.X+area.Width/2.0)+","+S(area.Y+area.Height/2.0)},
                        {"corner",S(area.X)+","+S(area.Y)},{"size",S(area.Width)+","+S(area.Height)},
                        {"rect",S(area.X)+","+S(area.Y)+","+S(area.Width)+","+S(area.Height)}};
                    foreach(var child in paint.Elements())
                    {
                        var copy=new XElement(child);
                        foreach(var a in copy.DescendantsAndSelf().Attributes())a.Value=Token.Replace(a.Value,m=>{
                            string value;if(!values.TryGetValue(m.Groups[1].Value,out value))throw new InvalidDataException("Unknown painted-area token: "+m.Value);return value;});
                        if(paint.Parent.Name=="Paints")
                        {
                            if(copy.HasAttributes||!new[]{"Components","Assets","Graph","Bindings","Variables","Effects","Rules","Exports","Trees","Instances"}.Contains(copy.Name.ToString()))throw new InvalidDataException("Paints templates accept scene sections without attributes");
                            var section=document.Root.Element(copy.Name);if(section==null){section=new XElement(copy.Name);document.Root.Add(section);}
                            section.Add(copy.Elements().Select(e=>new XElement(e)));
                        }
                        else paint.AddBeforeSelf(copy);
                    }
                }
                paint.Remove();
            }
            foreach(var container in document.Descendants("Paints").ToArray())
            {if(container.HasAttributes||container.HasElements)throw new InvalidDataException("Paints accepts only Paint templates");container.Remove();}
            return document;
        }
        private static string S(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
        internal static uint[] ReadPixels(string root,MapLayout layout)
        {
            string path=Path.Combine(root,layout.Image+".xnb");uint[] values=null;
            if(File.Exists(path))CollisionXnb.Read(path,layout,(reader,count)=>{
                values=new uint[count];for(int i=0;i<count;i++){uint abgr=reader.ReadUInt32();values[i]=(abgr&0xff00ff00U)|((abgr&255)<<16)|((abgr>>16)&255);}
            });
            else
            {
                path=Path.Combine(root,layout.Image+".png");if(!File.Exists(path))throw new FileNotFoundException("Paint collision image is missing",path);
                using(var bitmap=new Bitmap(path))
                {layout.ValidateAtlas(bitmap.Width,bitmap.Height);values=new uint[bitmap.Width*bitmap.Height];for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)values[y*bitmap.Width+x]=unchecked((uint)bitmap.GetPixel(x,y).ToArgb());}
            }
            layout.ValidateSampling((x,y)=>values[y*layout.ImageWidth+x]);return values;
        }
        internal static List<Area> Scan(MapLayout layout,uint[] pixels,uint color,string mode)
        {
            if(mode!="pixels"&&mode!="rectangles")throw new InvalidDataException("Paint mode requires pixels or rectangles");
            var result=new List<Area>();
            for(int screen=0;screen<layout.Screens;screen++)
            {
                int ox,oy;layout.Origin(screen,out ox,out oy);int width=layout.Width(screen),height=layout.Height(screen),stride=layout.CellWidth/width,scale=480/width;
                var previous=new Dictionary<string,Area>();
                for(int y=0;y<height;y++)
                {
                    var row=new Dictionary<string,Area>();
                    for(int x=0;x<width;x++)
                    {
                        if(pixels[(oy+y*stride)*layout.ImageWidth+ox+x*stride]!=color)continue;
                        int start=x;if(mode=="rectangles")while(x+1<width&&pixels[(oy+y*stride)*layout.ImageWidth+ox+(x+1)*stride]==color)x++;
                        string key=start+":"+(x-start+1);Area old;
                        if(mode=="rectangles"&&previous.TryGetValue(key,out old)){old.Height+=scale;row.Add(key,old);}
                        else{var area=new Area{Screen=screen+1,X=start*scale,Y=y*scale,Width=(x-start+1)*scale,Height=scale};result.Add(area);row.Add(key,area);}
                        if(result.Count>1024)throw new InvalidDataException("Paint exceeds 1024 areas; simplify the image");
                    }
                    previous=row;
                }
            }
            return result;
        }
    }
}
