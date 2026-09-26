using System;
using System.ComponentModel;
using System.Xml.Serialization;
namespace MegaMappingExpansion
{
    [Serializable]
    public sealed class SurfData
    {
        public SurfData(){Screen=1;Width=480;Y=240;Height=48;Period=7;Wavelength=200;Depth=100;Break=1;Spray=100;Color="#071D29";CrestColor="#6E9FA9";Layer="background";Impacts=new SurfImpactData[0];}
        [XmlAttribute("id")] public string Id {get;set;}
        [XmlAttribute("screen"),DefaultValue(1)] public int Screen {get;set;}
        [XmlAttribute("x"),DefaultValue(0f)] public float X {get;set;}
        [XmlAttribute("y"),DefaultValue(240f)] public float Y {get;set;}
        [XmlAttribute("width"),DefaultValue(480f)] public float Width {get;set;}
        [XmlAttribute("height"),DefaultValue(48f)] public float Height {get;set;}
        [XmlAttribute("depth"),DefaultValue(100f)] public float Depth {get;set;}
        [XmlAttribute("period"),DefaultValue(7f)] public float Period {get;set;}
        [XmlAttribute("wavelength"),DefaultValue(200f)] public float Wavelength {get;set;}
        [XmlAttribute("phase"),DefaultValue(0f)] public float Phase {get;set;}
        [XmlAttribute("variation"),DefaultValue(0f)] public float Variation {get;set;}
        [XmlAttribute("chop"),DefaultValue(0f)] public float Chop {get;set;}
        [XmlAttribute("foamDetail"),DefaultValue(0f)] public float FoamDetail {get;set;}
        [XmlAttribute("bakedBody"),DefaultValue(false)] public bool BakedBody {get;set;}
        private SurfImpactData[] impacts=new SurfImpactData[0];
        [XmlElement("Impact")] public SurfImpactData[] Impacts {get{return impacts;}set{impacts=value??new SurfImpactData[0];}}
        [XmlAttribute("break"),DefaultValue(1f)] public float Break {get;set;}
        [XmlAttribute("spray"),DefaultValue(100)] public int Spray {get;set;}
        [XmlAttribute("wind"),DefaultValue(0f)] public float Wind {get;set;}
        [XmlAttribute("color"),DefaultValue("#071D29")] public string Color {get;set;}
        [XmlAttribute("crestColor"),DefaultValue("#6E9FA9")] public string CrestColor {get;set;}
        [XmlAttribute("layer"),DefaultValue("background")] public string Layer {get;set;}
        [XmlAttribute("z"),DefaultValue(0f)] public float Z {get;set;}
    }
    [Serializable]
    public sealed class SurfImpactData
    {
        public SurfImpactData(){Angle=-110;Reach=80;Width=30;Particles=90;Layer="world";Z=-8;ArrivalPhase=-1;}
        [XmlAttribute("arrivalPhase"),DefaultValue(-1f)] public float ArrivalPhase {get;set;}
        [XmlAttribute("x")] public float X {get;set;}
        [XmlAttribute("y")] public float Y {get;set;}
        [XmlAttribute("angle"),DefaultValue(-110f)] public float Angle {get;set;}
        [XmlAttribute("reach"),DefaultValue(80f)] public float Reach {get;set;}
        [XmlAttribute("width"),DefaultValue(30f)] public float Width {get;set;}
        [XmlAttribute("particles"),DefaultValue(90)] public int Particles {get;set;}
        [XmlAttribute("layer"),DefaultValue("world")] public string Layer {get;set;}
        [XmlAttribute("z"),DefaultValue(-8f)] public float Z {get;set;}
    }
}
