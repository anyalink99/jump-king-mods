using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JKRuntime
{
    public sealed class CapabilityDefinition
    {
        public string Id { get; private set; }
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public CapabilityDefinition(string id, int major, int minor)
        {
            Id = ModuleDefinition.ValidId(id);
            if (major < 1 || minor < 0) throw new ArgumentOutOfRangeException("major/minor");
            Major = major; Minor = minor;
        }
    }

    public sealed class CapabilityRequirement
    {
        public string Id { get; private set; }
        public int Major { get; private set; }
        public int MinimumMinor { get; private set; }
        public bool Optional { get; private set; }
        public CapabilityRequirement(string id, int major, int minimumMinor, bool optional = false)
        {
            Id = ModuleDefinition.ValidId(id);
            if (major < 1 || minimumMinor < 0) throw new ArgumentOutOfRangeException("major/minor");
            Major = major; MinimumMinor = minimumMinor; Optional = optional;
        }
        internal bool Accepts(CapabilityDefinition capability)
        { return capability.Major == Major && capability.Minor >= MinimumMinor; }
    }

    // Definitions are immutable process-lifetime registrations. Live player
    // references and undo leases belong in the per-level ModuleContext.
    public sealed class ModuleDefinition
    {
        public string Id { get; private set; }
        public Version Version { get; private set; }
        public IList<CapabilityRequirement> Requires { get; private set; }
        public IList<CapabilityDefinition> Provides { get; private set; }
        public IList<string> Before { get; private set; }
        public IList<string> After { get; private set; }
        public IList<string> ExclusiveResources { get; private set; }
        public Action<ModuleContext> Install { get; private set; }
        public Action<ModuleContext> Start { get; private set; }
        public Action<ModuleContext> Stop { get; private set; }
        internal string Origin { get; set; }
        /// <summary>Opt in only when skipping every map callback is safe and previous world teardown removes all resources.</summary>
        public bool MapSuspendable { get { return mapSuspendable; } set { if (Registered) throw new InvalidOperationException("Module policy is frozen after registration"); mapSuspendable = value; } }
        private bool mapSuspendable;
        internal bool Registered;
        // Map-owned packages order optional integrations without requiring them.
        // Capability requirements remain the dependency contract.
        internal bool OptionalOrderingTargets;

        public ModuleDefinition(string id, Version version, Action<ModuleContext> install,
            IEnumerable<CapabilityRequirement> requires = null,
            IEnumerable<CapabilityDefinition> provides = null,
            IEnumerable<string> before = null, IEnumerable<string> after = null,
            IEnumerable<string> exclusiveResources = null,
            Action<ModuleContext> start = null, Action<ModuleContext> stop = null)
        {
            Id = ValidId(id);
            if (version == null) throw new ArgumentNullException("version");
            if (install == null) throw new ArgumentNullException("install");
            Version = version; Install = install; Start = start; Stop = stop;
            Origin = install.Method.Module.Assembly.FullName;
            Requires = Freeze(requires); Provides = Freeze(provides);
            Before = Ids(before); After = Ids(after); ExclusiveResources = Ids(exclusiveResources);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CapabilityDefinition item in Provides)
                if (!ids.Add(item.Id)) throw new ArgumentException("Duplicate provided capability: " + item.Id);
            ids.Clear();
            foreach (CapabilityRequirement item in Requires)
                if (!ids.Add(item.Id)) throw new ArgumentException("Duplicate required capability: " + item.Id);
        }

        internal static string ValidId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 160) throw new ArgumentException("A stable ID is required.");
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '.' && c != '-' && c != '_')
                    throw new ArgumentException("IDs must use lower-case ASCII letters, digits, '.', '-' or '_': " + id);
            return id;
        }
        private static IList<string> Ids(IEnumerable<string> values)
        {
            var result = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? new string[0]) result.Add(ValidId(value));
            return new ReadOnlyCollection<string>(new List<string>(result));
        }
        private static IList<T> Freeze<T>(IEnumerable<T> values) where T : class
        {
            var result = new List<T>(values ?? new T[0]);
            if (result.Contains(null)) throw new ArgumentException("Null declaration.");
            return result.AsReadOnly();
        }
    }
}
