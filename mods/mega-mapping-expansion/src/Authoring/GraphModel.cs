using System;
using System.Xml.Serialization;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    public sealed class ValueNodeData
    {
        public ValueNodeData() { Clock = SceneClock.Gameplay; Kind = SceneValueKind.Number; Inputs = ""; Value = "0"; Units = ""; MinimumQuality=ObservationQuality.Estimated; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("op")] public string Operation { get; set; }
        [XmlAttribute("inputs")] public string Inputs { get; set; }
        [XmlAttribute("source")] public string Source { get; set; }
        [XmlAttribute("kind")] public SceneValueKind Kind { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
        [XmlAttribute("units")] public string Units { get; set; }
        [XmlAttribute("clock")] public SceneClock Clock { get; set; }
        [XmlAttribute("curve")] public string Curve { get; set; }
        [XmlAttribute("quality")] public ObservationQuality MinimumQuality { get; set; }
    }
    public sealed class BindingData
    {
        public BindingData() { Mode = "set"; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("source")] public string Source { get; set; }
        [XmlAttribute("target")] public string Target { get; set; }
        [XmlAttribute("mode")] public string Mode { get; set; }
        [XmlAttribute("priority")] public int Priority { get; set; }
    }
    internal sealed class GraphPort
    {
        internal SceneValueKind Kind;
        internal string Units;
        internal GraphPort(SceneValueKind kind, string units = "") { Kind = kind; Units = units; }
    }
    internal enum ValueOperation
    {
        Constant, Input, Pass, Add, Subtract, Multiply, Divide, Vector, X, Y, Length, Normalize, Dot, Angle,
        Sin, Cos, Abs, Floor, Round, Minimum, Maximum, Clamp, Remap, Wrap, Curve, Select, Less, Greater, Equal,
        And, Or, Not, Smooth, Spring, Integrate, Delay, Number, Integer, Format, Power, Hysteresis, Available, Quality
    }
    internal sealed class GraphInstruction
    {
        internal readonly ValueNodeData Data;
        internal readonly ValueOperation Op;
        internal readonly int[] Inputs;
        internal readonly SceneValue Initial;
        internal readonly double[] Curve;
        internal GraphPort Port;
        internal GraphInstruction(ValueNodeData data, ValueOperation op, int[] inputs)
        {
            Data = new ValueNodeData { Id=data.Id, Operation=data.Operation, Inputs=data.Inputs, Source=data.Source, Kind=data.Kind, Value=data.Value, Units=data.Units, Clock=data.Clock, Curve=data.Curve, MinimumQuality=data.MinimumQuality }; Op = op; Inputs = inputs;
            Initial = SceneValue.Parse(data.Kind, data.Value);
            Curve = string.IsNullOrEmpty(data.Curve) ? new double[0] : ParseCurve(data.Curve);
        }
        private static double[] ParseCurve(string source)
        {
            var keys = source.Split(';'); if (keys.Length < 2 || keys.Length > 128) throw new System.IO.InvalidDataException("Curve needs 2..128 keys");
            var values = new double[keys.Length * 2];
            for (int i = 0; i < keys.Length; i++)
            {
                var pair = keys[i].Split(':'); if (pair.Length != 2) throw new System.IO.InvalidDataException("Curve keys use position:value");
                values[i * 2] = SceneValue.Parse(SceneValueKind.Number, pair[0]).X; values[i * 2 + 1] = SceneValue.Parse(SceneValueKind.Number, pair[1]).X;
                if (i > 0 && values[i * 2] <= values[(i - 1) * 2]) throw new System.IO.InvalidDataException("Curve positions must increase");
            }
            return values;
        }
    }
}
