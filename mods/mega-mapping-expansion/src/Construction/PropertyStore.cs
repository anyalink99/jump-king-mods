using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class PropertySlot
    {
        internal readonly string Component;
        internal readonly FieldDescriptor Descriptor;
        internal readonly SceneValue Baseline;
        internal SceneValue Value;
        internal string Owner;
        internal SceneValue NextValue;
        internal string NextOwner;
        internal PropertySlot(string component, FieldDescriptor descriptor, SceneValue baseline)
        { Component = component; Descriptor = descriptor; Baseline = baseline; Value = baseline; Owner = "authored"; }
    }
    internal sealed class PropertyContribution
    {
        internal string Id, Owner, Target, Mode;
        internal int Priority;
        internal SceneValue Value;
        internal double Gain = 1;
    }
    internal sealed class CompiledBinding
    {
        internal readonly BindingData Data;
        internal readonly PropertySlot Target;
        internal readonly PropertyContribution Contribution;
        internal bool HasValue;
        internal CompiledBinding(BindingData data, PropertySlot target)
        {
            Data = new BindingData { Id=data.Id, Source=data.Source, Target=data.Target, Mode=data.Mode, Priority=data.Priority }; Target = target;
            Contribution = new PropertyContribution { Id = data.Id, Owner = "binding:" + data.Id, Target = data.Target, Mode = data.Mode, Priority = data.Priority };
        }
    }
    internal sealed class PropertyStore
    {
        private readonly Dictionary<string, PropertySlot> slots = new Dictionary<string, PropertySlot>(StringComparer.Ordinal);
        private sealed class ComponentSlots{internal ComponentDescriptor Descriptor;internal PropertySlot[] Values;}
        private readonly Dictionary<string,ComponentSlots> componentsById=new Dictionary<string,ComponentSlots>(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyContribution> overrides = new Dictionary<string, PropertyContribution>(StringComparer.Ordinal);
        private readonly CompiledBinding[] bindings;
        private static readonly PropertySlot[] EmptySlots=new PropertySlot[0];
        private PropertySlot[] activeSlots=EmptySlots,retiredSlots=EmptySlots;
        private readonly PropertySlot[] bindingSlots;
        private readonly List<PropertySlot> changed=new List<PropertySlot>();
        private PropertyContribution[] orderedOverrides=new PropertyContribution[0];
        private bool overridesDirty;
        private readonly ValueGraph graph;
        internal readonly CompiledComponent[] Components;
        internal Action<string, string, ResourceImpact> Changed;
        internal PropertyStore(CompiledComponent[] components, ValueGraph program, BindingData[] definitions)
        {
            Components = components; graph = program;
            foreach (var component in components)
            {
                var prepared=new ComponentSlots{Descriptor=component.Descriptor,Values=new PropertySlot[component.Descriptor.Fields.Length]};componentsById.Add(component.Id,prepared);
                for (int i = 0; i < component.Descriptor.Fields.Length; i++)
                {var slot=new PropertySlot(component.Id, component.Descriptor.Fields[i], component.Baseline[i]);prepared.Values[i]=slot;slots.Add(component.Id + "/" + component.Descriptor.Fields[i].Name,slot);}
            }
            if (definitions == null || definitions.Length > 4096) throw new InvalidDataException("Scene supports at most 4096 bindings");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            bindings = definitions.Select(data => {
                if (data == null || string.IsNullOrWhiteSpace(data.Id) || !ids.Add(data.Id)) throw new InvalidDataException("Binding requires a unique ID");
                var target = Require(data.Target); var source = graph.Describe(data.Source);
                if (!target.Descriptor.Bindable || source.Kind != target.Descriptor.Kind) throw new InvalidDataException(data.Id + ": binding target and source types differ or target is structural");
                if (source.Units != "" && target.Descriptor.Units != "" && source.Units != target.Descriptor.Units)
                    throw new InvalidDataException(data.Id + ": units differ: " + source.Units + " -> " + target.Descriptor.Units + "; use an explicit conversion");
                Mode(data.Mode, target); return new CompiledBinding(data, target);
            }).OrderBy(b => b.Data.Priority).ThenBy(b => b.Data.Id, StringComparer.Ordinal).ToArray();
            bindingSlots=bindings.Select(b=>b.Target).Distinct().ToArray();activeSlots=bindingSlots;
        }
        internal PropertySlot Require(string address)
        { PropertySlot slot; if (address == null || !slots.TryGetValue(address, out slot)) throw new InvalidDataException("Unknown scene property: " + address); return slot; }
        internal IEnumerable<KeyValuePair<string, PropertySlot>> All { get { return slots; } }
        internal SceneValue Read(string component, string property)
        {ComponentSlots value;if(component==null||!componentsById.TryGetValue(component,out value))throw new InvalidDataException("Unknown component: "+component);return value.Values[value.Descriptor.Index(property)].Value;}
        private static void Mode(string mode, PropertySlot slot)
        {
            if (mode != "set" && mode != "add" && mode != "multiply") throw new InvalidDataException("Contribution mode must be set, add or multiply");
            if (mode != "set" && slot.Descriptor.Kind != SceneValueKind.Number && slot.Descriptor.Kind != SceneValueKind.Vector2)
                throw new InvalidDataException("Only numbers and vectors support additive or multiplicative contributions");
        }
        internal void Set(PropertyContribution request)
        {
            Validate(request);
            string key = request.Owner + "/" + request.Id;
            if (!overrides.ContainsKey(key) && overrides.Count >= 4096) throw new InvalidDataException("Scene contribution limit exceeded");
            PropertyContribution previous;
            if(overrides.TryGetValue(key,out previous)&&previous.Target==request.Target&&previous.Mode==request.Mode&&previous.Priority==request.Priority)
            {previous.Value=request.Value;previous.Gain=request.Gain;return;}
            overrides[key] = new PropertyContribution { Id = request.Id, Owner = request.Owner, Target = request.Target, Mode = request.Mode, Priority = request.Priority, Value = request.Value, Gain = request.Gain };
            overridesDirty=true;
        }
        internal void Validate(PropertyContribution request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Owner) || request.Id.Length > 128 || request.Owner.Length > 128)
                throw new InvalidDataException("Property contribution needs bounded owner and ID");
            PropertySlot slot = Require(request.Target);
            if (slot.Descriptor.Impact == ResourceImpact.Structural) throw new InvalidDataException(request.Target + ": structural properties require preparation");
            if (request.Value.Kind != slot.Descriptor.Kind || !SceneValue.Finite(request.Gain) || request.Gain < 0 || request.Gain > 1) throw new InvalidDataException("Invalid contribution value or gain");
            Mode(request.Mode, slot);
            if(slot.Descriptor.Kind!=SceneValueKind.Number&&slot.Descriptor.Kind!=SceneValueKind.Vector2)slot.Descriptor.Validate(request.Value);
        }
        internal void RemoveOwner(string owner) { foreach (string key in overrides.Where(p => p.Value.Owner == owner).Select(p => p.Key).ToArray()) overrides.Remove(key); overridesDirty=true; }
        internal void Remove(string owner, string id) { if(overrides.Remove(owner + "/" + id))overridesDirty=true; }
        internal void Compose()
        {
            if(overridesDirty)
            {
                orderedOverrides=overrides.Values.OrderBy(p=>p.Priority).ThenBy(p=>p.Owner,StringComparer.Ordinal).ThenBy(p=>p.Id,StringComparer.Ordinal).ToArray();
                var next=bindingSlots.Concat(orderedOverrides.Select(p=>Require(p.Target))).Distinct().ToArray();retiredSlots=activeSlots.Except(next).ToArray();activeSlots=next;overridesDirty=false;
            }
            foreach (var slot in activeSlots) { slot.NextValue = slot.Baseline; slot.NextOwner = "authored"; }
            foreach (var binding in bindings)
            {
                if(graph.Available(binding.Data.Source)){binding.Contribution.Value = graph.Read(binding.Data.Source);binding.HasValue=true;}
                if(!binding.HasValue)continue;
                Apply(binding.Target, binding.Contribution);
            }
            foreach (var request in orderedOverrides) Apply(Require(request.Target), request);
            changed.Clear();
            foreach (var slot in activeSlots)
            {
                var next=Constrain(slot.NextValue, slot.Descriptor);
                if(!slot.Value.Equals(next))changed.Add(slot);
                slot.Value=next;slot.Owner=slot.NextOwner;
            }
            foreach(var slot in retiredSlots){if(!slot.Value.Equals(slot.Baseline))changed.Add(slot);slot.Value=slot.Baseline;slot.Owner="authored";}
            retiredSlots=EmptySlots;
            // callbacks see the whole composed frame, even when they read another component
            if(Changed!=null)foreach(var slot in changed)Changed(slot.Component, slot.Descriptor.Name, slot.Descriptor.Impact);
        }
        private static void Apply(PropertySlot slot, PropertyContribution c)
        {
            if (c.Gain == 0) return;
            if (slot.Descriptor.Kind != SceneValueKind.Number && slot.Descriptor.Kind != SceneValueKind.Vector2)
                slot.NextValue = c.Value;
            else
            {
                var a=slot.NextValue;var b=c.Value;
                double x=ComposeNumber(a.X,b.X,c,slot.Descriptor),y=ComposeNumber(a.Y,b.Y,c,slot.Descriptor);
                slot.NextValue=a.Kind==SceneValueKind.Vector2?SceneValue.Vector(x,y):SceneValue.Number(x);
            }
            slot.NextOwner = c.Owner;
        }
        private static double ComposeNumber(double a,double b,PropertyContribution c,FieldDescriptor field)
        {double n=c.Mode=="add"?a+b*c.Gain:c.Mode=="multiply"?a*(1+(b-1)*c.Gain):a+(b-a)*c.Gain;return Math.Max(field.Minimum,Math.Min(field.Maximum,n));}
        private static SceneValue Constrain(SceneValue value, FieldDescriptor field)
        {
            if (value.Kind == SceneValueKind.Number || value.Kind == SceneValueKind.Vector2)
                return ValueGraph.Clamp(value,field.Minimum,field.Maximum);
            if(value.Kind==SceneValueKind.Integer)return SceneValue.Integer((int)Math.Max(field.Minimum,Math.Min(field.Maximum,value.X)));
            return value;
        }
        internal PropertyContribution[] Capture()
        { return overrides.Values.Select(p => new PropertyContribution { Id = p.Id, Owner = p.Owner, Target = p.Target, Mode = p.Mode, Priority = p.Priority, Value = p.Value, Gain = p.Gain }).ToArray(); }
        internal void RestoreBindings(){foreach(var binding in bindings){binding.HasValue=graph.HasValue(binding.Data.Source);binding.Contribution.Value=graph.Read(binding.Data.Source);}}
        internal void Restore(PropertyContribution[] state)
        {
            if (state == null || state.Length > 4096) throw new InvalidDataException("Invalid contribution snapshot");
            var saved = Capture(); overrides.Clear();overridesDirty=true;
            try { foreach (var value in state) Set(value); }
            catch { overrides.Clear(); foreach (var value in saved) Set(value); throw; }
            Compose();
        }
    }
}
