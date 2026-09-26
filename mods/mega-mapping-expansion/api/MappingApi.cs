using System;
using System.Reflection;
using System.Xml.Serialization;

[assembly: AssemblyVersion("1.0.0.0")]

namespace MegaMappingExpansion.Api
{
    /// <summary>Game-thread scene access. Resolve mega.mapping.scene:1:0 through JK Runtime.
    /// Handles belong to one scene generation; disposal releases only the caller's effect.</summary>
    public interface IMappingScene
    {
        /// <summary>False when the map has no scene or this level capability has ended.</summary>
        bool Available { get; }
        /// <summary>Changes on scene replacement or snapshot restore. Use to invalidate cached selections.</summary>
        long Generation { get; }
        /// <summary>Activate an authored Effect ID for the named owner. Track/dispose the returned lease.</summary>
        ISceneEffect Activate(string owner, string definitionId);
        /// <summary>Validate and copy a complete request before committing. Unknown targets/assets or budgets throw.</summary>
        ISceneEffect Apply(string owner, EffectDefinition definition);
        /// <summary>Detached effective values and winning owners, formatted with invariant XML-compatible strings.</summary>
        SceneObjectInfo[] InspectObjects();
        /// <summary>Supported dynamic fields and validation metadata. Unknown object IDs throw.</summary>
        ScenePropertyInfo[] DescribeProperties(string objectId);
        /// <summary>Detached list of current effect instances and remaining seconds.</summary>
        EffectInfo[] InspectEffects();
        /// <summary>Up to 64 recent observer errors. Failed observers stay disabled until reload/reset.</summary>
        string[] InspectErrors();
        /// <summary>Read a declared, case-sensitive flag ID. Unknown keys throw.</summary>
        string GetFlag(string id);
        /// <summary>Set a declared flag to at most 256 characters. Changed values queue flag:ID; unchanged writes do nothing.</summary>
        void SetFlag(string id, string value);
        /// <summary>Queue a 1..128-character event ID. A full 256-event queue throws before mutation.</summary>
        void Emit(string eventId);
    }

    /// <summary>A generation-safe effect lease. Dispose is idempotent, including after unload.</summary>
    public interface ISceneEffect : IDisposable
    {
        /// <summary>Instance ID within its scene generation.</summary>
        long Id { get; }
        /// <summary>False after expiry, cancellation, unload, reload or snapshot restore.</summary>
        bool Active { get; }
        /// <summary>Zero for inactive leases; positive infinity for an active duration-zero effect.</summary>
        double RemainingSeconds { get; }
    }

    /// <summary>Declarative effect. Apply takes an owned copy; subsequent caller edits have no effect.</summary>
    public sealed class EffectDefinition
    {
        public EffectDefinition()
        {
            Clock = "gameplay"; Repeat = "refresh"; MaxStacks = 4;
            Changes = new SceneChange[0]; Lights = new LightSpawn[0];
        }
        /// <summary>Required definition ID, 1..128 characters. Repeat matching uses owner plus this ID.</summary>
        [XmlAttribute("id")] public string Id { get; set; }
        /// <summary>0 means until cancelled; otherwise at most 86400 seconds, including fades.</summary>
        [XmlAttribute("duration")] public double Duration { get; set; }
        /// <summary>gameplay (default) freezes under pause; presentation continues in native/modal UI frames.</summary>
        [XmlAttribute("clock")] public string Clock { get; set; }
        /// <summary>refresh (default), ignore, replace or stack. Refresh/ignore leases alias the existing instance.</summary>
        [XmlAttribute("repeat")] public string Repeat { get; set; }
        /// <summary>1..32 instances per owner/definition when Repeat is stack; default 4.</summary>
        [XmlAttribute("maxStacks")] public int MaxStacks { get; set; }
        /// <summary>Greater values apply after lower layers; ties use definition/owner/instance order.</summary>
        [XmlAttribute("priority")] public int Priority { get; set; }
        /// <summary>Optional owner-local exclusivity group, at most 128 characters.</summary>
        [XmlAttribute("group")] public string Group { get; set; }
        /// <summary>Numeric/light fade-in seconds, included in duration.</summary>
        [XmlAttribute("fadeIn")] public double FadeIn { get; set; }
        /// <summary>Numeric/light fade-out seconds; requires finite duration. Colors/assets switch discretely.</summary>
        [XmlAttribute("fadeOut")] public double FadeOut { get; set; }
        /// <summary>At most 64 property changes. Null is treated as empty.</summary>
        [XmlElement("Set")] public SceneChange[] Changes { get; set; }
        /// <summary>At most 8 template-based light spawns. The effect owns their lifetime.</summary>
        [XmlElement("SpawnLight")] public LightSpawn[] Lights { get; set; }
    }

    /// <summary>A supported object property and invariant string operand. Use DescribeProperties for field/choice metadata.</summary>
    public sealed class SceneChange
    {
        public SceneChange() { Mode = "set"; }
        [XmlAttribute("target")] public string Target { get; set; }
        [XmlAttribute("property")] public string Property { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
        [XmlAttribute("mode")] public string Mode { get; set; }
    }

    /// <summary>Spawn a predeclared LightTemplate; optional attachment override and additive local offsets in +/-4096 pixels.</summary>
    public sealed class LightSpawn
    {
        [XmlAttribute("template")] public string Template { get; set; }
        [XmlAttribute("attach")] public string Attach { get; set; }
        [XmlAttribute("offsetX")] public float OffsetX { get; set; }
        [XmlAttribute("offsetY")] public float OffsetY { get; set; }
    }

    public sealed class SceneObjectInfo
    {
        public string Id { get; internal set; }
        public string Kind { get; internal set; }
        public int Screen { get; internal set; }
        public string[] Properties { get; internal set; }
        public string[] Values { get; internal set; }
        public string[] Owners { get; internal set; }
        public SceneObjectInfo(string id, string kind, int screen, string[] properties, string[] values, string[] owners)
        { Id = id; Kind = kind; Screen = screen; Properties = properties; Values = values; Owners = owners; }
    }

    /// <summary>Editable property metadata. Numeric bounds describe effective values;
    /// add/multiply operands may be signed. Choices are asset IDs or enum values.</summary>
    public sealed class ScenePropertyInfo
    {
        public string Name { get; private set; }
        public string Kind { get; private set; }
        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public string[] Choices { get; private set; }
        public ScenePropertyInfo(string name, string kind, double minimum, double maximum, string[] choices)
        { Name = name; Kind = kind; Minimum = minimum; Maximum = maximum; Choices = choices == null ? new string[0] : (string[])choices.Clone(); }
    }

    public sealed class EffectInfo
    {
        public long Id { get; private set; }
        public string Definition { get; private set; }
        public string Owner { get; private set; }
        public double RemainingSeconds { get; private set; }
        public EffectInfo(long id, string definition, string owner, double remaining)
        { Id = id; Definition = definition; Owner = owner; RemainingSeconds = remaining; }
    }
}
