using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace MegaMappingExpansion
{
    // Integer links augment the native TeleportLink array. Native movement,
    // relocation events and third-party screen-query consumers keep one truth.
    internal sealed class MapLayout
    {
        internal const string RelativePath="props/mega-mapping-expansion/map.xml";
        internal int Screens,AtlasSide;
        internal readonly Dictionary<int,int[]> Links=new Dictionary<int,int[]>();

        internal static MapLayout Read(string root)
        {
            string path=Path.Combine(root,RelativePath);if(!File.Exists(path))return null;
            var document=new XmlDocument{XmlResolver=null};
            using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000}))document.Load(reader);
            XmlElement entry=document.DocumentElement;
            if(entry==null||entry.Name!="MapLayout")throw new InvalidDataException(path+": expected MapLayout");
            Attributes(entry,"version","screens","atlasSide");
            if(Number(entry,"version",1,1)!=1)throw new InvalidDataException("Unsupported map layout version");
            var result=new MapLayout{Screens=Number(entry,"screens",1,4096),AtlasSide=Number(entry,"atlasSide",1,64)};
            if(result.Screens>result.AtlasSide*result.AtlasSide)throw new InvalidDataException(path+": screens exceed declared atlas capacity");
            foreach(XmlNode child in entry.ChildNodes)
            {
                XmlElement link=child as XmlElement;if(link==null)continue;
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
            return result;
        }
        private static void Attributes(XmlElement element,params string[] names)
        {foreach(XmlAttribute attribute in element.Attributes)if(Array.IndexOf(names,attribute.Name)<0)throw new InvalidDataException(element.Name+": unknown @"+attribute.Name);}
        private static int Number(XmlElement element,string name,int min,int max)
        {int value;if(!int.TryParse(element.GetAttribute(name),out value)||value<min||value>max)throw new InvalidDataException(element.Name+" @"+name+" must be "+min+".."+max);return value;}
        internal void ValidateAtlas(int width,int height)
        {
            if(width!=AtlasSide*60||height!=AtlasSide*45)
                throw new InvalidDataException("Map declares "+Screens+" screens in a "+AtlasSide+"x"+AtlasSide+" atlas, but level.xnb is "+width+"x"+height+". Rebuild collision; changing expectedScreens does not enlarge it.");
        }
    }
}
