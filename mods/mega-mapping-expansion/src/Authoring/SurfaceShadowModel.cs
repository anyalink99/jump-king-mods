using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    [Serializable]
    public sealed class ShadowSurfaceData
    {
        public ShadowSurfaceData() { Screen=1; Outline=""; ScaleY=.45f; ShearX=-.35f; Opacity=.45f; Color="#352842"; MaxHeight=160; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen"), DefaultValue(1)] public int Screen { get; set; }
        [XmlAttribute("outline")] public string Outline { get; set; }
        [XmlAttribute("planeY")] public float PlaneY { get; set; }
        [XmlAttribute("scaleY"), DefaultValue(.45f)] public float ScaleY { get; set; }
        [XmlAttribute("shearX"), DefaultValue(-.35f)] public float ShearX { get; set; }
        [XmlAttribute("opacity"), DefaultValue(.45f)] public float Opacity { get; set; }
        [XmlAttribute("color"), DefaultValue("#352842")] public string Color { get; set; }
        [XmlAttribute("maxHeight"), DefaultValue(160f)] public float MaxHeight { get; set; }
    }
}
