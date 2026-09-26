using System;
using System.Linq;

namespace JKRuntime.Gameplay
{
    /// <summary>Composition describes a contract, not permission to reorder or disable another mod.</summary>
    public enum MechanicComposition { Independent, Additive, Exclusive }

    /// <summary>Immutable metadata linking a rule to its separately registered state and simulation contracts.</summary>
    public sealed class MechanicDefinition
    {
        private readonly string[] conflicts;
        public string Id { get; private set; }
        public Version Version { get; private set; }
        public MechanicEffects Effects { get; private set; }
        public MechanicComposition Composition { get; private set; }
        public string Channel { get; private set; }
        public string StateParticipant { get; private set; }
        public Simulation.SimulationRequirement Simulation { get; private set; }
        public string[] Conflicts { get { return (string[])conflicts.Clone(); } }
        public MechanicDefinition(string id, Version version, MechanicEffects effects,
            MechanicComposition composition = MechanicComposition.Independent, string channel = null,
            string[] incompatible = null, string stateParticipant = null, Simulation.SimulationRequirement simulation = null)
        {
            Id = ModuleDefinition.ValidId(id);
            if (version == null || ((int)effects & ~63) != 0 || !Enum.IsDefined(typeof(MechanicComposition), composition))
                throw new ArgumentException("Invalid mechanic definition");
            if (composition != MechanicComposition.Independent) ModuleDefinition.ValidId(channel);
            if (stateParticipant != null) ModuleDefinition.ValidId(stateParticipant);
            conflicts = (incompatible ?? new string[0]).Select(ModuleDefinition.ValidId).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Version = version; Effects = effects; Composition = composition; Channel = channel;
            StateParticipant = stateParticipant; Simulation = simulation;
        }
    }
}
