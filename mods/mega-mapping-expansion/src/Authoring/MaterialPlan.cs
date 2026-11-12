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
    internal sealed class MaterialSource
    {
        internal string Id,Color,Factory="",Sizing="cell";
    }
    internal sealed class MaterialLayer
    {
        internal string Id,Source,Before="",After="";
        internal int Order;
        internal MaterialLayer Copy(){return (MaterialLayer)MemberwiseClone();}
    }
    internal sealed class MaterialDefinition
    {
        internal string Id,Collision="source",Shape="rectangle";
        internal bool Enabled=true;
        internal int Priority;
        internal readonly List<MaterialLayer> Layers=new List<MaterialLayer>();
    }
    internal sealed class MaterialPaint
    {
        internal string Color,Material,Mode="cells";
        internal int[] Screens;
        internal bool Replace;
        internal bool Applies(int screen){return Screens==null||Array.IndexOf(Screens,screen)>=0;}
    }
    internal sealed class MaterialArea
    {
        internal string Id,Material,Shape="rectangle",Outline="",Mask="",Mode="overlay",Select="";
        internal int Screen;
        internal readonly List<PlatformRect> Parts=new List<PlatformRect>(),Holes=new List<PlatformRect>();
    }
    // the editor and game share the same schema, ordering and diagnostics
    internal sealed class MaterialPlan
    {
        internal const string PathName=MappingFiles.Materials;
        internal readonly Dictionary<string,MaterialSource> Sources=new Dictionary<string,MaterialSource>(StringComparer.Ordinal);
        internal readonly Dictionary<string,MaterialDefinition> Materials=new Dictionary<string,MaterialDefinition>(StringComparer.Ordinal);
        internal readonly List<MaterialPaint> Paint=new List<MaterialPaint>();
        internal readonly List<MaterialArea> Areas=new List<MaterialArea>();
        internal string Identity="";
        internal static MaterialPlan Read(string root,int screens)
        {
            string file=Path.Combine(root,PathName);
            if(!File.Exists(file))return new MaterialPlan();
            var result=Parse(MappingFiles.OpenPainted(file,root).OuterXml,screens);
            using(var sha=SHA256.Create())
            {
                var identity=new StringBuilder(result.Identity);
                foreach(string mask in result.Areas.Select(a=>a.Mask).Where(p=>p!="").Distinct().OrderBy(p=>p,StringComparer.Ordinal))
                {
                    string path=Path.Combine(root,mask);if(new FileInfo(path).Length>64L*1024*1024)throw new InvalidDataException("Material mask exceeds 64 MiB");
                    using(var stream=File.OpenRead(path))identity.Append(mask).Append(BitConverter.ToString(sha.ComputeHash(stream)));
                }
                result.Identity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()))).Replace("-","");
            }
            return result;
        }
        internal static MaterialPlan Parse(string text,int screens)
        {
            XElement root;
            using(var reader=System.Xml.XmlReader.Create(new StringReader(text),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000}))root=XDocument.Load(reader,LoadOptions.SetLineInfo).Root;
            if(root==null||root.Name!="Materials"||Required(root,"version")!="1")throw new InvalidDataException("Expected Materials version 1");
            Attributes(root,"version");var result=new MaterialPlan();
            var definitions=new Dictionary<string,XElement>(StringComparer.Ordinal);
            foreach(var e in root.Elements())
            {
                switch(e.Name.LocalName)
                {
                    case "Source":
                        Attributes(e,"id color factory sizing");Leaf(e);
                        var source=new MaterialSource{Id=Id(e,"id"),Color=Color(Required(e,"color")),Factory=Optional(e,"factory",""),Sizing=Choice(e,"sizing","cell","cell rectangle")};
                        if(result.Sources.ContainsKey(source.Id))throw Error(e,"duplicate source "+source.Id);
                        result.Sources.Add(source.Id,source);break;
                    case "Material":
                        string id=Id(e,"id");if(definitions.ContainsKey(id))throw Error(e,"duplicate material "+id);definitions.Add(id,e);break;
                    case "Paint":
                        Attributes(e,"color material screens mode replace");Leaf(e);
                        var paint=new MaterialPaint{Color=Color(Required(e,"color")),Material=Id(e,"material"),Mode=Choice(e,"mode","cells","cells rectangles connected"),Replace=Boolean(e,"replace",false)};
                        if(e.Attribute("screens")!=null)paint.Screens=ScreenList(Required(e,"screens"),screens);
                        if(result.Paint.Any(p=>p.Color==paint.Color&&(p.Screens==null||paint.Screens==null||p.Screens.Intersect(paint.Screens).Any())))throw Error(e,"overlapping palette entries for "+paint.Color);
                        result.Paint.Add(paint);break;
                    case "Area":
                        Attributes(e,"id material screen rect shape outline mask mode select");
                        var area=new MaterialArea{Id=Id(e,"id"),Material=Id(e,"material"),Screen=Integer(e,"screen",1,1,screens),Shape=Choice(e,"shape","rectangle","rectangle disc polygon mask"),Outline=Optional(e,"outline",""),Mask=Optional(e,"mask",""),Mode=Choice(e,"mode","overlay","overlay replace surfaces empty"),Select=Optional(e,"select","")};
                        if(e.Attribute("rect")!=null)area.Parts.Add(Rect(Required(e,"rect")));
                        foreach(var child in e.Elements())
                        {
                            Attributes(child,"rect");Leaf(child);
                            if(child.Name=="Part")area.Parts.Add(Rect(Required(child,"rect")));
                            else if(child.Name=="Exclude")area.Holes.Add(Rect(Required(child,"rect")));
                            else throw Error(child,"expected Part or Exclude");
                        }
                        if(area.Parts.Count==0||area.Parts.Count+area.Holes.Count>256)throw Error(e,"Area needs 1..256 rectangles");
                        if(area.Shape!="rectangle"&&area.Parts.Count!=1)throw Error(e,"disc, polygon and mask areas need one bounding rect");
                        if(area.Shape=="polygon")Points(area.Outline);
                        else if(area.Outline!="")throw Error(e,"outline requires polygon");
                        if(area.Shape=="mask"){if(area.Mask=="")throw Error(e,"mask path is required");MovingPlatformPlan.SafeRelative(area.Mask);}
                        else if(area.Mask!="")throw Error(e,"mask requires shape=mask");
                        if(area.Select!="")area.Select=Color(area.Select);
                        if(result.Areas.Any(a=>a.Id==area.Id))throw Error(e,"duplicate area "+area.Id);
                        result.Areas.Add(area);break;
                    default:throw Error(e,"unknown materials element "+e.Name);
                }
            }
            if(result.Sources.Count>1024||definitions.Count>1024||result.Paint.Count>1024||result.Areas.Count>1024)throw Error(root,"materials exceed 1024 entries per section");
            foreach(string id in definitions.Keys)result.Resolve(id,definitions,new HashSet<string>());
            foreach(var p in result.Paint)
            {
                result.Require(p.Material);
                if(p.Mode!="cells"&&result.Require(p.Material).Layers.Any(l=>result.Sources[l.Source].Sizing!="rectangle"))throw Error(root,"compacted paint requires sizing=rectangle on every source");
            }
            foreach(var a in result.Areas){result.Require(a.Material);if(result.Require(a.Material).Layers.Any(l=>result.Sources[l.Source].Sizing!="rectangle"))throw Error(root,"XML areas require sizing=rectangle on every source");}
            using(var hash=SHA256.Create())result.Identity=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting)))).Replace("-","");
            return result;
        }
        private MaterialDefinition Resolve(string id,Dictionary<string,XElement> definitions,HashSet<string> visiting)
        {
            MaterialDefinition ready;if(Materials.TryGetValue(id,out ready))return ready;
            XElement e;if(!definitions.TryGetValue(id,out e))throw new InvalidDataException("Unknown material "+id);
            if(!visiting.Add(id))throw Error(e,"material inheritance cycle at "+id);
            Attributes(e,"id extends collision shape enabled priority");
            var material=new MaterialDefinition{Id=id};string parent=Optional(e,"extends","");
            if(parent!="")
            {
                var original=Resolve(parent,definitions,visiting);
                material.Collision=original.Collision;material.Shape=original.Shape;material.Enabled=original.Enabled;material.Priority=original.Priority;
                material.Layers.AddRange(original.Layers.Select(l=>l.Copy()));
            }
            material.Collision=Choice(e,"collision",material.Collision,"source solid trigger oneWay");
            material.Shape=Choice(e,"shape",material.Shape,"rectangle TopLeft TopRight BottomLeft BottomRight");
            if(material.Collision=="oneWay"&&material.Shape!="rectangle")throw Error(e,"oneWay support requires rectangle shape");
            material.Enabled=Boolean(e,"enabled",material.Enabled);material.Priority=Integer(e,"priority",material.Priority,-100000,100000);
            var ownIds=new HashSet<string>();foreach(var child in e.Elements())
            {
                Attributes(child,"id source order before after");Leaf(child);if(child.Name!="Use")throw Error(child,"expected Use");
                var layer=new MaterialLayer{Id=Id(child,"id"),Source=Id(child,"source"),Order=Integer(child,"order",0,-100000,100000),Before=Optional(child,"before",""),After=Optional(child,"after","")};
                if(!ownIds.Add(layer.Id))throw Error(child,"duplicate layer "+layer.Id);
                if(!Sources.ContainsKey(layer.Source))throw Error(child,"unknown source "+layer.Source);
                material.Layers.RemoveAll(l=>l.Id==layer.Id);material.Layers.Add(layer);
            }
            if(material.Layers.Count<1||material.Layers.Count>16)throw Error(e,"material needs 1..16 Use entries");
            var sorted=Sort(material.Layers);material.Layers.Clear();material.Layers.AddRange(sorted);
            visiting.Remove(id);Materials.Add(id,material);return material;
        }
        internal static MaterialLayer[] Sort(IList<MaterialLayer> layers)
        {
            var pending=layers.OrderBy(l=>l.Order).ThenBy(l=>l.Id,StringComparer.Ordinal).ToList();var output=new List<MaterialLayer>();
            var edges=layers.ToDictionary(l=>l.Id,l=>new HashSet<string>());
            foreach(var l in layers)
            {
                foreach(string before in Words(l.Before)){if(!edges.ContainsKey(before))throw new InvalidDataException("Unknown order target "+before);edges[before].Add(l.Id);}
                foreach(string after in Words(l.After)){if(!edges.ContainsKey(after))throw new InvalidDataException("Unknown order target "+after);edges[l.Id].Add(after);}
            }
            while(pending.Count>0)
            {
                var next=pending.FirstOrDefault(l=>edges[l.Id].All(d=>output.Any(o=>o.Id==d)));
                if(next==null)throw new InvalidDataException("Material behavior order contains a cycle");output.Add(next);pending.Remove(next);
            }
            return output.ToArray();
        }
        internal MaterialDefinition Require(string id){MaterialDefinition value;if(!Materials.TryGetValue(id,out value))throw new InvalidDataException("Unknown material "+id);return value;}
        internal MaterialPaint Find(string color,int screen){return Paint.FirstOrDefault(p=>p.Color==color&&p.Applies(screen));}
        internal static string[] Words(string text){return text.Split(new[]{' ',','},StringSplitOptions.RemoveEmptyEntries);}
        internal static double[] Points(string text)
        {
            double[] values=Words(text).Select(v=>{double n;if(!double.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||double.IsNaN(n)||double.IsInfinity(n)||Math.Abs(n)>100000)throw new InvalidDataException("Invalid polygon coordinate");return n;}).ToArray();
            if(values.Length<6||values.Length>128||values.Length%2!=0)throw new InvalidDataException("Polygon requires 3..64 x,y pairs");return values;
        }
        internal static PlatformRect Rect(string value)
        {
            var p=value.Split(',');int[] n=new int[4];
            if(p.Length!=4||!Enumerable.Range(0,4).All(i=>int.TryParse(p[i],NumberStyles.Integer,CultureInfo.InvariantCulture,out n[i]))||n[0]<0||n[1]<0||n[2]<=0||n[3]<=0||n[0]+(long)n[2]>480||n[1]+(long)n[3]>360)throw new InvalidDataException("Area rect must fit its 480x360 screen");
            return new PlatformRect(n[0],n[1],n[2],n[3]);
        }
        internal static int[] ScreenList(string text,int screens)
        {
            var result=new HashSet<int>();
            foreach(string word in Words(text)){var pair=word.Split('-');int a,b;if(pair.Length>2||!int.TryParse(pair[0],out a))throw new InvalidDataException("Invalid screen range");b=a;if(pair.Length==2&&!int.TryParse(pair[1],out b))throw new InvalidDataException("Invalid screen range");if(a<1||b<a||b>screens)throw new InvalidDataException("Screen range leaves map");for(int i=a;i<=b;i++)result.Add(i);}
            if(result.Count==0)throw new InvalidDataException("Screen list is empty");return result.OrderBy(i=>i).ToArray();
        }
        internal static string Color(string value){return MovingPlatformPlan.Color(value);}
        internal static void Attributes(XElement e,string allowed){var names=allowed.Split(' ');if(e.Name.NamespaceName!=""||e.Attributes().Any(a=>a.Name.NamespaceName!=""||!names.Contains(a.Name.LocalName)))throw Error(e,"unknown attribute");if(e.Nodes().OfType<XText>().Any(t=>!string.IsNullOrWhiteSpace(t.Value)))throw Error(e,"unexpected text");}
        internal static void Leaf(XElement e){if(e.HasElements)throw Error(e,"unexpected child element");}
        internal static string Required(XElement e,string name){string value=(string)e.Attribute(name);if(string.IsNullOrWhiteSpace(value))throw Error(e,"missing "+name);return value;}
        internal static string Optional(XElement e,string name,string fallback){return (string)e.Attribute(name)??fallback;}
        internal static string Id(XElement e,string name){string value=Required(e,name);if(value.Length>80||value.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'&&c!='_'&&c!='.'))throw Error(e,"invalid ID");return value;}
        internal static string Choice(XElement e,string name,string fallback,string choices){string value=Optional(e,name,fallback);if(!choices.Split(' ').Contains(value))throw Error(e,"invalid "+name+": "+value);return value;}
        internal static int Integer(XElement e,string name,int fallback,int min,int max){int value;if(e.Attribute(name)==null)return fallback;if(!int.TryParse((string)e.Attribute(name),NumberStyles.Integer,CultureInfo.InvariantCulture,out value)||value<min||value>max)throw Error(e,"invalid "+name);return value;}
        internal static bool Boolean(XElement e,string name,bool fallback){bool value;if(e.Attribute(name)==null)return fallback;if(!bool.TryParse((string)e.Attribute(name),out value))throw Error(e,"invalid "+name);return value;}
        internal static Exception Error(XElement e,string message){var line=(System.Xml.IXmlLineInfo)e;return new InvalidDataException("Materials line "+line.LineNumber+": "+message);}
    }
}
