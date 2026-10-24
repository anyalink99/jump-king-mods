using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace MegaMappingExpansion
{
    // Worldsmith shares these file boundaries, so don't add game types here
    internal static class MappingFiles
    {
        internal const string Settings="mapping_settings.xml";
        internal const string Transitions="screens/mega-mapping-expansion/transitions.xml";
        private static readonly string[] Sections={"MapLayout","Assets","Components","Graph","Bindings","Variables","Effects","Rules","Exports","Trees","Recipes","Instances","Include"};
        internal static XmlDocument Open(string path)
        {
            var document=new XmlDocument{XmlResolver=null};
            using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000}))document.Load(reader);return document;
        }
        internal static XmlElement SettingsLayout(string root)
        {
            string path=Path.Combine(root,Settings);if(!File.Exists(path))return null;var entry=Open(path).DocumentElement;
            if(entry==null||entry.Name!="MappingSettings"||entry.GetAttribute("version")!="1")throw new InvalidDataException(path+": expected MappingSettings version 1");
            foreach(XmlAttribute attribute in entry.Attributes)if(attribute.Name!="version"&&attribute.Name!="saveId")throw new InvalidDataException(path+": unknown @"+attribute.Name);
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(XmlNode node in entry.ChildNodes)
            {
                var section=node as XmlElement;
                if(section==null){if(node.NodeType==XmlNodeType.Text||node.NodeType==XmlNodeType.CDATA)throw new InvalidDataException(path+": unexpected text");continue;}
                if(Array.IndexOf(Sections,section.Name)<0||(section.Name!="Include"&&!seen.Add(section.Name)))throw new InvalidDataException(path+": unknown or duplicate section "+section.Name);
            }
            return entry["MapLayout"];
        }
        internal static bool Exists(string root){return File.Exists(Path.Combine(root,Settings))||File.Exists(Path.Combine(root,Transitions))||File.Exists(Path.Combine(root,MovingPlatformPlan.PathName));}
        internal static XmlElement Links(string root)
        {
            string path=Path.Combine(root,Transitions);if(!File.Exists(path))return null;var entry=Open(path).DocumentElement;
            if(entry==null||entry.Name!="Transitions"||entry.GetAttribute("version")!="1")throw new InvalidDataException(path+": expected Transitions version 1");
            foreach(XmlNode node in entry.ChildNodes)if(node is XmlElement&&Array.IndexOf(new[]{"SideLink","Pool","Exit","SchemePool","Reroll"},node.Name)<0)throw new InvalidDataException(path+": unknown transition "+node.Name);return entry;
        }
    }
}
