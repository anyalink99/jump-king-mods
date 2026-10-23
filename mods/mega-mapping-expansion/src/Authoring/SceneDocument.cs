using System.Xml.Serialization;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    [XmlRoot("MappingSettings")]
    public sealed class SceneDocument
    {
        internal static SceneDocument Copy(SceneDocument source)
        {
            if(source==null)throw new System.ArgumentNullException("source");var serializer=new XmlSerializer(typeof(SceneDocument));
            using(var stream=new System.IO.MemoryStream()){serializer.Serialize(stream,source);stream.Position=0;return (SceneDocument)serializer.Deserialize(stream);}
        }
        internal static string Xml(SceneDocument source)
        {
            var text=new System.Text.StringBuilder();var namespaces=new XmlSerializerNamespaces();namespaces.Add("","");
            using(var writer=System.Xml.XmlWriter.Create(text,new System.Xml.XmlWriterSettings{OmitXmlDeclaration=true,Indent=false,NewLineHandling=System.Xml.NewLineHandling.None}))new XmlSerializer(typeof(SceneDocument)).Serialize(writer,source,namespaces);return text.ToString();
        }
        public SceneDocument()
        {
            Version = 1; Assets = new AssetData[0]; Components = new ComponentData[0]; Graph = new ValueNodeData[0]; Bindings = new BindingData[0];
            Variables = new VariableData[0]; Effects = new EffectData[0]; Rules = new ReactionData[0]; Exports = new ExportData[0]; Trees = new ControlTreeData[0];
        }
        [XmlAttribute("version")] public int Version { get; set; }
        [XmlAttribute("saveId")] public string SaveId { get; set; }
        [XmlAnyElement("MapLayout")] public System.Xml.XmlElement MapLayout { get; set; }
        [XmlArray("Assets"), XmlArrayItem("Asset")] public AssetData[] Assets { get; set; }
        [XmlArray("Components"), XmlArrayItem("Component")] public ComponentData[] Components { get; set; }
        [XmlArray("Graph"), XmlArrayItem("Node")] public ValueNodeData[] Graph { get; set; }
        [XmlArray("Bindings"), XmlArrayItem("Bind")] public BindingData[] Bindings { get; set; }
        [XmlArray("Variables"), XmlArrayItem("Variable")] public VariableData[] Variables { get; set; }
        [XmlArray("Effects"), XmlArrayItem("Effect")] public EffectData[] Effects { get; set; }
        [XmlArray("Rules"), XmlArrayItem("Rule")] public ReactionData[] Rules { get; set; }
        [XmlArray("Exports"), XmlArrayItem("Export")] public ExportData[] Exports { get; set; }
        [XmlArray("Trees"), XmlArrayItem("Tree")] public ControlTreeData[] Trees { get; set; }
    }
    public sealed class AssetData
    {
        public AssetData() { Kind = "png"; Columns = Rows = 1; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("path")] public string Path { get; set; }
        [XmlAttribute("kind")] public string Kind { get; set; }
        [XmlAttribute("columns")] public int Columns { get; set; }
        [XmlAttribute("rows")] public int Rows { get; set; }
        [XmlAttribute("frames")] public int Frames { get; set; }
        [XmlAttribute("fps")] public double Fps { get; set; }
    }
    public sealed class VariableData
    {
        public VariableData() { Kind = SceneValueKind.Number; Value = "0"; Scope = SceneStateScope.Attempt; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("kind")] public SceneValueKind Kind { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
        [XmlAttribute("scope")] public SceneStateScope Scope { get; set; }
    }
    public sealed class EffectData
    {
        public EffectData() { Clock = SceneClock.Gameplay; Repeat = "refresh"; Values = new OverrideData[0]; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("duration")] public double Duration { get; set; }
        [XmlAttribute("fadeIn")] public double FadeIn { get; set; }
        [XmlAttribute("fadeOut")] public double FadeOut { get; set; }
        [XmlAttribute("clock")] public SceneClock Clock { get; set; }
        [XmlAttribute("repeat")] public string Repeat { get; set; }
        [XmlAttribute("priority")] public int Priority { get; set; }
        [XmlAttribute("group")] public string Group { get; set; }
        [XmlElement("Set")] public OverrideData[] Values { get; set; }
    }
    public sealed class OverrideData
    {
        public OverrideData() { Mode = "set"; }
        [XmlAttribute("target")] public string Target { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
        [XmlAttribute("source")] public string Source { get; set; }
        [XmlAttribute("mode")] public string Mode { get; set; }
    }
    public sealed class ReactionData
    {
        public ReactionData() { Actions = new ActionData[0]; Clock=SceneClock.Gameplay; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("event")] public string Event { get; set; }
        [XmlAttribute("condition")] public string Condition { get; set; }
        [XmlAttribute("once")] public bool Once { get; set; }
        [XmlAttribute("cooldown")] public double Cooldown { get; set; }
        [XmlAttribute("clock")] public SceneClock Clock { get; set; }
        [XmlElement("Action")] public ActionData[] Actions { get; set; }
    }
    public sealed class ActionData
    {
        [XmlAttribute("op")] public string Operation { get; set; }
        [XmlAttribute("target")] public string Target { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
        [XmlAttribute("source")] public string Source { get; set; }
    }
    public sealed class ExportData
    {
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("target")] public string Target { get; set; }
        [XmlAttribute("role")] public string Role { get; set; }
    }
}
