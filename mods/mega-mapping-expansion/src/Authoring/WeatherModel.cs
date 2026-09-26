using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    [Serializable]
    public sealed class RainData
    {
        public RainData() { Screen=1; Count=100; Speed=190; Wind=-32; Length=6; Opacity=.3f; Color="#7AA9BD"; Layer="foreground"; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen"), DefaultValue(1)] public int Screen { get; set; }
        [XmlAttribute("count"), DefaultValue(100)] public int Count { get; set; }
        [XmlAttribute("speed"), DefaultValue(190f)] public float Speed { get; set; }
        [XmlAttribute("wind"), DefaultValue(-32f)] public float Wind { get; set; }
        [XmlAttribute("length"), DefaultValue(6f)] public float Length { get; set; }
        [XmlAttribute("opacity"), DefaultValue(.3f)] public float Opacity { get; set; }
        [XmlAttribute("color"), DefaultValue("#7AA9BD")] public string Color { get; set; }
        [XmlAttribute("layer"), DefaultValue("foreground")] public string Layer { get; set; }
        [XmlAttribute("z"), DefaultValue(0f)] public float Z { get; set; }
        [XmlAttribute("collide"), DefaultValue(false)] public bool Collide { get; set; }
        [XmlAttribute("snow"), DefaultValue(false)] public bool Snow { get; set; }
        [XmlAttribute("drift"), DefaultValue(0f)] public float Drift { get; set; }
    }

    [Serializable]
    public sealed class PuddleData
    {
        public PuddleData() { Screen=1; Outline=""; ReflectionScaleY=.35f; Perspective=.004f; Opacity=.65f; Ripple=.6f; RainRings=12; Color="#80B8CC"; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen"), DefaultValue(1)] public int Screen { get; set; }
        [XmlAttribute("outline")] public string Outline { get; set; }
        [XmlAttribute("reflectionObjects")] public string ReflectionObjects { get; set; }
        [XmlAttribute("planeY")] public float PlaneY { get; set; }
        [XmlAttribute("reflectionScaleY"), DefaultValue(.35f)] public float ReflectionScaleY { get; set; }
        [XmlAttribute("perspective"), DefaultValue(.004f)] public float Perspective { get; set; }
        [XmlAttribute("opacity"), DefaultValue(.65f)] public float Opacity { get; set; }
        [XmlAttribute("ripple"), DefaultValue(.6f)] public float Ripple { get; set; }
        [XmlAttribute("rainRings"), DefaultValue(12)] public int RainRings { get; set; }
        [XmlAttribute("color"), DefaultValue("#80B8CC")] public string Color { get; set; }
    }
}
