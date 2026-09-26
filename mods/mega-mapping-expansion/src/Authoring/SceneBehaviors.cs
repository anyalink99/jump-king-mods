using System;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    public sealed class RegionData
    {
        internal RegionData Copy() { return (RegionData)MemberwiseClone(); }
        public RegionData() { Screen = 1; Test = "hitbox"; SpawnInside = "fire"; Lifetime = "effect"; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen")] public int Screen { get; set; }
        [XmlAttribute("x")] public float X { get; set; }
        [XmlAttribute("y")] public float Y { get; set; }
        [XmlAttribute("width")] public float Width { get; set; }
        [XmlAttribute("height")] public float Height { get; set; }
        [XmlAttribute("test")] public string Test { get; set; }
        [XmlAttribute("anchor")] public string Anchor { get; set; }
        [XmlAttribute("grounded")] public bool Grounded { get; set; }
        [XmlAttribute("spawnInside")] public string SpawnInside { get; set; }
        [XmlAttribute("hysteresis")] public float Hysteresis { get; set; }
        [XmlAttribute("dwell")] public double Dwell { get; set; }
        [XmlAttribute("enter")] public string Enter { get; set; }
        [XmlAttribute("owner")] public string Owner { get; set; }
        [XmlAttribute("exit")] public string Exit { get; set; }
        [XmlAttribute("lifetime")] public string Lifetime { get; set; }
        [XmlAttribute("once")] public bool Once { get; set; }
        [XmlAttribute("requiresFlag")] public string RequiresFlag { get; set; }
        [XmlAttribute("equals")] public string EqualsValue { get; set; }
    }

    public sealed class FlagData
    {
        public FlagData() { Value = "false"; Scope = "run"; Type = "string"; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
        [XmlAttribute("scope")] public string Scope { get; set; }
        [XmlAttribute("from")] public string PreviousId { get; set; }
        [XmlAttribute("type")] public string Type { get; set; }
    }

    public sealed class RuleData
    {
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("event")] public string Event { get; set; }
        [XmlAttribute("effect")] public string Effect { get; set; }
        [XmlAttribute("sound")] public string Sound { get; set; }
        [XmlAttribute("owner")] public string Owner { get; set; }
        [XmlAttribute("screen")] public int Screen { get; set; }
        [XmlAttribute("requiresFlag")] public string RequiresFlag { get; set; }
        [XmlAttribute("equals")] public string EqualsValue { get; set; }
        [XmlAttribute("setFlag")] public string SetFlag { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
        [XmlAttribute("once")] public bool Once { get; set; }
        [XmlAttribute("cooldown")] public double Cooldown { get; set; }
        [XmlAttribute("increment")] public string Increment { get; set; }
        [XmlAttribute("amount")] public int Amount { get; set; }
    }
}
