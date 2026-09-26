using System;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    public sealed class MapString
    {
        public MapString() { Translations = new Translation[0]; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
        private Translation[] translations = new Translation[0];
        [XmlElement("Translation")] public Translation[] Translations { get { return translations; } set { translations = value ?? new Translation[0]; } }
    }
    public sealed class Translation
    {
        [XmlAttribute("culture")] public string Culture { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
    }
    public sealed class SceneText
    {
        public SceneText() { Screen = 1; Width = 440; Font = "menu"; Align = "left"; Layer = "foreground"; Color = "#FFFFFF"; Opacity = 1; Visible = true; Space = "world"; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen")] public int Screen { get; set; }
        [XmlAttribute("string")] public string String { get; set; }
        [XmlAttribute("x")] public float X { get; set; }
        [XmlAttribute("y")] public float Y { get; set; }
        [XmlAttribute("width")] public float Width { get; set; }
        [XmlAttribute("font")] public string Font { get; set; }
        [XmlAttribute("align")] public string Align { get; set; }
        [XmlAttribute("color")] public string Color { get; set; }
        [XmlAttribute("opacity")] public float Opacity { get; set; }
        [XmlAttribute("visible")] public bool Visible { get; set; }
        [XmlAttribute("layer")] public string Layer { get; set; }
        [XmlAttribute("z")] public float Z { get; set; }
        [XmlAttribute("space")] public string Space { get; set; }
    }
    public sealed class IntroPage
    {
        public IntroPage() { Stay = 3; FadeIn = 1; FadeOut = 1; Color = "#FFFFFF"; }
        [XmlAttribute("string")] public string String { get; set; }
        [XmlAttribute("stay")] public float Stay { get; set; }
        [XmlAttribute("fadeIn")] public float FadeIn { get; set; }
        [XmlAttribute("fadeOut")] public float FadeOut { get; set; }
        [XmlAttribute("color")] public string Color { get; set; }
    }
    public sealed class ResultPage
    {
        public ResultPage() { Rows = new ResultRow[0]; Color = "#FFFFFF"; Background = "#000000"; }
        [XmlAttribute("title")] public string Title { get; set; }
        [XmlAttribute("color")] public string Color { get; set; }
        [XmlAttribute("background")] public string Background { get; set; }
        private ResultRow[] rows = new ResultRow[0];
        [XmlElement("Row")] public ResultRow[] Rows { get { return rows; } set { rows = value ?? new ResultRow[0]; } }
    }
    public sealed class ResultRow
    {
        [XmlAttribute("string")] public string String { get; set; }
        [XmlAttribute("counter")] public string Counter { get; set; }
        [XmlAttribute("requiresFlag")] public string RequiresFlag { get; set; }
        [XmlAttribute("equals")] public string EqualsValue { get; set; }
    }
    public sealed class NativeActorData
    {
        public NativeActorData() { Kind = "oldman"; Visible = true; TextVisible = true; Opacity = 1; Tint = "#FFFFFF"; Quotes = new ActorQuote[0]; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("name")] public string Name { get; set; }
        [XmlAttribute("kind")] public string Kind { get; set; }
        [XmlAttribute("visible")] public bool Visible { get; set; }
        [XmlAttribute("textVisible")] public bool TextVisible { get; set; }
        [XmlAttribute("opacity")] public float Opacity { get; set; }
        [XmlAttribute("tint")] public string Tint { get; set; }
        [XmlAttribute("offsetX")] public float OffsetX { get; set; }
        [XmlAttribute("offsetY")] public float OffsetY { get; set; }
        private ActorQuote[] quotes = new ActorQuote[0];
        [XmlElement("Quote")] public ActorQuote[] Quotes { get { return quotes; } set { quotes = value ?? new ActorQuote[0]; } }
    }
    public sealed class ActorQuote
    {
        [XmlAttribute("string")] public string String { get; set; }
        [XmlAttribute("requiresFlag")] public string RequiresFlag { get; set; }
        [XmlAttribute("equals")] public string EqualsValue { get; set; }
    }
}
