using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace MegaMappingExpansion
{
    internal sealed class PhysicsSettings
    {
        internal string Profile="native";
        internal readonly Dictionary<int,int> Frames=new Dictionary<int,int>();
        internal bool Symmetric{get{return Profile=="symmetric";}}
        internal bool Enabled{get{return Symmetric||Frames.Count>0;}}
        internal static PhysicsSettings Read(XmlElement element,int screens,Dictionary<int,int> legacy)
        {
            var result=new PhysicsSettings();
            if(element==null)
            {
                foreach(var pair in legacy)result.Frames.Add(pair.Key,pair.Value);
                if(legacy.Count>0)result.Profile="reference";
                return result;
            }
            if(legacy.Count>0)throw new InvalidDataException("Use either Physics or legacy PhysicsFrame, not both.");
            foreach(XmlAttribute a in element.Attributes)if(a.Name!="version"&&a.Name!="profile")throw new InvalidDataException("Unknown Physics attribute: "+a.Name);
            if(element.GetAttribute("version")!="1")throw new InvalidDataException("Physics requires version=1.");
            result.Profile=element.GetAttribute("profile");
            if(result.Profile!="native"&&result.Profile!="reference"&&!result.Symmetric)throw new InvalidDataException("Physics profile must be native, reference or symmetric.");
            foreach(XmlNode node in element.ChildNodes)
            {
                var frame=node as XmlElement;
                if(frame==null){if((node.NodeType==XmlNodeType.Text||node.NodeType==XmlNodeType.CDATA)&&!string.IsNullOrWhiteSpace(node.InnerText))throw new InvalidDataException("Unexpected Physics text.");continue;}
                if(result.Profile!="reference"||frame.Name!="Frame"||frame.Attributes.Count!=2||frame.HasChildNodes)throw new InvalidDataException("Only reference physics accepts Frame screen/referenceScreen entries.");
                int screen,reference;
                if(!int.TryParse(frame.GetAttribute("screen"),out screen)||screen<1||screen>screens||!int.TryParse(frame.GetAttribute("referenceScreen"),out reference)||reference<1||reference>4096||result.Frames.ContainsKey(screen-1))throw new InvalidDataException("Invalid or duplicate physics Frame.");
                result.Frames.Add(screen-1,reference-1);
            }
            if(result.Profile=="reference"&&result.Frames.Count==0)throw new InvalidDataException("Reference physics requires at least one Frame.");
            return result;
        }
    }
}
