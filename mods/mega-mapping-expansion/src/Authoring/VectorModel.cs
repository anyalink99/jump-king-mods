using System;
using System.ComponentModel;
using System.Xml.Serialization;
namespace MegaMappingExpansion
{
    [Serializable, XmlRoot("VectorAsset")]
    public sealed class VectorAssetData
    {
        public VectorAssetData() { Width = 64; Height = 64; Supersample = 2; Shapes = new VectorShapeData[0]; ClipPath = ""; ClipMode = "none"; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("source")] public string Source { get; set; }
        [XmlAttribute("width"), DefaultValue(64)] public int Width { get; set; }
        [XmlAttribute("height"), DefaultValue(64)] public int Height { get; set; }
        [XmlAttribute("supersample"), DefaultValue(2)] public int Supersample { get; set; }
        [XmlAttribute("clipPath"), DefaultValue("")] public string ClipPath { get; set; }
        [XmlAttribute("clipMode"), DefaultValue("none")] public string ClipMode { get; set; }
        [XmlAttribute("edgeBleed"), DefaultValue(0f)] public float EdgeBleed { get; set; }
        [XmlAttribute("pixelSnap"), DefaultValue(false)] public bool PixelSnap { get; set; }
        [XmlAttribute("alphaCutoff"), DefaultValue(0f)] public float AlphaCutoff { get; set; }
        [XmlElement("Shape")] public VectorShapeData[] Shapes { get; set; }
    }

    [Serializable]
    public sealed class VectorShapeData
    {
        public VectorShapeData()
        {
            Type = "path"; Fill = "#FFFFFF"; Fill2 = ""; Gradient = "none";
            Stroke = ""; StrokeWidth = 0f; Opacity = 1f; Data = "";
        }
        [XmlAttribute("type"), DefaultValue("path")] public string Type { get; set; }
        [XmlAttribute("x")] public float X { get; set; }
        [XmlAttribute("y")] public float Y { get; set; }
        [XmlAttribute("width")] public float Width { get; set; }
        [XmlAttribute("height")] public float Height { get; set; }
        [XmlAttribute("data"), DefaultValue("")] public string Data { get; set; }
        [XmlAttribute("fill"), DefaultValue("#FFFFFF")] public string Fill { get; set; }
        [XmlAttribute("fill2"), DefaultValue("")] public string Fill2 { get; set; }
        [XmlAttribute("gradient"), DefaultValue("none")] public string Gradient { get; set; }
        [XmlAttribute("angle"), DefaultValue(0f)] public float Angle { get; set; }
        [XmlAttribute("stroke"), DefaultValue("")] public string Stroke { get; set; }
        [XmlAttribute("strokeWidth"), DefaultValue(0f)] public float StrokeWidth { get; set; }
        [XmlAttribute("opacity"), DefaultValue(1f)] public float Opacity { get; set; }
    }

}
