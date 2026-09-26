using System;
using System.Collections.Generic;
using System.Linq;

namespace JKRuntime.Gameplay
{
    [Flags]
    public enum MechanicEffects { None = 0, Input = 1, Charge = 2, Movement = 4, Collision = 8, Presentation = 16, Time = 32 }
    public enum MechanicSource { Setting, Equipment, Surface, Zone, Screen, Controller }

    public sealed class MechanicState
    {
        public bool Enabled { get; private set; }
        public bool Available { get; private set; }
        public bool Active { get; private set; }
        public MechanicSource Source { get; private set; }
        public string Reason { get; private set; }
        public MechanicState(bool enabled, bool available, bool active, MechanicSource source, string reason)
        {
            if (!Enum.IsDefined(typeof(MechanicSource), source) || (active && (!enabled || !available)))
                throw new ArgumentException("Invalid mechanic state");
            Enabled = enabled; Available = available; Active = active; Source = source;
            Reason = reason ?? "";
        }
    }

    public sealed class MechanicInfo
    {
        public string Owner { get; internal set; }
        public string Id { get; internal set; }
        public Version Version { get; internal set; }
        public MechanicEffects Effects { get; internal set; }
        public MechanicState State { get; internal set; }
        public string Error { get; internal set; }
        public MechanicDefinition Definition { get; internal set; }
        public string[] Conflicts { get; internal set; }
    }

    /// <summary>On-demand mechanic inventory. Registration does not grant simulation coverage or modify run flags.</summary>
    public sealed class MechanicRegistry
    {
        private sealed class Entry
        {
            internal string Owner, Id;
            internal Version Version;
            internal MechanicEffects Effects;
            internal Func<MechanicState> Read;
            internal MechanicDefinition Definition;
        }
        private readonly SortedDictionary<string, Entry> entries = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
        private bool reading;
        public long Generation { get; private set; }

        public IDisposable Register(string owner, string id, Version version, MechanicEffects effects, Func<MechanicState> read)
        { return Register(owner, new MechanicDefinition(id, version, effects), read); }

        /// <summary>Register an owned identity and on-demand reader. Metadata links do not automatically register state or simulation providers.</summary>
        public IDisposable Register(string owner, MechanicDefinition definition, Func<MechanicState> read)
        {
            CheckMutation();
            owner = ModuleDefinition.ValidId(owner);
            if (definition == null || read == null) throw new ArgumentException("Invalid mechanic declaration");
            string id = definition.Id;
            if (!MapPolicy.AllowsMechanic(id)) throw new InvalidOperationException("Mechanic denied by map: " + id + ": " + MapPolicy.MechanicReason(id));
            if (entries.ContainsKey(id)) throw new InvalidOperationException("Mechanic already owned: " + id);
            entries.Add(id, new Entry { Owner = owner, Id = id, Version = definition.Version, Effects = definition.Effects, Definition = definition, Read = read });
            Generation++;
            return RuntimeResources.Track(owner, "mechanic:" + id, new ActionLease(delegate { CheckMutation(); if (entries.Remove(id)) Generation++; }));
        }

        /// <summary>Read detached descriptors once. Failed readers are reported per owner; conflicting active rules remain visible, not silently disabled.</summary>
        public MechanicInfo[] Inspect()
        {
            RuntimeApi.Kernel.CheckThread();
            if (reading) throw new InvalidOperationException("Recursive mechanic inspection");
            reading = true;
            try
            {
                var result = new List<MechanicInfo>();
                foreach (Entry entry in entries.Values)
                {
                    var item = new MechanicInfo { Owner = entry.Owner, Id = entry.Id, Version = entry.Version, Effects = entry.Effects, Definition = entry.Definition, Conflicts = new string[0] };
                    try { item.State = entry.Read(); if (item.State == null) throw new InvalidOperationException("Missing mechanic state"); }
                    catch (Exception error) { item.Error = error.ToString(); }
                    result.Add(item);
                }
                foreach (var item in result.Where(i => i.State != null && i.State.Active))
                    item.Conflicts = result.Where(other => other != item && other.State != null && other.State.Active &&
                        (item.Definition.Conflicts.Contains(other.Id) || other.Definition.Conflicts.Contains(item.Id) ||
                        (item.Definition.Channel != null && item.Definition.Channel == other.Definition.Channel &&
                        (item.Definition.Composition == MechanicComposition.Exclusive || other.Definition.Composition == MechanicComposition.Exclusive))))
                        .Select(other => other.Id).ToArray();
                return result.ToArray();
            }
            finally { reading = false; }
        }

        private void CheckMutation()
        {
            RuntimeApi.Kernel.CheckThread();
            if (reading) throw new InvalidOperationException("Mechanic registration changed during inspection");
        }
    }
}
