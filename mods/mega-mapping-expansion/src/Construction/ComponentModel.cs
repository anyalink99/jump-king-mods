using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    public sealed class ComponentData
    {
        public ComponentData() { Values = new ComponentValueData[0]; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("kind")] public string Kind { get; set; }
        [XmlAttribute("tags")] public string Tags { get; set; }
        [XmlElement("Value")] public ComponentValueData[] Values { get; set; }
    }
    public sealed class ComponentValueData
    {
        [XmlAttribute("name")] public string Name { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
    }
    internal enum ResourceImpact { None, Pose, Membership, Geometry, Material, Structural }
    internal enum ReferenceKind { None, Component, Asset, Signal, Selection }
    internal sealed class FieldDescriptor
    {
        internal readonly string Name, Units;
        internal readonly SceneValue Default;
        internal readonly double Minimum, Maximum;
        internal readonly ResourceImpact Impact;
        internal readonly ReferenceKind Reference;
        internal readonly string[] Choices;
        internal SceneValueKind Kind { get { return Default.Kind; } }
        internal bool Bindable { get { return Impact != ResourceImpact.Structural && Reference == ReferenceKind.None; } }
        internal FieldDescriptor(string name, SceneValue value, double min = -1000000, double max = 1000000,
            string units = "", ResourceImpact impact = ResourceImpact.None, ReferenceKind reference = ReferenceKind.None, params string[] choices)
        { Name = name; Default = value; Minimum = min; Maximum = max; Units = units; Impact = impact; Reference = reference; Choices = choices ?? new string[0]; }
        internal SceneValue Parse(string source) { var value = SceneValue.Parse(Kind, source); Validate(value); return value; }
        internal void Validate(SceneValue value)
        {
            if (value.Kind != Kind) throw new InvalidDataException(Name + ": expected " + Kind + ", got " + value.Kind);
            if (Kind == SceneValueKind.Number || Kind == SceneValueKind.Integer || Kind == SceneValueKind.Vector2)
                if (value.X < Minimum || value.X > Maximum || (Kind == SceneValueKind.Vector2 && (value.Y < Minimum || value.Y > Maximum)))
                    throw new InvalidDataException(Name + ": value outside " + Minimum + ".." + Maximum + " " + Units);
            if (Choices.Length != 0 && !Choices.Contains(value.Text)) throw new InvalidDataException(Name + ": expected " + string.Join(", ", Choices));
        }
    }
    internal sealed class ComponentDescriptor
    {
        internal readonly string Kind;
        internal readonly FieldDescriptor[] Fields;
        internal readonly SceneStateScope Scope;
        internal Func<NativeRenderer,CompiledComponent,Action<int>> DrawFactory;
        private readonly Dictionary<string,int> indices=new Dictionary<string,int>(StringComparer.Ordinal);
        internal ComponentDescriptor(string kind, SceneStateScope scope, params FieldDescriptor[] fields)
        { Kind = kind; Scope = scope; Fields = fields; for(int i=0;i<fields.Length;i++)indices.Add(fields[i].Name,i); }
        internal int Index(string name)
        { int index; if(name==null||!indices.TryGetValue(name,out index))throw new InvalidDataException(Kind + ": unknown property " + name); return index; }
    }
    internal sealed class CompiledComponent
    {
        internal readonly string Id;
        internal readonly ComponentDescriptor Descriptor;
        internal readonly SceneValue[] Baseline;
        internal readonly string[] Tags;
        internal CompiledComponent(ComponentData source, ComponentDescriptor descriptor)
        {
            Id = source.Id; Descriptor = descriptor; Baseline = Array.ConvertAll(descriptor.Fields, f => f.Default);
            Tags=string.IsNullOrWhiteSpace(source.Tags)?new string[0]:source.Tags.Split(';').Select(t=>t.Trim()).ToArray();
            if(Tags.Any(t=>t.Length==0||t.Length>128)||Tags.Distinct(StringComparer.Ordinal).Count()!=Tags.Length)throw new InvalidDataException(Id+": invalid or duplicate tags");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in source.Values ?? new ComponentValueData[0])
            {
                if (field == null || !seen.Add(field.Name)) throw new InvalidDataException(Id + ": duplicate or null property");
                int index = descriptor.Index(field.Name); Baseline[index] = descriptor.Fields[index].Parse(field.Value);
            }
        }
    }
    internal sealed class ComponentRegistry
    {
        private readonly Dictionary<string, ComponentDescriptor> kinds = new Dictionary<string, ComponentDescriptor>(StringComparer.Ordinal);
        internal IEnumerable<ComponentDescriptor> All { get { return kinds.Values.OrderBy(k => k.Kind, StringComparer.Ordinal); } }
        internal void Add(ComponentDescriptor descriptor) { kinds.Add(descriptor.Kind, descriptor); }
        internal void DrawWith(string kind,Func<NativeRenderer,CompiledComponent,Action<int>> factory)
        {
            var descriptor=Require(kind);if(descriptor.DrawFactory!=null||factory==null)throw new InvalidOperationException("Duplicate or invalid drawing backend: "+kind);
            descriptor=new ComponentDescriptor(kind,descriptor.Scope,descriptor.Fields.Concat(new[]{new FieldDescriptor("captureOnly",SceneValue.Boolean(false),impact:ResourceImpact.Membership)}).ToArray());
            descriptor.DrawFactory=factory;kinds[kind]=descriptor;
        }
        internal ComponentDescriptor Require(string kind)
        { ComponentDescriptor result; if (kind == null || !kinds.TryGetValue(kind, out result)) throw new InvalidDataException("Unknown component kind: " + kind); return result; }
        internal CompiledComponent Compile(ComponentData source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Id) || source.Id.Length > 128 || source.Id.Contains('/') || source.Id.StartsWith("@"))
                throw new InvalidDataException("Component requires an ID of 1..128 characters without / or leading @");
            return new CompiledComponent(source, Require(source.Kind));
        }
    }
}
