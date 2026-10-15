using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace MegaMappingExpansion
{
    // integer links augment the native TeleportLink array. native movement,
    // relocation events and third-party screen-query consumers keep one truth
    internal sealed class MapLayout
    {
        internal const string RelativePath=MappingFiles.Settings;
        internal int Screens,AtlasSide;
        internal int Version=2, Columns, Rows, CellWidth=60, CellHeight=45;
        internal string Source="atlas";
        internal readonly Dictionary<int,int> Resolutions=new Dictionary<int,int>();
        internal int Width(int screen) {int value;return Resolutions.TryGetValue(screen,out value)?value:CellWidth;}
        internal int Height(int screen) {return Width(screen)*3/4;}
        internal bool Pixel { get { return Source!="native"; } }
        internal int ImageWidth { get { return (Source=="strip"?1:Columns)*CellWidth; } }
        internal int ImageHeight { get { return (Source=="strip"?Screens:Rows)*CellHeight; } }
        internal void Origin(int screen,out int x,out int y)
        {
            if(screen<0||screen>=Screens)throw new ArgumentOutOfRangeException("screen");
            x=Source=="strip"?0:(screen/Rows)*CellWidth;
            y=Source=="strip"?(Screens-screen-1)*CellHeight:(screen%Rows)*CellHeight;
        }
        internal void ValidatePixel()
        {
            if(Screens<1||Screens>4096||Columns<1||Columns>4096||Rows<1||Rows>4096||Screens>(long)Columns*Rows)
                throw new InvalidDataException("MapLayout: invalid screen count or grid capacity (maximum 4096 screens).");
            if(Source!="atlas"&&Source!="strip")throw new InvalidDataException("MapLayout source must be atlas or strip.");
            if((CellWidth!=60&&CellWidth!=120&&CellWidth!=240&&CellWidth!=480)||CellHeight*4!=CellWidth*3)
                throw new InvalidDataException("MapLayout cells must be 60x45, 120x90, 240x180 or 480x360.");
            if(Source=="strip"&&(Columns!=1||Rows!=Screens))throw new InvalidDataException("A strip needs columns=1 and rows=screens.");
            if((long)ImageWidth*ImageHeight*4>512L*1024*1024)throw new InvalidDataException("Collision source exceeds the 512 MiB decoded pixel limit.");
            foreach(var pair in Resolutions)
                if(pair.Key<0||pair.Key>=Screens||(pair.Value!=60&&pair.Value!=120&&pair.Value!=240&&pair.Value!=480)||pair.Value>CellWidth)
                    throw new InvalidDataException("Screen resolution must be a supported scale no finer than its atlas cell.");
        }
        internal bool Coordinate(int screen,int x,int y,out int px,out int py)
        {
            px=py=0;if(screen<0||screen>=Screens)return false;
            int worldX=x*(480/Width(screen)),worldY=y*(360/Height(screen));
            while(worldY<0){worldY+=360;screen++;}while(worldY>=360){worldY-=360;screen--;}
            if(screen<0||screen>=Screens||worldX<0||worldX>=480)return false;
            int ox,oy;Origin(screen,out ox,out oy);
            int scale=480/Width(screen),stride=CellWidth/Width(screen);
            px=ox+(worldX/scale)*stride;py=oy+(worldY/scale)*stride;return true;
        }
        internal void ValidateSampling(Func<int,int,uint> colour)
        {
            foreach(var pair in Resolutions)
            {
                int stride=CellWidth/pair.Value;if(stride==1)continue;
                int ox,oy;Origin(pair.Key,out ox,out oy);
                for(int y=0;y<CellHeight;y++)for(int x=0;x<CellWidth;x++)
                    if(colour(ox+x,oy+y)!=colour(ox+(x/stride)*stride,oy+(y/stride)*stride))
                        throw new InvalidDataException("Screen "+(pair.Key+1)+" declares "+pair.Value+"x"+(pair.Value*3/4)+" resolution, but its "+stride+"x"+stride+" pixel groups differ. Use nearest-neighbour scaling or change the screen resolution.");
            }
        }
        internal readonly Dictionary<int,int[]> Links=new Dictionary<int,int[]>();
        internal TransitionPlan Transitions;

        internal static MapLayout Read(string root) { return ReadForScreens(root,169); }
        internal static MapLayout ReadForScreens(string root, int fallbackScreens)
        {
            string path=Path.Combine(root,MappingFiles.Settings);
            XmlElement entry=MappingFiles.SettingsLayout(root),routes=MappingFiles.Links(root);
            if(entry==null&&routes==null)return null;
            var result=new MapLayout{Version=2,Source="native",Screens=fallbackScreens};
            if(entry!=null)
            {
                Number(entry,"version",2,2);result.Screens=Number(entry,"screens",1,4096);result.Source=entry.GetAttribute("source");
                Attributes(entry,result.Source=="native"?new[]{"version","screens","source"}:new[]{"version","screens","columns","rows","cellWidth","cellHeight","source"});
                if(result.Pixel)
                {
                    result.Columns=Number(entry,"columns",1,4096);result.Rows=Number(entry,"rows",1,4096);result.CellWidth=Number(entry,"cellWidth",60,480);result.CellHeight=Number(entry,"cellHeight",45,360);result.ValidatePixel();
                }
            }
            var children=new List<XmlNode>();if(entry!=null)foreach(XmlNode child in entry.ChildNodes)children.Add(child);
            if(routes!=null)foreach(XmlNode child in routes.ChildNodes)if(!(child is XmlElement)||child.Name=="SideLink")children.Add(child);
            foreach(XmlNode child in children)
            {
                XmlElement link=child as XmlElement;
                if(link==null)
                {
                    if(child.NodeType==XmlNodeType.Text||child.NodeType==XmlNodeType.CDATA)throw new InvalidDataException(path+": unexpected text in layout or transitions.");
                    continue;
                }
                foreach(XmlNode nested in link.ChildNodes)if(nested is XmlElement)throw new InvalidDataException(path+": "+link.Name+" cannot contain child elements.");
                if(result.Pixel&&link.Name=="Resolution")
                {
                    Attributes(link,"screen","width","height");
                    int resolutionScreen=Number(link,"screen",1,result.Screens)-1,width=Number(link,"width",60,480),height=Number(link,"height",45,360);
                    if(height*4!=width*3||result.Resolutions.ContainsKey(resolutionScreen))throw new InvalidDataException("Invalid or duplicate screen resolution.");
                    result.Resolutions.Add(resolutionScreen,width);continue;
                }
                if(link.Name!="SideLink")throw new InvalidDataException(path+": unknown map element "+link.Name);
                Attributes(link,"screen","side","target");
                int screen=Number(link,"screen",1,result.Screens),target=Number(link,"target",1,result.Screens);
                string side=link.GetAttribute("side");if(side!="left"&&side!="right")throw new InvalidDataException(path+": SideLink side must be left or right");
                if(screen==target)throw new InvalidDataException(path+": self-link on screen "+screen);
                int[] pair;if(!result.Links.TryGetValue(screen-1,out pair)){pair=new[]{-1,-1};result.Links.Add(screen-1,pair);}
                int index=side=="left"?0:1;
                if(pair[index]!=-1)throw new InvalidDataException(path+": duplicate "+side+" link on screen "+screen);
                pair[index]=target;
            }
            int linkCount=0;foreach(var pair in result.Links)foreach(int target in pair.Value)if(target>0)linkCount++;
            if(linkCount>512)throw new InvalidDataException("MapLayout supports at most 512 SideLink entries.");
            if(routes!=null)result.Transitions=TransitionPlan.Parse(routes,result.Screens,result.Links);
            if(result.Pixel)result.ValidatePixel();return result;
        }
        private static void Attributes(XmlElement element,params string[] names)
        {foreach(XmlAttribute attribute in element.Attributes)if(Array.IndexOf(names,attribute.Name)<0)throw new InvalidDataException(element.Name+": unknown @"+attribute.Name);}
        private static int Number(XmlElement element,string name,int min,int max)
        {int value;if(!int.TryParse(element.GetAttribute(name),out value)||value<min||value>max)throw new InvalidDataException(element.Name+" @"+name+" must be "+min+".."+max);return value;}
        internal void ValidateAtlas(int width,int height)
        {
            if(Pixel)
            {
                if(width!=ImageWidth||height!=ImageHeight)throw new InvalidDataException("level+ dimensions must be "+ImageWidth+"x"+ImageHeight+" as declared in MapLayout; got "+width+"x"+height+".");
                return;
            }
            if(width<60||height<45||width%60!=0||height%45!=0||width/60!=height/45||Screens>(long)(width/60)*(height/45))throw new InvalidDataException("Native collision needs a square 60x45 atlas with capacity for all authored screens");
            AtlasSide=width/60;
        }
    }
}