using System;
using System.ComponentModel;
using System.Xml.Serialization;
namespace MegaMappingExpansion
{
    [Serializable]
    public sealed class PlanetData
    {
        public PlanetData(){Screen=1;Radius=180;Period=120;CloudPeriod=141;CloudOpacity=.85f;Layer="background";}
        [XmlAttribute("id")] public string Id {get;set;}
        [XmlAttribute("screen"),DefaultValue(1)] public int Screen {get;set;}
        [XmlAttribute("surfaceAsset")] public string SurfaceAsset {get;set;}
        [XmlAttribute("cloudAsset")] public string CloudAsset {get;set;}
        [XmlAttribute("x")] public float X {get;set;}
        [XmlAttribute("y")] public float Y {get;set;}
        [XmlAttribute("radius"),DefaultValue(180f)] public float Radius {get;set;}
        [XmlAttribute("period"),DefaultValue(120f)] public float Period {get;set;}
        [XmlAttribute("cloudPeriod"),DefaultValue(141f)] public float CloudPeriod {get;set;}
        [XmlAttribute("cloudOpacity"),DefaultValue(.85f)] public float CloudOpacity {get;set;}
        [XmlAttribute("longitude")] public float Longitude {get;set;}
        [XmlAttribute("viewTilt")] public float ViewTilt {get;set;}
        [XmlAttribute("axisTilt")] public float AxisTilt {get;set;}
        [XmlAttribute("layer"),DefaultValue("background")] public string Layer {get;set;}
        [XmlAttribute("z")] public float Z {get;set;}
    }
}
