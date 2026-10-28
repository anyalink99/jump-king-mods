using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace MegaMappingExpansion
{
    // shared with Worldsmith; keep native types out of the authoring model
    internal struct PlatformPoint
    {
        internal double X,Y;
        internal PlatformPoint(double x,double y){X=x;Y=y;}
    }
    internal struct PlatformRect
    {
        internal int X,Y,Width,Height;
        internal PlatformRect(int x,int y,int width,int height){X=x;Y=y;Width=width;Height=height;}
    }
    internal sealed class PlatformWaypoint
    {
        internal PlatformPoint Position;
        internal double Wait;
    }
    internal sealed class PlatformMotion
    {
        internal string Id,Mode="pingPong",OnBlocked="wait";
        internal double Speed=60,Phase,StartDistance,BlockedWait=0.2;
        internal bool Playing=true;
        internal readonly List<PlatformWaypoint> Points=new List<PlatformWaypoint>();
    }
    internal sealed class PlatformProfile
    {
        internal string Id,Motion,Collision="solid",Texture="",Fit="tile",Label="",MomentumAxes="both";
        internal double Momentum;
        internal int Columns=1,Rows=1,Frames=1;
        internal double Fps=8;
    }
    internal sealed class PlatformPaint
    {
        internal string Color,Range,Profile;
    }
    internal sealed class PlatformDefinition
    {
        internal string Id,Profile,Group="",Label="";
        internal int Screen=1;
        internal readonly List<PlatformRect> Parts=new List<PlatformRect>();
    }
    internal sealed class PlatformCell
    {
        internal string Color;
        internal int Screen;
        internal PlatformRect Rect;
    }
    internal sealed class MovingPlatformPlan
    {
        internal const string PathName="screens/mega-mapping-expansion/moving-platforms.xml";
        internal const string DefaultText="<MovingPlatforms version='1'><Motion id='horizontal' axis='horizontal' distance='96'/><Motion id='vertical' axis='vertical' distance='-96'/><Profile id='horizontal' motion='horizontal'/><Profile id='vertical' motion='vertical'/><Paint color='#D060A0' range='#D061A0' profile='horizontal'/><Paint color='#60A0D0' range='#61A0D0' profile='vertical'/></MovingPlatforms>";
        internal const int MaximumPlatforms=128,MaximumParts=4096;
        internal readonly Dictionary<string,PlatformMotion> Motions=new Dictionary<string,PlatformMotion>(StringComparer.Ordinal);
        internal readonly Dictionary<string,PlatformProfile> Profiles=new Dictionary<string,PlatformProfile>(StringComparer.Ordinal);
        internal readonly List<PlatformPaint> Paint=new List<PlatformPaint>();
        internal readonly List<PlatformDefinition> Platforms=new List<PlatformDefinition>();
        internal string Identity;
        internal static MovingPlatformPlan Read(string root,int screens)
        {
            string path=Path.Combine(root,PathName);
            return File.Exists(path)?Parse(MappingFiles.Open(path).OuterXml,screens):Defaults();
        }
        internal static MovingPlatformPlan Defaults()
        {
            return Parse(DefaultText,4096);
        }
        internal static MovingPlatformPlan Parse(string text,int screens)
        {
            var result=new MovingPlatformPlan();XElement xml;
            using(var reader=System.Xml.XmlReader.Create(new StringReader(text),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000}))xml=XDocument.Load(reader,LoadOptions.SetLineInfo).Root;
            if(xml==null)throw new InvalidDataException("MovingPlatforms root is missing");
            Attributes(xml,"version");if(xml.Name!="MovingPlatforms"||Required(xml,"version")!="1")throw Error(xml,"expected MovingPlatforms version 1");
            foreach(var item in xml.Elements())
            {
                if(item.Name.NamespaceName!="")throw Error(item,"XML namespaces are not supported");
                switch(item.Name.LocalName)
                {
                    case "Motion":
                        Attributes(item,"id mode speed phase playing axis distance wait onBlocked blockedWait");
                        var motion=new PlatformMotion{Id=Id(item,"id"),Mode=Choice(item,"mode","pingPong","pingPong loop once"),Speed=Number(item,"speed",60,0.01,600),Phase=Number(item,"phase",0,0,86400),Playing=Boolean(item,"playing",true),OnBlocked=Choice(item,"onBlocked","wait","wait reverse stop"),BlockedWait=Number(item,"blockedWait",0.2,0,3600)};
                        double wait=Number(item,"wait",0,0,3600);
                        if(item.Attribute("axis")!=null)
                        {
                            if(item.HasElements)throw Error(item,"axis/distance and Point elements cannot be mixed");
                            string axis=Choice(item,"axis","horizontal","horizontal vertical");double distance=Number(item,"distance",96,-100000,100000);
                            motion.Points.Add(new PlatformWaypoint{Position=new PlatformPoint(0,0),Wait=wait});
                            motion.Points.Add(new PlatformWaypoint{Position=new PlatformPoint(axis=="horizontal"?distance:0,axis=="vertical"?distance:0),Wait=wait});
                        }
                        else
                        {
                            if(item.Attribute("distance")!=null)throw Error(item,"distance requires axis");
                            foreach(var point in item.Elements())
                            {
                                Attributes(point,"x y wait");if(point.Name!="Point"||point.HasElements)throw Error(point,"expected Point");
                                motion.Points.Add(new PlatformWaypoint{Position=new PlatformPoint(Number(point,"x",0,-100000,100000),Number(point,"y",0,-100000,100000)),Wait=Number(point,"wait",wait,0,3600)});
                            }
                        }
                        if(motion.Points.Count<2||motion.Points.Count>64)throw Error(item,"motion requires 2 to 64 points");
                        if(motion.Points[0].Position.X!=0||motion.Points[0].Position.Y!=0)throw Error(item,"the first motion point must be 0,0");
                        for(int i=1;i<motion.Points.Count;i++)if(Distance(motion.Points[i-1].Position,motion.Points[i].Position)<1)throw Error(item,"adjacent motion points must be at least one pixel apart");
                        if(result.Motions.ContainsKey(motion.Id))throw Error(item,"duplicate motion ID");result.Motions.Add(motion.Id,motion);break;
                    case "Profile":
                        Attributes(item,"id motion collision texture fit columns rows frames fps label momentum momentumAxes");if(item.HasElements)throw Error(item,"Profile cannot contain children");
                        var profile=new PlatformProfile{Id=Id(item,"id"),Motion=Id(item,"motion"),Collision=Choice(item,"collision","solid","solid oneWay"),Texture=Optional(item,"texture",""),Fit=Choice(item,"fit","tile","tile stretch native"),Columns=Integer(item,"columns",1,1,64),Rows=Integer(item,"rows",1,1,64),Frames=Integer(item,"frames",1,1,4096),Fps=Number(item,"fps",8,0,120),Label=Caption(item),Momentum=Number(item,"momentum",0,0,4),MomentumAxes=Choice(item,"momentumAxes","both","both horizontal vertical")};
                        if(profile.Frames>profile.Columns*profile.Rows)throw Error(item,"frames exceed texture grid");
                        if(profile.Texture.Length>0)SafeRelative(profile.Texture);
                        if(result.Profiles.ContainsKey(profile.Id))throw Error(item,"duplicate profile ID");result.Profiles.Add(profile.Id,profile);break;
                    case "Paint":
                        Attributes(item,"color range profile");if(item.HasElements)throw Error(item,"Paint cannot contain children");
                        result.Paint.Add(new PlatformPaint{Color=Color(Required(item,"color")),Range=item.Attribute("range")==null?"":Color(Required(item,"range")),Profile=Id(item,"profile")});break;
                    case "Platform":
                        Attributes(item,"id profile screen rect group label");
                        var platform=new PlatformDefinition{Id=Id(item,"id"),Profile=Id(item,"profile"),Screen=Integer(item,"screen",1,1,screens),Group=Optional(item,"group",""),Label=Caption(item)};
                        if(platform.Group!="")CheckId(platform.Group);
                        if(item.Attribute("rect")!=null)platform.Parts.Add(Rect(Required(item,"rect")));
                        foreach(var part in item.Elements()){Attributes(part,"rect");if(part.Name!="Part"||part.HasElements)throw Error(part,"expected Part rect");platform.Parts.Add(Rect(Required(part,"rect")));}
                        if(platform.Parts.Count==0)throw Error(item,"platform requires rect or Part elements");
                        if(result.Platforms.Any(p=>p.Id==platform.Id))throw Error(item,"duplicate platform ID");result.Platforms.Add(platform);break;
                    default:throw Error(item,"unknown moving-platform element");
                }
            }
            if(result.Motions.Count>128||result.Profiles.Count>128||result.Paint.Count>128)throw Error(xml,"too many platform profiles or motions");
            foreach(var p in result.Profiles.Values)if(!result.Motions.ContainsKey(p.Motion))throw Error(xml,"unknown motion: "+p.Motion);
            var colors=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in result.Paint)
            {
                if(!result.Profiles.ContainsKey(p.Profile))throw Error(xml,"unknown paint profile: "+p.Profile);
                foreach(string c in new[]{p.Color,p.Range}.Where(c=>c!=""))if(!colors.Add(c)||Reserved(c))throw Error(xml,"duplicate or native marker color: "+c);
                if(p.Range!=""&&result.Motions[result.Profiles[p.Profile].Motion].Points.Count!=2)throw Error(xml,"range markers require a two-point motion");
            }
            foreach(var p in result.Platforms)if(!result.Profiles.ContainsKey(p.Profile))throw Error(xml,"unknown platform profile: "+p.Profile);
            result.ValidateDefinitions();
            using(var hash=SHA256.Create())result.Identity=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting)))).Replace("-","");
            return result;
        }
        internal void ValidateDefinitions()
        {
            if(Platforms.Count>MaximumPlatforms||Platforms.Sum(p=>p.Parts.Count)>MaximumParts)throw new InvalidDataException("Moving platforms exceed 128 platforms or 4096 rectangles");
            foreach(var group in Platforms.Where(p=>p.Group!="").GroupBy(p=>p.Group))
            {
                if(Platforms.Any(p=>p.Id==group.Key))throw new InvalidDataException("Group IDs must differ from platform IDs: "+group.Key);
                var first=Profiles[group.First().Profile];
                if(group.Any(p=>Profiles[p.Profile].Motion!=first.Motion))throw new InvalidDataException("Synchronized platforms must share a motion: "+group.Key);
            }
        }
        internal bool Owns(string color){return Paint.Any(p=>p.Color==color||p.Range==color);}
        internal static string Color(string value)
        {
            uint rgb;if(value.Length!=7||value[0]!='#'||!uint.TryParse(value.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb))throw new InvalidDataException("Platform colors require #RRGGBB");return value.ToUpperInvariant();
        }
        private static bool Reserved(string color)
        {
            if(new[]{"#000000","#FF0000","#00FFFF","#FFFF00","#FF6A00","#FFFFFF","#00AAAA","#B6FF00","#808080","#FF00FF"}.Contains(color))return true;
            return color.Substring(3)=="FF00"||color.Substring(3)=="00FF";
        }
        internal void AddPainted(IList<PlatformCell> cells)
        {
            cells=Compact(cells);
            foreach(var paint in Paint)
            {
                foreach(var screen in cells.Where(c=>c.Color==paint.Color).GroupBy(c=>c.Screen).OrderBy(c=>c.Key))
                {
                    var remaining=new List<PlatformCell>(screen.OrderBy(c=>c.Rect.Y).ThenBy(c=>c.Rect.X));int index=0;
                    while(remaining.Count>0)
                    {
                        var parts=new List<PlatformCell>{remaining[0]};remaining.RemoveAt(0);
                        for(int i=0;i<parts.Count;i++)for(int j=remaining.Count-1;j>=0;j--)if(Connected(parts[i].Rect,remaining[j].Rect)){parts.Add(remaining[j]);remaining.RemoveAt(j);}
                        string id="paint-"+paint.Color.Substring(1).ToLowerInvariant()+"-"+screen.Key+"-"+(++index);
                        if(Platforms.Any(p=>p.Id==id))throw new InvalidDataException("Reserved generated platform ID: "+id);
                        var definition=new PlatformDefinition{Id=id,Screen=screen.Key,Profile=paint.Profile};definition.Parts.AddRange(parts.Select(p=>p.Rect));
                        if(paint.Range!=""&&cells.Any(c=>c.Screen==screen.Key&&c.Color==paint.Range))
                        {
                            var original=Motions[Profiles[paint.Profile].Motion];bool horizontal=original.Points[1].Position.Y==0;
                            if(!horizontal&&original.Points[1].Position.X!=0)throw new InvalidDataException("Range markers require horizontal or vertical motion");
                            var ranges=cells.Where(c=>c.Screen==screen.Key&&c.Color==paint.Range).ToList();
                            int negative=RangeDistance(parts,ranges,horizontal,false),positive=RangeDistance(parts,ranges,horizontal,true);
                            if(negative+positive==0)throw new InvalidDataException(id+": range markers must touch the platform along its movement axis");
                            // shift the route around the painted start; no static block is left behind
                            var motion=new PlatformMotion{Id=id,Speed=original.Speed,Mode=original.Mode,Phase=original.Phase,Playing=original.Playing,OnBlocked=original.OnBlocked,BlockedWait=original.BlockedWait};
                            motion.Points.Add(new PlatformWaypoint{Position=new PlatformPoint(0,0),Wait=original.Points[0].Wait});
                            if(positive>0&&negative>0)
                            {
                                motion.StartDistance=negative;
                                for(int i=0;i<definition.Parts.Count;i++){var r=definition.Parts[i];if(horizontal)r.X-=negative;else r.Y-=negative;definition.Parts[i]=r;}
                                int length=positive+negative;motion.Points.Add(new PlatformWaypoint{Position=new PlatformPoint(horizontal?length:0,horizontal?0:length),Wait=original.Points[1].Wait});
                            }
                            else{int length=positive>0?positive:-negative;motion.Points.Add(new PlatformWaypoint{Position=new PlatformPoint(horizontal?length:0,horizontal?0:length),Wait=original.Points[1].Wait});}
                            Motions.Add(id,motion);var source=Profiles[paint.Profile];Profiles.Add(id,new PlatformProfile{Id=id,Motion=id,Collision=source.Collision,Texture=source.Texture,Fit=source.Fit,Columns=source.Columns,Rows=source.Rows,Frames=source.Frames,Fps=source.Fps,Label=source.Label,Momentum=source.Momentum,MomentumAxes=source.MomentumAxes});definition.Profile=id;
                        }
                        Platforms.Add(definition);
                        if(Platforms.Count>MaximumPlatforms)throw new InvalidDataException("Too many painted platforms");
                    }
                }
            }
            ValidateDefinitions();
        }
        private static IList<PlatformCell> Compact(IList<PlatformCell> input)
        {
            var result=new List<PlatformCell>();var runs=new Dictionary<string,PlatformCell>();
            foreach(var cell in input.OrderBy(c=>c.Screen).ThenBy(c=>c.Rect.Y).ThenBy(c=>c.Rect.X))
            {
                string key=cell.Screen+":"+cell.Color+":"+cell.Rect.X+":"+cell.Rect.Width;PlatformCell previous;
                if(runs.TryGetValue(key,out previous)&&previous.Rect.Y+previous.Rect.Height==cell.Rect.Y)
                {var rect=previous.Rect;rect.Height+=cell.Rect.Height;previous.Rect=rect;}
                else{var copy=new PlatformCell{Color=cell.Color,Screen=cell.Screen,Rect=cell.Rect};runs[key]=copy;result.Add(copy);}
            }
            return result;
        }
        private static int RangeDistance(List<PlatformCell> pieces,List<PlatformCell> ranges,bool horizontal,bool positive)
        {
            int distance=0;
            foreach(var piece in pieces)
            {
                var visited=new HashSet<PlatformCell>();var queue=new Queue<PlatformCell>();
                foreach(var r in ranges)if(Adjacent(piece.Rect,r.Rect,horizontal,positive)){visited.Add(r);queue.Enqueue(r);}
                while(queue.Count>0)
                {
                    var r=queue.Dequeue().Rect;
                    distance=Math.Max(distance,horizontal?(positive?r.X+r.Width-piece.Rect.X-piece.Rect.Width:piece.Rect.X-r.X):(positive?r.Y+r.Height-piece.Rect.Y-piece.Rect.Height:piece.Rect.Y-r.Y));
                    foreach(var next in ranges)if(!visited.Contains(next)&&Adjacent(r,next.Rect,horizontal,positive)){visited.Add(next);queue.Enqueue(next);}
                }
            }
            return distance;
        }
        private static bool Adjacent(PlatformRect a,PlatformRect b,bool horizontal,bool positive)
        {return horizontal?(positive?a.X+a.Width==b.X:b.X+b.Width==a.X)&&a.Y<b.Y+b.Height&&a.Y+a.Height>b.Y:(positive?a.Y+a.Height==b.Y:b.Y+b.Height==a.Y)&&a.X<b.X+b.Width&&a.X+a.Width>b.X;}
        private static bool Connected(PlatformRect a,PlatformRect b)
        {return Adjacent(a,b,true,true)||Adjacent(a,b,true,false)||Adjacent(a,b,false,true)||Adjacent(a,b,false,false);}
        internal static double Distance(PlatformPoint a,PlatformPoint b){double x=a.X-b.X,y=a.Y-b.Y;return Math.Sqrt(x*x+y*y);}
        internal static PlatformRect Rect(string text)
        {
            var parts=text.Split(',');int x,y,w,h;
            if(parts.Length!=4||!int.TryParse(parts[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out x)||!int.TryParse(parts[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out y)||!int.TryParse(parts[2],NumberStyles.Integer,CultureInfo.InvariantCulture,out w)||!int.TryParse(parts[3],NumberStyles.Integer,CultureInfo.InvariantCulture,out h)||Math.Abs((long)x)>100000||Math.Abs((long)y)>100000||w<1||h<1||w>480||h>360)throw new InvalidDataException("Platform rect requires x,y,width,height in game pixels");
            return new PlatformRect(x,y,w,h);
        }
        internal static void SafeRelative(string path)
        {if(Path.IsPathRooted(path)||path.Split('/','\\').Any(p=>p==".."||p=="")||path.IndexOf(':')>=0)throw new InvalidDataException("Platform texture must be a map-relative path");}
        private static InvalidDataException Error(XElement e,string message){var line=(System.Xml.IXmlLineInfo)e;return new InvalidDataException("Moving platforms "+e.Name+" at line "+line.LineNumber+": "+message);}
        private static void Attributes(XElement element,string allowed)
        {foreach(var a in element.Attributes())if(a.IsNamespaceDeclaration||!allowed.Split(' ').Contains(a.Name.ToString()))throw Error(element,"unknown attribute "+a.Name);if(element.Nodes().OfType<XText>().Any(t=>!string.IsNullOrWhiteSpace(t.Value)))throw Error(element,"unexpected text");}
        private static string Required(XElement e,string name){var a=e.Attribute(name);if(a==null||string.IsNullOrWhiteSpace(a.Value))throw Error(e,"missing "+name);return a.Value;}
        private static string Optional(XElement e,string name,string fallback){return (string)e.Attribute(name)??fallback;}
        private static string Caption(XElement e){string text=Optional(e,"label","");if(text.Length>80||text.Any(char.IsControl))throw Error(e,"label must fit one line of up to 80 characters");return text;}
        private static string Id(XElement e,string name){string value=Required(e,name);CheckId(value);return value;}
        private static void CheckId(string value){if(value.Length>96||!value.All(c=>c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='-'||c=='_'||c=='.'))throw new InvalidDataException("Platform IDs use 1 to 96 ASCII letters, digits, '.', '-' or '_'");}
        private static string Choice(XElement e,string name,string fallback,string choices){string value=Optional(e,name,fallback);if(!choices.Split(' ').Contains(value))throw Error(e,"invalid "+name);return value;}
        private static double Number(XElement e,string name,double fallback,double minimum,double maximum){double value;if(e.Attribute(name)==null)return fallback;if(!double.TryParse(Required(e,name),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||double.IsNaN(value)||double.IsInfinity(value)||value<minimum||value>maximum)throw Error(e,"invalid "+name);return value;}
        private static int Integer(XElement e,string name,int fallback,int minimum,int maximum){double value=Number(e,name,fallback,minimum,maximum);if(value!=(int)value)throw Error(e,name+" must be an integer");return (int)value;}
        private static bool Boolean(XElement e,string name,bool fallback){return Choice(e,name,fallback?"true":"false","true false")=="true";}
    }
}
